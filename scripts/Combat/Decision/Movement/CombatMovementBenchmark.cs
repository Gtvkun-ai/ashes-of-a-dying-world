using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// P2 runtime benchmark cho movement.
    /// Mục tiêu không phải "chấm điểm cho đẹp" mà tạo số liệu lặp lại được trước khi đổi thuật toán:
    /// success / collision / time / path stretch / jitter / CPU.
    ///
    /// Segment chỉ được tính khi AI thật sự đang có mục tiêu di chuyển. Nếu anchor đổi quá xa giữa chừng
    /// (ví dụ target chạy sang vị trí khác), segment được abort chứ không tính fail để tránh phạt sai.
    /// </summary>
    public sealed class CombatMovementBenchmark
    {
        private readonly CombatCharacter _self;
        private readonly CombatMovementMetrics _metrics;
        private readonly float _segmentTimeoutSeconds;
        private readonly float _targetShiftResetDistance;
        private readonly float _minSuccessRate;
        private readonly float _maxCollisionRate;
        private readonly float _cpuBudgetUsec;

        private bool _running;
        private string _label = "movement";
        private ulong _startedAtMs;
        private ulong _lastObserveMs;
        private ulong _lastObserveFrame = ulong.MaxValue;
        private Vector2 _lastObservedPosition;
        private Vector2 _lastObservedVelocity;
        private bool _hasLastObservation;

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

        private ulong _startSolverTicks;
        private ulong _startMotorTicks;
        private ulong _startCollisionFrames;
        private ulong _startCollisionContacts;
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

        public CombatMovementBenchmark(
            CombatCharacter self,
            CombatMovementMetrics metrics,
            float segmentTimeoutSeconds = 4f,
            float targetShiftResetDistance = 28f,
            float minSuccessRate = 0.98f,
            float maxCollisionRate = 0.01f,
            float cpuBudgetUsec = 250f)
        {
            _self = self;
            _metrics = metrics;
            _segmentTimeoutSeconds = Mathf.Clamp(segmentTimeoutSeconds, 0.5f, 20f);
            _targetShiftResetDistance = Mathf.Max(4f, targetShiftResetDistance);
            _minSuccessRate = Mathf.Clamp(minSuccessRate, 0f, 1f);
            _maxCollisionRate = Mathf.Clamp(maxCollisionRate, 0f, 1f);
            _cpuBudgetUsec = Mathf.Max(1f, cpuBudgetUsec);
        }

        public void Start(string label = "movement")
        {
            ResetCounters();
            _label = string.IsNullOrWhiteSpace(label) ? "movement" : label.Trim();
            _running = true;
            _startedAtMs = Time.GetTicksMsec();
            CaptureMetricBaseline();
        }

        public CombatMovementBenchmarkSnapshot Stop()
        {
            if (_running && _segmentActive)
            {
                // Segment dang dở khi user dừng benchmark không được tính fail.
                AbortSegment();
            }

            _running = false;
            return Snapshot();
        }

        public void Reset()
        {
            bool wasRunning = _running;
            string label = _label;
            ResetCounters();
            CaptureMetricBaseline();
            _running = wasRunning;
            _label = label;
            _startedAtMs = wasRunning ? Time.GetTicksMsec() : 0UL;
        }

        /// <summary>
        /// Gọi tối đa 1 lần / physics frame cho mỗi actor. Hàm dùng vị trí thực tế hiện tại của CharacterBody2D,
        /// nên path length phản ánh kết quả sau MoveAndSlide của frame trước thay vì chỉ tích phân command mong muốn.
        /// </summary>
        public void Observe(Vector2 targetPosition, float successDistance, Vector2 safeVelocity)
        {
            if (!_running || _self == null || !GodotObject.IsInstanceValid(_self))
            {
                return;
            }

            ulong frame = Engine.GetPhysicsFrames();
            if (_lastObserveFrame == frame)
            {
                return;
            }
            _lastObserveFrame = frame;
            _sampledPhysicsFrames++;

            ulong nowMs = Time.GetTicksMsec();
            Vector2 currentPosition = _self.CombatCenter;
            float clampedSuccessDistance = Mathf.Max(1f, successDistance);

            float activeDelta = 0f;
            if (_hasLastObservation)
            {
                ulong gapMs = nowMs >= _lastObserveMs ? nowMs - _lastObserveMs : 0UL;
                // Gap lớn thường là attack/cast/pause. Không cộng thời gian đó vào movement benchmark.
                if (gapMs <= 250UL)
                {
                    activeDelta = Mathf.Clamp(gapMs / 1000f, 0f, 0.25f);
                    if (_segmentActive)
                    {
                        _segmentPathLength += currentPosition.DistanceTo(_lastObservedPosition);
                    }
                }

                if (safeVelocity.LengthSquared() > 1f && _lastObservedVelocity.LengthSquared() > 1f)
                {
                    float dot = Mathf.Clamp(
                        safeVelocity.Normalized().Dot(_lastObservedVelocity.Normalized()),
                        -1f,
                        1f);
                    float turnDegrees = Mathf.RadToDeg(Mathf.Acos(dot));
                    if (_segmentActive)
                    {
                        _segmentJitterDegrees += turnDegrees;
                        _segmentVelocityTransitions++;
                    }
                }
            }

            if (!_segmentActive)
            {
                BeginSegment(targetPosition, clampedSuccessDistance, currentPosition);
            }
            else if (_segmentTarget.DistanceTo(targetPosition) > _targetShiftResetDistance)
            {
                AbortSegment();
                BeginSegment(targetPosition, clampedSuccessDistance, currentPosition);
            }

            if (_segmentActive)
            {
                _segmentActiveSeconds += activeDelta;
                float remaining = currentPosition.DistanceTo(_segmentTarget);
                if (remaining <= _segmentSuccessDistance)
                {
                    CompleteSegment(success: true);
                }
                else if (_segmentActiveSeconds >= _segmentTimeoutSeconds)
                {
                    CompleteSegment(success: false);
                }
            }

            _lastObservedPosition = currentPosition;
            _lastObservedVelocity = safeVelocity;
            _lastObserveMs = nowMs;
            _hasLastObservation = true;
        }

        public CombatMovementBenchmarkSnapshot Snapshot()
        {
            int completed = _successfulSegments + _failedSegments;
            float successRate = completed > 0 ? (float)_successfulSegments / completed : 0f;
            ulong collisionFrames = Delta(_metrics?.CollisionFrames ?? 0UL, _startCollisionFrames);
            ulong collisionContacts = Delta(_metrics?.CollisionContacts ?? 0UL, _startCollisionContacts);
            float collisionRate = _sampledPhysicsFrames > 0
                ? (float)collisionFrames / _sampledPhysicsFrames
                : 0f;
            float averageSeconds = completed > 0 ? _completedSeconds / completed : 0f;
            float averagePathStretch = completed > 0 ? _completedPathStretchSum / completed : 0f;
            float averageJitter = _completedVelocityTransitions > 0
                ? _completedJitterDegreesSum / _completedVelocityTransitions
                : 0f;

            ulong solverTicks = Delta(_metrics?.SolverTicks ?? 0UL, _startSolverTicks);
            ulong motorTicks = Delta(_metrics?.MotorTicks ?? 0UL, _startMotorTicks);
            ulong solverUsec = Delta(_metrics?.SolverMicroseconds ?? 0UL, _startSolverMicroseconds);
            ulong motorUsec = Delta(_metrics?.MotorMicroseconds ?? 0UL, _startMotorMicroseconds);
            ulong cpuTicks = solverTicks + motorTicks;
            float averageCpuUsec = cpuTicks > 0 ? (float)(solverUsec + motorUsec) / cpuTicks : 0f;

            float elapsedSeconds = _startedAtMs > 0
                ? Mathf.Max(0f, (Time.GetTicksMsec() - _startedAtMs) / 1000f)
                : 0f;

            bool hasGateSample = completed > 0 && _sampledPhysicsFrames > 0;
            bool hardGatePass = hasGateSample
                && successRate >= _minSuccessRate
                && collisionRate <= _maxCollisionRate;

            float score = ComputeScore(
                hardGatePass,
                successRate,
                collisionRate,
                averageSeconds,
                averagePathStretch,
                averageJitter,
                averageCpuUsec);

            return new CombatMovementBenchmarkSnapshot(
                _label,
                _running,
                elapsedSeconds,
                _sampledPhysicsFrames,
                _successfulSegments,
                _failedSegments,
                _abortedSegments,
                successRate,
                collisionFrames,
                collisionContacts,
                collisionRate,
                averageSeconds,
                averagePathStretch,
                averageJitter,
                averageCpuUsec,
                hardGatePass,
                score,
                _minSuccessRate,
                _maxCollisionRate,
                _cpuBudgetUsec,
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
            string gate = snapshot.CompletedSegments <= 0
                ? "gate=WAIT"
                : (snapshot.HardGatePass ? "gate=PASS" : "gate=FAIL");
            return $"bench={snapshot.Label} {gate} score={snapshot.Score:0.0} "
                + $"success={snapshot.SuccessfulSegments}/{snapshot.CompletedSegments}({snapshot.SuccessRate:P0}) "
                + $"collision={snapshot.CollisionRate:P1} time={snapshot.AverageSeconds:0.00}s "
                + $"stretch={snapshot.AveragePathStretch:0.00} jitter={snapshot.AverageJitterDegrees:0.0}deg "
                + $"cpu={snapshot.AverageCpuUsec:0.0}us";
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

        private float ComputeScore(
            bool hardGatePass,
            float successRate,
            float collisionRate,
            float averageSeconds,
            float pathStretch,
            float jitterDegrees,
            float cpuUsec)
        {
            // Research yêu cầu success + collision là hard gate. Nếu gate rớt thì không dùng "game feel"
            // để cứu điểm. Score 0 giúp bảng A/B không vô tình chọn preset mượt mắt nhưng hay va chạm.
            if (!hardGatePass)
            {
                return 0f;
            }

            float successQuality = Mathf.Clamp(successRate, 0f, 1f);
            float collisionQuality = 1f - Mathf.Clamp(collisionRate / Mathf.Max(0.0001f, _maxCollisionRate), 0f, 1f);
            float timeQuality = 1f - Mathf.Clamp(averageSeconds / _segmentTimeoutSeconds, 0f, 1f);
            float pathQuality = pathStretch <= 0f ? 0f : 1f / Mathf.Max(1f, pathStretch);
            float jitterQuality = 1f - Mathf.Clamp(jitterDegrees / 45f, 0f, 1f);
            float cpuQuality = 1f - Mathf.Clamp(cpuUsec / _cpuBudgetUsec, 0f, 1f);

            float weighted = 0.35f * successQuality
                + 0.25f * collisionQuality
                + 0.10f * timeQuality
                + 0.15f * pathQuality
                + 0.10f * jitterQuality
                + 0.05f * cpuQuality;
            return Mathf.Clamp(weighted * 100f, 0f, 100f);
        }

        private void CaptureMetricBaseline()
        {
            if (_metrics == null)
            {
                return;
            }

            _startSolverTicks = _metrics.SolverTicks;
            _startMotorTicks = _metrics.MotorTicks;
            _startCollisionFrames = _metrics.CollisionFrames;
            _startCollisionContacts = _metrics.CollisionContacts;
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
        public bool IsRunning { get; }
        public float ElapsedSeconds { get; }
        public ulong SampledPhysicsFrames { get; }
        public int SuccessfulSegments { get; }
        public int FailedSegments { get; }
        public int AbortedSegments { get; }
        public int CompletedSegments => SuccessfulSegments + FailedSegments;
        public float SuccessRate { get; }
        public ulong CollisionFrames { get; }
        public ulong CollisionContacts { get; }
        public float CollisionRate { get; }
        public float AverageSeconds { get; }
        public float AveragePathStretch { get; }
        public float AverageJitterDegrees { get; }
        public float AverageCpuUsec { get; }
        public bool HardGatePass { get; }
        public float Score { get; }
        public float MinSuccessRate { get; }
        public float MaxCollisionRate { get; }
        public float CpuBudgetUsec { get; }
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
            bool isRunning,
            float elapsedSeconds,
            ulong sampledPhysicsFrames,
            int successfulSegments,
            int failedSegments,
            int abortedSegments,
            float successRate,
            ulong collisionFrames,
            ulong collisionContacts,
            float collisionRate,
            float averageSeconds,
            float averagePathStretch,
            float averageJitterDegrees,
            float averageCpuUsec,
            bool hardGatePass,
            float score,
            float minSuccessRate,
            float maxCollisionRate,
            float cpuBudgetUsec,
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
            IsRunning = isRunning;
            ElapsedSeconds = elapsedSeconds;
            SampledPhysicsFrames = sampledPhysicsFrames;
            SuccessfulSegments = successfulSegments;
            FailedSegments = failedSegments;
            AbortedSegments = abortedSegments;
            SuccessRate = successRate;
            CollisionFrames = collisionFrames;
            CollisionContacts = collisionContacts;
            CollisionRate = collisionRate;
            AverageSeconds = averageSeconds;
            AveragePathStretch = averagePathStretch;
            AverageJitterDegrees = averageJitterDegrees;
            AverageCpuUsec = averageCpuUsec;
            HardGatePass = hardGatePass;
            Score = score;
            MinSuccessRate = minSuccessRate;
            MaxCollisionRate = maxCollisionRate;
            CpuBudgetUsec = cpuBudgetUsec;
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
