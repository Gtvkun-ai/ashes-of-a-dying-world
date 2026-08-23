using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Bộ giải quỹ đạo ánh sáng 2D cho mặt trời / mặt trăng.
    ///
    /// V2.1 tách rõ hai khái niệm:
    /// - LightDirection: hướng tia sáng dùng cho direct lighting.
    /// - ShadowDirection2D: hướng BÓNG chạy trên mặt đất.
    ///
    /// Shadow azimuth đi qua một cung 180 độ thật sự trong ngày. Vì vậy bóng buổi sáng và
    /// buổi chiều có thể nằm ở hai nửa mặt phẳng đối nhau, thay vì chỉ đổi chút X nhưng mãi
    /// nằm phía dưới vật thể như bản cũ.
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

            public Vector2 KeyDirection { get; init; } = Vector2.Down;
            public float KeyElevation { get; init; } = 1f;
            public float KeyStrength01 { get; init; } = 1f;
            public Color KeyColor { get; init; } = Colors.White;

            /// <summary>Hướng bóng chạy trên mặt đất, đã có dấu và đã chọn Sun/Moon key.</summary>
            public Vector2 ShadowDirection2D { get; init; } = Vector2.Down;
            public float ShadowLength01 { get; init; }
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
            Vector2 moonDirection = DirectionAcrossSky(moonProgress < 0f ? 0.5f : 1f - moonProgress, 0.62f);
            float moonEnergy = profile.MoonLightEnergy
                * Mathf.Pow(Mathf.Clamp(moonElevation, 0f, 1f), 0.78f)
                * nightFactor
                * (1f - Mathf.Clamp(cloudiness, 0f, 1f) * 0.55f);

            // 06h-ish: bóng xuống-phải. 12h: gần xuống dưới chân. 18h-ish: bóng lên-trái.
            // Đây là một azimuth orbit thật sự, không còn y luôn dương như Shadow Core V2 cũ.
            Vector2 sunShadowDirection = ShadowOrbitDay(sunProgress < 0f ? 0.5f : sunProgress);

            // Moon tiếp tục quỹ đạo theo nửa vòng kế tiếp để đêm không reset bóng về cùng một hướng.
            Vector2 moonShadowDirection = ShadowOrbitNight(moonProgress < 0f ? 0.5f : moonProgress);

            bool sunIsKey = sunEnergy >= moonEnergy;
            float maxSun = Mathf.Max(profile.SunLightEnergy, 0.0001f);
            float maxMoon = Mathf.Max(profile.MoonLightEnergy, 0.0001f);
            float keyStrength01 = sunIsKey
                ? Mathf.Clamp(sunEnergy / maxSun, 0f, 1f)
                : Mathf.Clamp(moonEnergy / maxMoon, 0f, 1f);

            float shadowElevation = sunIsKey ? sunElevation : moonElevation;

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
                KeyElevation = shadowElevation,
                KeyStrength01 = keyStrength01,
                KeyColor = sunIsKey ? sunColor : profile.MoonLightColor,
                ShadowDirection2D = sunIsKey ? sunShadowDirection : moonShadowDirection,
                ShadowLength01 = 1f - Mathf.Clamp(shadowElevation, 0f, 1f)
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

        private static Vector2 ShadowOrbitDay(float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);

            // V4.1: tách hướng sáng / trưa / chiều quyết liệt hơn để người chơi NHÌN THẤY
            // mặt trời đang chạy, thay vì ba frame dùng gần như cùng một họ bóng.
            float angleDegrees;
            if (progress <= 0.5f)
            {
                float t = Smooth01(progress / 0.5f);
                // Morning: bóng quăng xuống-phải khá mạnh.
                angleDegrees = Mathf.Lerp(18f, 94f, t);
            }
            else
            {
                float t = Smooth01((progress - 0.5f) / 0.5f);
                // Evening: lật sang nửa mặt phẳng đối diện rõ hơn trước.
                angleDegrees = Mathf.Lerp(94f, 236f, t);
            }

            float radians = Mathf.DegToRad(angleDegrees);
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).Normalized();
        }

        private static Vector2 ShadowOrbitNight(float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            // Moon tiếp tục quỹ đạo nhưng nhẹ hơn. Bắt đầu từ đúng hướng kết thúc của hoàng hôn.
            float angleDegrees = Mathf.Lerp(236f, 416f, Smooth01(progress));
            float radians = Mathf.DegToRad(angleDegrees);
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).Normalized();
        }

        private static Vector2 DirectionAcrossSky(float progress, float horizontalReach)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            float elevation = Mathf.Sin(progress * Mathf.Pi);
            float x = Mathf.Lerp(-horizontalReach, horizontalReach, progress);
            float y = Mathf.Lerp(0.50f, 1.0f, elevation);
            return new Vector2(x, y).Normalized();
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
