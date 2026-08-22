using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Chỉ lệch phase lúc spawn cho AnimatedSprite2D môi trường.
    /// Không _Process, không đọc weather mỗi frame: animation frame đảm nhiệm chuyển động pixel-art,
    /// còn WorldEnvironment vẫn quản lý state toàn cục cho shader/lighting.
    /// </summary>
    public partial class FloraIdleAnimator : AnimatedSprite2D
    {
        [Export(PropertyHint.Range, "0,0.3,0.01")]
        public float SpeedJitter { get; set; } = 0.12f;

        public override void _Ready()
        {
            if (SpriteFrames == null)
            {
                return;
            }

            int frameCount = SpriteFrames.GetFrameCount(Animation);
            if (frameCount <= 0)
            {
                return;
            }

            // Deterministic theo vị trí, nên reload scene không làm cây cỏ đổi phase lung tung.
            int px = Mathf.RoundToInt(GlobalPosition.X);
            int py = Mathf.RoundToInt(GlobalPosition.Y);
            uint seed = unchecked((uint)(px * 73856093) ^ (uint)(py * 19349663) ^ 0x9E3779B9u);

            Play(Animation);
            Frame = (int)(seed % (uint)frameCount);

            float unit = ((seed >> 8) & 1023u) / 1023.0f;
            SpeedScale *= Mathf.Lerp(1.0f - SpeedJitter, 1.0f + SpeedJitter, unit);
        }
    }
}
