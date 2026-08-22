using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Profile hình ảnh của một biome/map.
    /// Clock là global, còn profile có thể đổi theo map mà không làm mất thời gian thế giới.
    /// </summary>
    [GlobalClass]
    public partial class EnvironmentProfile : Resource
    {
        [ExportGroup("Day / Night")]
        [Export(PropertyHint.Range, "0,24,0.05")]
        public float SunriseHour { get; set; } = 5.5f;

        [Export(PropertyHint.Range, "0,24,0.05")]
        public float SunsetHour { get; set; } = 19f;

        /// <summary>
        /// Màu tint toàn scene theo thời gian 0..1. Điểm 0 và 1 nên cùng màu đêm.
        /// </summary>
        [Export]
        public Gradient AmbientTint { get; set; }

        /// <summary>Màu ánh sáng mặt trời / mặt trăng theo thời gian 0..1.</summary>
        [Export]
        public Gradient SunTint { get; set; }

        [ExportGroup("Atmosphere")]
        [Export(PropertyHint.Range, "0,2,0.01")]
        public float BaseWindStrength { get; set; } = 0.2f;

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float BaseWetness { get; set; } = 0f;

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float ShadowStrength { get; set; } = 0.55f;

        [Export(PropertyHint.Range, "0,2,0.01")]
        public float WaterShimmerStrength { get; set; } = 0.12f;

        [Export(PropertyHint.Range, "0,2,0.01")]
        public float WaterRippleStrength { get; set; } = 0.08f;

        [ExportGroup("Weather")]
        [Export]
        public EnvironmentWeatherPreset DefaultWeather { get; set; }

        public Color SampleAmbient(float time01)
        {
            return AmbientTint?.Sample(PositiveMod(time01, 1f)) ?? Colors.White;
        }

        public Color SampleSunColor(float time01)
        {
            return SunTint?.Sample(PositiveMod(time01, 1f)) ?? Colors.White;
        }

        private static float PositiveMod(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }
    }
}
