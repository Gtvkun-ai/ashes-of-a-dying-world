using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Adapter giữa một map 2D và WorldEnvironmentService.
    ///
    /// Shadow Core V3.3: binder scans materials, then pushes environment state once per frame.
    /// Per-caster shadow data lives on cloned materials, avoiding Godot instance-uniform limits.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        private const double MaterialRescanSeconds = 4.0;

        private readonly EnvironmentMaterialBus _materialBus = new();
        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;
        private WorldLighting2D _lighting;
        private AmbientFireflies2D _fireflies;
        private WorldAtmosphere2D _atmosphere;
        private ShadowRenderer2D _shadowRenderer;
        private double _materialRescanCountdown;
        private int _lastReportedMaterialCount = -1;

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
            RebuildMaterialBindings();
            ApplyVisualState();
        }

        public override void _Process(double delta)
        {
            _materialRescanCountdown -= delta;
            if (_materialRescanCountdown <= 0.0)
            {
                RebuildMaterialBindings();
            }

            ApplyVisualState();
        }

        private void EnsureRuntimeFx()
        {
            _lighting = GetNodeOrNull<WorldLighting2D>("CelestialLighting");
            if (_lighting == null)
            {
                _lighting = new WorldLighting2D { Name = "CelestialLighting" };
                AddChild(_lighting);
            }

            _shadowRenderer = GetNodeOrNull<ShadowRenderer2D>("ShadowRenderer");
            if (_shadowRenderer == null)
            {
                _shadowRenderer = new ShadowRenderer2D { Name = "ShadowRenderer" };
                AddChild(_shadowRenderer);
            }

            _fireflies = GetNodeOrNull<AmbientFireflies2D>("NightFireflies");
            if (_fireflies == null)
            {
                _fireflies = new AmbientFireflies2D { Name = "NightFireflies" };
                AddChild(_fireflies);
            }

            _atmosphere = GetNodeOrNull<WorldAtmosphere2D>("Atmosphere");
            if (_atmosphere == null)
            {
                _atmosphere = new WorldAtmosphere2D { Name = "Atmosphere" };
                AddChild(_atmosphere);
            }
        }

        private void RebuildMaterialBindings()
        {
            Node root = GetTree()?.CurrentScene ?? GetTree()?.Root;
            int materialCount = _materialBus.Rebuild(root);
            _materialRescanCountdown = MaterialRescanSeconds;

            if (materialCount != _lastReportedMaterialCount)
            {
                _lastReportedMaterialCount = materialCount;
                GD.Print(
                    $"[EnvironmentBinder2D] BOUND materials={materialCount} shadow_core=V4-authored-look+footprint " +
                    $"profile={Profile?.ResourcePath ?? "<none>"}");
            }
        }

        private void ApplyVisualState()
        {
            if (_environment == null)
            {
                return;
            }

            EnvironmentState state = _environment.CurrentState;
            _materialBus.Push(state);
            _lighting?.ApplyEnvironment(state);
            _shadowRenderer?.ApplyEnvironment(state);
            _fireflies?.ApplyEnvironment(state);
            _atmosphere?.ApplyEnvironment(state);

            if (_canvasModulate != null)
            {
                _canvasModulate.Color = state.AmbientColor;
            }
        }
    }
}
