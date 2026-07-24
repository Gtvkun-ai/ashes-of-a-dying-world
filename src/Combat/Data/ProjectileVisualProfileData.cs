using Godot;

namespace AshesofaDyingWorld.Combat.Data
{
    /// <summary>
    /// Presentation thuần của projectile. Gameplay spec không còn phải biết sprite sheet
    /// có bao nhiêu cột, row hướng lên nằm đâu hay fallback vẽ hình tròn màu gì.
    /// </summary>
    [GlobalClass]
    public partial class ProjectileVisualProfileData : Resource
    {
        [ExportGroup("Core Sprite")]
        [Export(PropertyHint.File, "*.png")]
        public string SpriteSheetPath { get; set; } = "";

        [Export(PropertyHint.File, "*.png")]
        public string UpSpriteSheetOverridePath { get; set; } = "";

        [Export] public int SpriteColumns { get; set; } = 1;
        [Export] public int SpriteRows { get; set; } = 1;
        [Export] public int SpriteFrameWidth { get; set; } = 0;
        [Export] public int SpriteFrameHeight { get; set; } = 0;
        [Export] public int SpriteColumn { get; set; } = 0;
        [Export] public int DownRow { get; set; } = 0;
        [Export] public int RightRow { get; set; } = 0;
        [Export] public int LeftRow { get; set; } = 0;
        [Export] public int UpRow { get; set; } = 0;
        [Export] public float SpriteScale { get; set; } = 1f;

        [ExportGroup("Launch Animation")]
        [Export(PropertyHint.File, "*.png")]
        public string LaunchSpriteSheetPath { get; set; } = "";

        [Export(PropertyHint.File, "*.png")]
        public string UpLaunchSpriteSheetOverridePath { get; set; } = "";

        [Export] public int LaunchStartColumn { get; set; } = 0;
        [Export] public int LaunchFrameCount { get; set; } = 0;
        [Export] public float LaunchAnimationFps { get; set; } = 12f;
        [Export] public float LaunchSpriteScale { get; set; } = 1f;

        [ExportGroup("Alignment")]
        [Export] public float SpriteEmbeddedForwardOffset { get; set; } = 0f;
        [Export] public bool RotateSpriteTowardExactAim { get; set; } = true;
        [Export] public bool UseProceduralFallback { get; set; } = true;
        [Export] public bool DebugVisualLogging { get; set; } = false;

        [ExportGroup("Procedural Fallback")]
        [Export] public Color CoreColor { get; set; } = new Color(0.72f, 0.96f, 1f, 1f);
        [Export] public Color GlowColor { get; set; } = new Color(0.18f, 0.72f, 1f, 0.65f);
        [Export] public float VisualLength { get; set; } = 18f;
        [Export] public float VisualWidth { get; set; } = 4f;
    }
}
