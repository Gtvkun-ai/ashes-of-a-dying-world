using Godot;

namespace AshesofaDyingWorld.Combat.Data
{
    /// <summary>
    /// Cách một combat action đưa hit ra thế giới.
    /// Melee dùng CombatHitbox hiện có; Projectile tạo một thực thể bay riêng.
    /// </summary>
    public enum CombatDeliveryMode
    {
        MeleeHitbox = 0,
        Projectile = 1,
        SelfEffect = 2
    }

    /// <summary>
    /// Dữ liệu projectile thuần Resource. Không nhét tên riêng IceBolt vào runtime,
    /// để spell khác chỉ cần thêm .tres thay vì sinh thêm một lớp C# mới.
    /// </summary>
    [GlobalClass]
    public partial class ProjectileSpecData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ProjectileId { get; set; } = "projectile";

        [ExportGroup("Motion")]
        [Export] public float Speed { get; set; } = 240f;
        [Export] public float Lifetime { get; set; } = 1.5f;
        [Export] public float Radius { get; set; } = 6f;
        [Export] public float SpawnOffset { get; set; } = 14f;
        [Export] public bool PierceTargets { get; set; } = false;
        [Export] public int MaxTargetHits { get; set; } = 1;

        [ExportGroup("Collision")]
        [Export(PropertyHint.Layers2DPhysics)] public uint HurtboxCollisionMask { get; set; } = 16;
        [Export(PropertyHint.Layers2DPhysics)] public uint WorldCollisionMask { get; set; } = 1;
        [Export] public bool StopOnWorldCollision { get; set; } = true;

        [ExportGroup("Damage")]
        [Export] public HitProfileData HitProfileOverride { get; set; }

        [ExportGroup("Presentation")]
        [Export] public Color CoreColor { get; set; } = new Color(0.72f, 0.96f, 1f, 1f);
        [Export] public Color GlowColor { get; set; } = new Color(0.18f, 0.72f, 1f, 0.65f);
        [Export] public float VisualLength { get; set; } = 18f;
        [Export] public float VisualWidth { get; set; } = 4f;
    }
}
