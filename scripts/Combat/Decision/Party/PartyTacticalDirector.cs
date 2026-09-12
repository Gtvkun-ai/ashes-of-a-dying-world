using System.Collections.Generic;
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
        private readonly CombatEngagementPolicy _engagementPolicy;
        private readonly CombatVisionSensor _visionSensor;
        private readonly float _targetMemorySeconds;
        private readonly List<CombatCharacter> _spatialQueryBuffer = new(24);

        public PartyTacticalDirector(
            SceneTree tree,
            float searchRadius,
            float leaderDangerRadius,
            CombatEngagementPolicy engagementPolicy = null,
            CombatVisionSensor visionSensor = null,
            float targetMemorySeconds = 1.25f,
            float backlineOffset = 48f)
        {
            _tree = tree;
            _searchRadius = Mathf.Max(1f, searchRadius);
            _leaderDangerRadius = Mathf.Max(1f, leaderDangerRadius);
            _engagementPolicy = engagementPolicy
                ?? new CombatEngagementPolicy(_searchRadius, false, _searchRadius, _searchRadius * 1.25f, _searchRadius, _searchRadius * 1.25f, 99999f);
            _visionSensor = visionSensor;
            _targetMemorySeconds = Mathf.Max(0f, targetMemorySeconds);
            _backlineOffset = Mathf.Max(8f, backlineOffset);
        }

        public CombatRoleAssignment? GetAssignment(
            CombatCharacter actor,
            CombatCharacter leader,
            CombatBlackboard blackboard,
            float timeSeconds = 0f)
        {
            if (!IsUsable(actor))
            {
                return null;
            }

            CombatCharacter priorityTarget = FindLeaderThreat(actor, leader)
                ?? FindRememberedTarget(actor, leader, blackboard, timeSeconds)
                ?? FindNearestHostile(actor, leader);

            Vector2 anchor = actor.CombatCenter;
            if (IsUsable(leader))
            {
                if (IsUsable(priorityTarget))
                {
                    bool sharedThreat = IsImmediateLeaderThreat(leader, priorityTarget);
                    bool visibleNow = _visionSensor == null || _visionSensor.CanSee(actor, priorityTarget);
                    Vector2 knownTargetPosition = visibleNow || sharedThreat
                        ? priorityTarget.CombatCenter
                        : (blackboard?.LastKnownTargetPosition ?? priorityTarget.CombatCenter);
                    Vector2 leaderToTarget = knownTargetPosition - leader.CombatCenter;
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
            CombatSpatialIndex.QueryRadius(_tree, leader.CombatCenter, _leaderDangerRadius, _spatialQueryBuffer);
            for (int i = 0; i < _spatialQueryBuffer.Count; i++)
            {
                CombatCharacter hostile = _spatialQueryBuffer[i];
                if (!IsHostile(actor, hostile)
                    || hostile.CombatCenter.DistanceSquaredTo(leader.CombatCenter) > dangerRadiusSquared)
                {
                    continue;
                }

                CombatStateId state = hostile.StateMachine?.Current ?? CombatStateId.Locomotion;
                // P3.2: "leader threat" là threat thật, không phải enemy chỉ đứng gần leader.
                // Nếu không gate state ở đây, proximity+facing có thể biến một slime idle thành wall-hack override.
                if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
                {
                    continue;
                }

                float stateScore = state == CombatStateId.AttackActive ? 1f : 0.82f;
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

        private CombatCharacter FindRememberedTarget(
            CombatCharacter actor,
            CombatCharacter leader,
            CombatBlackboard blackboard,
            float timeSeconds)
        {
            if (_tree == null || blackboard == null || !blackboard.CurrentTargetId.HasValue)
            {
                return null;
            }

            CombatCharacter candidate = CombatSpatialIndex.FindById(_tree, blackboard.CurrentTargetId.Value);
            bool leaderEmergency = IsImmediateLeaderThreat(leader, candidate);
            bool visibleNow = candidate != null
                && (_visionSensor == null || _visionSensor.CanSee(actor, candidate));
            bool freshMemory = candidate != null
                && blackboard.HasFreshVisualMemory(candidate, timeSeconds, _targetMemorySeconds);

            return candidate != null
                && IsHostile(actor, candidate)
                && _engagementPolicy.AllowsRetain(actor, leader, candidate, leaderEmergency)
                && (leaderEmergency || visibleNow || freshMemory)
                    ? candidate
                    : null;
        }

        private CombatCharacter FindNearestHostile(CombatCharacter actor, CombatCharacter leader)
        {
            if (_tree == null)
            {
                return null;
            }

            float queryRadius = _engagementPolicy.GetPassiveQueryRadius(leader);
            CombatCharacter nearest = null;
            float bestDistanceSquared = queryRadius * queryRadius;
            CombatSpatialIndex.QueryRadius(_tree, actor.CombatCenter, queryRadius, _spatialQueryBuffer);
            for (int i = 0; i < _spatialQueryBuffer.Count; i++)
            {
                CombatCharacter candidate = _spatialQueryBuffer[i];
                if (!IsHostile(actor, candidate)
                    || !_engagementPolicy.AllowsPassiveAcquire(actor, leader, candidate)
                    || (_visionSensor != null && !_visionSensor.CanSee(actor, candidate)))
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

        private bool IsImmediateLeaderThreat(CombatCharacter leader, CombatCharacter hostile)
        {
            if (!IsUsable(leader)
                || !IsUsable(hostile)
                || hostile.StateMachine == null
                || hostile.CombatCenter.DistanceSquaredTo(leader.CombatCenter)
                    > _leaderDangerRadius * _leaderDangerRadius)
            {
                return false;
            }

            CombatStateId state = hostile.StateMachine.Current;
            if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
            {
                return false;
            }

            Vector2 toLeader = leader.CombatCenter - hostile.CombatCenter;
            return toLeader.LengthSquared() <= 0.001f
                || hostile.FacingDirection.Dot(toLeader.Normalized()) >= 0.25f;
        }

        private static bool IsHostile(CombatCharacter actor, CombatCharacter candidate)
        {
            return IsUsable(candidate)
                && candidate != actor
                && candidate.IsAlive
                && FactionRules.IsHostile(actor.Faction, candidate.Faction);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
