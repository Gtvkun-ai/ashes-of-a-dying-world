using System;
using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Bus material theo scene cho Environment Core.
    ///
    /// Vì shader globals của Godot PHẢI tồn tại trong Project Settings trước khi shader được compile,
    /// một patch thiếu override.cfg có thể làm toàn bộ shader world fail compile. Bus này tránh dependency
    /// đó: shader dùng uniform local, còn map binder chỉ cập nhật các ShaderMaterial world duy nhất.
    ///
    /// PackedScene resources mặc định được share giữa các instance, nên hàng trăm cây thường vẫn chỉ tạo
    /// vài material unique. Rebuild chỉ quét scene định kỳ để bắt prop spawn động; Push không quét tree.
    /// </summary>
    public sealed class EnvironmentMaterialBus
    {
        private const string WorldShaderPrefix = "res://assets/shaders/world/";

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
        private static readonly StringName EnvShadowStrength = "env_shadow_strength";
        private static readonly StringName EnvWaterShimmer = "env_water_shimmer";
        private static readonly StringName EnvWaterRipple = "env_water_ripple";

        private readonly Dictionary<ulong, ShaderMaterial> _materials = new();

        public int MaterialCount => _materials.Count;

        public int Rebuild(Node root)
        {
            _materials.Clear();
            if (root != null)
            {
                Collect(root);
            }
            return _materials.Count;
        }

        public void Push(EnvironmentState state)
        {
            if (state == null || _materials.Count == 0)
            {
                return;
            }

            foreach (ShaderMaterial material in _materials.Values)
            {
                if (!GodotObject.IsInstanceValid(material))
                {
                    continue;
                }

                // Tất cả shader world V1.3 dùng cùng contract local-uniform này.
                material.SetShaderParameter(EnvTime01, state.TimeOfDay01);
                material.SetShaderParameter(EnvDaylight, state.Daylight);
                material.SetShaderParameter(EnvNight, state.NightFactor);
                material.SetShaderParameter(EnvSunDirection, state.SunDirection);
                material.SetShaderParameter(EnvSunColor, state.SunColor);
                material.SetShaderParameter(EnvWind, state.WindStrength);
                material.SetShaderParameter(EnvRain, state.RainAmount);
                material.SetShaderParameter(EnvWetness, state.Wetness);
                material.SetShaderParameter(EnvFog, state.FogAmount);
                material.SetShaderParameter(EnvCloudiness, state.Cloudiness);
                material.SetShaderParameter(EnvShadowStrength, state.ShadowStrength);
                material.SetShaderParameter(EnvWaterShimmer, state.WaterShimmerStrength);
                material.SetShaderParameter(EnvWaterRipple, state.WaterRippleStrength);
            }
        }

        private void Collect(Node node)
        {
            if (node is CanvasItem canvasItem && canvasItem.Material is ShaderMaterial shaderMaterial)
            {
                Shader shader = shaderMaterial.Shader;
                string path = shader?.ResourcePath ?? string.Empty;
                // projected_asset_shadow có lifecycle riêng trong EnvironmentShadowBus;
                // nếu MaterialBus chạm vào nó sẽ set một loạt uniform không tồn tại vô ích.
                bool isProjectedShadow = path.EndsWith("/projected_asset_shadow.gdshader", StringComparison.OrdinalIgnoreCase);
                if (!isProjectedShadow && path.StartsWith(WorldShaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _materials[shaderMaterial.GetInstanceId()] = shaderMaterial;
                }
            }

            foreach (Node child in node.GetChildren())
            {
                Collect(child);
            }
        }
    }
}
