using AshesofaDyingWorld.UI.Theme;
using Godot;

namespace AshesofaDyingWorld.UI.Shared
{
    /// <summary>
    /// Stable code-native button states shared by menus and utility panels.
    /// Every state keeps the same silhouette, margins and minimum size.
    /// </summary>
    public static class PixelButtonSkin
    {
        public enum Variant
        {
            Primary,
            Secondary,
            Danger
        }

        public const float CompactHeight = UiTokens.ButtonHeightCompact;
        public const float TabHeight = UiTokens.ButtonHeightCompact;
        public const float RegularHeight = UiTokens.ButtonHeightRegular;
        public const float LargeActionHeight = UiTokens.ButtonHeightLarge;
        public const float FeatureTileWidth = 120f;
        public const float FeatureTileHeight = 80f;

        private const float DefaultMinHeight = RegularHeight;
        private const float HorizontalContentPadding = UiTokens.Space3;
        private const float VerticalContentPadding = UiTokens.Space2;

        public static void ApplyPrimary(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Primary, minHeight, minWidth);
        }

        public static void ApplySecondary(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Secondary, minHeight, minWidth);
        }

        public static void ApplyDanger(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Danger, minHeight, minWidth);
        }

        public static void ApplyTab(Button button, bool selected, float minHeight = TabHeight, float minWidth = 0f)
        {
            Apply(button, selected ? Variant.Primary : Variant.Secondary, minHeight, minWidth);
        }

        public static void Apply(Button button, Variant variant, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            if (button == null)
            {
                return;
            }

            Vector2 currentMinimum = button.CustomMinimumSize;
            button.CustomMinimumSize = new Vector2(
                Mathf.Max(currentMinimum.X, minWidth),
                Mathf.Max(currentMinimum.Y, minHeight));
            button.Flat = false;

            button.AddThemeStyleboxOverride("normal", CreateStateStyle(variant, ButtonState.Normal));
            button.AddThemeStyleboxOverride("hover", CreateStateStyle(variant, ButtonState.Hover));
            button.AddThemeStyleboxOverride("focus", CreateStateStyle(variant, ButtonState.Focus));
            button.AddThemeStyleboxOverride("pressed", CreateStateStyle(variant, ButtonState.Pressed));
            button.AddThemeStyleboxOverride("disabled", CreateStateStyle(variant, ButtonState.Disabled));

            button.AddThemeColorOverride("font_color", TextColor(variant));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_focus_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", UiTokens.TextDisabled);
            button.AddThemeColorOverride("icon_normal_color", UiTokens.TextPrimary);
            button.AddThemeColorOverride("icon_hover_color", Colors.White);
            button.AddThemeColorOverride("icon_focus_color", Colors.White);
            button.AddThemeColorOverride("icon_pressed_color", Colors.White);
            button.AddThemeColorOverride("icon_disabled_color", UiTokens.TextDisabled);
        }

        private enum ButtonState
        {
            Normal,
            Hover,
            Focus,
            Pressed,
            Disabled
        }

        private static StyleBoxFlat CreateStateStyle(Variant variant, ButtonState state)
        {
            (Color background, Color border) = BaseColors(variant);

            switch (state)
            {
                case ButtonState.Hover:
                    background = background.Lightened(0.08f);
                    border = border.Lightened(0.12f);
                    break;
                case ButtonState.Focus:
                    border = variant == Variant.Danger ? UiTokens.Danger.Lightened(0.18f) : UiTokens.TextPrimary;
                    break;
                case ButtonState.Pressed:
                    background = background.Darkened(0.12f);
                    border = border.Darkened(0.06f);
                    break;
                case ButtonState.Disabled:
                    background = WithAlpha(background, 0.46f);
                    border = WithAlpha(border, 0.38f);
                    break;
            }

            var style = new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = border,
                ContentMarginLeft = HorizontalContentPadding,
                ContentMarginRight = HorizontalContentPadding,
                ContentMarginTop = VerticalContentPadding,
                ContentMarginBottom = VerticalContentPadding
            };
            style.SetBorderWidthAll(UiTokens.SelectionBorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);

            if (state == ButtonState.Focus)
            {
                style.ShadowColor = WithAlpha(UiTokens.Accent, 0.35f);
                style.ShadowSize = UiTokens.SelectionBorderWidth;
            }

            return style;
        }

        private static (Color Background, Color Border) BaseColors(Variant variant)
        {
            return variant switch
            {
                Variant.Primary => (UiTokens.SurfaceRaised, UiTokens.Accent),
                Variant.Danger => (UiTokens.Danger.Darkened(0.58f), UiTokens.Danger),
                _ => (UiTokens.Surface, UiTokens.BorderStrong)
            };
        }

        private static Color TextColor(Variant variant)
        {
            return variant == Variant.Danger
                ? UiTokens.TextPrimary.Lightened(0.02f)
                : UiTokens.TextPrimary;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.R, color.G, color.B, alpha);
        }
    }
}
