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
    /// Dữ liệu projectile thuần Resource. Runtime chỉ đọc presentation từ data.
    /// Core là hình viên đạn đang bay; launch sheet là lớp animation nhả đạn ngắn
    /// ở thời điểm projectile vừa rời người cast.
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

        [ExportGroup("Soft Homing")]
        [Export] public bool HomingEnabled { get; set; } = false;
        [Export(PropertyHint.Range, "0,1,0.05")] public float HomingStrength { get; set; } = 0f;
        [Export(PropertyHint.Range, "30,720,5")] public float HomingMaxTurnDegreesPerSecond { get; set; } = 260f;
        [Export(PropertyHint.Range, "0,128,1")] public float HomingStopDistance { get; set; } = 10f;

        [ExportGroup("Collision")]
        [Export(PropertyHint.Layers2DPhysics)] public uint HurtboxCollisionMask { get; set; } = 16;
        [Export(PropertyHint.Layers2DPhysics)] public uint WorldCollisionMask { get; set; } = 8;
        [Export] public bool StopOnWorldCollision { get; set; } = true;

        [ExportGroup("Damage")]
        [Export] public HitProfileData HitProfileOverride { get; set; }

        [ExportGroup("Presentation")]
        [Export] public ProjectileVisualProfileData VisualProfile { get; set; }
    }
}
