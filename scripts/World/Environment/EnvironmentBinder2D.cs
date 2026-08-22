using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Adapter mỏng giữa một map 2D và WorldEnvironmentService.
    ///
    /// V1.1 không còn scan node/material. Shader đọc global uniforms trực tiếp từ GPU;
    /// binder chỉ chọn profile của map và áp ambient tint lên CanvasModulate.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;

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

            ApplyAmbient();
        }

        public override void _Process(double delta)
        {
            ApplyAmbient();
        }

        private void ApplyAmbient()
        {
            if (_environment == null || _canvasModulate == null)
            {
                return;
            }

            _canvasModulate.Color = _environment.CurrentState.AmbientColor;
        }
    }
}
