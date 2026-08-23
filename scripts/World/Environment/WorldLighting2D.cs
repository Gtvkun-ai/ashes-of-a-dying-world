using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Direct-light layer của Environment Core.
    ///
    /// CanvasModulate chịu trách nhiệm ambient; hai DirectionalLight2D này bù direct light của
    /// mặt trời / mặt trăng. Tách ambient và direct light là phần giúp 12h vẫn có chiều sâu thay
    /// vì toàn map bị phủ một màu trắng phẳng.
    /// </summary>
    public partial class WorldLighting2D : Node2D
    {
        // V4: direct light is deliberately restrained. Material shaders create directional form;
        // DirectionalLight2D only provides a coherent fill so baked AI assets do not blow out neon-green.
        private const float SunDirectScale = 0.24f;
        private const float MoonDirectScale = 0.55f;
        private const float MaxSunFillEnergy = 0.14f;
        private const float MaxMoonFillEnergy = 0.05f;

        private DirectionalLight2D _sun;
        private DirectionalLight2D _moon;
        private bool _reportedReady;

        public override void _Ready()
        {
            EnsureLights();
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null)
            {
                return;
            }

            EnsureLights();

            ApplyDirectionalLight(
                _sun,
                state.SunDirection,
                state.SunElevation,
                state.SunColor,
                state.SunEnergy,
                SunDirectScale,
                MaxSunFillEnergy);

            ApplyDirectionalLight(
                _moon,
                state.MoonDirection,
                state.MoonElevation,
                state.MoonColor,
                state.MoonEnergy,
                MoonDirectScale,
                MaxMoonFillEnergy);

            if (!_reportedReady)
            {
                _reportedReady = true;
                GD.Print("[WorldLighting2D] READY V4 restrained sun fill + readable moon fill");
            }
        }

        private void EnsureLights()
        {
            if (_sun == null || !GodotObject.IsInstanceValid(_sun))
            {
                _sun = new DirectionalLight2D
                {
                    Name = "SunLight2D",
                    Enabled = true,
                    Energy = 0f,
                    Color = Colors.White,
                    Height = 0.85f,
                    MaxDistance = 4096f,
                    // Shadow hình học của Godot sẽ dành cho cliff/building ở pass sau.
                    // Cây/đá dùng Shadow Core V2 asset-projection để giữ silhouette pixel-art dễ kiểm soát.
                    ShadowEnabled = false
                };
                AddChild(_sun);
            }

            if (_moon == null || !GodotObject.IsInstanceValid(_moon))
            {
                _moon = new DirectionalLight2D
                {
                    Name = "MoonLight2D",
                    Enabled = true,
                    Energy = 0f,
                    Color = new Color(0.70f, 0.79f, 1f, 1f),
                    Height = 0.72f,
                    MaxDistance = 4096f,
                    ShadowEnabled = false
                };
                AddChild(_moon);
            }
        }

        private static void ApplyDirectionalLight(
            DirectionalLight2D light,
            Vector2 rayDirection,
            float elevation,
            Color color,
            float energy,
            float directScale,
            float maxEnergy)
        {
            if (light == null)
            {
                return;
            }

            // V3 vẫn tôn trọng baked pixel-art, nhưng direct light phải đủ mạnh để người chơi
            // thực sự đọc được hướng mặt trời. Giá trị cũ ~0.01 gần như vô hình.
            float safeEnergy = Mathf.Min(Mathf.Max(energy, 0f) * directScale, maxEnergy);
            light.Enabled = safeEnergy > 0.001f;
            light.Energy = safeEnergy;
            light.Color = color;
            light.Height = Mathf.Lerp(0.22f, 0.94f, Mathf.Clamp(elevation, 0f, 1f));

            // DirectionalLight2D phát sáng theo +Y của local basis.
            // Vector2.Down có góc PI/2 nên trừ PI/2 để +Y quay đúng theo rayDirection.
            Vector2 direction = rayDirection.LengthSquared() > 0.0001f
                ? rayDirection.Normalized()
                : Vector2.Down;
            light.Rotation = direction.Angle() - Mathf.Pi * 0.5f;
        }
    }
}
