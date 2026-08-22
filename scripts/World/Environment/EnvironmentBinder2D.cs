using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Adapter giữa state global và một map 2D cụ thể.
    /// Core không biết Field 1 có node nào; map chỉ opt-in consumer bằng group.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        public const string DefaultConsumerGroup = "environment_shader_consumer";

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

        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        /// <summary>
        /// Root dùng để scan consumer. Để trống thì binder tự lấy root của map hiện tại.
        /// </summary>
        [Export]
        public NodePath ScopeRootPath { get; set; }

        [Export]
        public StringName ConsumerGroup { get; set; } = DefaultConsumerGroup;

        private readonly List<CanvasItem> _consumers = new();
        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;
        private Node _scopeRoot;

        public override void _Ready()
        {
            _environment = WorldEnvironmentService.GetOrCreate(GetTree());
            if (_environment == null)
            {
                GD.PrintErr("[EnvironmentBinder2D] WorldEnvironmentService unavailable.");
                SetProcess(false);
                return;
            }

            if (Profile != null)
            {
                _environment.SetProfile(Profile, snapToDefaultWeather: false);
            }

            if (CanvasModulatePath != null && !CanvasModulatePath.IsEmpty)
            {
                _canvasModulate = GetNodeOrNull<CanvasModulate>(CanvasModulatePath);
            }

            _scopeRoot = ResolveScopeRoot();
            RefreshConsumers();
            ApplyState();
        }

        public override void _Process(double delta)
        {
            ApplyState();
        }

        public void RefreshConsumers()
        {
            _consumers.Clear();
            if (_scopeRoot == null)
            {
                return;
            }

            CollectConsumersRecursive(_scopeRoot);
        }

        private Node ResolveScopeRoot()
        {
            if (ScopeRootPath != null && !ScopeRootPath.IsEmpty)
            {
                Node configured = GetNodeOrNull(ScopeRootPath);
                if (configured != null)
                {
                    return configured;
                }
            }

            // Cấu trúc chuẩn: MapRoot/EnvironmentFX/WorldFxController.
            return GetParent()?.GetParent() ?? GetTree()?.CurrentScene;
        }

        private void CollectConsumersRecursive(Node node)
        {
            if (node is CanvasItem item && item.IsInGroup(ConsumerGroup))
            {
                _consumers.Add(item);
            }

            foreach (Node child in node.GetChildren())
            {
                CollectConsumersRecursive(child);
            }
        }

        private void ApplyState()
        {
            if (_environment == null)
            {
                return;
            }

            EnvironmentState state = _environment.CurrentState;

            if (_canvasModulate != null)
            {
                _canvasModulate.Color = state.AmbientColor;
            }

            for (int i = _consumers.Count - 1; i >= 0; i--)
            {
                CanvasItem item = _consumers[i];
                if (item == null || !GodotObject.IsInstanceValid(item))
                {
                    _consumers.RemoveAt(i);
                    continue;
                }

                if (item.Material is ShaderMaterial material)
                {
                    PushEnvironmentUniforms(material, state);
                }
            }
        }

        private static void PushEnvironmentUniforms(ShaderMaterial material, EnvironmentState state)
        {
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
        }
    }
}
