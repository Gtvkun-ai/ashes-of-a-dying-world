using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Snapshot môi trường đã được tính toán cho frame hiện tại.
    /// Đây là derived state: consumer chỉ đọc, save system không serialize trực tiếp.
    /// </summary>
    public sealed class EnvironmentState
    {
        public int Day { get; internal set; }
        public float TimeOfDayHours { get; internal set; }
        public float TimeOfDay01 { get; internal set; }

        public float Daylight { get; internal set; }
        public float NightFactor { get; internal set; }
        public Vector2 SunDirection { get; internal set; } = Vector2.Up;
        public Color SunColor { get; internal set; } = Colors.White;
        public Color AmbientColor { get; internal set; } = Colors.White;

        public float WindStrength { get; internal set; }
        public float RainAmount { get; internal set; }
        public float Wetness { get; internal set; }
        public float FogAmount { get; internal set; }
        public float Cloudiness { get; internal set; }

        // Biome/material response values. These are still derived state, not save data.
        public float ShadowStrength { get; internal set; }
        public float WaterShimmerStrength { get; internal set; }
        public float WaterRippleStrength { get; internal set; }

        public float WeatherDarken { get; internal set; }
        public float WeatherDesaturate { get; internal set; }
    }
}
