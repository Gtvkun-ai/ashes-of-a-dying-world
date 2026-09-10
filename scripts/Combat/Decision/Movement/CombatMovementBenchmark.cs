using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// P2.1 runtime benchmark cho movement.
    ///
    /// Điểm sửa quan trọng so với P2 cũ:
    /// - Follow KHÔNG còn coi formation anchor đang chạy là một target tĩnh rồi abort liên tục.
    /// - StaticPath, Follow và CombatPositioning có tiêu chí đo riêng.
    /// - "collision" dùng blocking collision thật (va đập làm mất tiến độ), không dùng mọi slide contact.
    ///
    /// Benchmark chỉ đo, tuyệt đối không đổi steering/pathfinding để tránh thước đo tự can thiệp vào bài test.
    /// </summary>
    public sealed class CombatMovementBenchmark
    {
        private const int FollowHistogramBucketCount = 65;
        private const float FollowHistogramBucketPixels = 8f;
        private const int MinimumDynamicSampleFrames = 30;

        private readonly CombatCharacter _self;
        private readonly CombatMovementMetrics _metrics;
        private readonly float _segmentTimeoutSeconds;
        private readonly float _targetShiftResetDistance;
        private readonly float _minSuccessRate;
        private readonly float _minFollowBandRate;
        private readonly float _maxBlockingCollisionRate;
        private readonly float _cpuBudgetUsec;

        private bool _running;
        private string _label = "movement";
        private CombatMovementBenchmarkMode _mode = CombatMovementBenchmarkMode.Follow;
        private ulong _startedAtMs;
        private ulong _lastObserveMs;
        private ulong _lastObserveFrame = ulong.MaxValue;
        private Vector2 _lastObservedPosition;
        private Vector2 _lastObservedVelocity;
        private bool _hasLastObservation;

        // Segment dùng cho StaticPath và CombatPositioning.
        private bool _segmentActive;
        private Vector2 _segmentTarget;
        private float _segmentSuccessDistance;
        private float _segmentInitialDistance;
        private float _segmentPathLength;
        private float _segmentActiveSeconds;
        private float _segmentJitterDegrees;
        private int _segmentVelocityTransitions;

        private int _successfulSegments;
        private int _failedSegments;
        private int _abortedSegments;
        private float _completedSeconds;
        private float _completedPathStretchSum;
        private float _completedJitterDegreesSum;
        private int _completedVelocityTransitions;
        private ulong _sampledPhysicsFrames;

        // Follow dùng live anchor. Không có khái niệm "target moved => abort".
        private ulong _followSampleFrames;
        private ulong _followInsideBandFrames;
        private float _followErrorSum;
        private float _followMaxError;
        private float _followBandDistance;
        private readonly ulong[] _followErrorHistogram = new ulong[FollowHistogramBucketCount];
        private float _followJitterDegrees;
        private int _followVelocityTransitions;
        private bool _followCatchupActive;
        private float _followCatchupSeconds;
        private int _followCatchupEvents;
        private float _followCatchupSecondsSum;
        private float _followCatchupMaxSeconds;

        private ulong _startSolverTicks;
        private ulong _startMotorTicks;
        private ulong _startRawCollisionFrames;
        private ulong _startRawCollisionContacts;
        private ulong _startBlockingCollisionFrames;
        private ulong _startBlockingCollisionContacts;
        private ulong _startSolverMicroseconds;
        private ulong _startMotorMicroseconds;
        private ulong _startPathRequests;
        private ulong _startPathReuseHits;
        private ulong _startPathBudgetDeferrals;
        private ulong _startProbeSamples;
        private ulong _startShapeProbeSamples;
        private ulong _startAvoidanceSubmissions;
        private ulong _startAvoidanceCorrections;
        private ulong _startPassingSideLocks;
        private ulong _startStuckEvents;

        public bool IsRunning => _running;
        public string Label => _label;
        public CombatMovementBenchmarkMode Mode => _mode;

        public CombatMovementBenchmark(
            CombatCharacter self,
            CombatMovementMetrics metrics,
            float segmentTimeoutSeconds = 4f,
            float targetShiftResetDistance = 28f,
            float minSuccessRate = 0.98f,
            float minFollowBandRate = 0.90f,
            float maxBlockingCollisionRate = 0.03f,
            float cpuBudgetUsec = 250f)
        {
            _self = self;
            _metrics = metrics;
            _segmentTimeoutSeconds = Mathf.Clamp(segmentTimeoutSeconds, 0.5f, 20f);
            _targetShiftResetDistance = Mathf.Max(4f, targetShiftResetDistance);
            _minSuccessRate = Mathf.Clamp(minSuccessRate, 0f, 1f);
            _minFollowBandRate = Mathf.Clamp(minFollowBandRate, 0f, 1f);
            _maxBlockingCollisionRate = Mathf.Clamp(maxBlockingCollisionRate, 0f, 1f);
            _cpuBudgetUsec = Mathf.Max(1f, cpuBudgetUsec);
        }

        public void Start(string label = "movement", CombatMovementBenchmarkMode mode = CombatMovementBenchmarkMode.Follow)
        {
            ResetCounters();
            _label = string.IsNullOrWhiteSpace(label) ? "movement" : label.Trim();
            _mode = mode;
            _running = true;
            _startedAtMs = Time.GetTicksMsec();
            CaptureMetricBaseline();
        }

        public CombatMovementBenchmarkSnapshot Stop()
        {
            if (_running && _segmentActive)
            {
                // User chủ động dừng giữa một episode thì không tính fail.
                AbortSegment();
            }

            _running = false;
            return Snapshot();
        }

        public void Reset()
        {
            bool wasRunning = _running;
            string label = _label;
            CombatMovementBenchmarkMode mode = _mode;
            ResetCounters();
            CaptureMetricBaseline();
            _running = wasRunning;
            _label = label;
            _mode = mode;
            _startedAtMs = wasRunning ? Time.GetTicksMsec() : 0UL;
        }

        /// <summary>
        /// Bài A -> B tĩnh. Chỉ mode này mới abort khi target bị dịch quá xa,
        /// vì target di chuyển trong bài static nghĩa là setup test đã đổi giữa chừng.
        /// </summary>
        public void ObserveStaticPath(Vector2 targetPosition, float successDistance, Vector2 safeVelocity)
        {
            if (_mode != CombatMovementBenchmarkMode.StaticPath
                || !TryBeginObservation(safeVelocity, out Vector2 currentPosition, out float activeDelta, out float stepDistance, out float turnDegrees))
            {
                return;
            }

            float clampedSuccessDistance = Mathf.Max(1f, successDistance);
            if (!_segmentActive)
            {
                BeginSegment(targetPosition, clampedSuccessDistance, currentPosition);
            }
            else if (_segmentTarget.DistanceTo(targetPosition) > _targetShiftResetDistance)
            {
                AbortSegment();
                BeginSegment(targetPosition, clampedSuccessDistance, currentPosition);
            }

            UpdateActiveSegment(currentPosition, activeDelta, stepDistance, turnDegrees, _segmentTarget);
        }

        /// <summary>
        /// Formation follow: targetPosition là anchor SỐNG, có thể đổi mỗi frame.
        /// Đo error/band/catch-up trực tiếp thay vì cố biến nó thành segment target đứng yên.
        /// </summary>
        public void ObserveFollow(
            Vector2 targetPosition,
            float stopDistance,
            float followBandDistance,
            Vector2 safeVelocity)
        {
            if (_mode == CombatMovementBenchmarkMode.StaticPath)
            {
                ObserveStaticPath(targetPosition, stopDistance, safeVelocity);
                return;
            }

            if (_mode != CombatMovementBenchmarkMode.Follow
                || !TryBeginObservation(safeVelocity, out Vector2 currentPosition, out float activeDelta, out _, out float turnDegrees))
            {
                return;
            }

            float band = Mathf.Max(Mathf.Max(1f, stopDistance), followBandDistance);
            float error = currentPosition.DistanceTo(targetPosition);
            _followBandDistance = band;
            _followSampleFrames++;
            _followErrorSum += error;
            _followMaxError = Mathf.Max(_followMaxError, error);
            _followJitterDegrees += turnDegrees;
            if (turnDegrees > 0f)
            {
                _followVelocityTransitions++;
            }

            int bucket = Mathf.Clamp(
                Mathf.FloorToInt(error / FollowHistogramBucketPixels),
                0,
                FollowHistogramBucketCount - 1);
            _followErrorHistogram[bucket]++;

            bool insideBand = error <= band;
            if (insideBand)
            {
                _followInsideBandFrames++;
                if (_followCatchupActive)
                {
                    _followCatchupEvents++;
                    _followCatchupSecondsSum += _followCatchupSeconds;
                    _followCatchupMaxSeconds = Mathf.Max(_followCatchupMaxSeconds, _followCatchupSeconds);
                    _followCatchupActive = false;
                    _followCatchupSeconds = 0f;
                }
            }
            else
            {
                if (!_followCatchupActive)
                {
                    _followCatchupActive = true;
                    _followCatchupSeconds = 0f;
                }
                _followCatchupSeconds += activeDelta;
            }
        }

        /// <summary>
        /// Combat positioning dùng live tactical anchor. Target được phép đổi mỗi frame;
        /// episode thành công khi AI vào lại success band trước timeout.
        /// </summary>
        public void ObserveCombatPositioning(Vector2 targetPosition, float successDistance, Vector2 safeVelocity)
        {
            if (_mode == CombatMovementBenchmarkMode.StaticPath)
            {
                // Cho phép dùng cùng motor trong scene test A->B mà không phải thêm một đường code riêng.
                ObserveStaticPath(targetPosition, successDistance, safeVelocity);
                return;
            }

            if (_mode != CombatMovementBenchmarkMode.CombatPositioning
                || !TryBeginObservation(safeVelocity, out Vector2 currentPosition, out float activeDelta, out float stepDistance, out float turnDegrees))
            {
                return;
            }

            float clampedSuccessDistance = Mathf.Max(1f, successDistance);
            float remaining = currentPosition.DistanceTo(targetPosition);
            if (!_segmentActive && remaining > clampedSuccessDistance)
            {
                BeginSegment(targetPosition, clampedSuccessDistance, currentPosition);
            }

            if (_segmentActive)
            {
                // Không freeze target: combat anchor thay đổi là bình thường.
                _segmentTarget = targetPosition;
                _segmentSuccessDistance = clampedSuccessDistance;
                UpdateActiveSegment(currentPosition, activeDelta, stepDistance, turnDegrees, targetPosition);
            }
        }

        public CombatMovementBenchmarkSnapshot Snapshot()
        {
            int completed = _successfulSegments + _failedSegments;
            float segmentSuccessRate = completed > 0 ? (float)_successfulSegments / completed : 0f;
            float followBandRate = _followSampleFrames > 0
                ? (float)_followInsideBandFrames / _followSampleFrames
                : 0f;
            float successRate = _mode == CombatMovementBenchmarkMode.Follow
                ? followBandRate
                : ResolveDynamicSuccessRate(completed, segmentSuccessRate);

            ulong solverTicks = Delta(_metrics?.SolverTicks ?? 0UL, _startSolverTicks);
            ulong motorTicks = Delta(_metrics?.MotorTicks ?? 0UL, _startMotorTicks);
            ulong rawCollisionFrames = Delta(_metrics?.RawCollisionFrames ?? 0UL, _startRawCollisionFrames);
            ulong rawCollisionContacts = Delta(_metrics?.RawCollisionContacts ?? 0UL, _startRawCollisionContacts);
            ulong blockingCollisionFrames = Delta(_metrics?.BlockingCollisionFrames ?? 0UL, _startBlockingCollisionFrames);
            ulong blockingCollisionContacts = Delta(_metrics?.BlockingCollisionContacts ?? 0UL, _startBlockingCollisionContacts);
            // Collision được sample ở local motor. Dùng motor ticks làm mẫu số để mode Follow không bị
            // méo rate khi giữa benchmark có vài frame combat/attack không được tính vào follow samples.
            ulong collisionSampleFrames = motorTicks > 0 ? motorTicks : _sampledPhysicsFrames;
            float rawCollisionRate = collisionSampleFrames > 0
                ? (float)rawCollisionFrames / collisionSampleFrames
                : 0f;
            float blockingCollisionRate = collisionSampleFrames > 0
                ? (float)blockingCollisionFrames / collisionSampleFrames
                : 0f;

            float averageSeconds = _mode == CombatMovementBenchmarkMode.Follow
                ? (_followCatchupEvents > 0 ? _followCatchupSecondsSum / _followCatchupEvents : 0f)
                : (completed > 0 ? _completedSeconds / completed : 0f);
            float averagePathStretch = completed > 0 ? _completedPathStretchSum / completed : 0f;
            float averageJitter = _mode == CombatMovementBenchmarkMode.Follow
                ? (_followVelocityTransitions > 0 ? _followJitterDegrees / _followVelocityTransitions : 0f)
                : (_completedVelocityTransitions > 0 ? _completedJitterDegreesSum / _completedVelocityTransitions : 0f);

            ulong solverUsec = Delta(_metrics?.SolverMicroseconds ?? 0UL, _startSolverMicroseconds);
            ulong motorUsec = Delta(_metrics?.MotorMicroseconds ?? 0UL, _startMotorMicroseconds);
            ulong cpuTicks = solverTicks + motorTicks;
            float averageCpuUsec = cpuTicks > 0 ? (float)(solverUsec + motorUsec) / cpuTicks : 0f;

            float elapsedSeconds = _startedAtMs > 0
                ? Mathf.Max(0f, (Time.GetTicksMsec() - _startedAtMs) / 1000f)
                : 0f;

            float averageFollowError = _followSampleFrames > 0
                ? _followErrorSum / _followSampleFrames
                : 0f;
            float p95FollowError = ResolveFollowPercentile(0.95f);
            float averageCatchupSeconds = _followCatchupEvents > 0
                ? _followCatchupSecondsSum / _followCatchupEvents
                : 0f;

            bool hasGateSample = _mode == CombatMovementBenchmarkMode.Follow
                ? _followSampleFrames >= MinimumDynamicSampleFrames
                : (_mode == CombatMovementBenchmarkMode.CombatPositioning
                    ? (_sampledPhysicsFrames >= MinimumDynamicSampleFrames || completed > 0)
                    : completed > 0);

            float requiredSuccessRate = _mode == CombatMovementBenchmarkMode.Follow
                ? _minFollowBandRate
                : _minSuccessRate;
            bool hardGatePass = hasGateSample
                && successRate >= requiredSuccessRate
                && blockingCollisionRate <= _maxBlockingCollisionRate;

            float score = ComputeScore(
                hardGatePass,
                successRate,
                blockingCollisionRate,
                averageSeconds,
                averagePathStretch,
                averageJitter,
                averageCpuUsec,
                averageFollowError,
                p95FollowError,
                _followBandDistance);

            return new CombatMovementBenchmarkSnapshot(
                _label,
                _mode,
                _running,
                elapsedSeconds,
                _sampledPhysicsFrames,
                hasGateSample,
                _successfulSegments,
                _failedSegments,
                _abortedSegments,
                successRate,
                rawCollisionFrames,
                rawCollisionContacts,
                rawCollisionRate,
                blockingCollisionFrames,
                blockingCollisionContacts,
                blockingCollisionRate,
                averageSeconds,
                averagePathStretch,
                averageJitter,
                averageCpuUsec,
                hardGatePass,
                score,
                _minSuccessRate,
                _minFollowBandRate,
                _maxBlockingCollisionRate,
                _cpuBudgetUsec,
                _followSampleFrames,
                _followInsideBandFrames,
                followBandRate,
                _followBandDistance,
                averageFollowError,
                p95FollowError,
                _followMaxError,
                _followCatchupEvents,
                averageCatchupSeconds,
                _followCatchupMaxSeconds,
                solverTicks,
                motorTicks,
                Delta(_metrics?.PathRequests ?? 0UL, _startPathRequests),
                Delta(_metrics?.PathReuseHits ?? 0UL, _startPathReuseHits),
                Delta(_metrics?.PathBudgetDeferrals ?? 0UL, _startPathBudgetDeferrals),
                Delta(_metrics?.ProbeSamples ?? 0UL, _startProbeSamples),
                Delta(_metrics?.ShapeProbeSamples ?? 0UL, _startShapeProbeSamples),
                Delta(_metrics?.AvoidanceSubmissions ?? 0UL, _startAvoidanceSubmissions),
                Delta(_metrics?.AvoidanceCorrections ?? 0UL, _startAvoidanceCorrections),
                Delta(_metrics?.PassingSideLocks ?? 0UL, _startPassingSideLocks),
                Delta(_metrics?.StuckEvents ?? 0UL, _startStuckEvents));
        }

        public string ToCompactString()
        {
            CombatMovementBenchmarkSnapshot snapshot = Snapshot();
            string gate = !snapshot.HasQualitySample
                ? "gate=WAIT"
                : (snapshot.HardGatePass ? "gate=PASS" : "gate=FAIL");

            if (snapshot.Mode == CombatMovementBenchmarkMode.Follow)
            {
                return $"bench={snapshot.Label} mode=follow {gate} score={snapshot.Score:0.0} "
                    + $"band={snapshot.FollowBandRate:P0} err={snapshot.AverageFollowError:0.0}/p95={snapshot.P95FollowError:0.0}px "
                    + $"block={snapshot.BlockingCollisionRate:P1} raw={snapshot.RawCollisionRate:P1} "
                    + $"catch={snapshot.AverageCatchupSeconds:0.00}s cpu={snapshot.AverageCpuUsec:0.0}us";
            }

            return $"bench={snapshot.Label} mode={snapshot.Mode.ToString().ToLowerInvariant()} {gate} score={snapshot.Score:0.0} "
                + $"success={snapshot.SuccessfulSegments}/{snapshot.CompletedSegments}({snapshot.SuccessRate:P0}) "
                + $"block={snapshot.BlockingCollisionRate:P1} raw={snapshot.RawCollisionRate:P1} "
                + $"time={snapshot.AverageSeconds:0.00}s stretch={snapshot.AveragePathStretch:0.00} "
                + $"jitter={snapshot.AverageJitterDegrees:0.0}deg cpu={snapshot.AverageCpuUsec:0.0}us";
        }

        private bool TryBeginObservation(
            Vector2 safeVelocity,
            out Vector2 currentPosition,
            out float activeDelta,
            out float stepDistance,
            out float turnDegrees)
        {
            currentPosition = Vector2.Zero;
            activeDelta = 0f;
            stepDistance = 0f;
            turnDegrees = 0f;

            if (!_running || _self == null || !GodotObject.IsInstanceValid(_self))
            {
                return false;
            }

            ulong frame = Engine.GetPhysicsFrames();
            if (_lastObserveFrame == frame)
            {
                return false;
            }
            _lastObserveFrame = frame;
            _sampledPhysicsFrames++;

            ulong nowMs = Time.GetTicksMsec();
            currentPosition = _self.CombatCenter;
            if (_hasLastObservation)
            {
                ulong gapMs = nowMs >= _lastObserveMs ? nowMs - _lastObserveMs : 0UL;
                // Attack/cast/pause dài không được tính như movement bị chậm.
                if (gapMs <= 250UL)
                {
                    activeDelta = Mathf.Clamp(gapMs / 1000f, 0f, 0.25f);
                    stepDistance = currentPosition.DistanceTo(_lastObservedPosition);
                }

                if (safeVelocity.LengthSquared() > 1f && _lastObservedVelocity.LengthSquared() > 1f)
                {
                    float dot = Mathf.Clamp(
                        safeVelocity.Normalized().Dot(_lastObservedVelocity.Normalized()),
                        -1f,
                        1f);
                    turnDegrees = Mathf.RadToDeg(Mathf.Acos(dot));
                }
            }

            _lastObservedPosition = currentPosition;
            _lastObservedVelocity = safeVelocity;
            _lastObserveMs = nowMs;
            _hasLastObservation = true;
            return true;
        }

        private void BeginSegment(Vector2 targetPosition, float successDistance, Vector2 currentPosition)
        {
            float distance = currentPosition.DistanceTo(targetPosition);
            if (distance <= successDistance)
            {
                return;
            }

            _segmentActive = true;
            _segmentTarget = targetPosition;
            _segmentSuccessDistance = successDistance;
            _segmentInitialDistance = Mathf.Max(distance, 1f);
            _segmentPathLength = 0f;
            _segmentActiveSeconds = 0f;
            _segmentJitterDegrees = 0f;
            _segmentVelocityTransitions = 0;
        }

        private void UpdateActiveSegment(
            Vector2 currentPosition,
            float activeDelta,
            float stepDistance,
            float turnDegrees,
            Vector2 liveTarget)
        {
            if (!_segmentActive)
            {
                return;
            }

            _segmentActiveSeconds += activeDelta;
            _segmentPathLength += stepDistance;
            _segmentJitterDegrees += turnDegrees;
            if (turnDegrees > 0f)
            {
                _segmentVelocityTransitions++;
            }

            float remaining = currentPosition.DistanceTo(liveTarget);
            if (remaining <= _segmentSuccessDistance)
            {
                CompleteSegment(success: true);
            }
            else if (_segmentActiveSeconds >= _segmentTimeoutSeconds)
            {
                CompleteSegment(success: false);
            }
        }

        private void CompleteSegment(bool success)
        {
            if (!_segmentActive)
            {
                return;
            }

            if (success)
            {
                _successfulSegments++;
            }
            else
            {
                _failedSegments++;
            }

            _completedSeconds += _segmentActiveSeconds;
            float stretch = _segmentPathLength / Mathf.Max(1f, _segmentInitialDistance);
            _completedPathStretchSum += Mathf.Max(1f, stretch);
            _completedJitterDegreesSum += _segmentJitterDegrees;
            _completedVelocityTransitions += _segmentVelocityTransitions;
            ClearSegment();
        }

        private void AbortSegment()
        {
            if (_segmentActive)
            {
                _abortedSegments++;
            }
            ClearSegment();
        }

        private void ClearSegment()
        {
            _segmentActive = false;
            _segmentPathLength = 0f;
            _segmentActiveSeconds = 0f;
            _segmentJitterDegrees = 0f;
            _segmentVelocityTransitions = 0;
        }

        private float ResolveDynamicSuccessRate(int completed, float segmentSuccessRate)
        {
            if (_mode == CombatMovementBenchmarkMode.StaticPath)
            {
                return segmentSuccessRate;
            }

            if (completed > 0)
            {
                return segmentSuccessRate;
            }

            // CombatPositioning mà đã sample đủ và không hề mở episode nghĩa là actor luôn ở trong band.
            if (_mode == CombatMovementBenchmarkMode.CombatPositioning
                && _sampledPhysicsFrames >= MinimumDynamicSampleFrames
                && !_segmentActive)
            {
                return 1f;
            }

            return 0f;
        }

        private float ComputeScore(
            bool hardGatePass,
            float successRate,
            float blockingCollisionRate,
            float averageSeconds,
            float pathStretch,
            float jitterDegrees,
            float cpuUsec,
            float averageFollowError,
            float p95FollowError,
            float followBandDistance)
        {
            // success/band + blocking collision là hard gate. Không để "mượt mắt" cứu một AI hay kẹt.
            if (!hardGatePass)
            {
                return 0f;
            }

            float collisionQuality = 1f - Mathf.Clamp(
                blockingCollisionRate / Mathf.Max(0.0001f, _maxBlockingCollisionRate),
                0f,
                1f);
            float cpuQuality = 1f - Mathf.Clamp(cpuUsec / _cpuBudgetUsec, 0f, 1f);

            if (_mode == CombatMovementBenchmarkMode.Follow)
            {
                float band = Mathf.Max(1f, followBandDistance);
                float errorQuality = 1f - Mathf.Clamp(averageFollowError / (band * 1.5f), 0f, 1f);
                float p95Quality = 1f - Mathf.Clamp(p95FollowError / (band * 2f), 0f, 1f);
                float jitterQuality = 1f - Mathf.Clamp(jitterDegrees / 60f, 0f, 1f);
                float weighted = 0.45f * Mathf.Clamp(successRate, 0f, 1f)
                    + 0.20f * collisionQuality
                    + 0.15f * errorQuality
                    + 0.10f * p95Quality
                    + 0.05f * jitterQuality
                    + 0.05f * cpuQuality;
                return Mathf.Clamp(weighted * 100f, 0f, 100f);
            }

            float successQuality = Mathf.Clamp(successRate, 0f, 1f);
            float timeQuality = 1f - Mathf.Clamp(averageSeconds / _segmentTimeoutSeconds, 0f, 1f);
            float pathQuality = pathStretch <= 0f ? 1f : 1f / Mathf.Max(1f, pathStretch);
            float jitterQualityStatic = 1f - Mathf.Clamp(jitterDegrees / 45f, 0f, 1f);
            float weightedStatic = 0.35f * successQuality
                + 0.25f * collisionQuality
                + 0.10f * timeQuality
                + 0.15f * pathQuality
                + 0.10f * jitterQualityStatic
                + 0.05f * cpuQuality;
            return Mathf.Clamp(weightedStatic * 100f, 0f, 100f);
        }

        private float ResolveFollowPercentile(float percentile)
        {
            if (_followSampleFrames <= 0)
            {
                return 0f;
            }

            ulong wanted = (ulong)Mathf.CeilToInt((float)_followSampleFrames * Mathf.Clamp(percentile, 0f, 1f));
            wanted = wanted > 0 ? wanted : 1UL;
            ulong running = 0;
            for (int i = 0; i < _followErrorHistogram.Length; i++)
            {
                running += _followErrorHistogram[i];
                if (running >= wanted)
                {
                    // Bucket cuối là overflow; trả mốc dưới thay vì bịa độ chính xác không có.
                    return i * FollowHistogramBucketPixels;
                }
            }

            return (FollowHistogramBucketCount - 1) * FollowHistogramBucketPixels;
        }

        private void CaptureMetricBaseline()
        {
            if (_metrics == null)
            {
                return;
            }

            _startSolverTicks = _metrics.SolverTicks;
            _startMotorTicks = _metrics.MotorTicks;
            _startRawCollisionFrames = _metrics.RawCollisionFrames;
            _startRawCollisionContacts = _metrics.RawCollisionContacts;
            _startBlockingCollisionFrames = _metrics.BlockingCollisionFrames;
            _startBlockingCollisionContacts = _metrics.BlockingCollisionContacts;
            _startSolverMicroseconds = _metrics.SolverMicroseconds;
            _startMotorMicroseconds = _metrics.MotorMicroseconds;
            _startPathRequests = _metrics.PathRequests;
            _startPathReuseHits = _metrics.PathReuseHits;
            _startPathBudgetDeferrals = _metrics.PathBudgetDeferrals;
            _startProbeSamples = _metrics.ProbeSamples;
            _startShapeProbeSamples = _metrics.ShapeProbeSamples;
            _startAvoidanceSubmissions = _metrics.AvoidanceSubmissions;
            _startAvoidanceCorrections = _metrics.AvoidanceCorrections;
            _startPassingSideLocks = _metrics.PassingSideLocks;
            _startStuckEvents = _metrics.StuckEvents;
        }

        private void ResetCounters()
        {
            _startedAtMs = 0;
            _lastObserveMs = 0;
            _lastObserveFrame = ulong.MaxValue;
            _lastObservedPosition = Vector2.Zero;
            _lastObservedVelocity = Vector2.Zero;
            _hasLastObservation = false;
            _segmentActive = false;
            _successfulSegments = 0;
            _failedSegments = 0;
            _abortedSegments = 0;
            _completedSeconds = 0f;
            _completedPathStretchSum = 0f;
            _completedJitterDegreesSum = 0f;
            _completedVelocityTransitions = 0;
            _sampledPhysicsFrames = 0;

            _followSampleFrames = 0;
            _followInsideBandFrames = 0;
            _followErrorSum = 0f;
            _followMaxError = 0f;
            _followBandDistance = 0f;
            for (int i = 0; i < _followErrorHistogram.Length; i++)
            {
                _followErrorHistogram[i] = 0;
            }
            _followJitterDegrees = 0f;
            _followVelocityTransitions = 0;
            _followCatchupActive = false;
            _followCatchupSeconds = 0f;
            _followCatchupEvents = 0;
            _followCatchupSecondsSum = 0f;
            _followCatchupMaxSeconds = 0f;
        }

        private static ulong Delta(ulong current, ulong baseline)
        {
            return current >= baseline ? current - baseline : 0UL;
        }
    }

    /// <summary>Snapshot bất biến để overlay/exporter đọc mà không chạm state benchmark.</summary>
    public readonly struct CombatMovementBenchmarkSnapshot
    {
        public string Label { get; }
        public CombatMovementBenchmarkMode Mode { get; }
        public bool IsRunning { get; }
        public float ElapsedSeconds { get; }
        public ulong SampledPhysicsFrames { get; }
        public bool HasQualitySample { get; }
        public int SuccessfulSegments { get; }
        public int FailedSegments { get; }
        public int AbortedSegments { get; }
        public int CompletedSegments => SuccessfulSegments + FailedSegments;
        public float SuccessRate { get; }

        public ulong RawCollisionFrames { get; }
        public ulong RawCollisionContacts { get; }
        public float RawCollisionRate { get; }
        public ulong BlockingCollisionFrames { get; }
        public ulong BlockingCollisionContacts { get; }
        public float BlockingCollisionRate { get; }

        // Alias tương thích code debug cũ: từ P2.1 "Collision" mặc định nghĩa là BLOCKING collision.
        public ulong CollisionFrames => BlockingCollisionFrames;
        public ulong CollisionContacts => BlockingCollisionContacts;
        public float CollisionRate => BlockingCollisionRate;

        public float AverageSeconds { get; }
        public float AveragePathStretch { get; }
        public float AverageJitterDegrees { get; }
        public float AverageCpuUsec { get; }
        public bool HardGatePass { get; }
        public float Score { get; }
        public float MinSuccessRate { get; }
        public float MinFollowBandRate { get; }
        public float MaxBlockingCollisionRate { get; }
        public float MaxCollisionRate => MaxBlockingCollisionRate;
        public float CpuBudgetUsec { get; }

        public ulong FollowSampleFrames { get; }
        public ulong FollowInsideBandFrames { get; }
        public float FollowBandRate { get; }
        public float FollowBandDistance { get; }
        public float AverageFollowError { get; }
        public float P95FollowError { get; }
        public float MaxFollowError { get; }
        public int FollowCatchupEvents { get; }
        public float AverageCatchupSeconds { get; }
        public float MaxCatchupSeconds { get; }

        public ulong SolverTicks { get; }
        public ulong MotorTicks { get; }
        public ulong PathRequests { get; }
        public ulong PathReuseHits { get; }
        public ulong PathBudgetDeferrals { get; }
        public ulong ProbeSamples { get; }
        public ulong ShapeProbeSamples { get; }
        public ulong AvoidanceSubmissions { get; }
        public ulong AvoidanceCorrections { get; }
        public ulong PassingSideLocks { get; }
        public ulong StuckEvents { get; }

        public CombatMovementBenchmarkSnapshot(
            string label,
            CombatMovementBenchmarkMode mode,
            bool isRunning,
            float elapsedSeconds,
            ulong sampledPhysicsFrames,
            bool hasQualitySample,
            int successfulSegments,
            int failedSegments,
            int abortedSegments,
            float successRate,
            ulong rawCollisionFrames,
            ulong rawCollisionContacts,
            float rawCollisionRate,
            ulong blockingCollisionFrames,
            ulong blockingCollisionContacts,
            float blockingCollisionRate,
            float averageSeconds,
            float averagePathStretch,
            float averageJitterDegrees,
            float averageCpuUsec,
            bool hardGatePass,
            float score,
            float minSuccessRate,
            float minFollowBandRate,
            float maxBlockingCollisionRate,
            float cpuBudgetUsec,
            ulong followSampleFrames,
            ulong followInsideBandFrames,
            float followBandRate,
            float followBandDistance,
            float averageFollowError,
            float p95FollowError,
            float maxFollowError,
            int followCatchupEvents,
            float averageCatchupSeconds,
            float maxCatchupSeconds,
            ulong solverTicks,
            ulong motorTicks,
            ulong pathRequests,
            ulong pathReuseHits,
            ulong pathBudgetDeferrals,
            ulong probeSamples,
            ulong shapeProbeSamples,
            ulong avoidanceSubmissions,
            ulong avoidanceCorrections,
            ulong passingSideLocks,
            ulong stuckEvents)
        {
            Label = label;
            Mode = mode;
            IsRunning = isRunning;
            ElapsedSeconds = elapsedSeconds;
            SampledPhysicsFrames = sampledPhysicsFrames;
            HasQualitySample = hasQualitySample;
            SuccessfulSegments = successfulSegments;
            FailedSegments = failedSegments;
            AbortedSegments = abortedSegments;
            SuccessRate = successRate;
            RawCollisionFrames = rawCollisionFrames;
            RawCollisionContacts = rawCollisionContacts;
            RawCollisionRate = rawCollisionRate;
            BlockingCollisionFrames = blockingCollisionFrames;
            BlockingCollisionContacts = blockingCollisionContacts;
            BlockingCollisionRate = blockingCollisionRate;
            AverageSeconds = averageSeconds;
            AveragePathStretch = averagePathStretch;
            AverageJitterDegrees = averageJitterDegrees;
            AverageCpuUsec = averageCpuUsec;
            HardGatePass = hardGatePass;
            Score = score;
            MinSuccessRate = minSuccessRate;
            MinFollowBandRate = minFollowBandRate;
            MaxBlockingCollisionRate = maxBlockingCollisionRate;
            CpuBudgetUsec = cpuBudgetUsec;
            FollowSampleFrames = followSampleFrames;
            FollowInsideBandFrames = followInsideBandFrames;
            FollowBandRate = followBandRate;
            FollowBandDistance = followBandDistance;
            AverageFollowError = averageFollowError;
            P95FollowError = p95FollowError;
            MaxFollowError = maxFollowError;
            FollowCatchupEvents = followCatchupEvents;
            AverageCatchupSeconds = averageCatchupSeconds;
            MaxCatchupSeconds = maxCatchupSeconds;
            SolverTicks = solverTicks;
            MotorTicks = motorTicks;
            PathRequests = pathRequests;
            PathReuseHits = pathReuseHits;
            PathBudgetDeferrals = pathBudgetDeferrals;
            ProbeSamples = probeSamples;
            ShapeProbeSamples = shapeProbeSamples;
            AvoidanceSubmissions = avoidanceSubmissions;
            AvoidanceCorrections = avoidanceCorrections;
            PassingSideLocks = passingSideLocks;
            StuckEvents = stuckEvents;
        }
    }
}
