using Godot;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Decision.Model
{
    /// <summary>
    /// Ảnh chụp bất biến của một tick quyết định.
    /// Evaluator chỉ đọc snapshot này, không tự lục scene tree rải rác.
    /// </summary>
    public readonly struct CombatSnapshot
    {
        public ulong SelfId { get; }
        public CombatStateId SelfState { get; }
        public float HealthRatio { get; }
        public float ManaRatio { get; }
        public float StaminaRatio { get; }
        public float GuardRatio { get; }
        public float PoiseRatio { get; }

        public ulong? TargetId { get; }
        public Vector2 TargetPosition { get; }
        public float TargetDistance { get; }
        public Vector2 DirectionToTarget { get; }
        public bool HasLineOfSight { get; }
        public bool TargetFacingSelf { get; }
        public bool TargetInRecovery { get; }
        public bool TargetIsCasting { get; }
        public CombatStateId? TargetState { get; }

        public float ThreatEtaSeconds { get; }
        public float ThreatSeverity { get; }

        public ulong? LeaderId { get; }
        public Vector2 LeaderPosition { get; }
        public float DistanceToLeader { get; }
        public bool LeaderThreatened { get; }

        public bool HasSafeRetreatVector { get; }
        public Vector2 SafeRetreatVector { get; }
        public bool NearObstacle { get; }
        public bool IsCornered { get; }
        public float TimeSeconds { get; }

        public bool HasTarget => TargetId.HasValue;
        public bool HasLeader => LeaderId.HasValue;

        public CombatSnapshot(
            ulong selfId,
            CombatStateId selfState,
            float healthRatio,
            float manaRatio,
            float staminaRatio,
            float guardRatio,
            float poiseRatio,
            ulong? targetId,
            Vector2 targetPosition,
            float targetDistance,
            Vector2 directionToTarget,
            bool hasLineOfSight,
            bool targetFacingSelf,
            bool targetInRecovery,
            bool targetIsCasting,
            CombatStateId? targetState,
            float threatEtaSeconds,
            float threatSeverity,
            ulong? leaderId,
            Vector2 leaderPosition,
            float distanceToLeader,
            bool leaderThreatened,
            bool hasSafeRetreatVector,
            Vector2 safeRetreatVector,
            bool nearObstacle,
            bool isCornered,
            float timeSeconds)
        {
            SelfId = selfId;
            SelfState = selfState;
            HealthRatio = Mathf.Clamp(healthRatio, 0f, 1f);
            ManaRatio = Mathf.Clamp(manaRatio, 0f, 1f);
            StaminaRatio = Mathf.Clamp(staminaRatio, 0f, 1f);
            GuardRatio = Mathf.Clamp(guardRatio, 0f, 1f);
            PoiseRatio = Mathf.Clamp(poiseRatio, 0f, 1f);
            TargetId = targetId;
            TargetPosition = targetPosition;
            TargetDistance = Mathf.Max(0f, targetDistance);
            DirectionToTarget = directionToTarget;
            HasLineOfSight = hasLineOfSight;
            TargetFacingSelf = targetFacingSelf;
            TargetInRecovery = targetInRecovery;
            TargetIsCasting = targetIsCasting;
            TargetState = targetState;
            ThreatEtaSeconds = Mathf.Max(0f, threatEtaSeconds);
            ThreatSeverity = Mathf.Clamp(threatSeverity, 0f, 1f);
            LeaderId = leaderId;
            LeaderPosition = leaderPosition;
            DistanceToLeader = Mathf.Max(0f, distanceToLeader);
            LeaderThreatened = leaderThreatened;
            HasSafeRetreatVector = hasSafeRetreatVector;
            SafeRetreatVector = safeRetreatVector;
            NearObstacle = nearObstacle;
            IsCornered = isCornered;
            TimeSeconds = Mathf.Max(0f, timeSeconds);
        }
    }
}
