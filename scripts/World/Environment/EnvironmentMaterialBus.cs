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
        private static readonly StringName ShadowDirection = "shadow_direction";
        private static readonly StringName ShadowLength01 = "shadow_length01";
        private static readonly StringName ShadowStrength = "shadow_strength";
        private static readonly StringName ShadowNightFactor = "shadow_night_factor";

        private readonly Dictionary<ulong, ShaderMaterial> _materials = new();
        private readonly Dictionary<ulong, ShaderMaterial> _shadowMaterials = new();

        public int MaterialCount => _materials.Count + _shadowMaterials.Count;

        public int Rebuild(Node root)
        {
            _materials.Clear();
            _shadowMaterials.Clear();
            if (root != null)
            {
                Collect(root);
            }
            return MaterialCount;
        }

        public void Push(EnvironmentState state)
        {
            if (state == null || (_materials.Count == 0 && _shadowMaterials.Count == 0))
            {
                return;
            }

            float sunElevation = Mathf.Clamp(state.SunElevation, 0f, 1f);
            float lowSun = 1f - SmoothStep(0.30f, 0.68f, sunElevation);
            float horizonVisible = SmoothStep(0.055f, 0.18f, sunElevation);
            float goldenHour = Mathf.Clamp(state.Daylight * lowSun * horizonVisible, 0f, 1f);
            Vector2 keyDirection = state.KeyLightDirection.LengthSquared() > 0.0001f
                ? state.KeyLightDirection.Normalized()
                : Vector2.Down;

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
                material.SetShaderParameter(EnvKeyDirection, keyDirection);
                material.SetShaderParameter(EnvKeyColor, state.KeyLightColor);
                material.SetShaderParameter(EnvKeyElevation, Mathf.Clamp(state.KeyLightElevation, 0f, 1f));
                material.SetShaderParameter(EnvKeyStrength, Mathf.Clamp(state.KeyLightStrength01, 0f, 1f));
                material.SetShaderParameter(EnvGoldenHour, goldenHour);
                material.SetShaderParameter(EnvShadowLength, Mathf.Clamp(state.ShadowLength01, 0f, 1f));
                material.SetShaderParameter(EnvWind, state.WindStrength);
                material.SetShaderParameter(EnvRain, state.RainAmount);
                material.SetShaderParameter(EnvWetness, state.Wetness);
                material.SetShaderParameter(EnvFog, state.FogAmount);
                material.SetShaderParameter(EnvCloudiness, state.Cloudiness);
                material.SetShaderParameter(EnvShadowStrength, state.ShadowStrength);
                material.SetShaderParameter(EnvWaterShimmer, state.WaterShimmerStrength);
                material.SetShaderParameter(EnvWaterRipple, state.WaterRippleStrength);
            }

            Vector2 shadowDirection = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;
            // Bóng dài đẹp nhất ở lúc thiên thể thấp, nên không được nhân thẳng với key strength
            // như V2.2 (cách đó làm bình minh/hoàng hôn còn ~1-2% alpha). Ta giữ visibility nền,
            // rồi chỉ giảm mềm khi đêm sâu / mây dày.
            float keyVisibility = 0.46f + 0.54f * Mathf.Sqrt(Mathf.Clamp(state.KeyLightStrength01, 0f, 1f));
            float nightAttenuation = Mathf.Lerp(1.0f, 0.66f, Mathf.Clamp(state.NightFactor, 0f, 1f));
            float cloudAttenuation = 1.0f - Mathf.Clamp(state.Cloudiness, 0f, 1f) * 0.22f;
            float longShadowArtBoost = Mathf.Lerp(1.0f, 1.12f, Mathf.Clamp(state.ShadowLength01, 0f, 1f));
            float shadowStrength = Mathf.Clamp(
                state.ShadowStrength * keyVisibility * nightAttenuation * cloudAttenuation * longShadowArtBoost,
                0f,
                1f);

            foreach (ShaderMaterial material in _shadowMaterials.Values)
            {
                if (!GodotObject.IsInstanceValid(material))
                {
                    continue;
                }

                material.SetShaderParameter(ShadowDirection, shadowDirection);
                material.SetShaderParameter(ShadowLength01, Mathf.Clamp(state.ShadowLength01, 0f, 1f));
                material.SetShaderParameter(ShadowStrength, shadowStrength);
                material.SetShaderParameter(ShadowNightFactor, Mathf.Clamp(state.NightFactor, 0f, 1f));
            }
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (Mathf.Abs(edge1 - edge0) < 0.00001f)
            {
                return value < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private void Collect(Node node)
        {
            if (node is CanvasItem canvasItem && canvasItem.Material is ShaderMaterial shaderMaterial)
            {
                Shader shader = shaderMaterial.Shader;
                string path = shader?.ResourcePath ?? string.Empty;
                // Shadow Core V2.2 uses cloned per-caster materials, so the bus also
                // pushes the small set of shared shadow environment uniforms.
                bool isProjectedShadow = path.EndsWith("/projected_asset_shadow.gdshader", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/projected_shadow_v2.gdshader", StringComparison.OrdinalIgnoreCase);
                if (isProjectedShadow)
                {
                    _shadowMaterials[shaderMaterial.GetInstanceId()] = shaderMaterial;
                }
                else if (path.StartsWith(WorldShaderPrefix, StringComparison.OrdinalIgnoreCase))
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
