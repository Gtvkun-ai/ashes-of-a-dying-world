using Godot;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Decision.Model
{
    /// <summary>
    /// Ảnh chụp một tài nguyên tại đúng tick ra quyết định.
    /// HasPool tách rõ "không có tài nguyên" khỏi "có nhưng đã cạn" để evaluator
    /// không còn hiểu MaxMP = 0 là mana bằng 0% rồi đòi hồi mãi mãi.
    /// </summary>
    public readonly struct CombatResourceSnapshot
    {
        public float Current { get; }
        public float Maximum { get; }
        public float Ratio { get; }
        public bool HasPool { get; }

        public CombatResourceSnapshot(float current, float maximum)
        {
            Maximum = Mathf.Max(0f, maximum);
            HasPool = Maximum > 0.001f;
            Current = HasPool
                ? Mathf.Clamp(current, 0f, Maximum)
                : 0f;
            Ratio = HasPool
                ? Mathf.Clamp(Current / Maximum, 0f, 1f)
                : 1f;
        }

        public bool CanAfford(float amount)
        {
            float safeAmount = Mathf.Max(0f, amount);
            return safeAmount <= 0f || (HasPool && Current + 0.001f >= safeAmount);
        }

        public override string ToString()
        {
            return HasPool
                ? $"{Current:0.0}/{Maximum:0.0}({Ratio:0.00})"
                : "none";
        }
    }

    /// <summary>
    /// Ảnh chụp bất biến của một tick quyết định.
    /// Evaluator chỉ đọc snapshot này, không tự lục scene tree rải rác.
    /// </summary>
    public readonly struct CombatSnapshot
    {
        public ulong SelfId { get; }
        public CombatStateId SelfState { get; }
        public CombatResourceSnapshot Health { get; }
        public CombatResourceSnapshot Mana { get; }
        public CombatResourceSnapshot Stamina { get; }
        public CombatResourceSnapshot Guard { get; }
        public CombatResourceSnapshot Poise { get; }

        // Giữ API ratio cũ để code gọi hiện tại không phải đổi hàng loạt.
        public float HealthRatio => Health.Ratio;
        public float ManaRatio => Mana.Ratio;
        public float StaminaRatio => Stamina.Ratio;
        public float GuardRatio => Guard.Ratio;
        public float PoiseRatio => Poise.Ratio;

        public bool CanMove { get; }
        public bool CanBlock { get; }
        public bool CanStartAction { get; }

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
        public bool ThreatBlockable { get; }
        public bool ThreatDodgeable { get; }

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
            CombatResourceSnapshot health,
            CombatResourceSnapshot mana,
            CombatResourceSnapshot stamina,
            CombatResourceSnapshot guard,
            CombatResourceSnapshot poise,
            bool canMove,
            bool canBlock,
            bool canStartAction,
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
            bool threatBlockable,
            bool threatDodgeable,
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
            Health = health;
            Mana = mana;
            Stamina = stamina;
            Guard = guard;
            Poise = poise;
            CanMove = canMove;
            CanBlock = canBlock;
            CanStartAction = canStartAction;
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
            ThreatBlockable = threatBlockable;
            ThreatDodgeable = threatDodgeable;
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
