using Godot;
using GodotTheme = Godot.Theme;

namespace AshesofaDyingWorld.UI.Theme
{
    public static class UiThemeFactory
    {
        private const string FontRoot = "res://assets/fonts";
        private const string RegularFontPath = FontRoot + "/BeVietnamPro-Regular.ttf";
        private const string MediumFontPath = FontRoot + "/BeVietnamPro-Medium.ttf";
        private const string SemiBoldFontPath = FontRoot + "/BeVietnamPro-SemiBold.ttf";
        private const string ItalicFontPath = FontRoot + "/BeVietnamPro-Italic.ttf";
        private const string SemiBoldItalicFontPath = FontRoot + "/BeVietnamPro-SemiBoldItalic.ttf";

        private static GodotTheme _sharedTheme;
        private static Font _regular;
        private static Font _medium;
        private static Font _semiBold;
        private static Font _italic;
        private static Font _semiBoldItalic;

        public static GodotTheme GetSharedTheme()
        {
            if (_sharedTheme != null)
            {
                return _sharedTheme;
            }

            LoadFonts();

            var theme = new GodotTheme
            {
                DefaultFontSize = UiTokens.BodyFontSize
            };

            if (_regular != null)
            {
                theme.DefaultFont = _regular;
            }

            SetFont(theme, "font", "Label", _regular);
            SetFont(theme, "font", "Button", _medium);
            SetFont(theme, "font", "CheckButton", _medium);
            SetFont(theme, "font", "OptionButton", _medium);
            SetFont(theme, "font", "LineEdit", _regular);
            SetFont(theme, "normal_font", "RichTextLabel", _regular);
            SetFont(theme, "bold_font", "RichTextLabel", _semiBold);
            SetFont(theme, "italics_font", "RichTextLabel", _italic);
            SetFont(theme, "bold_italics_font", "RichTextLabel", _semiBoldItalic);

            theme.SetColor("font_color", "Label", UiTokens.TextPrimary);
            theme.SetColor("font_color", "Button", UiTokens.TextPrimary);
            theme.SetColor("font_hover_color", "Button", Colors.White);
            theme.SetColor("font_focus_color", "Button", Colors.White);
            theme.SetColor("font_pressed_color", "Button", Colors.White);
            theme.SetColor("font_disabled_color", "Button", UiTokens.TextDisabled);
            theme.SetColor("default_color", "RichTextLabel", UiTokens.TextPrimary);

            theme.SetFontSize("normal_font_size", "RichTextLabel", UiTokens.BodyFontSize);
            theme.SetFontSize("bold_font_size", "RichTextLabel", UiTokens.BodyFontSize);
            theme.SetFontSize("italics_font_size", "RichTextLabel", UiTokens.BodyFontSize);
            theme.SetFontSize("bold_italics_font_size", "RichTextLabel", UiTokens.BodyFontSize);

            theme.SetStylebox("panel", "Panel", CreatePanelStyle(UiTokens.Background));
            theme.SetStylebox("panel", "PanelContainer", CreatePanelStyle(UiTokens.Background));

            _sharedTheme = theme;
            return _sharedTheme;
        }

        public static void Apply(Control root)
        {
            if (root == null)
            {
                return;
            }

            if (root.Theme == null)
            {
                // Existing screen themes may carry semantic component variants.
                root.Theme = GetSharedTheme();
            }
        }

        public static void ApplyText(Control control, UiTextRole role)
        {
            if (control == null)
            {
                return;
            }

            LoadFonts();
            (Font font, int size) = role switch
            {
                UiTextRole.ScreenTitle => (_semiBold, UiTokens.ScreenTitleFontSize),
                UiTextRole.SectionTitle => (_semiBold, UiTokens.SectionTitleFontSize),
                UiTextRole.Label => (_medium, UiTokens.LabelFontSize),
                UiTextRole.Micro => (_medium, UiTokens.MicroFontSize),
                UiTextRole.CombatNumber => (_semiBold, UiTokens.CombatNumberFontSize),
                _ => (_regular, UiTokens.BodyFontSize)
            };

            if (control is RichTextLabel)
            {
                AddFontOverride(control, "normal_font", _regular);
                AddFontOverride(control, "bold_font", _semiBold);
                AddFontOverride(control, "italics_font", _italic);
                AddFontOverride(control, "bold_italics_font", _semiBoldItalic);
                control.AddThemeFontSizeOverride("normal_font_size", size);
                control.AddThemeFontSizeOverride("bold_font_size", size);
                control.AddThemeFontSizeOverride("italics_font_size", size);
                control.AddThemeFontSizeOverride("bold_italics_font_size", size);
            }
            else
            {
                AddFontOverride(control, "font", font);
                control.AddThemeFontSizeOverride("font_size", size);
            }

            control.AddThemeColorOverride(
                "font_color",
                role == UiTextRole.Micro ? UiTokens.TextSecondary : UiTokens.TextPrimary);
        }

        private static void LoadFonts()
        {
            _regular ??= LoadFont(RegularFontPath);
            _medium ??= LoadFont(MediumFontPath) ?? _regular;
            _semiBold ??= LoadFont(SemiBoldFontPath) ?? _medium ?? _regular;
            _italic ??= LoadFont(ItalicFontPath) ?? _regular;
            _semiBoldItalic ??= LoadFont(SemiBoldItalicFontPath) ?? _semiBold ?? _italic ?? _regular;
        }

        private static Font LoadFont(string path)
        {
            return ResourceLoader.Exists(path) ? GD.Load<Font>(path) : null;
        }

        private static void SetFont(GodotTheme theme, string name, string type, Font font)
        {
            if (font != null)
            {
                theme.SetFont(name, type, font);
            }
        }

        private static void AddFontOverride(Control control, string name, Font font)
        {
            if (font != null)
            {
                control.AddThemeFontOverride(name, font);
            }
        }

        private static StyleBoxFlat CreatePanelStyle(Color background)
        {
            var style = new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = UiTokens.Border
            };
            style.SetBorderWidthAll(UiTokens.BorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);
            return style;
        }
    }
}
