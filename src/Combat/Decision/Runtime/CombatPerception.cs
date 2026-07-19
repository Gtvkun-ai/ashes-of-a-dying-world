using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Cổng duy nhất đọc thế giới cho Decision Core.
    /// Foundation chỉ đo những gì repo hiện chứng minh được; các sensor chưa có sẽ để false thay vì bịa dữ liệu.
    /// </summary>
    public sealed class CombatPerception : ICombatPerception
    {
        private readonly SceneTree _tree;
        private readonly RayCast2D _lineOfSightRay;
        private readonly IThreatPredictor _threatPredictor;
        private readonly float _enemySearchRadius;
        private readonly float _leaderDangerRadius;

        public CombatPerception(
            SceneTree tree,
            RayCast2D lineOfSightRay,
            IThreatPredictor threatPredictor,
            float enemySearchRadius,
            float leaderDangerRadius)
        {
            _tree = tree;
            _lineOfSightRay = lineOfSightRay;
            _threatPredictor = threatPredictor;
            _enemySearchRadius = Mathf.Max(1f, enemySearchRadius);
            _leaderDangerRadius = Mathf.Max(1f, leaderDangerRadius);
        }

        public CombatSnapshot BuildSnapshot(
            CombatCharacter self,
            CombatCharacter leader,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard,
            float timeSeconds)
        {
            CombatCharacter target = ResolveTarget(self, assignment, blackboard);
            bool hasTarget = IsUsable(target) && target.IsAlive;
            Vector2 targetPosition = hasTarget ? target.CombatCenter : blackboard.LastKnownTargetPosition;
            Vector2 toTarget = hasTarget ? targetPosition - self.CombatCenter : Vector2.Zero;
            float targetDistance = toTarget.Length();
            Vector2 directionToTarget = targetDistance > 0.001f
                ? toTarget / targetDistance
                : Vector2.Zero;

            bool hasLineOfSight = hasTarget && EvaluateLineOfSight(self, target);
            bool targetFacingSelf = hasTarget
                && directionToTarget != Vector2.Zero
                && target.FacingDirection.Dot(-directionToTarget) >= 0.35f;
            CombatStateId? targetState = hasTarget && target.StateMachine != null
                ? target.StateMachine.Current
                : null;
            bool targetInRecovery = targetState == CombatStateId.AttackRecovery;

            ThreatAssessment threat = hasTarget
                ? _threatPredictor.EvaluateThreats(self, target, targetDistance)
                : ThreatAssessment.None;

            if (hasTarget)
            {
                blackboard.CurrentTargetId = target.GetInstanceId();
                blackboard.LastKnownTargetPosition = targetPosition;
                if (hasLineOfSight)
                {
                    blackboard.LastSeenTargetTime = Mathf.Max(0f, timeSeconds);
                }
            }
            else
            {
                blackboard.CurrentTargetId = null;
            }

            bool hasLeader = IsUsable(leader) && leader.IsAlive;
            Vector2 leaderPosition = hasLeader ? leader.CombatCenter : Vector2.Zero;
            float distanceToLeader = hasLeader
                ? self.CombatCenter.DistanceTo(leaderPosition)
                : 0f;
            bool leaderThreatened = hasLeader && IsActorThreatened(leader, _leaderDangerRadius);

            return new CombatSnapshot(
                self.GetInstanceId(),
                self.StateMachine?.Current ?? CombatStateId.Locomotion,
                Ratio(self.Stats?.CurrentHP ?? 0f, self.Stats?.MaxHP ?? 0f),
                Ratio(self.Stats?.CurrentMP ?? 0f, self.Stats?.MaxMP ?? 0f),
                Ratio(self.Stats?.CurrentStamina ?? 0f, self.Stats?.MaxStamina ?? 0f),
                Ratio(self.Stats?.CurrentGuard ?? 0f, self.Stats?.MaxGuard ?? 0f),
                Ratio(self.Stats?.CurrentPoise ?? 0f, self.Stats?.MaxPoise ?? 0f),
                hasTarget ? target.GetInstanceId() : null,
                targetPosition,
                targetDistance,
                directionToTarget,
                hasLineOfSight,
                targetFacingSelf,
                targetInRecovery,
                false,
                targetState,
                threat.EtaSeconds,
                threat.Severity,
                hasLeader ? leader.GetInstanceId() : null,
                leaderPosition,
                distanceToLeader,
                leaderThreatened,
                false,
                Vector2.Zero,
                false,
                false,
                timeSeconds);
        }

        private CombatCharacter ResolveTarget(
            CombatCharacter self,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard)
        {
            if (assignment.HasValue)
            {
                CombatCharacter assigned = assignment.Value.PriorityTarget;
                if (IsValidHostile(self, assigned, _enemySearchRadius * 1.5f))
                {
                    return assigned;
                }
            }

            if (blackboard.CurrentTargetId.HasValue)
            {
                CombatCharacter remembered = FindCombatantById(blackboard.CurrentTargetId.Value);
                if (IsValidHostile(self, remembered, _enemySearchRadius * 1.25f))
                {
                    return remembered;
                }
            }

            CombatCharacter nearest = null;
            float bestDistanceSquared = _enemySearchRadius * _enemySearchRadius;
            if (_tree == null)
            {
                return null;
            }

            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate
                    || !IsValidHostile(self, candidate, _enemySearchRadius))
                {
                    continue;
                }

                float distanceSquared = self.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                nearest = candidate;
                bestDistanceSquared = distanceSquared;
            }

            return nearest;
        }

        private CombatCharacter FindCombatantById(ulong instanceId)
        {
            if (_tree == null)
            {
                return null;
            }

            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is CombatCharacter combatant && combatant.GetInstanceId() == instanceId)
                {
                    return combatant;
                }
            }

            return null;
        }

        private bool IsActorThreatened(CombatCharacter actor, float radius)
        {
            if (_tree == null || !IsUsable(actor))
            {
                return false;
            }

            float radiusSquared = radius * radius;
            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter hostile
                    || hostile == actor
                    || !hostile.IsAlive
                    || !FactionRules.CanDamage(hostile.Faction, actor.Faction))
                {
                    continue;
                }

                float distanceSquared = actor.CombatCenter.DistanceSquaredTo(hostile.CombatCenter);
                if (distanceSquared > radiusSquared || hostile.StateMachine == null)
                {
                    continue;
                }

                CombatStateId state = hostile.StateMachine.Current;
                if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
                {
                    continue;
                }

                Vector2 toActor = actor.CombatCenter - hostile.CombatCenter;
                if (toActor.LengthSquared() > 0.001f
                    && hostile.FacingDirection.Dot(toActor.Normalized()) >= 0.25f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool EvaluateLineOfSight(CombatCharacter self, CombatCharacter target)
        {
            if (_lineOfSightRay == null || !GodotObject.IsInstanceValid(_lineOfSightRay))
            {
                // Không có sensor thì không được giả định nhìn xuyên tường.
                return false;
            }

            _lineOfSightRay.TargetPosition = _lineOfSightRay.ToLocal(target.CombatCenter);
            _lineOfSightRay.ForceRaycastUpdate();
            if (!_lineOfSightRay.IsColliding())
            {
                return true;
            }

            GodotObject collider = _lineOfSightRay.GetCollider();
            if (collider == target)
            {
                return true;
            }

            return collider is Node colliderNode && target.IsAncestorOf(colliderNode);
        }

        private static bool IsValidHostile(CombatCharacter self, CombatCharacter candidate, float radius)
        {
            return IsUsable(candidate)
                && candidate != self
                && candidate.IsAlive
                && FactionRules.CanDamage(self.Faction, candidate.Faction)
                && self.CombatCenter.DistanceSquaredTo(candidate.CombatCenter) <= radius * radius;
        }

        private static float Ratio(float current, float maximum)
        {
            return maximum <= 0.001f
                ? 0f
                : Mathf.Clamp(current / maximum, 0f, 1f);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
