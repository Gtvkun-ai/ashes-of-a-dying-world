using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Party
{
    /// <summary>
    /// Director bản đầu chỉ giao target ưu tiên, actor được bảo vệ và backline anchor.
    /// Nó không điều khiển từng frame, vì một ông sếp tốt không tự giật bàn phím của nhân viên.
    /// </summary>
    public sealed class PartyTacticalDirector
    {
        private readonly SceneTree _tree;
        private readonly float _searchRadius;
        private readonly float _leaderDangerRadius;
        private readonly float _backlineOffset;

        public PartyTacticalDirector(
            SceneTree tree,
            float searchRadius,
            float leaderDangerRadius,
            float backlineOffset = 48f)
        {
            _tree = tree;
            _searchRadius = Mathf.Max(1f, searchRadius);
            _leaderDangerRadius = Mathf.Max(1f, leaderDangerRadius);
            _backlineOffset = Mathf.Max(8f, backlineOffset);
        }

        public CombatRoleAssignment? GetAssignment(
            CombatCharacter actor,
            CombatCharacter leader,
            CombatBlackboard blackboard)
        {
            if (!IsUsable(actor))
            {
                return null;
            }

            CombatCharacter priorityTarget = FindLeaderThreat(actor, leader)
                ?? FindRememberedTarget(actor, blackboard)
                ?? FindNearestHostile(actor);

            Vector2 anchor = actor.CombatCenter;
            if (IsUsable(leader))
            {
                if (IsUsable(priorityTarget))
                {
                    Vector2 leaderToTarget = priorityTarget.CombatCenter - leader.CombatCenter;
                    Vector2 awayFromTarget = leaderToTarget.LengthSquared() <= 0.001f
                        ? -leader.FacingDirection
                        : -leaderToTarget.Normalized();
                    anchor = leader.CombatCenter + awayFromTarget * _backlineOffset;
                }
                else
                {
                    anchor = leader.CombatCenter - leader.FacingDirection * _backlineOffset;
                }
            }

            return new CombatRoleAssignment(
                CombatRoleId.BacklineController,
                CombatRoleId.Protector,
                priorityTarget,
                leader,
                anchor,
                0.5f);
        }

        private CombatCharacter FindLeaderThreat(CombatCharacter actor, CombatCharacter leader)
        {
            if (_tree == null || !IsUsable(leader))
            {
                return null;
            }

            CombatCharacter best = null;
            float bestScore = float.NegativeInfinity;
            float dangerRadiusSquared = _leaderDangerRadius * _leaderDangerRadius;
            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter hostile
                    || !IsHostile(actor, hostile)
                    || hostile.CombatCenter.DistanceSquaredTo(leader.CombatCenter) > dangerRadiusSquared)
                {
                    continue;
                }

                CombatStateId state = hostile.StateMachine?.Current ?? CombatStateId.Locomotion;
                float stateScore = state switch
                {
                    CombatStateId.AttackActive => 1f,
                    CombatStateId.AttackStartup => 0.82f,
                    _ => 0.18f
                };
                Vector2 toLeader = leader.CombatCenter - hostile.CombatCenter;
                float facingScore = toLeader.LengthSquared() <= 0.001f
                    ? 1f
                    : Mathf.Max(0f, hostile.FacingDirection.Dot(toLeader.Normalized()));
                float proximity = 1f - Mathf.Clamp(
                    Mathf.Sqrt(hostile.CombatCenter.DistanceSquaredTo(leader.CombatCenter)) / _leaderDangerRadius,
                    0f,
                    1f);
                float score = 0.50f * stateScore + 0.30f * facingScore + 0.20f * proximity;
                if (score > bestScore)
                {
                    best = hostile;
                    bestScore = score;
                }
            }

            return bestScore >= 0.34f ? best : null;
        }

        private CombatCharacter FindRememberedTarget(CombatCharacter actor, CombatBlackboard blackboard)
        {
            if (_tree == null || blackboard == null || !blackboard.CurrentTargetId.HasValue)
            {
                return null;
            }

            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is CombatCharacter candidate
                    && candidate.GetInstanceId() == blackboard.CurrentTargetId.Value
                    && IsHostile(actor, candidate)
                    && actor.CombatCenter.DistanceSquaredTo(candidate.CombatCenter) <= _searchRadius * _searchRadius * 1.5f)
                {
                    return candidate;
                }
            }

            return null;
        }

        private CombatCharacter FindNearestHostile(CombatCharacter actor)
        {
            if (_tree == null)
            {
                return null;
            }

            CombatCharacter nearest = null;
            float bestDistanceSquared = _searchRadius * _searchRadius;
            foreach (Node node in _tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate || !IsHostile(actor, candidate))
                {
                    continue;
                }

                float distanceSquared = actor.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
                if (distanceSquared < bestDistanceSquared)
                {
                    nearest = candidate;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private static bool IsHostile(CombatCharacter actor, CombatCharacter candidate)
        {
            return IsUsable(candidate)
                && candidate != actor
                && candidate.IsAlive
                && FactionRules.CanDamage(actor.Faction, candidate.Faction);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
