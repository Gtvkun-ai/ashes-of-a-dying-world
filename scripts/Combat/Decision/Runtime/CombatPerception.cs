using System.Collections.Generic;
using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Cổng duy nhất đọc thế giới cho Decision Core.
    ///
    /// P3.2 tách ba khái niệm vốn trước đây bị trộn vào nhau:
    /// - Awareness: actor tồn tại trong bán kính query.
    /// - Vision: thật sự nhìn thấy (range/FOV/elevation/world LOS).
    /// - Line of fire: projectile corridor có bắn tới target hay không.
    ///
    /// Nhờ vậy enemy ở sau cliff/tường không còn được acquire chỉ vì nằm trong EnemySearchRadius.
    /// </summary>
    public sealed class CombatPerception : ICombatPerception
    {
        private readonly struct ResolvedTarget
        {
            public CombatCharacter Actor { get; }
            public bool Visible { get; }
            public bool FromMemory { get; }
            public bool ThreatOverride { get; }

            public ResolvedTarget(
                CombatCharacter actor,
                bool visible,
                bool fromMemory,
                bool threatOverride)
            {
                Actor = actor;
                Visible = visible;
                FromMemory = fromMemory;
                ThreatOverride = threatOverride;
            }

            public static ResolvedTarget None => new(null, false, false, false);
        }

        private readonly SceneTree _tree;
        private readonly RayCast2D _lineOfSightRay;
        private readonly CombatLineOfFireSensor _lineOfFireSensor;
        private readonly ProjectileSpecData _primaryProjectileSpec;
        private readonly IThreatPredictor _threatPredictor;
        private readonly CombatEngagementPolicy _engagementPolicy;
        private readonly CombatVisionSensor _visionSensor;
        private readonly float _targetMemorySeconds;
        private readonly float _enemySearchRadius;
        private readonly float _leaderDangerRadius;
        private readonly List<CombatCharacter> _spatialQueryBuffer = new(24);

        public CombatPerception(
            SceneTree tree,
            RayCast2D lineOfSightRay,
            CombatLineOfFireSensor lineOfFireSensor,
            ProjectileSpecData primaryProjectileSpec,
            IThreatPredictor threatPredictor,
            CombatEngagementPolicy engagementPolicy,
            float leaderDangerRadius,
            CombatVisionSensor visionSensor = null,
            float targetMemorySeconds = 1.25f)
        {
            _tree = tree;
            _lineOfSightRay = lineOfSightRay;
            _lineOfFireSensor = lineOfFireSensor;
            _primaryProjectileSpec = primaryProjectileSpec;
            _threatPredictor = threatPredictor;
            _engagementPolicy = engagementPolicy ?? new CombatEngagementPolicy(240f, false, 240f, 300f, 240f, 300f, 99999f);
            _visionSensor = visionSensor ?? new CombatVisionSensor(
                lineOfSightRay,
                _engagementPolicy.SensorRadius,
                360f,
                true,
                8,
                false);
            _targetMemorySeconds = Mathf.Max(0f, targetMemorySeconds);
            _enemySearchRadius = Mathf.Max(1f, _engagementPolicy.SensorRadius);
            _leaderDangerRadius = Mathf.Max(1f, leaderDangerRadius);
        }

        public CombatSnapshot BuildSnapshot(
            CombatCharacter self,
            CombatCharacter leader,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard,
            float timeSeconds)
        {
            blackboard?.PruneVisualMemory(timeSeconds, _targetMemorySeconds);
            ResolvedTarget resolved = ResolveTarget(self, leader, assignment, blackboard, timeSeconds);
            CombatCharacter target = resolved.Actor;
            bool hasTarget = IsUsable(target) && target.IsAlive;

            // Chỉ visual contact mới được cập nhật LastKnownTargetPosition.
            // Khi target khuất tường, dùng đúng vị trí nhìn thấy cuối cùng thay vì đọc CombatCenter hiện tại.
            if (hasTarget && resolved.Visible)
            {
                blackboard?.RecordVisualContact(target, timeSeconds);
            }

            Vector2 targetPosition = Vector2.Zero;
            if (hasTarget)
            {
                targetPosition = resolved.Visible || resolved.ThreatOverride
                    ? target.CombatCenter
                    : (blackboard?.LastKnownTargetPosition ?? target.CombatCenter);
            }
            else if (blackboard != null)
            {
                targetPosition = blackboard.LastKnownTargetPosition;
            }

            Vector2 toTarget = hasTarget ? targetPosition - self.CombatCenter : Vector2.Zero;
            float targetDistance = toTarget.Length();
            Vector2 directionToTarget = targetDistance > 0.001f
                ? toTarget / targetDistance
                : Vector2.Zero;

            // Snapshot LOS vẫn mang nghĩa "đường hành động/bắn sạch" để evaluator hiện tại không gãy.
            // Vision được expose riêng bằng TargetVisible/TargetFromMemory.
            bool hasLineOfSight = hasTarget
                && resolved.Visible
                && HasLineOfSight(self, target);

            // Không đọc state/facing thật của actor đang khuất: đó cũng là một dạng wall-hack.
            bool hasLiveTargetKnowledge = hasTarget && (resolved.Visible || resolved.ThreatOverride);
            bool targetFacingSelf = hasLiveTargetKnowledge
                && directionToTarget != Vector2.Zero
                && target.FacingDirection.Dot(-directionToTarget) >= 0.35f;
            CombatStateId? targetState = hasLiveTargetKnowledge && target.StateMachine != null
                ? target.StateMachine.Current
                : null;
            bool targetInRecovery = targetState == CombatStateId.AttackRecovery;
            bool targetIsCasting = hasLiveTargetKnowledge
                && target.Actions?.CurrentAction?.DeliveryMode == CombatDeliveryMode.Projectile;

            ThreatAssessment threat = hasLiveTargetKnowledge
                ? _threatPredictor.EvaluateThreats(self, target, targetDistance)
                : ThreatAssessment.None;

            if (blackboard != null)
            {
                blackboard.CurrentTargetId = hasTarget ? target.GetInstanceId() : null;
            }

            bool hasLeader = IsUsable(leader) && leader.IsAlive;
            Vector2 leaderPosition = hasLeader ? leader.CombatCenter : Vector2.Zero;
            float distanceToLeader = hasLeader
                ? self.CombatCenter.DistanceTo(leaderPosition)
                : 0f;
            bool leaderThreatened = hasLeader && IsActorThreatened(leader, _leaderDangerRadius);

            // Với bộ kỹ năng hiện có, chạy nhanh chính là dodge. Perception chỉ cung cấp
            // hướng thoát an toàn tương đối; movement solver vẫn chịu trách nhiệm tránh vật cản.
            Vector2 safeRetreatVector = Vector2.Zero;
            bool hasSafeRetreatVector = false;
            if (hasTarget && directionToTarget.LengthSquared() > 0.001f)
            {
                Vector2 tangent = new Vector2(-directionToTarget.Y, directionToTarget.X)
                    * ((blackboard?.OrbitSide ?? 1) < 0 ? -1f : 1f);
                safeRetreatVector = (-directionToTarget * 0.86f + tangent * 0.52f).Normalized();
                hasSafeRetreatVector = safeRetreatVector.LengthSquared() > 0.001f;
            }

            var health = new CombatResourceSnapshot(self.Stats?.CurrentHP ?? 0f, self.Stats?.MaxHP ?? 0f);
            var mana = new CombatResourceSnapshot(self.Stats?.CurrentMP ?? 0f, self.Stats?.MaxMP ?? 0f);
            var stamina = new CombatResourceSnapshot(self.Stats?.CurrentStamina ?? 0f, self.Stats?.MaxStamina ?? 0f);
            var guard = new CombatResourceSnapshot(self.Stats?.CurrentGuard ?? 0f, self.Stats?.MaxGuard ?? 0f);
            var poise = new CombatResourceSnapshot(self.Stats?.CurrentPoise ?? 0f, self.Stats?.MaxPoise ?? 0f);

            CombatStateMachine stateMachine = self.StateMachine;
            bool canMove = stateMachine?.CanMove ?? false;
            bool canBlock = (stateMachine?.CanStartBlock ?? false)
                && guard.HasPool
                && guard.Current > 0.001f;
            bool canStartAction = stateMachine?.CanStartAttack ?? false;

            return new CombatSnapshot(
                self.GetInstanceId(),
                self.CombatCenter,
                stateMachine?.Current ?? CombatStateId.Locomotion,
                health,
                mana,
                stamina,
                guard,
                poise,
                canMove,
                canBlock,
                canStartAction,
                hasTarget ? target.GetInstanceId() : null,
                targetPosition,
                targetDistance,
                directionToTarget,
                hasLineOfSight,
                resolved.Visible,
                resolved.FromMemory,
                targetFacingSelf,
                targetInRecovery,
                targetIsCasting,
                targetState,
                threat.EtaSeconds,
                threat.Severity,
                threat.Blockable,
                threat.Dodgeable,
                hasLeader ? leader.GetInstanceId() : null,
                leaderPosition,
                distanceToLeader,
                leaderThreatened,
                hasSafeRetreatVector,
                safeRetreatVector,
                false,
                false,
                timeSeconds);
        }

        private ResolvedTarget ResolveTarget(
            CombatCharacter self,
            CombatCharacter leader,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard,
            float timeSeconds)
        {
            // Director chỉ được vượt Vision khi enemy đang thật sự startup/active vào leader.
            // Đây là "shared threat" của party, không phải radar xuyên tường cho mọi hostile gần leader.
            if (assignment.HasValue)
            {
                CombatCharacter assigned = assignment.Value.PriorityTarget;
                bool immediateLeaderThreat = IsSpecificActorThreatening(
                    leader,
                    assigned,
                    _leaderDangerRadius);
                if (IsValidHostile(self, assigned, _engagementPolicy.GetRetentionRadius(leader))
                    && _engagementPolicy.AllowsRetain(self, leader, assigned, immediateLeaderThreat))
                {
                    CombatVisionResult vision = _visionSensor.Evaluate(self, assigned);
                    if (vision.Visible)
                    {
                        return new ResolvedTarget(assigned, true, false, false);
                    }

                    if (immediateLeaderThreat)
                    {
                        return new ResolvedTarget(assigned, false, false, true);
                    }

                    if (blackboard?.HasFreshVisualMemory(assigned, timeSeconds, _targetMemorySeconds) == true)
                    {
                        return new ResolvedTarget(assigned, false, true, false);
                    }
                }
            }

            // Hysteresis target cũ: còn nhìn thấy thì refresh memory; khuất thì chỉ giữ đúng MemorySeconds.
            if (blackboard != null && blackboard.CurrentTargetId.HasValue)
            {
                CombatCharacter remembered = FindCombatantById(blackboard.CurrentTargetId.Value);
                bool immediateLeaderThreat = IsSpecificActorThreatening(
                    leader,
                    remembered,
                    _leaderDangerRadius);
                if (IsValidHostile(self, remembered, _engagementPolicy.GetRetentionRadius(leader))
                    && _engagementPolicy.AllowsRetain(self, leader, remembered, immediateLeaderThreat))
                {
                    CombatVisionResult vision = _visionSensor.Evaluate(self, remembered);
                    if (vision.Visible)
                    {
                        return new ResolvedTarget(remembered, true, false, false);
                    }

                    if (immediateLeaderThreat)
                    {
                        return new ResolvedTarget(remembered, false, false, true);
                    }

                    if (blackboard.HasFreshVisualMemory(remembered, timeSeconds, _targetMemorySeconds))
                    {
                        return new ResolvedTarget(remembered, false, true, false);
                    }
                }
            }

            CombatCharacter nearest = null;
            float queryRadius = _engagementPolicy.GetPassiveQueryRadius(leader);
            float bestDistanceSquared = queryRadius * queryRadius;
            if (_tree == null)
            {
                return ResolvedTarget.None;
            }

            // Awareness/spatial hash tìm candidate rẻ. Vision mới là gate acquire thật sự.
            CombatSpatialIndex.QueryRadius(_tree, self.CombatCenter, queryRadius, _spatialQueryBuffer);
            for (int i = 0; i < _spatialQueryBuffer.Count; i++)
            {
                CombatCharacter candidate = _spatialQueryBuffer[i];
                if (!IsValidHostile(self, candidate, queryRadius)
                    || !_engagementPolicy.AllowsPassiveAcquire(self, leader, candidate))
                {
                    continue;
                }

                CombatVisionResult vision = _visionSensor.Evaluate(self, candidate);
                if (!vision.Visible)
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

            return nearest != null
                ? new ResolvedTarget(nearest, true, false, false)
                : ResolvedTarget.None;
        }

        private CombatCharacter FindCombatantById(ulong instanceId)
        {
            return CombatSpatialIndex.FindById(_tree, instanceId);
        }

        private bool IsActorThreatened(CombatCharacter actor, float radius)
        {
            if (_tree == null || !IsUsable(actor))
            {
                return false;
            }

            float radiusSquared = radius * radius;
            CombatSpatialIndex.QueryRadius(_tree, actor.CombatCenter, radius, _spatialQueryBuffer);
            for (int i = 0; i < _spatialQueryBuffer.Count; i++)
            {
                CombatCharacter hostile = _spatialQueryBuffer[i];
                if (hostile == actor
                    || !hostile.IsAlive
                    || !FactionRules.IsHostile(hostile.Faction, actor.Faction))
                {
                    continue;
                }

                if (hostile.CombatCenter.DistanceSquaredTo(actor.CombatCenter) > radiusSquared)
                {
                    continue;
                }

                if (IsSpecificActorThreatening(actor, hostile, radius))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSpecificActorThreatening(
            CombatCharacter protectedActor,
            CombatCharacter hostile,
            float radius)
        {
            if (!IsUsable(protectedActor)
                || !IsUsable(hostile)
                || !hostile.IsAlive
                || hostile.StateMachine == null
                || hostile.CombatCenter.DistanceSquaredTo(protectedActor.CombatCenter) > radius * radius)
            {
                return false;
            }

            CombatStateId state = hostile.StateMachine.Current;
            if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
            {
                return false;
            }

            Vector2 toProtected = protectedActor.CombatCenter - hostile.CombatCenter;
            return toProtected.LengthSquared() <= 0.001f
                || hostile.FacingDirection.Dot(toProtected.Normalized()) >= 0.25f;
        }

        /// <summary>
        /// Line-of-fire tactical query. Tên interface giữ lại để không phá API cũ;
        /// đây KHÔNG còn là gate acquire target của P3.2.
        /// </summary>
        public bool HasLineOfSight(CombatCharacter self, CombatCharacter target)
        {
            if (!IsUsable(self) || !IsUsable(target))
            {
                return false;
            }

            if (_lineOfFireSensor != null && GodotObject.IsInstanceValid(_lineOfFireSensor))
            {
                LineOfFireResult line = _lineOfFireSensor.Query(self, target, _primaryProjectileSpec);
                if (line.IsValid)
                {
                    return line.ReachesTarget;
                }
            }

            // Fallback dùng world LOS ray. Với Hyou bình thường ShapeCast ở trên sẽ là đường chính.
            if (_lineOfSightRay == null || !GodotObject.IsInstanceValid(_lineOfSightRay))
            {
                return false;
            }

            _lineOfSightRay.GlobalPosition = self.CombatCenter;
            _lineOfSightRay.ClearExceptions();
            _lineOfSightRay.AddException(self);
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

        public bool CanSeeTarget(CombatCharacter self, CombatCharacter target)
        {
            return _visionSensor?.CanSee(self, target) == true;
        }

        public string GetVisionDiagnosticsSummary()
        {
            return _visionSensor?.ToCompactString() ?? "vision=unavailable";
        }

        private static bool IsValidHostile(CombatCharacter self, CombatCharacter candidate, float radius)
        {
            return IsUsable(candidate)
                && candidate != self
                && candidate.IsAlive
                && FactionRules.IsHostile(self.Faction, candidate.Faction)
                && self.CombatCenter.DistanceSquaredTo(candidate.CombatCenter) <= radius * radius;
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
