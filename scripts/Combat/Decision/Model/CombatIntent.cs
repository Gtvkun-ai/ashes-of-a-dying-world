using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Model
{
    /// <summary>
    /// Ngôn ngữ trung gian giữa TacticalEvaluator và các executor sẽ được thêm ở phase sau.
    /// Struct này không được tự gọi mechanics để giữ ranh giới kiến trúc sạch.
    /// </summary>
    public readonly struct CombatIntent
    {
        public CombatIntentType Type { get; }
        public StringName ActionId { get; }
        public ulong? TargetId { get; }
        public Vector2 DesiredAnchor { get; }
        public float DesiredRangeMin { get; }
        public float DesiredRangeMax { get; }
        public float MinCommitmentSeconds { get; }
        public CombatInterruptMask InterruptMask { get; }
        public StringName ReasonKey { get; }

        public bool IsNone => Type == CombatIntentType.None;

        public CombatIntent(
            CombatIntentType type,
            StringName actionId,
            ulong? targetId,
            Vector2 desiredAnchor,
            float desiredRangeMin,
            float desiredRangeMax,
            float minCommitmentSeconds,
            CombatInterruptMask interruptMask,
            StringName reasonKey)
        {
            Type = type;
            ActionId = actionId;
            TargetId = targetId;
            DesiredAnchor = desiredAnchor;
            DesiredRangeMin = Mathf.Max(0f, desiredRangeMin);
            DesiredRangeMax = Mathf.Max(DesiredRangeMin, desiredRangeMax);
            MinCommitmentSeconds = Mathf.Max(0f, minCommitmentSeconds);
            InterruptMask = interruptMask;
            ReasonKey = reasonKey;
        }

        public static CombatIntent None(StringName reasonKey)
        {
            return new CombatIntent(
                CombatIntentType.None,
                new StringName(string.Empty),
                null,
                Vector2.Zero,
                0f,
                0f,
                0f,
                CombatInterruptMask.Dead | CombatInterruptMask.Hitstun,
                reasonKey);
        }

        public override string ToString()
        {
            string action = ActionId.ToString();
            return string.IsNullOrWhiteSpace(action)
                ? Type.ToString()
                : $"{Type}({action})";
        }
    }
}
