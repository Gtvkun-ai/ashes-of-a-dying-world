using Godot;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// One-shot pixel VFX player dùng cho combat asset dạng horizontal sheet hoặc SpriteFrames.
    /// Presentation-only: không chứa damage/status logic.
    /// </summary>
    public partial class CombatSpriteVfx2D : Node2D
    {
        private AnimatedSprite2D _sprite;

        public bool InitializeFromHorizontalSheet(
            string texturePath,
            int frameWidth,
            int frameHeight,
            int frameCount,
            float fps,
            float visualScale = 1f,
            float rotation = 0f,
            int zIndex = 250)
        {
            Texture2D texture = string.IsNullOrWhiteSpace(texturePath)
                ? null
                : GD.Load<Texture2D>(texturePath);
            if (texture == null || frameWidth <= 0 || frameHeight <= 0 || frameCount <= 0)
            {
                return false;
            }

            int safeCount = Mathf.Min(frameCount, texture.GetWidth() / frameWidth);
            if (safeCount <= 0 || texture.GetHeight() < frameHeight)
            {
                return false;
            }

            var frames = new SpriteFrames();
            frames.AddAnimation("fx");
            frames.SetAnimationLoop("fx", false);
            frames.SetAnimationSpeed("fx", Mathf.Max(1f, fps));

            for (int index = 0; index < safeCount; index++)
            {
                frames.AddFrame("fx", new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(index * frameWidth, 0, frameWidth, frameHeight)
                });
            }

            return BuildSprite(frames, "fx", visualScale, rotation, zIndex);
        }

        public bool InitializeFromSpriteFrames(
            string framesPath,
            StringName animation,
            float visualScale = 1f,
            float rotation = 0f,
            int zIndex = 250)
        {
            SpriteFrames frames = string.IsNullOrWhiteSpace(framesPath)
                ? null
                : GD.Load<SpriteFrames>(framesPath);
            if (frames == null || !frames.HasAnimation(animation))
            {
                return false;
            }

            return BuildSprite(frames, animation, visualScale, rotation, zIndex);
        }

        private bool BuildSprite(
            SpriteFrames frames,
            StringName animation,
            float visualScale,
            float rotation,
            int zIndex)
        {
            ZIndex = zIndex;
            Rotation = rotation;
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

            _sprite = new AnimatedSprite2D
            {
                Name = "Sprite",
                SpriteFrames = frames,
                Animation = animation,
                Centered = true,
                Scale = Vector2.One * Mathf.Max(0.05f, visualScale),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            AddChild(_sprite);
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play();
            return true;
        }

        private void OnAnimationFinished()
        {
            QueueFree();
        }
    }
}
