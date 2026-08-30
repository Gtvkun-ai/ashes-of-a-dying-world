using Godot;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Cách hệ thống runtime thực thi kỹ năng.
    /// Đây là hành vi của kỹ năng, không phải trạng thái riêng của người chơi.
    /// </summary>
    public enum SkillExecutionType
    {
        TimedBuff = 0,
        CombatAction = 1,
        Heal = 2,
        RestoreResources = 3
    }

    /// <summary>
    /// Nhóm kỹ năng dùng cho lọc UI và luật trang bị.
    /// Ghi rõ trong resource để UI không phải đoán dựa trên mana, cooldown hay tên kỹ năng.
    /// </summary>
    public enum SkillCategory
    {
        Active = 0,
        Passive = 1,
        Innate = 2
    }

    /// <summary>
    /// Thuộc tính nguyên tố/phong cách của kỹ năng.
    /// Chỉ chứa dữ liệu mô tả ổn định; cấp hiện tại của người chơi không nằm ở đây.
    /// </summary>
    public enum SkillElement
    {
        None = 0,
        Physical = 1,
        Ice = 2,
        Fire = 3,
        Lightning = 4,
        Wind = 5,
        Earth = 6,
        Light = 7,
        Dark = 8,
        Arcane = 9
    }

    /// <summary>
    /// Resource định nghĩa một kỹ năng dùng chung.
    ///
    /// Quy tắc quan trọng:
    /// - Tên, icon, category, max level và thông số combat nằm trong SkillData.
    /// - Level hiện tại, trạng thái mở khóa và slot đang trang bị nằm trong PlayerSkillState.
    ///
    /// Tách như vậy để hai nhân vật cùng dùng một resource nhưng vẫn có tiến trình riêng.
    /// </summary>
    [GlobalClass]
    public partial class SkillData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string SkillId { get; set; } = "";
        [Export] public string SkillName { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

        [ExportGroup("Classification")]
        [Export] public SkillCategory Category { get; set; } = SkillCategory.Active;
        [Export] public SkillElement Element { get; set; } = SkillElement.None;
        [Export(PropertyHint.Range, "1,99,1")]
        public int MaxLevel { get; set; } = 1;
        [Export] public bool DefaultUnlocked { get; set; } = true;

        [ExportGroup("Execution")]
        [Export] public SkillExecutionType ExecutionType { get; set; } = SkillExecutionType.TimedBuff;
        [Export] public CombatActionData CombatAction { get; set; }
        [Export] public bool CanUseWhileBlocking { get; set; } = false;

        [ExportGroup("Timed Effect")]
        [Export] public float Duration { get; set; } = 0f;
        [Export] public float MoveSpeedBonusPercent { get; set; } = 0f;
        [Export] public float DexterityBonusPercent { get; set; } = 0f;

        [ExportGroup("Reactive Evasion")]
        /// <summary>
        /// Baseline ở kèo ngang trình. Runtime có thể scale theo level/DEX/INT/mobility
        /// và độ khó của attack; đây không còn là xác suất cố định.
        /// </summary>
        [Export(PropertyHint.Range, "0,100,0.5")]
        public float AutoEvadeChancePercent { get; set; } = 0f;
        [Export] public bool AutoEvadeUseRelativeMastery { get; set; } = false;
        [Export(PropertyHint.Range, "0,100,0.5")]
        public float AutoEvadeMinChancePercent { get; set; } = 2f;
        [Export(PropertyHint.Range, "0,100,0.5")]
        public float AutoEvadeMaxChancePercent { get; set; } = 95f;

        [ExportGroup("Reactive Evasion / Mastery Scaling")]
        [Export(PropertyHint.Range, "0,3,0.05")]
        public float AutoEvadeLevelDeltaWeight { get; set; } = 0.85f;
        [Export(PropertyHint.Range, "0,2,0.05")]
        public float AutoEvadeDexterityDeltaWeight { get; set; } = 0.35f;
        [Export(PropertyHint.Range, "0,2,0.05")]
        public float AutoEvadeIntelligenceDeltaWeight { get; set; } = 0.30f;
        [Export(PropertyHint.Range, "0,0.25,0.005")]
        public float AutoEvadeMoveSpeedDeltaWeight { get; set; } = 0.04f;
        [Export(PropertyHint.Range, "0,20,0.5")]
        public float AutoEvadeAttackSpeedPressureWeight { get; set; } = 8f;
        [Export(PropertyHint.Range, "0,40,0.5")]
        public float AutoEvadeStartupReadWeight { get; set; } = 18f;
        [Export(PropertyHint.Range, "0,0.25,0.005")]
        public float AutoEvadeTravelSpeedPressureWeight { get; set; } = 0.03f;

        [ExportGroup("Reactive Evasion / Overmatch")]
        [Export(PropertyHint.Range, "1,98,1")]
        public int AutoEvadeOvermatchLevelGap { get; set; } = 25;
        [Export(PropertyHint.Range, "1,5,0.05")]
        public float AutoEvadeOvermatchMasteryRatio { get; set; } = 1.65f;

        [ExportGroup("Reactive Evasion / Motion")]
        [Export(PropertyHint.Range, "0,500,1")]
        public float AutoEvadeImpulse { get; set; } = 230f;
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float AutoEvadeInvulnerabilitySeconds { get; set; } = 0.18f;
        [Export(PropertyHint.Range, "0,2,0.01")]
        public float AutoEvadeInternalCooldownSeconds { get; set; } = 0.60f;
        [Export(PropertyHint.Range, "0,2,0.01")]
        public float AutoReengageWindowSeconds { get; set; } = 0.80f;
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float AutoReengageDelaySeconds { get; set; } = 0.06f;
        [Export(PropertyHint.Range, "0,500,1")]
        public float AutoReengageImpulse { get; set; } = 165f;

        [ExportGroup("Instant Effect")]
        [Export] public float HealAmount { get; set; } = 0f;
        [Export] public float RestoreStaminaAmount { get; set; } = 0f;
        [Export] public float RestoreGuardAmount { get; set; } = 0f;

        [ExportGroup("Combat Cost")]
        [Export] public float Cooldown { get; set; } = 5f;
        [Export] public float DamageMultiplier { get; set; } = 1f;
        [Export] public int ManaCost { get; set; } = 0;
        [Export] public int StaminaCost { get; set; } = 0;
        [Export] public string AnimationName { get; set; } = "";
    }
}
