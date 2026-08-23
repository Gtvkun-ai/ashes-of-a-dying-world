using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Ambient life rất nhẹ cho chiều tối/ban đêm.
    /// Dùng firefly.png từ GODOT-VFX-LIBRARY (MIT) đã có sẵn trong third_party_refs của project.
    /// Chỉ tạo vài Sprite2D quanh camera, không GPU particle dependency và không ảnh hưởng ban ngày.
    /// </summary>
    public partial class AmbientFireflies2D : Node2D
    {
        private const string FireflyTexturePath = "res://assets/graphics/vfx/environment/firefly.png";
        private const int FireflyCount = 12;

        private sealed class Firefly
        {
            public Sprite2D Sprite;
            public Vector2 Anchor01;
            public float Phase;
            public float DriftRadius;
            public float DriftSpeed;
            public float PulseSpeed;
        }

        private readonly List<Firefly> _fireflies = new();
        private float _nightVisibility;
        private double _elapsed;

        public override void _Ready()
        {
            Texture2D texture = ResourceLoader.Exists(FireflyTexturePath)
                ? GD.Load<Texture2D>(FireflyTexturePath)
                : null;

            if (texture == null)
            {
                GD.PushWarning($"[AmbientFireflies2D] Missing texture: {FireflyTexturePath}");
                SetProcess(false);
                return;
            }

            var rng = new RandomNumberGenerator { Seed = 0xA51E5u };
            for (int i = 0; i < FireflyCount; i++)
            {
                var sprite = new Sprite2D
                {
                    Name = $"Firefly{i + 1:00}",
                    Texture = texture,
                    Scale = Vector2.One * rng.RandfRange(0.12f, 0.20f),
                    ZIndex = 1,
                    Visible = false
                };
                AddChild(sprite);

                _fireflies.Add(new Firefly
                {
                    Sprite = sprite,
                    Anchor01 = new Vector2(rng.RandfRange(0.08f, 0.92f), rng.RandfRange(0.10f, 0.90f)),
                    Phase = rng.RandfRange(0f, Mathf.Tau),
                    DriftRadius = rng.RandfRange(5f, 16f),
                    DriftSpeed = rng.RandfRange(0.25f, 0.58f),
                    PulseSpeed = rng.RandfRange(1.3f, 2.4f)
                });
            }
        }

        public override void _Process(double delta)
        {
            if (_fireflies.Count == 0)
            {
                return;
            }

            _elapsed += delta;
            bool visible = _nightVisibility > 0.01f;
            Camera2D camera = GetViewport()?.GetCamera2D();
            Vector2 center = camera?.GlobalPosition ?? Vector2.Zero;
            Vector2 screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1600, 900);

            if (camera != null)
            {
                Vector2 zoom = camera.Zoom;
                screenSize = new Vector2(
                    screenSize.X / Mathf.Max(Mathf.Abs(zoom.X), 0.001f),
                    screenSize.Y / Mathf.Max(Mathf.Abs(zoom.Y), 0.001f));
            }

            Vector2 topLeft = center - screenSize * 0.5f;
            float time = (float)_elapsed;

            foreach (Firefly firefly in _fireflies)
            {
                Sprite2D sprite = firefly.Sprite;
                if (sprite == null || !GodotObject.IsInstanceValid(sprite))
                {
                    continue;
                }

                sprite.Visible = visible;
                if (!visible)
                {
                    continue;
                }

                float a = time * firefly.DriftSpeed + firefly.Phase;
                Vector2 drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a * 0.73f)) * firefly.DriftRadius;
                sprite.GlobalPosition = topLeft + firefly.Anchor01 * screenSize + drift;

                float pulse = 0.55f + 0.45f * Mathf.Sin(time * firefly.PulseSpeed + firefly.Phase);
                float alpha = _nightVisibility * Mathf.Lerp(0.35f, 0.90f, pulse);
                sprite.Modulate = new Color(1.0f, 0.94f, 0.46f, alpha);
            }
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null)
            {
                _nightVisibility = 0f;
                return;
            }

            // Bắt đầu xuất hiện từ hoàng hôn, bị mưa lớn che bớt.
            float night = Smooth01(Mathf.InverseLerp(0.25f, 0.82f, state.NightFactor));
            float weatherVisibility = 1f - state.RainAmount * 0.75f;
            _nightVisibility = Mathf.Clamp(night * weatherVisibility, 0f, 1f);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
