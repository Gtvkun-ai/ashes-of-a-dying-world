using Godot;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Prompt HUD nhỏ dùng chung cho interactable. Không phụ thuộc panel inventory/menu.
    /// </summary>
    public partial class InteractionPromptHud : CanvasLayer
    {
        public const string RuntimeNodeName = "InteractionPromptHud";
        public static InteractionPromptHud Current { get; private set; }

        private PanelContainer _panel;
        private Label _label;

        public static InteractionPromptHud GetOrCreate(SceneTree tree)
        {
            if (Current != null && GodotObject.IsInstanceValid(Current))
            {
                return Current;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            InteractionPromptHud existing = tree.Root.GetNodeOrNull<InteractionPromptHud>(RuntimeNodeName);
            if (existing != null)
            {
                Current = existing;
                return existing;
            }

            var created = new InteractionPromptHud { Name = RuntimeNodeName, Layer = 80 };
            tree.Root.AddChild(created);
            return created;
        }

        public override void _Ready()
        {
            Current = this;
            BuildUi();
            HidePrompt();
        }

        public override void _ExitTree()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void ShowPrompt(string text)
        {
            if (_panel == null || _label == null || string.IsNullOrWhiteSpace(text))
            {
                HidePrompt();
                return;
            }

            _label.Text = $"[E]  {text}";
            _panel.Show();
        }

        public void HidePrompt()
        {
            _panel?.Hide();
        }

        private void BuildUi()
        {
            if (_panel != null)
            {
                return;
            }

            _panel = new PanelContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f,
                AnchorTop = 1f,
                AnchorRight = 0.5f,
                AnchorBottom = 1f,
                OffsetLeft = -150f,
                OffsetTop = -82f,
                OffsetRight = 150f,
                OffsetBottom = -42f
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.07f, 0.055f, 0.04f, 0.92f),
                BorderColor = new Color(0.48f, 0.38f, 0.22f, 0.95f)
            };
            style.SetBorderWidthAll(1);
            style.BorderWidthBottom = 2;
            style.SetCornerRadiusAll(3);
            _panel.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 7);
            margin.AddThemeConstantOverride("margin_bottom", 7);
            _panel.AddChild(margin);

            _label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _label.AddThemeFontSizeOverride("font_size", 14);
            _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f));
            margin.AddChild(_label);

            AddChild(_panel);
        }
    }
}
