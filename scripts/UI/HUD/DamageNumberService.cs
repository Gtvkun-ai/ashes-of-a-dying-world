using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class DamageNumberService : CanvasLayer
    {
        private class DamageNumber
        {
            public Label Label;
            public Vector2 Velocity;
            public float Lifetime;
            public float Age;
            public Color BaseColor;
            public float StartScale;
        }

        public static DamageNumberService Instance { get; private set; }

        [Export] public Vector2 ScreenOffset { get; set; } = new(0f, -42f);
        [Export] public Vector2 FloatVelocity { get; set; } = new(0f, -46f);
        [Export] public float Lifetime { get; set; } = 0.78f;
        [Export] public int FontSize { get; set; } = 18;
        [Export] public Color DamageColor { get; set; } = new Color(1f, 0.35f, 0.16f);

        private readonly List<DamageNumber> _numbers = new();

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }

            Instance = this;
            Layer = 80;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static DamageNumberService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            var service = new DamageNumberService { Name = "DamageNumberService" };
            tree.Root.AddChild(service);
            return service;
        }

        public void ShowDamage(Node2D source, float amount)
        {
            ShowDamage(source, amount, false, false, false);
        }

        public void ShowDamage(Node2D source, float amount, bool shattered, bool ice, bool blocked)
        {
            if (source == null || amount <= 0f)
            {
                return;
            }

            Color color = shattered
                ? new Color(0.70f, 0.96f, 1f)
                : ice
                    ? new Color(0.48f, 0.84f, 1f)
                    : blocked
                        ? new Color(1f, 0.86f, 0.42f)
                        : DamageColor;
            string prefix = shattered ? "SHATTER  " : string.Empty;
            float startScale = shattered ? 1.45f : blocked ? 0.92f : 1.12f;

            var label = new Label
            {
                Text = prefix + Mathf.RoundToInt(amount),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                TopLevel = true,
                ZIndex = 220,
                Modulate = color,
                Scale = Vector2.One * startScale
            };
            label.AddThemeFontSizeOverride("font_size", shattered ? FontSize + 3 : FontSize);
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", shattered ? 5 : 4);
            AddChild(label);

            Vector2 screenPos = source.GetGlobalTransformWithCanvas().Origin + ScreenOffset;
            Vector2 labelSize = label.GetCombinedMinimumSize();
            label.PivotOffset = labelSize * 0.5f;
            label.Position = screenPos - new Vector2(labelSize.X * 0.5f, labelSize.Y);

            _numbers.Add(new DamageNumber
            {
                Label = label,
                Velocity = FloatVelocity * (shattered ? 1.18f : 1f),
                Lifetime = Mathf.Max(0.1f, shattered ? Lifetime * 1.15f : Lifetime),
                Age = 0f,
                BaseColor = color,
                StartScale = startScale
            });
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                DamageNumber number = _numbers[i];
                if (!GodotObject.IsInstanceValid(number.Label))
                {
                    _numbers.RemoveAt(i);
                    continue;
                }

                number.Age += dt;
                float progress = Mathf.Clamp(number.Age / number.Lifetime, 0f, 1f);
                number.Label.Position += number.Velocity * dt;
                number.Label.Modulate = new Color(
                    number.BaseColor.R,
                    number.BaseColor.G,
                    number.BaseColor.B,
                    1f - progress);
                float pop = progress < 0.18f
                    ? Mathf.Lerp(number.StartScale, number.StartScale * 1.12f, progress / 0.18f)
                    : Mathf.Lerp(number.StartScale * 1.12f, 0.94f, (progress - 0.18f) / 0.82f);
                number.Label.Scale = Vector2.One * pop;

                if (progress >= 1f)
                {
                    number.Label.QueueFree();
                    _numbers.RemoveAt(i);
                }
            }
        }
    }
}
