using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Shadow System V5.0.
    ///
    /// Khác V4:
    /// - Không còn ShaderMaterial clone theo từng caster.
    /// - Không còn projected_shadow vertex shader.
    /// - Không còn cluster_mass / border_mass heuristic.
    /// - Bóng của từng vật thể là Sprite2D footprint thật nằm trên ground plane.
    ///
    /// Hệ thống chỉ cập nhật transform khi hướng/độ dài/cường độ ánh sáng thay đổi đủ lớn.
    /// Caster động (player/Hyou/slime) vẫn tự đi theo parent vì footprint là node con.
    /// </summary>
    public partial class EnvironmentShadowSystem2D : Node
    {
        private const double DynamicCasterRescanSeconds = 2.0;
        private const float DirectionEpsilon = 0.0025f;
        private const float LengthEpsilon = 0.0035f;
        private const float StrengthEpsilon = 0.006f;
        private const float NightEpsilon = 0.01f;

        private readonly List<ShadowCaster2D> _casters = new();
        private double _rescanCountdown;
        private EnvironmentState _lastState;
        private Vector2 _lastDirection = Vector2.Zero;
        private float _lastLength = -1f;
        private float _lastStrength = -1f;
        private float _lastNight = -1f;
        private int _lastCasterCount = -1;
        private bool _forceApply = true;

        public override void _Ready()
        {
            RefreshCasters();
            _rescanCountdown = DynamicCasterRescanSeconds;
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            _rescanCountdown -= delta;
            if (_rescanCountdown > 0.0)
            {
                return;
            }

            _rescanCountdown = DynamicCasterRescanSeconds;
            int previous = _casters.Count;
            RefreshCasters();
            if (_casters.Count != previous && _lastState != null)
            {
                ApplyAll(_lastState);
            }
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null)
            {
                return;
            }

            _lastState = state;
            Vector2 direction = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;
            float length = Mathf.Clamp(state.ShadowLength01, 0f, 1f);
            float strength = Mathf.Clamp(state.ShadowStrength, 0f, 1f);
            float night = Mathf.Clamp(state.NightFactor, 0f, 1f);

            bool changed = _forceApply
                || direction.DistanceSquaredTo(_lastDirection) > DirectionEpsilon * DirectionEpsilon
                || Mathf.Abs(length - _lastLength) > LengthEpsilon
                || Mathf.Abs(strength - _lastStrength) > StrengthEpsilon
                || Mathf.Abs(night - _lastNight) > NightEpsilon;

            if (!changed)
            {
                return;
            }

            _forceApply = false;
            _lastDirection = direction;
            _lastLength = length;
            _lastStrength = strength;
            _lastNight = night;
            ApplyAll(state);
        }

        public void ForceRefresh()
        {
            RefreshCasters();
            _forceApply = true;
            if (_lastState != null)
            {
                ApplyEnvironment(_lastState);
            }
        }

        private void ApplyAll(EnvironmentState state)
        {
            for (int i = _casters.Count - 1; i >= 0; i--)
            {
                ShadowCaster2D caster = _casters[i];
                if (caster == null || !GodotObject.IsInstanceValid(caster) || !caster.IsInsideTree())
                {
                    _casters.RemoveAt(i);
                    continue;
                }

                caster.ApplyEnvironment(state);
            }
        }

        private void RefreshCasters()
        {
            _casters.Clear();
            SceneTree tree = GetTree();
            if (tree == null)
            {
                return;
            }

            foreach (Node node in tree.GetNodesInGroup(ShadowCaster2D.RuntimeGroup))
            {
                if (node is ShadowCaster2D caster && caster.IsInsideTree())
                {
                    _casters.Add(caster);
                }
            }

            if (_casters.Count != _lastCasterCount)
            {
                _lastCasterCount = _casters.Count;
                GD.Print($"[EnvironmentShadowSystem2D] READY V5.1 | footprint_casters={_casters.Count} | shader_projection=OFF | mass_heuristic=OFF");
            }
        }
    }
}
