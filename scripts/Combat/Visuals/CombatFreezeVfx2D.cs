using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Freeze overlay modular, không phụ thuộc kích thước enemy cố định.
    /// Crystal/sparkle/frost được scale theo visual bounds của BodySprite.
    /// </summary>
    public partial class CombatFreezeVfx2D : Node2D
    {
        private static readonly string[] CrystalSinglePaths =
        {
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_01.png",
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_02.png",
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_03.png"
        };

        private static readonly string[] CrystalClusterPaths =
        {
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_04.png",
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_05.png",
            "res://assets/graphics/vfx/combat/ice/crystals/freeze_crystal_06.png"
        };

        private static readonly string[] SparklePaths =
        {
            "res://assets/graphics/vfx/combat/ice/sparkles/freeze_sparkle_01.png",
            "res://assets/graphics/vfx/combat/ice/sparkles/freeze_sparkle_02.png",
            "res://assets/graphics/vfx/combat/ice/sparkles/freeze_sparkle_03.png",
            "res://assets/graphics/vfx/combat/ice/sparkles/freeze_sparkle_04.png"
        };

        private static readonly string[] FrostPaths =
        {
            "res://assets/graphics/vfx/combat/ice/frost/frost_particle_01.png",
            "res://assets/graphics/vfx/combat/ice/frost/frost_particle_02.png",
            "res://assets/graphics/vfx/combat/ice/frost/frost_particle_03.png",
            "res://assets/graphics/vfx/combat/ice/frost/frost_particle_04.png"
        };

        private CombatCharacter _owner;
        private Sprite2D _sparkle;
        private float _age;
        private Vector2 _sparkleBaseScale = Vector2.One;

        public void Initialize(CombatCharacter owner)
        {
            _owner = owner;
        }

        public override void _Ready()
        {
            if (_owner == null || !GodotObject.IsInstanceValid(_owner))
            {
                QueueFree();
                return;
            }

            Name = "FreezeVfx";
            ZIndex = 180;
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            BuildModularOverlay();
        }

        public override void _Process(double delta)
        {
            if (_owner == null
                || !GodotObject.IsInstanceValid(_owner)
                || !_owner.IsAlive
                || _owner.Statuses?.IsFrozen != true)
            {
                QueueFree();
                return;
            }

            _age += Mathf.Max(0f, (float)delta);
            AnimatedSprite2D body = _owner.BodySprite;
            Position = body?.Position ?? Vector2.Zero;

            if (_sparkle != null)
            {
                float pulse = 0.86f + (Mathf.Sin(_age * 11f) + 1f) * 0.07f;
                _sparkle.Scale = _sparkleBaseScale * pulse;
                _sparkle.Modulate = new Color(1f, 1f, 1f, 0.58f + (Mathf.Sin(_age * 8f) + 1f) * 0.12f);
            }
        }

        private void BuildModularOverlay()
        {
            Vector2 size = EstimateVisualSize();
            float width = Mathf.Clamp(size.X, 24f, 160f);
            float height = Mathf.Clamp(size.Y, 24f, 160f);
            float minDim = Mathf.Min(width, height);

            int variant = (int)(_owner.GetInstanceId() % 12UL);
            string frostPath = FrostPaths[variant % FrostPaths.Length];
            string crystalA = CrystalSinglePaths[variant % CrystalSinglePaths.Length];
            string crystalB = CrystalSinglePaths[(variant + 1) % CrystalSinglePaths.Length];
            string crystalCluster = CrystalClusterPaths[variant % CrystalClusterPaths.Length];
            string sparklePath = SparklePaths[variant % SparklePaths.Length];

            // Frost nằm sát chân, rất nhẹ để không biến enemy thành một đám mây xanh.
            AddSizedSprite(
                frostPath,
                new Vector2(0f, height * 0.34f),
                desiredWidth: Mathf.Clamp(width * 0.48f, 16f, 46f),
                desiredHeight: 0f,
                zIndex: -1,
                alpha: 0.46f);

            float crystalHeight = Mathf.Clamp(minDim * 0.20f, 8f, 16f);
            AddSizedSprite(
                crystalA,
                new Vector2(-width * 0.27f, height * 0.27f),
                0f,
                crystalHeight * 0.82f,
                1,
                0.88f);
            AddSizedSprite(
                crystalB,
                new Vector2(width * 0.27f, height * 0.25f),
                0f,
                crystalHeight,
                1,
                0.90f);

            // Chỉ quái vừa/lớn mới thêm cluster thứ ba. Slime nhỏ vẫn giữ silhouette sạch.
            if (width >= 70f || height >= 78f)
            {
                AddSizedSprite(
                    crystalCluster,
                    new Vector2(0f, height * 0.34f),
                    0f,
                    Mathf.Clamp(crystalHeight * 1.15f, 10f, 20f),
                    2,
                    0.86f);
            }

            _sparkle = AddSizedSprite(
                sparklePath,
                new Vector2(width * 0.22f, -height * 0.22f),
                0f,
                Mathf.Clamp(minDim * 0.10f, 5f, 9f),
                3,
                0.72f);
            if (_sparkle != null)
            {
                _sparkleBaseScale = _sparkle.Scale;
            }
        }

        private Vector2 EstimateVisualSize()
        {
            AnimatedSprite2D body = _owner?.BodySprite;
            if (body?.SpriteFrames == null)
            {
                return new Vector2(48f, 56f);
            }

            Texture2D texture = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame);
            if (texture == null)
            {
                return new Vector2(48f, 56f);
            }

            Vector2 raw = texture.GetSize();
            Vector2 scale = new Vector2(Mathf.Abs(body.Scale.X), Mathf.Abs(body.Scale.Y));
            Vector2 visual = new Vector2(raw.X * scale.X, raw.Y * scale.Y);
            if (visual.X <= 1f || visual.Y <= 1f)
            {
                return new Vector2(48f, 56f);
            }

            return visual;
        }

        private Sprite2D AddSizedSprite(
            string texturePath,
            Vector2 position,
            float desiredWidth,
            float desiredHeight,
            int zIndex,
            float alpha)
        {
            Texture2D texture = GD.Load<Texture2D>(texturePath);
            if (texture == null)
            {
                return null;
            }

            Vector2 source = texture.GetSize();
            float scale = desiredHeight > 0f
                ? desiredHeight / Mathf.Max(1f, source.Y)
                : desiredWidth / Mathf.Max(1f, source.X);
            scale = Mathf.Max(0.01f, scale);

            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                Position = position,
                Scale = Vector2.One * scale,
                ZIndex = zIndex,
                Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f)),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            AddChild(sprite);
            return sprite;
        }
    }
}
