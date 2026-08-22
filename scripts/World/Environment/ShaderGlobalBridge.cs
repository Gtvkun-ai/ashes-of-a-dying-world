using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Cầu nối duy nhất từ EnvironmentState sang GPU.
    ///
    /// Shader không biết WorldClock / weather preset / map hiện tại. Chúng chỉ đọc
    /// global uniforms đã khai báo trong res://override.cfg. Nhờ vậy số lượng shader
    /// consumer không làm tăng công việc C# mỗi frame.
    /// </summary>
    public static class ShaderGlobalBridge
    {
        private static readonly StringName EnvTime01 = "env_time01";
        private static readonly StringName EnvDaylight = "env_daylight";
        private static readonly StringName EnvNight = "env_night";
        private static readonly StringName EnvSunDirection = "env_sun_direction";
        private static readonly StringName EnvSunColor = "env_sun_color";
        private static readonly StringName EnvWind = "env_wind";
        private static readonly StringName EnvRain = "env_rain";
        private static readonly StringName EnvWetness = "env_wetness";
        private static readonly StringName EnvFog = "env_fog";
        private static readonly StringName EnvCloudiness = "env_cloudiness";

        private static bool _validationAttempted;
        private static bool _configurationValid;

        /// <summary>
        /// Kiểm tra config một lần. Không đọc ngược global uniform từ RenderingServer,
        /// vì thao tác đó buộc render thread đồng bộ với CPU và không nên nằm trong loop.
        /// </summary>
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
                "shader_globals/env_wind",
                "shader_globals/env_rain",
                "shader_globals/env_wetness",
                "shader_globals/env_fog",
                "shader_globals/env_cloudiness"
            };

            bool ok = true;
            foreach (string setting in required)
            {
                if (ProjectSettings.HasSetting(setting))
                {
                    continue;
                }

                ok = false;
                GD.PushError($"[ShaderGlobalBridge] Missing ProjectSetting '{setting}'. " +
                             "Keep res://override.cfg from Environment Core V1.1 in the project root.");
            }

            _configurationValid = ok;
            return _configurationValid;
        }

        /// <summary>
        /// Publish một snapshot state sang RenderingServer. Set global parameter là thao tác
        /// write-only, không cần CPU/GPU synchronization và được thiết kế cho environment state.
        /// </summary>
        public static void Push(EnvironmentState state)
        {
            if (state == null || !ValidateConfiguration())
            {
                return;
            }

            RenderingServer.GlobalShaderParameterSet(EnvTime01, state.TimeOfDay01);
            RenderingServer.GlobalShaderParameterSet(EnvDaylight, state.Daylight);
            RenderingServer.GlobalShaderParameterSet(EnvNight, state.NightFactor);
            RenderingServer.GlobalShaderParameterSet(EnvSunDirection, state.SunDirection);
            RenderingServer.GlobalShaderParameterSet(EnvSunColor, state.SunColor);
            RenderingServer.GlobalShaderParameterSet(EnvWind, state.WindStrength);
            RenderingServer.GlobalShaderParameterSet(EnvRain, state.RainAmount);
            RenderingServer.GlobalShaderParameterSet(EnvWetness, state.Wetness);
            RenderingServer.GlobalShaderParameterSet(EnvFog, state.FogAmount);
            RenderingServer.GlobalShaderParameterSet(EnvCloudiness, state.Cloudiness);
        }
    }
}
