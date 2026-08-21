using Godot;
using System;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Context steering 16 hướng: interest chọn hướng đạt anchor/range, danger phạt obstacle.
    /// NavigationAgent2D chỉ hỗ trợ đường dài; local solver vẫn chịu trách nhiệm đứng đẹp quanh target.
    /// </summary>
    public sealed class CombatMovementSolver
    {
        private const int DirectionCount = 16;

        private readonly CombatCharacter _self;
        private readonly NavigationAgent2D _navigationAgent;
        private readonly RayCast2D[] _dangerRays = new RayCast2D[DirectionCount];
        private readonly float _probeDistance;
        private readonly float _arrivalDistance;
        private readonly float _navigationThreshold;
        private readonly uint _obstacleMask;

        private int _lastDirectionSlot = -1;
        private Vector2 _lastNavigationTarget = new(float.PositiveInfinity, float.PositiveInfinity);

        public CombatMovementSolver(
            CombatCharacter self,
            NavigationAgent2D navigationAgent,
            uint obstacleMask,
            float probeDistance,
            float arrivalDistance,
            float navigationThreshold)
        {
            _self = self;
            _navigationAgent = navigationAgent;
            _obstacleMask = obstacleMask;
            _probeDistance = Mathf.Max(8f, probeDistance);
            _arrivalDistance = Mathf.Max(1f, arrivalDistance);
            _navigationThreshold = Mathf.Max(_arrivalDistance, navigationThreshold);
            BuildSensorRig();
        }

        public MovementCommand Solve(
            in CombatSnapshot snapshot,
            in CombatIntent intent,
            in CombatPose pose,
            CombatBlackboard blackboard)
        {
            bool interruptibleRunEvade = intent.Type == CombatIntentType.PanicEvade
                && (snapshot.SelfState == AshesofaDyingWorld.Combat.Model.CombatStateId.AttackStartup
                    || snapshot.SelfState == AshesofaDyingWorld.Combat.Model.CombatStateId.AttackRecovery);
            if (_self == null || (!snapshot.CanMove && !interruptibleRunEvade) || intent.IsNone)
            {
                return MovementCommand.Stop(snapshot.TargetPosition);
            }

            Vector2 safeAnchor = _self.ClampWorldPointToLevelBounds(pose.Anchor, 6f);
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
                return MovementCommand.Stop(snapshot.TargetPosition);
            }

            Vector2 desiredDirection = ResolveDesiredDirection(snapshot, pose, toAnchor);
            if (desiredDirection.LengthSquared() <= 0.001f)
            {
                return MovementCommand.Stop(snapshot.TargetPosition);
            }

            desiredDirection = BlendNavigationDirection(snapshot, pose, desiredDirection, anchorDistance, safeAnchor);
            int bestSlot = -1;
            float bestScore = -1f;
            Vector2 bestDirection = Vector2.Zero;

            for (int slot = 0; slot < DirectionCount; slot++)
            {
                float angle = Mathf.Tau * slot / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                float alignment = Mathf.Max(0f, direction.Dot(desiredDirection));
                float interest = alignment * alignment;

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
                    score += 0.10f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                    bestDirection = direction;
                }
            }

            if (bestSlot < 0 || bestScore <= 0.03f)
            {
                return MovementCommand.Stop(snapshot.TargetPosition);
            }

            // Nội suy nhẹ giữa hướng tốt nhất và desired vector để không lộ 16 nấc cứng.
            Vector2 smoothDirection = (bestDirection * 0.72f + desiredDirection * 0.28f).Normalized();
            _lastDirectionSlot = bestSlot;
            bool wantsRun = pose.Mode == CombatMovementMode.PanicEvade
                || anchorDistance >= 112f
                || (pose.Mode == CombatMovementMode.Approach && snapshot.TargetDistance >= pose.DesiredRangeMax + 70f);
            return new MovementCommand(
                smoothDirection,
                wantsRun,
                pose.FaceTarget,
                snapshot.TargetPosition,
                bestSlot,
                bestScore);
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
                CombatMovementMode.StrafeLeft => (tangentLeft * 0.82f + toAnchor.Normalized() * 0.18f).Normalized(),
                CombatMovementMode.StrafeRight => (-tangentLeft * 0.82f + toAnchor.Normalized() * 0.18f).Normalized(),
                _ => toAnchor.LengthSquared() <= 0.001f ? Vector2.Zero : toAnchor.Normalized()
            };
        }

        private Vector2 BlendNavigationDirection(
            in CombatSnapshot snapshot,
            in CombatPose pose,
            Vector2 desiredDirection,
            float anchorDistance,
            Vector2 navigationTarget)
        {
            if (_navigationAgent == null
                || !_navigationAgent.IsInsideTree()
                || anchorDistance < _navigationThreshold)
            {
                return desiredDirection;
            }

            if (_lastNavigationTarget.DistanceSquaredTo(navigationTarget) > 64f)
            {
                _lastNavigationTarget = navigationTarget;
                _navigationAgent.TargetPosition = navigationTarget;
            }

            if (_navigationAgent.IsNavigationFinished())
            {
                return desiredDirection;
            }

            Vector2 next = _navigationAgent.GetNextPathPosition();
            Vector2 navDirection = next - snapshot.SelfPosition;
            if (navDirection.LengthSquared() <= 0.001f)
            {
                return desiredDirection;
            }

            return (desiredDirection * 0.45f + navDirection.Normalized() * 0.55f).Normalized();
        }

        private float SampleDanger(
            int slot,
            in CombatSnapshot snapshot,
            Vector2 direction,
            in CombatPose pose)
        {
            float danger = 0f;
            RayCast2D ray = _dangerRays[slot];
            if (ray != null && GodotObject.IsInstanceValid(ray) && ray.IsInsideTree())
            {
                ray.ForceRaycastUpdate();
                if (ray.IsColliding())
                {
                    float hitDistance = snapshot.SelfPosition.DistanceTo(ray.GetCollisionPoint());
                    danger = Mathf.Max(danger, 1f - Mathf.Clamp(hitDistance / _probeDistance, 0f, 1f));
                }
            }

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

        private static float ScorePredictedRange(float distance, float minimum, float maximum)
        {
            float edge = Mathf.Max(8f, (maximum - minimum) * 0.5f);
            return ResponseCurve.SmoothBand(distance, minimum, maximum, edge);
        }

        private void BuildSensorRig()
        {
            if (_self == null || !_self.IsInsideTree())
            {
                return;
            }

            var rig = new Node2D { Name = "CombatMovementSensorsRuntime" };

            for (int slot = 0; slot < DirectionCount; slot++)
            {
                float angle = Mathf.Tau * slot / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                var ray = new RayCast2D
                {
                    Name = $"DangerRay{slot:00}",
                    Enabled = true,
                    TargetPosition = direction * _probeDistance,
                    CollisionMask = _obstacleMask,
                    CollideWithAreas = false,
                    CollideWithBodies = true,
                    ExcludeParent = false
                };
                rig.AddChild(ray);
                ray.AddException(_self);
                _dangerRays[slot] = ray;
            }

            // Solver thường được dựng từ một deferred Initialize, nhưng không giả định lifecycle đó.
            // Gắn cả rig một lần ở deferred frame để không đụng pha parent đang setup children.
            _self.CallDeferred("add_child", rig);
        }
    }
}
