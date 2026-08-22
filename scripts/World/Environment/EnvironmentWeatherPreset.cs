using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Weather là một trục độc lập với time-of-day.
    /// Có thể ghép noon + storm hoặc night + clear mà không cần profile mới.
    /// </summary>
    [GlobalClass]
    public partial class EnvironmentWeatherPreset : Resource
    {
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Rain { get; set; }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Wetness { get; set; }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Fog { get; set; }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Cloudiness { get; set; } = 0.1f;

        /// <summary>1 = gió đúng profile; 0 = lặng; >1 = bão.</summary>
        [Export(PropertyHint.Range, "0,3,0.01")]
        public float WindMultiplier { get; set; } = 1f;

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Darken { get; set; }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Desaturate { get; set; }
    }

    internal struct EnvironmentWeatherSample
    {
        public float Rain;
        public float Wetness;
        public float Fog;
        public float Cloudiness;
        public float WindMultiplier;
        public float Darken;
        public float Desaturate;

        public static EnvironmentWeatherSample FromPreset(EnvironmentWeatherPreset preset)
        {
            if (preset == null)
            {
                return new EnvironmentWeatherSample { WindMultiplier = 1f };
            }

            return new EnvironmentWeatherSample
            {
                Rain = preset.Rain,
                Wetness = preset.Wetness,
                Fog = preset.Fog,
                Cloudiness = preset.Cloudiness,
                WindMultiplier = preset.WindMultiplier,
                Darken = preset.Darken,
                Desaturate = preset.Desaturate
            };
        }

        public static EnvironmentWeatherSample Lerp(
            EnvironmentWeatherSample from,
            EnvironmentWeatherSample to,
            float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return new EnvironmentWeatherSample
            {
                Rain = Mathf.Lerp(from.Rain, to.Rain, t),
                Wetness = Mathf.Lerp(from.Wetness, to.Wetness, t),
                Fog = Mathf.Lerp(from.Fog, to.Fog, t),
                Cloudiness = Mathf.Lerp(from.Cloudiness, to.Cloudiness, t),
                WindMultiplier = Mathf.Lerp(from.WindMultiplier, to.WindMultiplier, t),
                Darken = Mathf.Lerp(from.Darken, to.Darken, t),
                Desaturate = Mathf.Lerp(from.Desaturate, to.Desaturate, t)
            };
        }
    }
}
