using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// P1 movement stack:
    /// 1) Global path: NavigationAgent2D cho đường dài.
    /// 2) Static clearance: context steering 16 hướng né tường/đá ở cự ly gần.
    /// 3) Dynamic avoidance: preferred velocity -> RVO safe velocity khi NavAgent hỗ trợ.
    ///
    /// P1 bổ sung spatial hash, corridor reuse, passing-side lock và adaptive ray/ShapeCast.
    /// Nếu thiếu NavAgent/nav map, global path + RVO tự fallback; local solver vẫn hoạt động độc lập.
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

        private readonly CombatMovementMetrics _metrics;
        private readonly CombatGlobalPathPlanner _globalPath;
        private readonly CombatStaticClearance _staticClearance;
        private readonly CombatDynamicAvoidance _dynamicAvoidance;

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
        private bool _hasMotorNavigationTarget;
        private Vector2 _motorNavigationTarget;

        public CombatMovementMetrics Metrics => _metrics;
        public bool IsRecoveringFromStuck { get; private set; }

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
            float stuckRecoverySeconds = 0.55f)
        {
            _self = self;
            _arrivalDistance = Mathf.Max(1f, arrivalDistance);
            _stuckCheckSeconds = Mathf.Max(0.20f, stuckCheckSeconds);
            _stuckMoveEpsilon = Mathf.Max(0.5f, stuckMoveEpsilon);
            _stuckRecoverySeconds = Mathf.Max(0.20f, stuckRecoverySeconds);
            _preferredSideSign = self != null && (self.GetInstanceId() & 1UL) == 0UL ? 1 : -1;
            _stuckSideSign = _preferredSideSign;

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
                pathBudgetPerPhysicsFrame);
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

                Vector2 safeAnchor = _self.ClampWorldPointToLevelBounds(pose.Anchor, 6f);
                _motorNavigationTarget = safeAnchor;
                _hasMotorNavigationTarget = true;
                Vector2 toAnchor = safeAnchor - snapshot.SelfPosition;
                float anchorDistance = toAnchor.Length();
                bool rangeSatisfied = !snapshot.HasTarget
                    || (snapshot.TargetDistance >= pose.DesiredRangeMin
                        && snapshot.TargetDistance <= pose.DesiredRangeMax);
                bool isContinuousStrafe = pose.Mode == CombatMovementMode.StrafeLeft
                    || pose.Mode == CombatMovementMode.StrafeRight;

                if (anchorDistance <= _arrivalDistance && rangeSatisfied && !isContinuousStrafe)
                {
                    _lastDirectionSlot = -1;
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                Vector2 desiredDirection = ResolveDesiredDirection(snapshot, pose, toAnchor);
                if (desiredDirection.LengthSquared() <= 0.001f)
                {
                    ResetProgress(snapshot.SelfPosition, snapshot.TimeSeconds);
                    return MovementCommand.Stop(snapshot.TargetPosition);
                }

                IsRecoveringFromStuck = UpdateStuckState(snapshot, anchorDistance);
                desiredDirection = _globalPath.ResolveDirection(
                    snapshot.SelfPosition,
                    safeAnchor,
                    desiredDirection,
                    anchorDistance,
                    IsRecoveringFromStuck);

                float movementSpeedFraction = Mathf.Clamp(
                    _self.Velocity.Length() / Mathf.Max(1f, _self.RunSpeed),
                    0f,
                    1.25f);
                _staticClearance.Refresh(
                    snapshot.SelfPosition,
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
                    float rangeInterest = snapshot.HasTarget
                        ? ScorePredictedRange(predictedDistance, pose.DesiredRangeMin, pose.DesiredRangeMax)
                        : 1f;
                    interest = Mathf.Clamp(0.72f * interest + 0.28f * rangeInterest, 0f, 1f);

                    float danger = SampleDanger(slot, snapshot, direction, pose);
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
            SampleCollisionBaseline();
            if (_self == null || preferredVelocity.LengthSquared() <= 0.001f)
            {
                return Vector2.Zero;
            }

            Vector2 velocity = preferredVelocity;
            float speed = preferredVelocity.Length();
            if (_hasMotorNavigationTarget)
            {
                Vector2 direction = preferredVelocity / Mathf.Max(speed, 0.001f);
                float distance = _self.CombatCenter.DistanceTo(_motorNavigationTarget);
                Vector2 pathDirection = _globalPath.ResolveDirection(
                    _self.CombatCenter,
                    _motorNavigationTarget,
                    direction,
                    distance,
                    false);
                velocity = pathDirection * speed;
            }

            // P1: local clearance không còn bị khóa theo DecisionIntervalSeconds.
            // Vùng trống update thưa, gần obstacle update nhanh; cognition vẫn giữ nhịp thấp.
            ulong now = Time.GetTicksMsec();
            Vector2 localDesired = velocity / Mathf.Max(speed, 0.001f);
            if (now >= _nextMotorProbeTickMs)
            {
                _nextMotorProbeTickMs = now + (_staticClearance.IsNearObstacle ? 17UL : 50UL);
                float speedFraction = Mathf.Clamp(speed / Mathf.Max(1f, _self.RunSpeed), 0f, 1.25f);
                _staticClearance.Refresh(
                    _self.CombatCenter,
                    localDesired,
                    IsRecoveringFromStuck || _staticClearance.IsNearObstacle,
                    speedFraction);
            }

            Vector2 localDirection = ResolveLocalClearanceDirection(localDesired);
            velocity = localDirection * speed;
            return _dynamicAvoidance.ResolveVelocity(velocity);
        }

        /// <summary>
        /// Dùng cho formation follow không có target. Vẫn đi global path + local clearance + RVO,
        /// tránh tình trạng combat thì thông minh nhưng vừa hết combat lại chạy thẳng vào đá.
        /// </summary>
        public Vector2 ResolveFreeMovementVelocity(
            Vector2 selfPosition,
            Vector2 targetPosition,
            Vector2 preferredVelocity)
        {
            SampleCollisionBaseline();
            if (_self == null || preferredVelocity.LengthSquared() <= 0.001f)
            {
                return Vector2.Zero;
            }

            float speed = preferredVelocity.Length();
            Vector2 directDirection = preferredVelocity / speed;
            float distance = selfPosition.DistanceTo(targetPosition);
            Vector2 desired = _globalPath.ResolveDirection(
                selfPosition,
                targetPosition,
                directDirection,
                distance,
                false);

            ulong now = Time.GetTicksMsec();
            if (now >= _nextFreeProbeTickMs)
            {
                _nextFreeProbeTickMs = now + (_staticClearance.IsNearObstacle ? 34UL : 67UL);
                float speedFraction = Mathf.Clamp(speed / Mathf.Max(1f, _self.RunSpeed), 0f, 1.25f);
                _staticClearance.Refresh(
                    selfPosition,
                    desired,
                    _staticClearance.IsNearObstacle,
                    speedFraction);
            }

            Vector2 localDirection = ResolveLocalClearanceDirection(desired);
            return _dynamicAvoidance.ResolveVelocity(localDirection * speed);
        }

        public void StopMotor()
        {
            _dynamicAvoidance.ResetVelocity();
        }

        public void Reset()
        {
            _lastDirectionSlot = -1;
            _lastHadMovement = false;
            _progressInitialized = false;
            _stuckRecoveryUntil = 0f;
            _hasMotorNavigationTarget = false;
            _lastCollisionSampleFrame = ulong.MaxValue;
            _nextMotorProbeTickMs = 0;
            _nextFreeProbeTickMs = 0;
            IsRecoveringFromStuck = false;
            _globalPath.Reset();
            _dynamicAvoidance.ResetVelocity();
            _metrics.Reset();
        }

        public void Dispose()
        {
            _dynamicAvoidance.Dispose();
            _staticClearance.Dispose();
        }

        private void SampleCollisionBaseline()
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
            if (contacts > 0)
            {
                _metrics.RecordCollisionFrame(contacts);
            }
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
            in CombatPose pose)
        {
            float danger = _staticClearance.GetDanger(slot);

            if (snapshot.HasTarget)
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
