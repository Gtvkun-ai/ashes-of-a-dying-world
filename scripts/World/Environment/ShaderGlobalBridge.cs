using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Environment GPU Bridge V5.0.
    ///
    /// Đây là đường DUY NHẤT đẩy EnvironmentState sang shader world.
    /// Không scan scene, không SetShaderParameter lên hàng trăm ShaderMaterial.
    /// Shader đọc `global uniform` đã khai báo trong res://override.cfg.
    /// </summary>
    public static class ShaderGlobalBridge
    {
        private static readonly StringName EnvTime01 = "env_time01";
        private static readonly StringName EnvDaylight = "env_daylight";
        private static readonly StringName EnvNight = "env_night";
        private static readonly StringName EnvSunDirection = "env_sun_direction";
        private static readonly StringName EnvSunColor = "env_sun_color";
        private static readonly StringName EnvKeyDirection = "env_key_direction";
        private static readonly StringName EnvKeyColor = "env_key_color";
        private static readonly StringName EnvKeyElevation = "env_key_elevation";
        private static readonly StringName EnvKeyStrength = "env_key_strength";
        private static readonly StringName EnvGoldenHour = "env_golden_hour";
        private static readonly StringName EnvShadowLength = "env_shadow_length";
        private static readonly StringName EnvWind = "env_wind";
        private static readonly StringName EnvRain = "env_rain";
        private static readonly StringName EnvWetness = "env_wetness";
        private static readonly StringName EnvFog = "env_fog";
        private static readonly StringName EnvCloudiness = "env_cloudiness";
        private static readonly StringName EnvShadowStrength = "env_shadow_strength";
        private static readonly StringName EnvWaterShimmer = "env_water_shimmer";
        private static readonly StringName EnvWaterRipple = "env_water_ripple";

        private static bool _validationAttempted;
        private static bool _configurationValid;

        public static bool ValidateConfiguration()
        {
            if (_validationAttempted)
            {
                return _configurationValid;
            }

            _validationAttempted = true;
            string[] required =
            {
                "shader_globals/env_time01",
                "shader_globals/env_daylight",
                "shader_globals/env_night",
                "shader_globals/env_sun_direction",
                "shader_globals/env_sun_color",
                "shader_globals/env_key_direction",
                "shader_globals/env_key_color",
                "shader_globals/env_key_elevation",
                "shader_globals/env_key_strength",
                "shader_globals/env_golden_hour",
                "shader_globals/env_shadow_length",
                "shader_globals/env_wind",
                "shader_globals/env_rain",
                "shader_globals/env_wetness",
                "shader_globals/env_fog",
                "shader_globals/env_cloudiness",
                "shader_globals/env_shadow_strength",
                "shader_globals/env_water_shimmer",
                "shader_globals/env_water_ripple"
            };

            bool ok = true;
            foreach (string setting in required)
            {
                if (ProjectSettings.HasSetting(setting))
                {
                    continue;
                }

                ok = false;
                GD.PushError(
                    $"[ShaderGlobalBridge V5] Thiếu ProjectSetting '{setting}'. " +
                    "Hãy giữ res://override.cfg đi cùng patch V5.0.");
            }

            _configurationValid = ok;
            if (ok)
            {
                GD.Print("[ShaderGlobalBridge] READY V5.0 | globals=19 | material_scan=OFF");
            }
            return ok;
        }

        /// <summary>
        /// Push write-only sang RenderingServer. Không đọc ngược global uniform để tránh sync CPU/GPU.
        /// </summary>
        public static void Push(EnvironmentState state)
        {
            if (state == null || !ValidateConfiguration())
            {
                return;
            }

            float sunElevation = Mathf.Clamp(state.SunElevation, 0f, 1f);
            float lowSun = 1f - SmoothStep(0.38f, 0.78f, sunElevation);
            float horizonVisible = SmoothStep(0.035f, 0.24f, sunElevation);
            float goldenHour = Mathf.Clamp(state.Daylight * lowSun * horizonVisible, 0f, 1f);
            Vector2 keyDirection = state.KeyLightDirection.LengthSquared() > 0.0001f
                ? state.KeyLightDirection.Normalized()
                : Vector2.Down;

            RenderingServer.GlobalShaderParameterSet(EnvTime01, state.TimeOfDay01);
            RenderingServer.GlobalShaderParameterSet(EnvDaylight, state.Daylight);
            RenderingServer.GlobalShaderParameterSet(EnvNight, state.NightFactor);
            RenderingServer.GlobalShaderParameterSet(EnvSunDirection, state.SunDirection);
            RenderingServer.GlobalShaderParameterSet(EnvSunColor, state.SunColor);
            RenderingServer.GlobalShaderParameterSet(EnvKeyDirection, keyDirection);
            RenderingServer.GlobalShaderParameterSet(EnvKeyColor, state.KeyLightColor);
            RenderingServer.GlobalShaderParameterSet(EnvKeyElevation, Mathf.Clamp(state.KeyLightElevation, 0f, 1f));
            RenderingServer.GlobalShaderParameterSet(EnvKeyStrength, Mathf.Clamp(state.KeyLightStrength01, 0f, 1f));
            RenderingServer.GlobalShaderParameterSet(EnvGoldenHour, goldenHour);
            RenderingServer.GlobalShaderParameterSet(EnvShadowLength, Mathf.Clamp(state.ShadowLength01, 0f, 1f));
            RenderingServer.GlobalShaderParameterSet(EnvWind, state.WindStrength);
            RenderingServer.GlobalShaderParameterSet(EnvRain, state.RainAmount);
            RenderingServer.GlobalShaderParameterSet(EnvWetness, state.Wetness);
            RenderingServer.GlobalShaderParameterSet(EnvFog, state.FogAmount);
            RenderingServer.GlobalShaderParameterSet(EnvCloudiness, state.Cloudiness);
            RenderingServer.GlobalShaderParameterSet(EnvShadowStrength, state.ShadowStrength);
            RenderingServer.GlobalShaderParameterSet(EnvWaterShimmer, state.WaterShimmerStrength);
            RenderingServer.GlobalShaderParameterSet(EnvWaterRipple, state.WaterRippleStrength);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float denom = Mathf.Max(edge1 - edge0, 0.0001f);
            float t = Mathf.Clamp((value - edge0) / denom, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
