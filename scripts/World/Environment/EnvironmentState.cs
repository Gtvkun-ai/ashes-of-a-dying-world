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

        // Celestial lighting. Direction là hướng tia sáng chiếu trên mặt phẳng 2D (+Y = xuống màn hình).
        public Vector2 SunDirection { get; internal set; } = Vector2.Down;
        public float SunElevation { get; internal set; } = 1f;
        public float SunEnergy { get; internal set; }
        public Color SunColor { get; internal set; } = Colors.White;

        public Vector2 MoonDirection { get; internal set; } = Vector2.Down;
        public float MoonElevation { get; internal set; }
        public float MoonEnergy { get; internal set; }
        public Color MoonColor { get; internal set; } = new Color(0.70f, 0.79f, 1f, 1f);

        // Key light là thiên thể mạnh hơn tại frame này, dùng chung cho projected shadow.
        public Vector2 KeyLightDirection { get; internal set; } = Vector2.Down;
        public float KeyLightElevation { get; internal set; } = 1f;
        public float KeyLightStrength01 { get; internal set; } = 1f;
        public Color KeyLightColor { get; internal set; } = Colors.White;

        public Color AmbientColor { get; internal set; } = Colors.White;

        public float WindStrength { get; internal set; }
        public float RainAmount { get; internal set; }
        public float Wetness { get; internal set; }
        public float FogAmount { get; internal set; }
        public float Cloudiness { get; internal set; }

        // Biome/material response values. Đây vẫn là derived state, không phải save data.
        public float ShadowStrength { get; internal set; }
        public float WaterShimmerStrength { get; internal set; }
        public float WaterRippleStrength { get; internal set; }

        public float WeatherDarken { get; internal set; }
        public float WeatherDesaturate { get; internal set; }
    }
}
