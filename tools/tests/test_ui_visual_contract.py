from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]
TOKENS_PATH = REPO_ROOT / "scripts/UI/Theme/UiTokens.cs"
THEME_PATH = REPO_ROOT / "scripts/UI/Theme/UiThemeFactory.cs"
CHROME_PATH = REPO_ROOT / "scripts/UI/HUD/InventoryPanelChrome.cs"
BUTTON_PATH = REPO_ROOT / "scripts/UI/HUD/PixelButtonSkin.cs"


def _source(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _hexes(source: str) -> set[str]:
    return {value.upper() for value in re.findall(r"#[0-9A-Fa-f]{6}", source)}


def _color_tokens(source: str) -> dict[str, str]:
    return {
        name: value.upper()
        for name, value in re.findall(
            r'public static readonly Color\s+(\w+)\s*=\s*new\("(#[0-9A-Fa-f]{6})"\)',
            source,
        )
    }


def _relative_luminance(hex_value: str) -> float:
    channels = [int(hex_value[index : index + 2], 16) / 255 for index in range(1, 6, 2)]

    def linearize(channel: float) -> float:
        return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4

    red, green, blue = (linearize(channel) for channel in channels)
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue


def _contrast_ratio(foreground: str, background: str) -> float:
    lighter, darker = sorted((_relative_luminance(foreground), _relative_luminance(background)), reverse=True)
    return (lighter + 0.05) / (darker + 0.05)


def test_ui_tokens_match_approved_palette():
    source = _source(TOKENS_PATH)
    expected = {
        "Canvas": "#120F0C",
        "Background": "#1D1813",
        "Surface": "#29221B",
        "SurfaceRaised": "#352C22",
        "Border": "#5C4B38",
        "BorderStrong": "#8A6B42",
        "Accent": "#C39A57",
        "TextPrimary": "#F0E5D2",
        "TextSecondary": "#BFAF98",
        "TextDisabled": "#756B5D",
        "Danger": "#B9574F",
        "Life": "#7FA56D",
        "Memory": "#9A72C7",
        "Ice": "#62B8D9",
    }

    assert _color_tokens(source) == expected
    assert "Grid = 4" in source
    assert "IconSize = 24" in source
    assert "ButtonHeightRegular = 40" in source
    assert "CornerRadius = 3" in source


def test_active_text_tokens_meet_contrast_ratio():
    source = _source(TOKENS_PATH)
    tokens = _color_tokens(source)
    assert _contrast_ratio(tokens["TextPrimary"], tokens["Background"]) >= 4.5
    assert _contrast_ratio(tokens["TextSecondary"], tokens["Background"]) >= 4.5


def test_vietnamese_font_faces_are_bundled():
    expected_fonts = {
        "BeVietnamPro-Regular.ttf",
        "BeVietnamPro-Medium.ttf",
        "BeVietnamPro-SemiBold.ttf",
        "BeVietnamPro-Italic.ttf",
        "BeVietnamPro-SemiBoldItalic.ttf",
    }

    bundled_fonts = {path.name for path in (REPO_ROOT / "assets/fonts").glob("BeVietnamPro-*.ttf")}
    assert expected_fonts <= bundled_fonts
    assert (REPO_ROOT / "assets/fonts/OFL.txt").is_file()

    theme_source = _source(THEME_PATH)
    for font_name in expected_fonts:
        assert font_name in theme_source


def test_shared_chrome_uses_theme_tokens():
    chrome_source = _source(CHROME_PATH)
    button_source = _source(BUTTON_PATH)
    theme_source = _source(THEME_PATH)

    assert "UiTokens" in chrome_source
    assert "UiThemeFactory.Apply" in chrome_source
    assert "UiTokens" in button_source
    assert "UiThemeFactory" in theme_source


def test_shared_theme_apply_preserves_existing_root_theme():
    source = _source(THEME_PATH)
    apply_body = re.search(
        r"public static void Apply\(Control root\)(.*?)(?=public static void ApplyText)",
        source,
        re.DOTALL,
    ).group(1)

    assert "if (root.Theme == null)" in apply_body
    assert apply_body.count("root.Theme = GetSharedTheme();") == 1


def test_pixel_button_skin_does_not_resize_ai_exports():
    source = _source(BUTTON_PATH)

    assert "StyleBoxFlat" in source
    for state in ("normal", "hover", "focus", "pressed", "disabled"):
        assert f'AddThemeStyleboxOverride("{state}", CreateStateStyle' in source

    assert "style.SetBorderWidthAll(UiTokens.SelectionBorderWidth)" in source
    assert "style.SetCornerRadiusAll(UiTokens.CornerRadius)" in source
    assert source.count("ContentMarginLeft = HorizontalContentPadding") == 1
    assert "ButtonState.Focus" in source
    assert "style.ShadowSize = UiTokens.SelectionBorderWidth" in source
    assert "MaximumSourceHeight" not in source
    assert "NormalizeSourceTexture" not in source
    assert "Asset AI/export" not in source
