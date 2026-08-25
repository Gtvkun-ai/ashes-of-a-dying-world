using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Lightweight atmosphere overlays adapted from WeatherSystem2D for the authored top-down map.
    /// Keeps weather/fog/beams on screen-space ColorRects so the painted terrain layers stay intact.
    /// </summary>
    public partial class WorldAtmosphere2D : Node
    {
        private const string RainShaderPath = "res://assets/shaders/world/atmosphere_rain.gdshader";
        private const string FogShaderPath = "res://assets/shaders/world/atmosphere_fog.gdshader";
        private const string CloudShadowShaderPath = "res://assets/shaders/world/atmosphere_cloud_shadow.gdshader";
        private const string SunbeamShaderPath = "res://assets/shaders/world/atmosphere_sunbeam.gdshader";

        private CanvasLayer _layer;
        private ShaderMaterial _cloudShadowMaterial;
        private ShaderMaterial _sunbeamMaterial;
        private ShaderMaterial _fogMaterial;
        private ShaderMaterial _rainMaterial;
        private bool _reportedReady;
        private bool _worldLockedCloudAvailable;

        public override void _Ready()
        {
            EnsureLayer();
            // Field V3 có cloud-shadow world-space riêng. Khi tồn tại pass này,
            // overlay screen-space cũ phải im để tránh hai lớp mây chồng nhau.
            _worldLockedCloudAvailable = GetTree()?.CurrentScene?.FindChild(
                "WorldCloudShadow",
                true,
                false) != null;
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null)
            {
                return;
            }

            EnsureLayer();

            float clearSky = 1f - Mathf.Clamp(state.Cloudiness * 0.78f + state.RainAmount * 0.95f, 0f, 1f);
            float lowSun = Smooth01(Mathf.InverseLerp(0.10f, 0.78f, state.ShadowLength01));
            float noonHaze = Smooth01(Mathf.InverseLerp(0.35f, 0.92f, state.KeyLightElevation))
                * (1f - lowSun * 0.62f);
            float shaftMood = Mathf.Clamp(lowSun * 0.92f + noonHaze * 0.44f, 0f, 1f);
            float beamIntensity = 0.225f
                * state.Daylight
                * Mathf.Max(state.KeyLightStrength01, 0.55f)
                * clearSky
                * shaftMood;

            Vector2 shadowDirection = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;

            _sunbeamMaterial?.SetShaderParameter("beam_direction", shadowDirection);
            _sunbeamMaterial?.SetShaderParameter("beam_color", WarmLight(state.KeyLightColor));
            _sunbeamMaterial?.SetShaderParameter("intensity", Mathf.Clamp(beamIntensity, 0f, 0.22f));
            _sunbeamMaterial?.SetShaderParameter("width", Mathf.Lerp(0.26f, 0.15f, lowSun));
            _sunbeamMaterial?.SetShaderParameter("softness", Mathf.Lerp(0.38f, 0.24f, lowSun));
            _sunbeamMaterial?.SetShaderParameter("center", Mathf.Lerp(-0.46f, -0.18f, lowSun));

            float cloudCoverage = Mathf.Clamp(state.Cloudiness, 0f, 1f);
            float cloudShadowStrength = _worldLockedCloudAvailable
                ? 0f
                : Mathf.Clamp(
                    cloudCoverage * state.Daylight * (0.45f + state.KeyLightStrength01 * 0.55f) * 0.10f,
                    0f,
                    0.10f);
            _cloudShadowMaterial?.SetShaderParameter("coverage", cloudCoverage);
            _cloudShadowMaterial?.SetShaderParameter("strength", cloudShadowStrength);
            _cloudShadowMaterial?.SetShaderParameter("wind_dir", new Vector2(1f, 0.12f + state.WindStrength * 0.08f));

            float fogDensity = Mathf.Clamp(
                state.FogAmount * 0.30f
                + state.RainAmount * 0.09f
                + state.Cloudiness * 0.030f
                + state.NightFactor * 0.012f,
                0f,
                0.32f);
            _fogMaterial?.SetShaderParameter("density", fogDensity);
            _fogMaterial?.SetShaderParameter("fog_color", FogColor(state));

            float rainAmount = Mathf.Clamp(state.RainAmount, 0f, 1f);
            _rainMaterial?.SetShaderParameter("amount", rainAmount);
            _rainMaterial?.SetShaderParameter("speed", 0.82f + state.WindStrength * 0.20f);
            _rainMaterial?.SetShaderParameter("slant", -0.04f - state.WindStrength * 0.055f);

            if (!_reportedReady)
            {
                _reportedReady = true;
                GD.Print("[WorldAtmosphere2D] READY V5.6 directional forest beams + world-locked cloud/fog/rain");
            }
        }

        private void EnsureLayer()
        {
            if (_layer != null && GodotObject.IsInstanceValid(_layer))
            {
                return;
            }

            _layer = new CanvasLayer
            {
                Name = "AtmosphereOverlay",
                Layer = 3
            };
            AddChild(_layer);

            _cloudShadowMaterial = CreateMaterial(CloudShadowShaderPath);
            _sunbeamMaterial = CreateMaterial(SunbeamShaderPath);
            _fogMaterial = CreateMaterial(FogShaderPath);
            _rainMaterial = CreateMaterial(RainShaderPath);

            _layer.AddChild(CreateOverlayRect("CloudShadow", _cloudShadowMaterial));
            _layer.AddChild(CreateOverlayRect("SunBeam", _sunbeamMaterial));
            _layer.AddChild(CreateOverlayRect("FogHaze", _fogMaterial));
            _layer.AddChild(CreateOverlayRect("RainOverlay", _rainMaterial));
        }

        private static ColorRect CreateOverlayRect(string name, ShaderMaterial material)
        {
            var rect = new ColorRect
            {
                Name = name,
                Color = Colors.Transparent,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = material
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rect.OffsetLeft = 0f;
            rect.OffsetTop = 0f;
            rect.OffsetRight = 0f;
            rect.OffsetBottom = 0f;
            return rect;
        }

        private static ShaderMaterial CreateMaterial(string shaderPath)
        {
            Shader shader = ResourceLoader.Exists(shaderPath)
                ? GD.Load<Shader>(shaderPath)
                : null;

            if (shader == null)
            {
                GD.PushWarning($"[WorldAtmosphere2D] Missing shader: {shaderPath}");
                return null;
            }

            return new ShaderMaterial
            {
                Shader = shader,
                ResourceLocalToScene = true
            };
        }

        private static Color WarmLight(Color keyColor)
        {
            Color fallback = new Color(1.0f, 0.82f, 0.48f, 0.34f);
            return new Color(
                Mathf.Lerp(fallback.R, keyColor.R, 0.35f),
                Mathf.Lerp(fallback.G, keyColor.G, 0.35f),
                Mathf.Lerp(fallback.B, keyColor.B, 0.25f),
                0.34f);
        }

        private static Color FogColor(EnvironmentState state)
        {
            Color day = new Color(0.68f, 0.78f, 0.72f, 0.72f);
            Color night = new Color(0.36f, 0.43f, 0.58f, 0.66f);
            Color rain = new Color(0.50f, 0.58f, 0.64f, 0.72f);
            Color color = day.Lerp(night, Mathf.Clamp(state.NightFactor, 0f, 1f));
            return color.Lerp(rain, Mathf.Clamp(Mathf.Max(state.RainAmount, state.Cloudiness * 0.35f), 0f, 1f));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
