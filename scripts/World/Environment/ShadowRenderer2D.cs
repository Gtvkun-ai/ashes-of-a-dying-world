using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Renderer trung tâm của Shadow Core V2.
    ///
    /// Toàn bộ caster dùng chung một ShaderMaterial. Mỗi frame renderer chỉ ghi 4 uniform chung.
    /// Không scan scene, không loop caster, không material-per-object.
    /// Animated caster tự cập nhật texture bằng signal FrameChanged của AnimatedSprite2D.
    /// </summary>
    public partial class ShadowRenderer2D : Node
    {
        private const string SharedMaterialPath = "res://assets/materials/world/projected_shadow_shared.tres";

        private ShaderMaterial _sharedMaterial;
        private bool _reportedReady;

        public override void _Ready()
        {
            _sharedMaterial = ResourceLoader.Load<ShaderMaterial>(SharedMaterialPath);
            if (_sharedMaterial == null)
            {
                GD.PrintErr($"[ShadowRenderer2D] Không load được shared material: {SharedMaterialPath}");
            }

            SetProcess(false);
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (_sharedMaterial == null || state == null)
            {
                return;
            }

            Vector2 direction = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;

            _sharedMaterial.SetShaderParameter("shadow_direction", direction);
            _sharedMaterial.SetShaderParameter("shadow_length01", Mathf.Clamp(state.ShadowLength01, 0f, 1f));
            _sharedMaterial.SetShaderParameter(
                "shadow_strength",
                Mathf.Clamp(state.ShadowStrength * state.KeyLightStrength01, 0f, 1f));
            _sharedMaterial.SetShaderParameter("shadow_night_factor", Mathf.Clamp(state.NightFactor, 0f, 1f));

            if (!_reportedReady)
            {
                _reportedReady = true;
                GD.Print("[ShadowRenderer2D] READY V2.2 | template material + material-bus shadow uniforms");
            }
        }
    }
}
