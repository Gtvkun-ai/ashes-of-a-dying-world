using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Bộ giải quỹ đạo ánh sáng 2D cho mặt trời / mặt trăng.
    ///
    /// Ý tưởng kiến trúc tham khảo WeatherSystem2D/TimeOfDay (MIT): một nguồn sáng thiên thể thống nhất
    /// phải cùng lúc quyết định hướng sáng, độ cao, cường độ và màu. Ashes chỉ mượn pattern đó rồi
    /// dùng công thức riêng phù hợp top-down pixel art, không kéo nguyên addon vào runtime.
    /// </summary>
    public static class CelestialSolver
    {
        public sealed class Sample
        {
            public Vector2 SunDirection { get; init; } = Vector2.Down;
            public float SunElevation { get; init; }
            public float SunEnergy { get; init; }
            public Color SunColor { get; init; } = Colors.White;

            public Vector2 MoonDirection { get; init; } = Vector2.Down;
            public float MoonElevation { get; init; }
            public float MoonEnergy { get; init; }
            public Color MoonColor { get; init; } = new Color(0.70f, 0.79f, 1.0f, 1f);

            /// <summary>Ánh sáng đang chi phối bóng đổ tại thời điểm hiện tại.</summary>
            public Vector2 KeyDirection { get; init; } = Vector2.Down;
            public float KeyElevation { get; init; } = 1f;
            public float KeyStrength01 { get; init; } = 1f;
            public Color KeyColor { get; init; } = Colors.White;
        }

        public static Sample Evaluate(
            float hour,
            EnvironmentProfile profile,
            float daylight,
            float nightFactor,
            float cloudiness)
        {
            if (profile == null)
            {
                return new Sample();
            }

            float sunrise = Mathf.Clamp(profile.SunriseHour, 0f, 24f);
            float sunset = Mathf.Clamp(profile.SunsetHour, 0f, 24f);
            float cloudAttenuation = 1f - Mathf.Clamp(cloudiness, 0f, 1f) * 0.68f;

            float sunProgress = DayArcProgress(hour, sunrise, sunset);
            float sunElevation = sunProgress < 0f ? 0f : Mathf.Sin(sunProgress * Mathf.Pi);
            Vector2 sunDirection = DirectionAcrossSky(sunProgress < 0f ? 0.5f : sunProgress, 0.88f);
            float sunEnergy = profile.SunLightEnergy
                * Mathf.Pow(Mathf.Clamp(sunElevation, 0f, 1f), 0.72f)
                * daylight
                * cloudAttenuation;
            Color sunColor = profile.SampleSunColor(hour / 24f);

            float moonProgress = NightArcProgress(hour, sunrise, sunset);
            float moonElevation = moonProgress < 0f ? 0f : Mathf.Sin(moonProgress * Mathf.Pi);
            // Mặt trăng đi ngược nhịp mặt trời một chút để bóng đêm không đứng cùng một hướng cả ngày.
            Vector2 moonDirection = DirectionAcrossSky(moonProgress < 0f ? 0.5f : 1f - moonProgress, 0.62f);
            float moonEnergy = profile.MoonLightEnergy
                * Mathf.Pow(Mathf.Clamp(moonElevation, 0f, 1f), 0.78f)
                * nightFactor
                * (1f - Mathf.Clamp(cloudiness, 0f, 1f) * 0.55f);

            bool sunIsKey = sunEnergy >= moonEnergy;
            float maxSun = Mathf.Max(profile.SunLightEnergy, 0.0001f);
            float maxMoon = Mathf.Max(profile.MoonLightEnergy, 0.0001f);
            float keyStrength01 = sunIsKey
                ? Mathf.Clamp(sunEnergy / maxSun, 0f, 1f)
                : Mathf.Clamp(moonEnergy / maxMoon, 0f, 1f);

            return new Sample
            {
                SunDirection = sunDirection,
                SunElevation = sunElevation,
                SunEnergy = sunEnergy,
                SunColor = sunColor,
                MoonDirection = moonDirection,
                MoonElevation = moonElevation,
                MoonEnergy = moonEnergy,
                MoonColor = profile.MoonLightColor,
                KeyDirection = sunIsKey ? sunDirection : moonDirection,
                KeyElevation = sunIsKey ? sunElevation : moonElevation,
                KeyStrength01 = keyStrength01,
                KeyColor = sunIsKey ? sunColor : profile.MoonLightColor
            };
        }

        private static float DayArcProgress(float hour, float sunrise, float sunset)
        {
            if (sunset <= sunrise || hour < sunrise || hour > sunset)
            {
                return -1f;
            }

            return Mathf.Clamp((hour - sunrise) / Mathf.Max(sunset - sunrise, 0.001f), 0f, 1f);
        }

        private static float NightArcProgress(float hour, float sunrise, float sunset)
        {
            if (sunset <= sunrise)
            {
                return -1f;
            }

            float nightLength = (24f - sunset) + sunrise;
            if (nightLength <= 0.001f)
            {
                return -1f;
            }

            if (hour >= sunset)
            {
                return Mathf.Clamp((hour - sunset) / nightLength, 0f, 1f);
            }

            if (hour <= sunrise)
            {
                return Mathf.Clamp(((24f - sunset) + hour) / nightLength, 0f, 1f);
            }

            return -1f;
        }

        private static Vector2 DirectionAcrossSky(float progress, float horizontalReach)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            float elevation = Mathf.Sin(progress * Mathf.Pi);
            float x = Mathf.Lerp(-horizontalReach, horizontalReach, progress);
            // Screen-space +Y là xuống. Ở giữa ngày tia gần thẳng xuống, sáng/chiều tia xiên rõ hơn.
            float y = Mathf.Lerp(0.50f, 1.0f, elevation);
            return new Vector2(x, y).Normalized();
        }
    }
}
