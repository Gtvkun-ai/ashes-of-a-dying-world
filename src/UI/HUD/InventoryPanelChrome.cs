using Godot;
using System;

namespace AshesofaDyingWorld.UI.Shared
{
    /// <summary>
    /// Lớp trình bày dùng chung cho InventoryPanel và CharacterDetailUI.
    /// Mọi thành phần tạo nên "khung inventory" nằm ở đây để hai panel không tự
    /// sao chép palette, margin và StyleBox rồi lệch nhau sau vài lần chỉnh sửa.
    /// </summary>
    public static class InventoryPanelChrome
    {
        public const float PanelWidth = 1120f;
        public const float PanelHeight = 640f;
        public const float SlotSize = 64f;
        public const float DetailPanelWidth = 330f;
        public const int OuterFramePatchMargin = 18;

        public const string AssetRoot = "res://assets/sprites/UI_HUD/Inventory";
        public const string OuterFramePath = AssetRoot + "/frame_9slice.png";
        public const string GrainTexturePath = AssetRoot + "/grain.png";

        public static readonly Color WindowColor = new("#20140e");
        public static readonly Color HeaderColor = new("#2a1a12");
        public static readonly Color SurfaceColor = new("#382318");
        public static readonly Color RaisedSurfaceColor = new("#442a1c");
        public static readonly Color DeepSurfaceColor = new("#1b120d");
        public static readonly Color SlotSurfaceColor = new("#241710");
        public static readonly Color BorderColor = new("#68482f");
        public static readonly Color StrongBorderColor = new("#8d6542");
        public static readonly Color AccentColor = new("#d0a45c");
        public static readonly Color MainTextColor = new("#f1e5d2");
        public static readonly Color MutedTextColor = new("#c0aa8e");
        public static readonly Color DangerColor = new("#6b342d");
        public static readonly Color ButtonNormalColor = new("#2c1c13");
        public static readonly Color ButtonHoverColor = new("#442a1c");

        public static void ApplyPanelSize(Control panel)
        {
            panel.AnchorLeft = 0.5f;
            panel.AnchorTop = 0.5f;
            panel.AnchorRight = 0.5f;
            panel.AnchorBottom = 0.5f;
            panel.OffsetLeft = -PanelWidth * 0.5f;
            panel.OffsetTop = -PanelHeight * 0.5f;
            panel.OffsetRight = PanelWidth * 0.5f;
            panel.OffsetBottom = PanelHeight * 0.5f;
            panel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
            panel.GrowHorizontal = Control.GrowDirection.Both;
            panel.GrowVertical = Control.GrowDirection.Both;
        }

        /// <summary>
        /// Dựng nguyên lớp vỏ đang được InventoryPanel sử dụng và trả về VBox chứa
        /// header, tab bar và body. Frame được thêm sau content để luôn nằm trên cùng.
        /// </summary>
        public static VBoxContainer BuildWindowShell(Control owner)
        {
            var layers = new Control();
            layers.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layers.ClipContents = true;
            owner.AddChild(layers);

            var window = new PanelContainer();
            window.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            window.AddThemeStyleboxOverride("panel", CreateWindowStyle());
            window.MouseFilter = Control.MouseFilterEnum.Ignore;
            layers.AddChild(window);

            var grainTexture = TryLoadTexture(GrainTexturePath);
            if (grainTexture != null)
            {
                var grain = new TextureRect();
                grain.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                grain.Size = new Vector2(PanelWidth, PanelHeight);
                grain.Texture = grainTexture;
                grain.StretchMode = TextureRect.StretchModeEnum.Tile;
                grain.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
                grain.Modulate = new Color(1f, 1f, 1f, 0.24f);
                grain.MouseFilter = Control.MouseFilterEnum.Ignore;
                layers.AddChild(grain);
            }

            var outerMargin = new MarginContainer();
            outerMargin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            outerMargin.AddThemeConstantOverride("margin_left", 22);
            outerMargin.AddThemeConstantOverride("margin_top", 22);
            outerMargin.AddThemeConstantOverride("margin_right", 22);
            outerMargin.AddThemeConstantOverride("margin_bottom", 20);
            layers.AddChild(outerMargin);

            var root = new VBoxContainer();
            root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 5);
            outerMargin.AddChild(root);

            var outerFrameTexture = TryLoadTexture(OuterFramePath);
            if (outerFrameTexture != null)
            {
                var frame = new NinePatchRect();
                frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                frame.Texture = outerFrameTexture;
                frame.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                frame.DrawCenter = false;
                frame.PatchMarginLeft = OuterFramePatchMargin;
                frame.PatchMarginTop = OuterFramePatchMargin;
                frame.PatchMarginRight = OuterFramePatchMargin;
                frame.PatchMarginBottom = OuterFramePatchMargin;
                frame.MouseFilter = Control.MouseFilterEnum.Ignore;
                layers.AddChild(frame);
            }

            return root;
        }

        public static PanelContainer CreateHeader(out HBoxContainer row)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(0, 52);
            panel.AddThemeStyleboxOverride("panel", CreateHeaderStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_top", 7);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 7);
            panel.AddChild(margin);

            row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            margin.AddChild(row);
            return panel;
        }

        public static PanelContainer CreateTabBar(out HBoxContainer tabs, int leftMargin = 6)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(0, 44);
            panel.AddThemeStyleboxOverride("panel", CreateTabsBarStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", leftMargin);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_right", 6);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            panel.AddChild(margin);

            tabs = new HBoxContainer();
            tabs.AddThemeConstantOverride("separation", 4);
            tabs.Alignment = BoxContainer.AlignmentMode.Begin;
            margin.AddChild(tabs);
            return panel;
        }

        public static Label CreateLabel(string text, int fontSize, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        public static ColorRect CreateDivider(bool thin = false)
        {
            var divider = new ColorRect();
            divider.Color = thin
                ? new Color(BorderColor.R, BorderColor.G, BorderColor.B, 0.72f)
                : BorderColor;
            divider.CustomMinimumSize = new Vector2(0, 1);
            divider.MouseFilter = Control.MouseFilterEnum.Ignore;
            return divider;
        }

        public static Button CreateCloseButton(Action onPressed)
        {
            var button = new Button();
            button.Text = "X";
            button.CustomMinimumSize = new Vector2(38, 36);
            button.FocusMode = Control.FocusModeEnum.None;
            button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            if (onPressed != null)
            {
                button.Pressed += onPressed;
            }

            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(DangerColor, StrongBorderColor, 2));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(DangerColor.Lightened(0.08f), AccentColor, 2));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(DangerColor.Darkened(0.08f), StrongBorderColor, 2));
            button.AddThemeColorOverride("font_color", MainTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 16);
            return button;
        }

        /// <summary>
        /// Đúng trạng thái tab của InventoryPanel: tab active là inset panel viền đồng,
        /// tab thường trong suốt, không dùng kiểu navbar xanh của panel cũ.
        /// </summary>
        public static void ApplyTabStyle(Button button, bool selected)
        {
            if (button == null) return;

            button.AddThemeStyleboxOverride("normal", CreateTabStyle(
                selected ? RaisedSurfaceColor.Lightened(0.025f) : new Color(0f, 0f, 0f, 0f),
                selected ? AccentColor.Darkened(0.16f) : new Color(0f, 0f, 0f, 0f),
                selected ? 1 : 0));
            button.AddThemeStyleboxOverride("hover", CreateTabStyle(
                RaisedSurfaceColor.Lightened(0.07f),
                StrongBorderColor,
                1));
            button.AddThemeStyleboxOverride("pressed", CreateTabStyle(
                DeepSurfaceColor,
                AccentColor,
                1));
            button.AddThemeColorOverride("font_color", selected ? MainTextColor : MutedTextColor);
            button.AddThemeColorOverride("font_hover_color", MainTextColor);
            button.AddThemeColorOverride("font_pressed_color", AccentColor.Lightened(0.08f));
            button.AddThemeFontSizeOverride("font_size", 14);
        }

        public static StyleBoxFlat CreateWindowStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WindowColor;
            style.BorderColor = new Color(StrongBorderColor.R, StrongBorderColor.G, StrongBorderColor.B, 0.42f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(4);
            style.ShadowColor = new Color(0f, 0f, 0f, 0.56f);
            style.ShadowSize = 10;
            return style;
        }

        public static StyleBoxFlat CreateHeaderStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WithAlpha(HeaderColor, 0.86f);
            style.BorderColor = BorderColor;
            style.BorderWidthBottom = 1;
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateTabsBarStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WithAlpha(SurfaceColor.Darkened(0.035f), 0.76f);
            style.BorderColor = BorderColor;
            style.BorderWidthBottom = 1;
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateSectionStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WithAlpha(SurfaceColor, 0.72f);
            style.BorderColor = new Color(BorderColor.R, BorderColor.G, BorderColor.B, 0.86f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateDetailSectionStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WithAlpha(RaisedSurfaceColor.Darkened(0.055f), 0.74f);
            style.BorderColor = new Color(StrongBorderColor.R, StrongBorderColor.G, StrongBorderColor.B, 0.9f).Darkened(0.1f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreatePreviewStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = WithAlpha(DeepSurfaceColor, 0.82f);
            style.BorderColor = new Color(StrongBorderColor.R, StrongBorderColor.G, StrongBorderColor.B, 0.86f).Darkened(0.18f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateButtonStyle(Color background, Color border, int borderWidth)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = border;
            style.SetBorderWidthAll(borderWidth);
            style.SetCornerRadiusAll(3);
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            return style;
        }

        public static StyleBoxFlat CreateSlotStyle(bool selected = false)
        {
            var style = new StyleBoxFlat();
            style.BgColor = selected ? RaisedSurfaceColor.Lightened(0.02f) : SlotSurfaceColor;
            style.BorderColor = selected ? AccentColor : BorderColor.Darkened(0.05f);
            style.SetBorderWidthAll(selected ? 2 : 1);
            style.SetCornerRadiusAll(2);
            style.ContentMarginLeft = 2;
            style.ContentMarginTop = 2;
            style.ContentMarginRight = 2;
            style.ContentMarginBottom = 2;

            if (selected)
            {
                style.ShadowColor = new Color(AccentColor.R, AccentColor.G, AccentColor.B, 0.24f);
                style.ShadowSize = 4;
            }
            return style;
        }

        public static StyleBoxFlat CreateSlotHoverStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(RaisedSurfaceColor.R, RaisedSurfaceColor.G, RaisedSurfaceColor.B, 0.8f);
            style.BorderColor = StrongBorderColor.Lightened(0.08f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateSlotPressedStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = DeepSurfaceColor.Darkened(0.04f);
            style.BorderColor = AccentColor.Darkened(0.08f);
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(2);
            return style;
        }

        public static StyleBoxFlat CreateTabStyle(Color background, Color border, int borderWidth)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = border;
            style.SetBorderWidthAll(borderWidth);
            style.SetCornerRadiusAll(2);
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            return style;
        }

        public static StyleBoxFlat CreateTransparentButtonStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0f, 0f, 0f, 0f);
            return style;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.R, color.G, color.B, alpha);
        }

        public static Texture2D TryLoadTexture(string path)
        {
            return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        }
    }
}
