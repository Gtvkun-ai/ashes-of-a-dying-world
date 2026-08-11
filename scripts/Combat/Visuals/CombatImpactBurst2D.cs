using Godot;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Impact VFX procedural để combat có feedback ngay cả khi chưa có asset particle riêng.
    /// Sau này có thể thay presentation này bằng scene/VFX asset mà không đổi combat logic.
    /// </summary>
    public partial class CombatImpactBurst2D : Node2D
    {
        private Vector2 _direction = Vector2.Right;
        private float _duration = 0.16f;
        private float _age;
        private float _strength = 1f;
        private bool _ice;
        private bool _blocked;
        private bool _shattered;

        public void Initialize(Vector2 direction, float strength, bool ice, bool blocked, bool shattered)
        {
            _direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Right;
            _strength = Mathf.Clamp(strength, 0.35f, 3f);
            _ice = ice;
            _blocked = blocked;
            _shattered = shattered;
            _duration = shattered ? 0.28f : blocked ? 0.13f : 0.18f;
            ZIndex = 250;
        }

        public override void _Process(double delta)
        {
            _age += Mathf.Max(0f, (float)delta);
            float progress = Mathf.Clamp(_age / Mathf.Max(0.01f, _duration), 0f, 1f);
            Scale = Vector2.One * (0.75f + progress * 0.65f);
            Modulate = new Color(1f, 1f, 1f, 1f - progress);
            QueueRedraw();
            if (progress >= 1f)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            Color main = _shattered
                ? new Color(0.72f, 0.96f, 1f, 0.95f)
                : _ice
                    ? new Color(0.48f, 0.86f, 1f, 0.92f)
                    : _blocked
                        ? new Color(1f, 0.91f, 0.55f, 0.92f)
                        : new Color(1f, 0.82f, 0.64f, 0.92f);
            Color core = new Color(1f, 1f, 1f, 0.96f);
            float baseLength = (_shattered ? 17f : 11f) * _strength;
            float width = Mathf.Max(1f, 1.7f * _strength);

            DrawCircle(Vector2.Zero, Mathf.Max(2f, 2.8f * _strength), core);
            DrawLine(-_direction * baseLength * 0.28f, _direction * baseLength, main, width, true);

            Vector2 tangent = new Vector2(-_direction.Y, _direction.X);
            DrawLine(-tangent * baseLength * 0.70f, tangent * baseLength * 0.70f, main, width, true);

            for (int i = 0; i < (_shattered ? 8 : 5); i++)
            {
                float angle = Mathf.Tau * i / (_shattered ? 8f : 5f) + 0.28f;
                Vector2 ray = Vector2.Right.Rotated(angle);
                float length = baseLength * (0.45f + (i % 3) * 0.12f);
                DrawLine(ray * 2f, ray * length, main, Mathf.Max(1f, width * 0.72f), true);
            }
        }
    }
}
