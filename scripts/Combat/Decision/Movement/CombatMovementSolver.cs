using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// P3 movement stack:
    /// 0) World topology: hiểu tầng thấp/tầng cao và route qua StairLink khi khác elevation.
    /// 1) Global path: NavigationAgent2D cho đường dài.
    /// 2) Static clearance: context steering 16 hướng né tường/đá ở cự ly gần.
    /// 3) Dynamic avoidance: preferred velocity -> RVO safe velocity khi NavAgent hỗ trợ.
    ///
    /// P1: spatial hash/corridor reuse/passing-side/adaptive probes. P2: benchmark runtime.
    /// P3 không bắt NavMesh mới: semantic topology đặt waypoint cầu thang trước global/local avoidance.
    /// </summary>
    public sealed class CombatMovementSolver
    {
        private const int DirectionCount = CombatStaticClearance.DirectionCount;

        private readonly CombatCharacter _self;
        private readonly float _arrivalDistance;
        private readonly float _stuckCheckSeconds;
        private readonly float _stuckMoveEpsilon;
        private readonly float _stuckRecoverySeconds;
        private readonly int _preferredSideSign;
        private readonly float _followBenchmarkBandDistance;
        private readonly uint _obstacleMask;
        private readonly float _bodyProbeRadius;

        private readonly CombatMovementMetrics _metrics;
        private readonly CombatGlobalPathPlanner _globalPath;
        private readonly CombatStaticClearance _staticClearance;
        private readonly CombatDynamicAvoidance _dynamicAvoidance;
        private readonly CombatMovementBenchmark _benchmark;
        private readonly CombatElevationRouter _elevationRouter;

        private int _lastDirectionSlot = -1;
        private bool _lastHadMovement;
        private bool _progressInitialized;
        private Vector2 _progressSamplePosition;
        private float _progressSampleTime;
        private float _stuckRecoveryUntil;
        private int _stuckSideSign;
        private ulong _nextMotorProbeTickMs;
        private ulong _nextFreeProbeTickMs;
        private ulong _lastCollisionSampleFrame = ulong.MaxValue;
        private Vector2 _lastResolvedMotorVelocity = Vector2.Zero;
        private Vector2 _lastBlockingNormal = Vector2.Zero;
        private ulong _blockingRecoveryUntilMs;
        private int _blockingRecoverySideSign;
        private bool _hasMotorNavigationTarget;
        // Final target dùng benchmark; path target có thể là chân/đầu cầu thang do P3 topology router chọn.
        private Vector2 _motorNavigationTarget;
        private Vector2 _motorPathTarget;
        private Vector2 _motorSemanticTarget;

        public CombatMovementMetrics Metrics => _metrics;
        public CombatMovementBenchmark Benchmark => _benchmark;
        public bool IsRecoveringFromStuck { get; private set; }
        public string TopologyDiagnostics => _elevationRouter?.ToCompactString() ?? "topology=unavailable";
        public string PathDiagnostics => _globalPath?.ToCompactString() ?? "path=unavailable";

        public CombatMovementSolver(
            CombatCharacter self,
            NavigationAgent2D navigationAgent,
            uint obstacleMask,
            float probeDistance,
            float arrivalDistance,
            float navigationThreshold,
            float navigationTargetRefreshDistance = 8f,
            float navigationPathReuseDistance = 16f,
            float navigationRepathIntervalSeconds = 0.25f,
            float navigationMaxTargetReuseSeconds = 0.55f,
            int pathBudgetPerPhysicsFrame = 4,
            bool useForwardShapeClearance = true,
            float movementBodyProbeRadius = 7f,
            bool useDynamicAvoidance = true,
            float avoidanceRadius = 10f,
            float avoidanceNeighborDistance = 70f,
            int avoidanceMaxNeighbors = 8,
            float avoidanceTimeHorizonAgents = 0.65f,
            float avoidanceTimeHorizonObstacles = 0.35f,
            float avoidancePassingLockSeconds = 0.75f,
            float avoidanceHeadOnBiasStrength = 0.09f,
            float stuckCheckSeconds = 0.60f,
            float stuckMoveEpsilon = 4f,
            float stuckRecoverySeconds = 0.55f,
            float benchmarkSegmentTimeoutSeconds = 4f,
            float benchmarkTargetShiftResetDistance = 28f,
            float benchmarkMinSuccessRate = 0.98f,
            float benchmarkMinFollowBandRate = 0.90f,
            float benchmarkMaxBlockingCollisionRate = 0.03f,
            float benchmarkFollowBandDistance = 42f,
            float benchmarkCpuBudgetUsec = 250f,
            bool useWorldTopology = true,
            bool topologyFailClosed = true,
            bool topologyDebugLogging = false)
        {
            _self = self;
            _arrivalDistance = Mathf.Max(1f, arrivalDistance);
            _stuckCheckSeconds = Mathf.Max(0.20f, stuckCheckSeconds);
            _stuckMoveEpsilon = Mathf.Max(0.5f, stuckMoveEpsilon);
            _stuckRecoverySeconds = Mathf.Max(0.20f, stuckRecoverySeconds);
            _preferredSideSign = self != null && (self.GetInstanceId() & 1UL) == 0UL ? 1 : -1;
            _stuckSideSign = _preferredSideSign;
            _blockingRecoverySideSign = _preferredSideSign;
            _followBenchmarkBandDistance = Mathf.Max(4f, benchmarkFollowBandDistance);
            _obstacleMask = obstacleMask;
            _bodyProbeRadius = Mathf.Clamp(movementBodyProbeRadius, 2f, Mathf.Max(8f, probeDistance) * 0.40f);

            _metrics = new CombatMovementMetrics();
            _globalPath = new CombatGlobalPathPlanner(
                self,
                navigationAgent,
                _metrics,
                _arrivalDistance,
                navigationThreshold,
                navigationTargetRefreshDistance,
                navigationPathReuseDistance,
                navigationRepathIntervalSeconds,
                navigationMaxTargetReuseSeconds,
                pathBudgetPerPhysicsFrame,
                obstacleMask,
                movementBodyProbeRadius);
            _staticClearance = new CombatStaticClearance(
                self,
                obstacleMask,
                probeDistance,
                _metrics,
                useForwardShapeClearance,
                movementBodyProbeRadius);
            _dynamicAvoidance = new CombatDynamicAvoidance(
                self,
                navigationAgent,
                _metrics,
                useDynamicAvoidance,
                avoidanceRadius,
                avoidanceNeighborDistance,
                avoidanceMaxNeighbors,
                avoidanceTimeHorizonAgents,
                avoidanceTimeHorizonObstacles,
                avoidancePassingLockSeconds,
                avoidanceHeadOnBiasStrength);
            _benchmark = new CombatMovementBenchmark(
                self,
                _metrics,
                benchmarkSegmentTimeoutSeconds,
                benchmarkTargetShiftResetDistance,
                benchmarkMinSuccessRate,
                benchmarkMinFollowBandRate,
                benchmarkMaxBlockingCollisionRate,
                benchmarkCpuBudgetUsec);
            _elevationRouter = new CombatElevationRouter(
                self,
                useWorldTopology,
                topologyFailClosed,
                topologyDebugLogging);
        }

        public MovementCommand Solve(
            in CombatSnapshot snapshot,
            in CombatIntent intent,
            in CombatPose pose,
            CombatBlackboard blackboard)
        {
            _metrics.BeginSolve();
            try
            {
                bool interruptibleRunEvade = intent.Type == CombatIntentType.PanicEvade
                    && (snapshot.SelfState == AshesofaDyingWorld.Combat.Model.CombatStateId.AttackStartup
                        || snapshot.SelfState == AshesofaDyingWorld.Combat.Model.CombatStateId.AttackRecovery);
                if (_self == null || (!snapshot.CanMove && !interruptibleRunEvade) || intent.IsNone)
                {
                    _hasMotorNavigationTarget = false;
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                Vector2 finalAnchor = _self.ClampWorldPointToLevelBounds(pose.Anchor, 6f);
                Vector2 semanticTarget = snapshot.HasTarget ? snapshot.TargetPosition : finalAnchor;
                Vector2 navigationOffset = _self.MovementCenter - snapshot.SelfPosition;
                Vector2 navigationSelfPosition = snapshot.SelfPosition + navigationOffset;
                CombatElevationRouter.RouteResult topologyRoute = _elevationRouter.Resolve(
                    navigationSelfPosition,
                    finalAnchor + navigationOffset,
                    semanticTarget + navigationOffset);
                Vector2 safeAnchor = _self.ClampWorldPointToLevelBounds(topologyRoute.MovementTarget, 6f);

                _motorNavigationTarget = finalAnchor;
                _motorPathTarget = safeAnchor;
                _motorSemanticTarget = semanticTarget;
                _hasMotorNavigationTarget = true;

                Vector2 toAnchor = safeAnchor - navigationSelfPosition;
                float anchorDistance = toAnchor.Length();
                float finalAnchorDistance = finalAnchor.DistanceTo(snapshot.SelfPosition);
                bool rangeSatisfied = !topologyRoute.IsTopologyRouted
                    && (!snapshot.HasTarget
                        || (snapshot.TargetDistance >= pose.DesiredRangeMin
                            && snapshot.TargetDistance <= pose.DesiredRangeMax));
                bool isContinuousStrafe = !topologyRoute.IsTopologyRouted
                    && (pose.Mode == CombatMovementMode.StrafeLeft
                        || pose.Mode == CombatMovementMode.StrafeRight);

                if (!topologyRoute.HasRoute)
                {
                    _lastDirectionSlot = -1;
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                if (!topologyRoute.IsTopologyRouted
                    && finalAnchorDistance <= _arrivalDistance
                    && rangeSatisfied
                    && !isContinuousStrafe)
                {
                    _lastDirectionSlot = -1;
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                // Khi đang đổi tầng, topology có quyền ưu tiên hơn strafe/backpedal combat.
                // Nếu không, AI có thể biết cầu thang ở đâu nhưng utility lại kéo nó chạy ngang khỏi cầu thang.
                Vector2 desiredDirection = topologyRoute.IsTopologyRouted
                    ? SafeNormalize(toAnchor)
                    : ResolveDesiredDirection(snapshot, pose, toAnchor);
                if (desiredDirection.LengthSquared() <= 0.001f)
                {
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                IsRecoveringFromStuck = UpdateStuckState(snapshot, anchorDistance);
                desiredDirection = _globalPath.ResolveDirection(
                    navigationSelfPosition,
                    safeAnchor,
                    desiredDirection,
                    anchorDistance,
                    IsRecoveringFromStuck);

                float movementSpeedFraction = Mathf.Clamp(
                    _self.Velocity.Length() / Mathf.Max(1f, _self.RunSpeed),
                    0f,
                    1.25f);
                _staticClearance.Refresh(
                    navigationSelfPosition,
                    desiredDirection,
                    IsRecoveringFromStuck || snapshot.NearObstacle || snapshot.IsCornered,
                    movementSpeedFraction);

                int bestSlot = -1;
                float bestScore = -1f;
                Vector2 bestDirection = Vector2.Zero;
                float leastDanger = float.PositiveInfinity;
                int safestFallbackSlot = -1;
                Vector2 safestFallbackDirection = Vector2.Zero;

                Vector2 recoveryDirection = ResolveRecoveryDirection(desiredDirection);

                for (int slot = 0; slot < DirectionCount; slot++)
                {
                    float angle = Mathf.Tau * slot / DirectionCount;
                    Vector2 direction = Vector2.Right.Rotated(angle);
                    float alignment = Mathf.Max(0f, direction.Dot(desiredDirection));
                    float interest = alignment * alignment;

                    if (IsRecoveringFromStuck)
                    {
                        // Khi kẹt, cho phép tạm đi ngang/thậm chí hơi ngược hướng anchor để thoát góc lõm.
                        float escapeAlignment = Mathf.Max(0f, direction.Dot(recoveryDirection));
                        interest = Mathf.Max(interest * 0.32f, escapeAlignment * escapeAlignment);
                    }

                    float predictedDistance = snapshot.HasTarget
                        ? (snapshot.SelfPosition + direction * 18f).DistanceTo(snapshot.TargetPosition)
                        : 0f;
                    float rangeInterest = snapshot.HasTarget && !topologyRoute.IsTopologyRouted
                        ? ScorePredictedRange(predictedDistance, pose.DesiredRangeMin, pose.DesiredRangeMax)
                        : 1f;
                    interest = Mathf.Clamp(0.72f * interest + 0.28f * rangeInterest, 0f, 1f);

                    float danger = SampleDanger(slot, snapshot, direction, pose, topologyRoute.IsTopologyRouted);
                    float score = interest * (1f - danger);

                    if (slot == _lastDirectionSlot)
                    {
                        score += IsRecoveringFromStuck ? 0.015f : 0.08f;
                    }

                    // Bias phía vượt nhau ổn định để giảm lắc trái/phải khi hai lựa chọn gần như bằng nhau.
                    float side = Cross(desiredDirection, direction) * _preferredSideSign;
                    if (side > 0.10f)
                    {
                        score += 0.018f;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSlot = slot;
                        bestDirection = direction;
                    }

                    bool reasonableFallback = IsRecoveringFromStuck || direction.Dot(desiredDirection) > -0.20f;
                    if (reasonableFallback && danger < leastDanger)
                    {
                        leastDanger = danger;
                        safestFallbackSlot = slot;
                        safestFallbackDirection = direction;
                    }
                }

                if (bestSlot < 0 || bestScore <= 0.03f)
                {
                    // Không đứng chết chỉ vì mọi hướng đều bị phạt. Chọn hướng ít nguy hiểm nhất,
                    // trừ trường hợp sensor xác nhận gần như bị bịt kín hoàn toàn.
                    if (safestFallbackSlot < 0 || leastDanger >= 0.98f)
                    {
                        _lastHadMovement = false;
                        return MovementCommand.Stop(snapshot.TargetPosition);
                    }

                    bestSlot = safestFallbackSlot;
                    bestDirection = safestFallbackDirection;
                    bestScore = Mathf.Clamp(1f - leastDanger, 0.04f, 0.35f);
                }

                float bestDanger = _staticClearance.GetDanger(bestSlot);
                float bestWeight = bestDanger > 0.30f || IsRecoveringFromStuck ? 0.86f : 0.72f;
                Vector2 smoothDirection = bestDirection * bestWeight + desiredDirection * (1f - bestWeight);
                smoothDirection = smoothDirection.LengthSquared() > 0.001f
                    ? smoothDirection.Normalized()
                    : bestDirection;

                _lastDirectionSlot = bestSlot;
                bool wantsRun = pose.Mode == CombatMovementMode.PanicEvade
                    || (topologyRoute.IsTopologyRouted && anchorDistance >= 72f)
                    || anchorDistance >= 112f
                    || (pose.Mode == CombatMovementMode.Approach
                        && snapshot.TargetDistance >= pose.DesiredRangeMax + 70f);
                float speedScale = ResolveSpeedScale(anchorDistance, bestDanger, IsRecoveringFromStuck);
                float baseSpeed = wantsRun ? _self.RunSpeed : _self.Speed;
                Vector2 preferredVelocity = smoothDirection * baseSpeed * speedScale;
                _lastHadMovement = true;

                return new MovementCommand(
                    smoothDirection,
                    wantsRun,
                    pose.FaceTarget,
                    snapshot.TargetPosition,
                    bestSlot,
                    bestScore,
                    speedScale,
                    preferredVelocity);
            }
            finally
            {
                _metrics.EndSolve();
            }
        }

        /// <summary>
        /// Chạy mỗi physics frame ở motor. Preferred velocity đi qua RVO nếu NavAgent có avoidance;
        /// nếu không có, hàm trả nguyên velocity nên rollout an toàn cho scene cũ.
        /// </summary>
        public Vector2 ResolveMotorVelocity(Vector2 preferredVelocity)
        {
            _metrics.BeginMotor();
            try
            {
                SampleCollisionOutcome();
                if (_self == null || preferredVelocity.LengthSquared() <= 0.001f)
                {
                    if (_hasMotorNavigationTarget)
                    {
                        _benchmark.ObserveCombatPositioning(_motorNavigationTarget, _arrivalDistance, Vector2.Zero);
                    }
                    _lastResolvedMotorVelocity = Vector2.Zero;
                    return Vector2.Zero;
                }

                Vector2 velocity = preferredVelocity;
                float speed = preferredVelocity.Length();
                if (_hasMotorNavigationTarget)
                {
                    Vector2 combatCenter = _self.CombatCenter;
                    Vector2 navigationOffset = _self.MovementCenter - combatCenter;
                    Vector2 navigationSelfPosition = combatCenter + navigationOffset;
                    CombatElevationRouter.RouteResult topologyRoute = _elevationRouter.Resolve(
                        navigationSelfPosition,
                        _motorNavigationTarget + navigationOffset,
                        _motorSemanticTarget + navigationOffset);
                    if (!topologyRoute.HasRoute)
                    {
                        _lastResolvedMotorVelocity = Vector2.Zero;
                        return Vector2.Zero;
                    }

                    _motorPathTarget = topologyRoute.MovementTarget;
                    Vector2 toPathTarget = _motorPathTarget - navigationSelfPosition;
                    Vector2 direction = topologyRoute.IsTopologyRouted && toPathTarget.LengthSquared() > 0.001f
                        ? toPathTarget.Normalized()
                        : preferredVelocity / Mathf.Max(speed, 0.001f);
                    float distance = navigationSelfPosition.DistanceTo(_motorPathTarget);
                    Vector2 pathDirection = _globalPath.ResolveDirection(
                        navigationSelfPosition,
                        _motorPathTarget,
                        direction,
                        distance,
                        false);
                    velocity = pathDirection * speed;
                }

                // P1: local clearance không còn bị khóa theo DecisionIntervalSeconds.
                // Vùng trống update thưa, gần obstacle update nhanh; cognition vẫn giữ nhịp thấp.
                ulong now = Time.GetTicksMsec();
                bool blockingRecovery = IsBlockingRecoveryActive();
                Vector2 localDesired = velocity / Mathf.Max(speed, 0.001f);
                if (blockingRecovery)
                {
                    localDesired = ResolveBlockingRecoveryDirection(localDesired);
                }
                if (now >= _nextMotorProbeTickMs)
                {
                    _nextMotorProbeTickMs = now + (_staticClearance.IsNearObstacle ? 17UL : 50UL);
                    float speedFraction = Mathf.Clamp(speed / Mathf.Max(1f, _self.RunSpeed), 0f, 1.25f);
                    _staticClearance.Refresh(
                        _self.MovementCenter,
                        localDesired,
                        blockingRecovery || IsRecoveringFromStuck || _staticClearance.IsNearObstacle,
                        speedFraction);
                }

                Vector2 localDirection = ResolveLocalClearanceDirection(localDesired);
                velocity = localDirection * speed;
                Vector2 safeVelocity = _dynamicAvoidance.ResolveVelocity(velocity);
                if (blockingRecovery)
                {
                    safeVelocity *= 0.72f;
                }
                if (_hasMotorNavigationTarget)
                {
                    _benchmark.ObserveCombatPositioning(_motorNavigationTarget, _arrivalDistance, safeVelocity);
                }
                _lastResolvedMotorVelocity = safeVelocity;
                return safeVelocity;
            }
            finally
            {
                _metrics.EndMotor();
            }
        }

        /// <summary>
        /// Dùng cho formation follow không có target. Vẫn đi global path + local clearance + RVO,
        /// tránh tình trạng combat thì thông minh nhưng vừa hết combat lại chạy thẳng vào đá.
        /// </summary>
        public Vector2 ResolveFreeMovementVelocity(
            Vector2 selfPosition,
            Vector2 targetPosition,
            Vector2 preferredVelocity,
            float successDistance = 0f)
        {
            _metrics.BeginMotor();
            try
            {
                SampleCollisionOutcome();
                float benchmarkArrival = successDistance > 0f ? successDistance : _arrivalDistance;
                if (_self == null || preferredVelocity.LengthSquared() <= 0.001f)
                {
                    _benchmark.ObserveFollow(
                        targetPosition,
                        benchmarkArrival,
                        _followBenchmarkBandDistance,
                        Vector2.Zero);
                    _lastResolvedMotorVelocity = Vector2.Zero;
                    return Vector2.Zero;
                }

                float speed = preferredVelocity.Length();
                Vector2 navigationOffset = _self.MovementCenter - selfPosition;
                Vector2 navigationSelfPosition = selfPosition + navigationOffset;
                Vector2 navigationTarget = targetPosition + navigationOffset;
                CombatElevationRouter.RouteResult topologyRoute = _elevationRouter.Resolve(
                    navigationSelfPosition,
                    navigationTarget,
                    navigationTarget);
                if (!topologyRoute.HasRoute)
                {
                    _benchmark.ObserveFollow(
                        targetPosition,
                        benchmarkArrival,
                        _followBenchmarkBandDistance,
                        Vector2.Zero);
                    _lastResolvedMotorVelocity = Vector2.Zero;
                    return Vector2.Zero;
                }

                Vector2 movementTarget = topologyRoute.MovementTarget;
                Vector2 toMovementTarget = movementTarget - navigationSelfPosition;
                Vector2 directDirection = topologyRoute.IsTopologyRouted && toMovementTarget.LengthSquared() > 0.001f
                    ? toMovementTarget.Normalized()
                    : preferredVelocity / speed;
                float distance = navigationSelfPosition.DistanceTo(movementTarget);
                Vector2 desired = _globalPath.ResolveDirection(
                    navigationSelfPosition,
                    movementTarget,
                    directDirection,
                    distance,
                    false);

                ulong now = Time.GetTicksMsec();
                bool blockingRecovery = IsBlockingRecoveryActive();
                if (blockingRecovery)
                {
                    desired = ResolveBlockingRecoveryDirection(desired);
                }
                if (now >= _nextFreeProbeTickMs)
                {
                    _nextFreeProbeTickMs = now + (_staticClearance.IsNearObstacle ? 34UL : 67UL);
                    float speedFraction = Mathf.Clamp(speed / Mathf.Max(1f, _self.RunSpeed), 0f, 1.25f);
                    _staticClearance.Refresh(
                        navigationSelfPosition,
                        desired,
                        blockingRecovery || _staticClearance.IsNearObstacle,
                        speedFraction);
                }

                Vector2 localDirection = ResolveLocalClearanceDirection(desired);
                Vector2 safeVelocity = _dynamicAvoidance.ResolveVelocity(localDirection * speed);
                if (blockingRecovery)
                {
                    safeVelocity *= 0.72f;
                }
                _benchmark.ObserveFollow(
                    targetPosition,
                    benchmarkArrival,
                    _followBenchmarkBandDistance,
                    safeVelocity);
                _lastResolvedMotorVelocity = safeVelocity;
                return safeVelocity;
            }
            finally
            {
                _metrics.EndMotor();
            }
        }

        public Vector2 ResolveFollowAnchor(
            Vector2 selfPosition,
            Vector2 leaderPosition,
            Vector2 preferredAnchor,
            Vector2 followForward,
            float followSide)
        {
            if (_self == null || !GodotObject.IsInstanceValid(_self))
            {
                return preferredAnchor;
            }

            Vector2 clampedPreferred = _self.ClampWorldPointToLevelBounds(preferredAnchor, 6f);
            Vector2 navigationOffset = _self.MovementCenter - selfPosition;
            Vector2 navigationSelfPosition = selfPosition + navigationOffset;
            Vector2 navigationLeaderPosition = leaderPosition + navigationOffset;
            if (IsFollowAnchorUsable(
                navigationSelfPosition,
                navigationLeaderPosition,
                clampedPreferred + navigationOffset))
            {
                return clampedPreferred;
            }

            Vector2 forward = followForward.LengthSquared() > 0.001f
                ? followForward.Normalized()
                : Vector2.Down;
            Vector2 side = new(-forward.Y, forward.X);
            float radius = Mathf.Clamp(leaderPosition.DistanceTo(clampedPreferred), 32f, 78f);

            Vector2[] basis =
            {
                -forward + side * followSide * 0.44f,
                -forward - side * followSide * 0.44f,
                side * followSide,
                -side * followSide,
                -forward,
                forward * 0.35f + side * followSide,
                forward * 0.35f - side * followSide
            };
            float[] distanceScales = { 1f, 0.78f, 1.22f };

            Vector2 best = clampedPreferred;
            float bestScore = float.NegativeInfinity;
            for (int d = 0; d < distanceScales.Length; d++)
            {
                float candidateRadius = radius * distanceScales[d];
                for (int i = 0; i < basis.Length; i++)
                {
                    Vector2 direction = basis[i].LengthSquared() > 0.001f
                        ? basis[i].Normalized()
                        : -forward;
                    Vector2 candidate = _self.ClampWorldPointToLevelBounds(
                        leaderPosition + direction * candidateRadius,
                        6f);
                    if (!IsFollowAnchorUsable(
                        navigationSelfPosition,
                        navigationLeaderPosition,
                        candidate + navigationOffset))
                    {
                        continue;
                    }

                    float score = 5f;
                    score -= candidate.DistanceTo(clampedPreferred) * 0.018f;
                    score -= candidate.DistanceTo(selfPosition) * 0.0025f;
                    if (i <= 1)
                    {
                        score += 0.4f;
                    }
                    if (d == 0)
                    {
                        score += 0.2f;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            return bestScore > float.NegativeInfinity ? best : clampedPreferred;
        }

        public void StopMotor()
        {
            _dynamicAvoidance.ResetVelocity();
            _lastResolvedMotorVelocity = Vector2.Zero;
        }

        public void Reset()
        {
            _lastDirectionSlot = -1;
            _lastHadMovement = false;
            _progressInitialized = false;
            _stuckRecoveryUntil = 0f;
            _hasMotorNavigationTarget = false;
            _motorNavigationTarget = Vector2.Zero;
            _motorPathTarget = Vector2.Zero;
            _motorSemanticTarget = Vector2.Zero;
            _lastCollisionSampleFrame = ulong.MaxValue;
            _lastResolvedMotorVelocity = Vector2.Zero;
            _lastBlockingNormal = Vector2.Zero;
            _blockingRecoveryUntilMs = 0;
            _blockingRecoverySideSign = _preferredSideSign;
            _nextMotorProbeTickMs = 0;
            _nextFreeProbeTickMs = 0;
            IsRecoveringFromStuck = false;
            _globalPath.Reset();
            _elevationRouter.Reset();
            _dynamicAvoidance.ResetVelocity();
            _metrics.Reset();
            _benchmark.Reset();
        }

        public void Dispose()
        {
            _globalPath.Dispose();
            _dynamicAvoidance.Dispose();
            _staticClearance.Dispose();
        }

        /// <summary>
        /// Godot giữ slide collision của MoveAndSlide vừa chạy. DecisionAgent là child của actor nên ở
        /// frame hiện tại ta đối chiếu contact đó với safe velocity mà motor đã ra ở frame trước.
        ///
        /// Raw contact chỉ là telemetry. Blocking contact mới là lỗi chất lượng: mặt va chạm phải thật sự
        /// nằm trước hướng đi VÀ vận tốc tiến bị hụt đáng kể. Đi men dọc tường không còn bị chấm như crash.
        /// </summary>
        private void SampleCollisionOutcome()
        {
            if (_self == null || !GodotObject.IsInstanceValid(_self))
            {
                return;
            }

            ulong frame = Engine.GetPhysicsFrames();
            if (frame == _lastCollisionSampleFrame)
            {
                return;
            }

            _lastCollisionSampleFrame = frame;
            int contacts = _self.GetSlideCollisionCount();
            if (contacts <= 0)
            {
                return;
            }

            _metrics.RecordRawCollisionFrame(contacts);

            float expectedSpeed = _lastResolvedMotorVelocity.Length();
            if (expectedSpeed <= 4f)
            {
                return;
            }

            Vector2 moveDirection = _lastResolvedMotorVelocity / expectedSpeed;
            float actualForwardSpeed = Mathf.Max(0f, _self.GetRealVelocity().Dot(moveDirection));
            float forwardLoss = 1f - Mathf.Clamp(actualForwardSpeed / expectedSpeed, 0f, 1f);
            if (forwardLoss < 0.20f)
            {
                // Vẫn tiến gần đủ tốc độ: đây thường chỉ là contact trượt/tangent vô hại.
                return;
            }

            int blockingContacts = 0;
            Vector2 blockingNormalSum = Vector2.Zero;
            for (int i = 0; i < contacts; i++)
            {
                KinematicCollision2D collision = _self.GetSlideCollision(i);
                if (collision == null)
                {
                    continue;
                }

                Vector2 normal = collision.GetNormal();
                if (normal.LengthSquared() <= 0.001f)
                {
                    continue;
                }

                // Normal hướng ra khỏi mặt va chạm. Giá trị dương ở đây nghĩa là actor đang lao vào mặt đó.
                float headOn = -moveDirection.Dot(normal.Normalized());
                if (headOn >= 0.35f)
                {
                    blockingContacts++;
                    blockingNormalSum += normal.Normalized() * headOn;
                }
            }

            if (blockingContacts > 0)
            {
                _metrics.RecordBlockingCollisionFrame(blockingContacts);
                if (blockingNormalSum.LengthSquared() > 0.001f)
                {
                    Vector2 normal = blockingNormalSum.Normalized();
                    if (_lastBlockingNormal.LengthSquared() > 0.001f
                        && normal.Dot(_lastBlockingNormal) < 0.35f)
                    {
                        _blockingRecoverySideSign *= -1;
                    }
                    _lastBlockingNormal = normal;
                    _blockingRecoveryUntilMs = Time.GetTicksMsec() + 420UL;
                    _nextMotorProbeTickMs = 0;
                    _nextFreeProbeTickMs = 0;
                }
            }
        }

        private bool IsBlockingRecoveryActive()
        {
            return _blockingRecoveryUntilMs > Time.GetTicksMsec()
                && _lastBlockingNormal.LengthSquared() > 0.001f;
        }

        private Vector2 ResolveBlockingRecoveryDirection(Vector2 desired)
        {
            Vector2 normal = _lastBlockingNormal.LengthSquared() > 0.001f
                ? _lastBlockingNormal.Normalized()
                : Vector2.Zero;
            Vector2 wanted = desired.LengthSquared() > 0.001f
                ? desired.Normalized()
                : Vector2.Zero;
            if (normal == Vector2.Zero)
            {
                return wanted;
            }

            Vector2 tangent = new(-normal.Y, normal.X);
            if (wanted.LengthSquared() > 0.001f)
            {
                float tangentDot = tangent.Dot(wanted);
                if (Mathf.Abs(tangentDot) > 0.08f)
                {
                    tangent *= Mathf.Sign(tangentDot);
                    _blockingRecoverySideSign = tangentDot >= 0f ? 1 : -1;
                }
                else
                {
                    tangent *= _blockingRecoverySideSign;
                }
            }
            else
            {
                tangent *= _blockingRecoverySideSign;
            }

            Vector2 escape = tangent * 0.88f + normal * 0.34f;
            if (wanted.LengthSquared() > 0.001f && wanted.Dot(normal) > 0.25f)
            {
                escape += wanted * 0.18f;
            }

            return escape.LengthSquared() > 0.001f ? escape.Normalized() : normal;
        }

        private Vector2 ResolveLocalClearanceDirection(Vector2 desired)
        {
            if (desired.LengthSquared() <= 0.001f)
            {
                return Vector2.Zero;
            }

            Vector2 normalizedDesired = desired.Normalized();
            int bestSlot = 0;
            float bestScore = float.NegativeInfinity;
            Vector2 bestDirection = normalizedDesired;
            for (int slot = 0; slot < DirectionCount; slot++)
            {
                float angle = Mathf.Tau * slot / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                float alignment = Mathf.Max(0f, direction.Dot(normalizedDesired));
                float danger = _staticClearance.GetDanger(slot);
                float score = alignment * alignment * (1f - danger);
                float side = Cross(normalizedDesired, direction) * _preferredSideSign;
                if (side > 0.10f)
                {
                    score += 0.012f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                    bestDirection = direction;
                }
            }

            float localDanger = _staticClearance.GetDanger(bestSlot);
            if (localDanger <= 0.02f)
            {
                return normalizedDesired;
            }

            float localWeight = localDanger > 0.25f ? 0.84f : 0.60f;
            Vector2 blended = bestDirection * localWeight + normalizedDesired * (1f - localWeight);
            return blended.LengthSquared() > 0.001f ? blended.Normalized() : bestDirection;
        }

        private bool IsFollowAnchorUsable(Vector2 selfPosition, Vector2 leaderPosition, Vector2 anchor)
        {
            return !IsCircleBlocked(anchor)
                && !IsSegmentBlocked(leaderPosition, anchor)
                && !IsSegmentBlocked(selfPosition, anchor);
        }

        private bool IsCircleBlocked(Vector2 center)
        {
            if (_self?.GetWorld2D()?.DirectSpaceState == null)
            {
                return false;
            }

            if (IsPointBlocked(center))
            {
                return true;
            }

            float radius = Mathf.Max(2f, _bodyProbeRadius);
            return IsPointBlocked(center + Vector2.Right * radius)
                || IsPointBlocked(center + Vector2.Left * radius)
                || IsPointBlocked(center + Vector2.Up * radius)
                || IsPointBlocked(center + Vector2.Down * radius);
        }

        private bool IsPointBlocked(Vector2 point)
        {
            var query = new PhysicsPointQueryParameters2D
            {
                Position = point,
                CollisionMask = _obstacleMask,
                CollideWithAreas = false,
                CollideWithBodies = true
            };
            return _self.GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
        }

        private bool IsSegmentBlocked(Vector2 from, Vector2 to)
        {
            if (_self?.GetWorld2D()?.DirectSpaceState == null || from.DistanceSquaredTo(to) <= 1f)
            {
                return false;
            }

            PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
                from,
                to,
                _obstacleMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            return _self.GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
        }

        private Vector2 ResolveDesiredDirection(
            in CombatSnapshot snapshot,
            in CombatPose pose,
            Vector2 toAnchor)
        {
            Vector2 targetDirection = snapshot.DirectionToTarget.LengthSquared() > 0.001f
                ? snapshot.DirectionToTarget.Normalized()
                : Vector2.Down;
            Vector2 tangentLeft = new(-targetDirection.Y, targetDirection.X);

            return pose.Mode switch
            {
                CombatMovementMode.Backpedal => -targetDirection,
                CombatMovementMode.PanicEvade => snapshot.HasSafeRetreatVector
                    ? snapshot.SafeRetreatVector.Normalized()
                    : -targetDirection,
                CombatMovementMode.StrafeLeft => (tangentLeft * 0.82f + SafeNormalize(toAnchor) * 0.18f).Normalized(),
                CombatMovementMode.StrafeRight => (-tangentLeft * 0.82f + SafeNormalize(toAnchor) * 0.18f).Normalized(),
                _ => SafeNormalize(toAnchor)
            };
        }

        private float SampleDanger(
            int slot,
            in CombatSnapshot snapshot,
            Vector2 direction,
            in CombatPose pose,
            bool ignoreCombatRange)
        {
            float danger = _staticClearance.GetDanger(slot);

            if (snapshot.HasTarget && !ignoreCombatRange)
            {
                bool movingTowardTarget = direction.Dot(snapshot.DirectionToTarget) > 0.35f;
                if (snapshot.TargetDistance < pose.DesiredRangeMin && movingTowardTarget)
                {
                    float closePressure = 1f - Mathf.Clamp(
                        snapshot.TargetDistance / Mathf.Max(1f, pose.DesiredRangeMin),
                        0f,
                        1f);
                    danger = Mathf.Max(danger, 0.55f + 0.40f * closePressure);
                }
            }

            if (snapshot.HasLeader && snapshot.DistanceToLeader < 34f)
            {
                Vector2 toLeader = snapshot.LeaderPosition - snapshot.SelfPosition;
                if (toLeader.LengthSquared() > 0.001f && direction.Dot(toLeader.Normalized()) > 0.25f)
                {
                    danger = Mathf.Max(danger, 0.72f);
                }
            }

            return Mathf.Clamp(danger, 0f, 1f);
        }

        private bool UpdateStuckState(in CombatSnapshot snapshot, float anchorDistance)
        {
            if (!_progressInitialized)
            {
                ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                return false;
            }

            if (!_lastHadMovement || anchorDistance <= _arrivalDistance * 1.5f)
            {
                ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                return snapshot.TimeSeconds < _stuckRecoveryUntil;
            }

            float elapsed = snapshot.TimeSeconds - _progressSampleTime;
            if (elapsed >= _stuckCheckSeconds)
            {
                float moved = snapshot.SelfPosition.DistanceTo(_progressSamplePosition);
                if (moved < _stuckMoveEpsilon)
                {
                    _stuckRecoveryUntil = snapshot.TimeSeconds + _stuckRecoverySeconds;
                    _stuckSideSign *= -1;
                    _metrics.RecordStuck();
                }

                _progressSamplePosition = snapshot.SelfPosition;
                _progressSampleTime = snapshot.TimeSeconds;
            }

            return snapshot.TimeSeconds < _stuckRecoveryUntil;
        }

        private Vector2 ResolveRecoveryDirection(Vector2 desiredDirection)
        {
            Vector2 desired = desiredDirection.LengthSquared() > 0.001f
                ? desiredDirection.Normalized()
                : Vector2.Down;
            Vector2 tangent = new Vector2(-desired.Y, desired.X) * _stuckSideSign;
            Vector2 escape = tangent * 0.88f - desired * 0.34f;
            return escape.LengthSquared() > 0.001f ? escape.Normalized() : tangent;
        }

        private void ResetProgress(Vector2 position, float timeSeconds)
        {
            _progressInitialized = true;
            _progressSamplePosition = position;
            _progressSampleTime = timeSeconds;
            _lastHadMovement = false;
            IsRecoveringFromStuck = false;
        }

        private float ResolveSpeedScale(float anchorDistance, float obstacleDanger, bool stuckRecovery)
        {
            if (stuckRecovery)
            {
                return 0.72f;
            }

            float arrivalSlow = Mathf.Clamp(
                (anchorDistance - _arrivalDistance) / Mathf.Max(8f, 36f - _arrivalDistance),
                0.35f,
                1f);
            float obstacleSlow = Mathf.Lerp(1f, 0.68f, Mathf.Clamp(obstacleDanger, 0f, 1f));
            return Mathf.Clamp(arrivalSlow * obstacleSlow, 0.28f, 1f);
        }

        private static float ScorePredictedRange(float distance, float minimum, float maximum)
        {
            float edge = Mathf.Max(8f, (maximum - minimum) * 0.5f);
            return ResponseCurve.SmoothBand(distance, minimum, maximum, edge);
        }

        private static Vector2 SafeNormalize(Vector2 value)
        {
            return value.LengthSquared() > 0.001f ? value.Normalized() : Vector2.Zero;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }
    }
}
