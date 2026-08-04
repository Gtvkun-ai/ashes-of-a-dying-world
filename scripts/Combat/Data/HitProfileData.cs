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

        [ExportGroup("Hitbox")]
        [Export] public Vector2 HitboxSize { get; set; } = new Vector2(18f, 28f);
        [Export] public float Reach { get; set; } = 18f;
        [Export] public Vector2 LocalOffset { get; set; } = Vector2.Zero;
    }
}
