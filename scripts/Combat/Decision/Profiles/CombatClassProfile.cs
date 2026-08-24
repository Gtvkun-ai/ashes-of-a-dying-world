using Godot;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Decision.Profiles
{
    /// <summary>
    /// Khả năng, range band và semantics tài nguyên của class.
    /// Mọi khoảng cách/threshold nằm trong Resource để class mới không cần sinh thêm một FooMageAI.cs.
    /// </summary>
    [GlobalClass]
    public partial class CombatClassProfile : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ClassId { get; set; } = "unassigned";

        [ExportGroup("Granted Skills")]
        [Export] public Godot.Collections.Array<SkillData> GrantedSkills { get; set; } = new();

        [ExportGroup("Range Doctrine")]
        [Export] public float PanicRange { get; set; } = 28f;
        [Export] public float UnsafeRange { get; set; } = 34f;
        [Export] public float PreferredMinRange { get; set; } = 36f;
        [Export] public float PreferredMaxRange { get; set; } = 48f;
        [Export] public float ReacquireRange { get; set; } = 72f;
        [Export] public float RangeSoftEdge { get; set; } = 10f;

        [ExportGroup("Existing-kit Combat Rhythm")]
        [Export] public bool AllowsMeleeFallback { get; set; } = false;
        [Export] public float MeleeRange { get; set; } = 42f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MeleeStaminaReserveRatio { get; set; } = 0.28f;
        [Export] public float PanicEvadeMinStamina { get; set; } = 18f;
        [Export] public float PanicEvadeCooldownSeconds { get; set; } = 1.25f;
        [Export] public float RepositionAfterActionSeconds { get; set; } = 3.8f;

        [ExportGroup("Resources")]
        [Export] public bool UsesMana { get; set; } = false;
        [Export] public bool UsesStamina { get; set; } = true;
        [Export] public bool CanRecoverManaPassively { get; set; } = false;
        [Export] public bool CanRecoverStaminaPassively { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float LowManaRatio { get; set; } = 0.22f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float CriticalManaRatio { get; set; } = 0.10f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float LowStaminaRatio { get; set; } = 0.20f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float CriticalStaminaRatio { get; set; } = 0.08f;

        public bool CanUseMeleeFallback(float targetDistance, float staminaRatio)
        {
            return AllowsMeleeFallback
                && targetDistance <= Mathf.Max(1f, MeleeRange)
                && staminaRatio >= Mathf.Clamp(MeleeStaminaReserveRatio, 0f, 1f);
        }

        public SkillData GetPrimarySkill()
        {
            return GrantedSkills != null && GrantedSkills.Count > 0
                ? GrantedSkills[0]
                : null;
        }

        public SkillData GetSecondarySkill()
        {
            return GrantedSkills != null && GrantedSkills.Count > 1
                ? GrantedSkills[1]
                : null;
        }

        public void GetValidatedRanges(
            out float panic,
            out float unsafeRange,
            out float preferredMin,
            out float preferredMax,
            out float reacquire,
            out float softEdge)
        {
            panic = Mathf.Max(1f, PanicRange);
            unsafeRange = Mathf.Max(panic + 1f, UnsafeRange);
            preferredMin = Mathf.Max(unsafeRange + 1f, PreferredMinRange);
            preferredMax = Mathf.Max(preferredMin + 1f, PreferredMaxRange);
            reacquire = Mathf.Max(preferredMax + 1f, ReacquireRange);
            softEdge = Mathf.Max(1f, RangeSoftEdge);
        }
    }
}
