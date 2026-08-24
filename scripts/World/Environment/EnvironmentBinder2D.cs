using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Binder map V5.4.
    ///
    /// EnvironmentState -> GPU globals đã do WorldEnvironmentService + ShaderGlobalBridge xử lý.
    /// Binder chỉ gắn các consumer thuộc riêng scene map: CanvasModulate, lighting, shadow system,
    /// fireflies và atmosphere. Không scan ShaderMaterial nữa.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;
        private WorldLighting2D _lighting;
        private AmbientFireflies2D _fireflies;
        private WorldAtmosphere2D _atmosphere;
        private EnvironmentShadowSystem2D _shadowSystem;
        private EnvironmentMassShadow2D _massShadow;
        private CanvasItem _sceneCloudShadow;
        private CanvasItem _sceneColorGrade;
        private bool _reportedReady;

        public override void _Ready()
        {
            _environment = WorldEnvironmentService.GetOrCreate(GetTree());
            if (_environment == null)
            {
                GD.PrintErr("[EnvironmentBinder2D V5.4] WorldEnvironmentService unavailable.");
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
            ApplyVisualState();
        }

        public override void _Process(double delta)
        {
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

            _shadowSystem = GetNodeOrNull<EnvironmentShadowSystem2D>("ShadowSystemV5");
            if (_shadowSystem == null)
            {
                _shadowSystem = new EnvironmentShadowSystem2D { Name = "ShadowSystemV5" };
                AddChild(_shadowSystem);
            }

            _massShadow = GetNodeOrNull<EnvironmentMassShadow2D>("MassShadowV51");
            if (_massShadow == null)
            {
                _massShadow = new EnvironmentMassShadow2D { Name = "MassShadowV51" };
                AddChild(_massShadow);
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

            _sceneCloudShadow = GetNodeOrNull<CanvasItem>("../WorldCloudShadow");
            _sceneColorGrade = GetNodeOrNull<CanvasItem>("../../WorldPostFX/ColorGrade");
            if (_sceneCloudShadow != null)
            {
                _sceneCloudShadow.Visible = true;
            }
            if (_sceneColorGrade != null)
            {
                _sceneColorGrade.Visible = true;
            }

            if (!_reportedReady)
            {
                _reportedReady = true;
                GD.Print(
                    $"[EnvironmentBinder2D] READY V5.4 | gpu=global_uniforms | material_scan=OFF | lighting_owner=material | " +
                    $"grass_canvas=456x474 | shadow=ground_footprint | mass_shadow=ON | profile={Profile?.ResourcePath ?? "<none>"}");
            }
        }

        private void ApplyVisualState()
        {
            if (_environment == null)
            {
                return;
            }

            EnvironmentState state = _environment.CurrentState;
            _lighting?.ApplyEnvironment(state);
            _shadowSystem?.ApplyEnvironment(state);
            _massShadow?.ApplyEnvironment(state);
            _fireflies?.ApplyEnvironment(state);
            _atmosphere?.ApplyEnvironment(state);

            if (_canvasModulate != null)
            {
                _canvasModulate.Color = state.AmbientColor;
            }
        }
    }
}
