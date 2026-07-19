using Godot;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Core.Data
{
    public enum SkillExecutionType
    {
        TimedBuff = 0,
        CombatAction = 1,
        Heal = 2,
        RestoreResources = 3
    }

    /// <summary>
    /// Dữ liệu ability chung. Skill không còn mặc định bị hiểu là "một buff có timer".
    /// Executor quyết định cách chạy dựa trên ExecutionType và vẫn giữ các field cũ để save tương thích.
    /// </summary>
    [GlobalClass]
    public partial class SkillData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string SkillId { get; set; } = "";
        [Export] public string SkillName { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

        [ExportGroup("Execution")]
        [Export] public SkillExecutionType ExecutionType { get; set; } = SkillExecutionType.TimedBuff;
        [Export] public CombatActionData CombatAction { get; set; }
        [Export] public bool CanUseWhileBlocking { get; set; } = false;

        [ExportGroup("Timed Effect")]
        [Export] public float Duration { get; set; } = 0f;
        [Export] public float MoveSpeedBonusPercent { get; set; } = 0f;
        [Export] public float DexterityBonusPercent { get; set; } = 0f;

        [ExportGroup("Instant Effect")]
        [Export] public float HealAmount { get; set; } = 0f;
        [Export] public float RestoreStaminaAmount { get; set; } = 0f;
        [Export] public float RestoreGuardAmount { get; set; } = 0f;

        [ExportGroup("Combat Cost")]
        [Export] public float Cooldown { get; set; } = 5f;
        [Export] public float DamageMultiplier { get; set; } = 1f;
        [Export] public int ManaCost { get; set; } = 10;
        [Export] public int StaminaCost { get; set; } = 20;
        [Export] public string AnimationName { get; set; } = "";
    }
}
