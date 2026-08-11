using Godot;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Data
{
    [GlobalClass]
    public partial class HitProfileData : Resource
    {
        [ExportGroup("Damage")]
        [Export] public DamageType DamageType { get; set; } = DamageType.Physical;
        [Export] public float BaseDamage { get; set; } = 0f;
        [Export] public float AttackPowerScale { get; set; } = 1f;
        [Export] public float ArmorPenetration { get; set; } = 0f;

        [ExportGroup("Control")]
        [Export] public float GuardDamage { get; set; } = 12f;
        [Export] public float PoiseDamage { get; set; } = 10f;
        [Export] public float HitstunSeconds { get; set; } = 0.12f;
        [Export] public float KnockbackForce { get; set; } = 80f;
        [Export] public bool ForceStagger { get; set; } = false;
        [Export] public float ForcedStaggerSeconds { get; set; } = 0.28f;
        [Export] public float LaunchHeight { get; set; } = 0f;
        [Export] public float LaunchDuration { get; set; } = 0.32f;

        [ExportGroup("Elemental Status")]
        [Export(PropertyHint.Range, "0,90,1")] public float SlowPercent { get; set; } = 0f;
        [Export] public float SlowSeconds { get; set; } = 0f;
        [Export(PropertyHint.Range, "0,10,1")] public int ChillStacks { get; set; } = 0;
        [Export] public float ChillSeconds { get; set; } = 3.5f;
        [Export(PropertyHint.Range, "1,10,1")] public int FreezeAtChillStacks { get; set; } = 3;
        [Export] public bool FreezeOnHit { get; set; } = false;
        [Export] public float FreezeSeconds { get; set; } = 0f;
        [Export] public bool ShatterFrozen { get; set; } = false;
        [Export] public float ShatterBonusDamage { get; set; } = 0f;
        [Export] public float ShatterKnockbackMultiplier { get; set; } = 1.5f;

        [ExportGroup("Impact Feedback")]
        [Export] public float HitStopSeconds { get; set; } = 0.045f;
        [Export] public float HitFlashSeconds { get; set; } = 0.08f;
        [Export] public float CameraShakeStrength { get; set; } = 0.75f;
        [Export] public float ImpactVfxScale { get; set; } = 1f;

        [ExportGroup("Hitbox")]
        [Export] public Vector2 HitboxSize { get; set; } = new Vector2(18f, 28f);
        [Export] public float Reach { get; set; } = 18f;
        [Export] public Vector2 LocalOffset { get; set; } = Vector2.Zero;
    }
}
