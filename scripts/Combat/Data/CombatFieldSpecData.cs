using Godot;

namespace AshesofaDyingWorld.Combat.Data
{
    /// <summary>
    /// Data thuần cho một field/hazard tồn tại trong world sau khi action hoàn tất.
    /// Frost Ward là consumer đầu tiên; các field khác có thể tái dùng cùng runtime.
    /// </summary>
    [GlobalClass]
    public partial class CombatFieldSpecData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string FieldId { get; set; } = "combat_field";

        [ExportGroup("Lifetime and Trigger")]
        [Export] public float Radius { get; set; } = 56f;
        [Export] public float DurationSeconds { get; set; } = 7f;
        [Export] public bool RequireExitBeforeRetrigger { get; set; } = true;
        [Export] public float PerTargetCooldownSeconds { get; set; } = 0.25f;
        [Export] public Vector2 GroundOffset { get; set; } = new Vector2(0f, 28f);

        [ExportGroup("Combat")]
        [Export] public HitProfileData HitProfile { get; set; }
        [Export] public float DamageMultiplier { get; set; } = 1f;

        [ExportGroup("Presentation")]
        [Export] public Texture2D CircleTexture { get; set; }
        [Export] public Texture2D ArmedTexture { get; set; }
        [Export] public Texture2D CrystalTexture { get; set; }
        [Export] public float VisualScale { get; set; } = 2f;
        [Export] public float CrystalVisualScale { get; set; } = 2f;
        [Export] public float TriggerPulseSeconds { get; set; } = 0.22f;
        [Export] public Color IdleModulate { get; set; } = Colors.White;
        [Export] public Color TriggerModulate { get; set; } = new Color(1.18f, 1.22f, 1.28f, 1f);
    }
}
