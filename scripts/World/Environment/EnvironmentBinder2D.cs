using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Adapter giữa một map 2D và WorldEnvironmentService.
    ///
    /// V1.4 giữ shader local-uniform đáng tin cậy của V1.3, đồng thời nối thêm Celestial Lighting,
    /// projected shadows và ambient fireflies. Map chỉ khai báo profile; core tự dựng runtime FX.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        private const double MaterialRescanSeconds = 4.0;
        private const double ShadowUpdateSeconds = 0.10;

        private readonly EnvironmentMaterialBus _materialBus = new();
        private readonly EnvironmentShadowBus _shadowBus = new();
        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;
        private WorldLighting2D _lighting;
        private AmbientFireflies2D _fireflies;
        private double _materialRescanCountdown;
        private double _shadowUpdateCountdown;
        private int _lastReportedMaterialCount = -1;
        private int _lastReportedShadowCount = -1;

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

            EnsureRuntimeFx();
            RebuildBindings();
            ApplyVisualState(forceShadowUpdate: true);
        }

        public override void _Process(double delta)
        {
            _materialRescanCountdown -= delta;
            _shadowUpdateCountdown -= delta;

            if (_materialRescanCountdown <= 0.0)
            {
                RebuildBindings();
            }

            bool updateShadows = _shadowUpdateCountdown <= 0.0;
            ApplyVisualState(updateShadows);
            if (updateShadows)
            {
                _shadowUpdateCountdown = ShadowUpdateSeconds;
            }
        }

        private void EnsureRuntimeFx()
        {
            _lighting = GetNodeOrNull<WorldLighting2D>("CelestialLighting");
            if (_lighting == null)
            {
                _lighting = new WorldLighting2D { Name = "CelestialLighting" };
                AddChild(_lighting);
            }

            _fireflies = GetNodeOrNull<AmbientFireflies2D>("NightFireflies");
            if (_fireflies == null)
            {
                _fireflies = new AmbientFireflies2D { Name = "NightFireflies" };
                AddChild(_fireflies);
            }
        }

        private void RebuildBindings()
        {
            Node root = GetTree()?.CurrentScene ?? GetTree()?.Root;
            int materialCount = _materialBus.Rebuild(root);
            int shadowCount = _shadowBus.Rebuild(root);
            _materialRescanCountdown = MaterialRescanSeconds;

            if (materialCount != _lastReportedMaterialCount || shadowCount != _lastReportedShadowCount)
            {
                _lastReportedMaterialCount = materialCount;
                _lastReportedShadowCount = shadowCount;
                GD.Print(
                    $"[EnvironmentBinder2D] BOUND materials={materialCount} projected_shadows={shadowCount} " +
                    $"profile={Profile?.ResourcePath ?? "<none>"}");
            }
        }

        private void ApplyVisualState(bool forceShadowUpdate)
        {
            if (_environment == null)
            {
                return;
            }

            EnvironmentState state = _environment.CurrentState;
            _materialBus.Push(state);
            _lighting?.ApplyEnvironment(state);
            _fireflies?.ApplyEnvironment(state);

            if (forceShadowUpdate)
            {
                _shadowBus.Push(state);
            }

            if (_canvasModulate != null)
            {
                _canvasModulate.Color = state.AmbientColor;
            }
        }
    }
}
