using Godot;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Trail chém procedural ngắn, không phụ thuộc texture. Chỉ là presentation và tự hủy.
    /// </summary>
    public partial class CombatSwingTrail2D : Node2D
    {
        private Vector2 _direction = Vector2.Right;
        private float _age;
        private float _duration = 0.14f;
        private float _strength = 1f;

        public void Initialize(Vector2 direction, float strength)
        {
            _direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Right;
            _strength = Mathf.Clamp(strength, 0.6f, 2.5f);
            _duration = 0.11f + 0.04f * _strength;
            ZIndex = 120;
        }

        public override void _Process(double delta)
        {
            _age += Mathf.Max(0f, (float)delta);
            float progress = Mathf.Clamp(_age / Mathf.Max(0.01f, _duration), 0f, 1f);
            Modulate = new Color(1f, 1f, 1f, 1f - progress);
            Scale = Vector2.One * (0.92f + progress * 0.18f);
            QueueRedraw();
            if (progress >= 1f)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            float radius = 23f * _strength;
            float halfArc = Mathf.DegToRad(58f);
            float centerAngle = _direction.Angle();
            Color color = new Color(1f, 0.92f, 0.76f, 0.72f);
            Vector2 previous = Vector2.Right.Rotated(centerAngle - halfArc) * radius;
            const int segments = 8;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(centerAngle - halfArc, centerAngle + halfArc, t);
                Vector2 current = Vector2.Right.Rotated(angle) * radius;
                float width = Mathf.Lerp(3.2f, 1.0f, t) * _strength;
                DrawLine(previous, current, color, width, true);
                previous = current;
            }
        }
    }
}
