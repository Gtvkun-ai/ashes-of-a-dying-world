from pathlib import Path
import re
import xml.etree.ElementTree as ET


REPO_ROOT = Path(__file__).resolve().parents[2]
TOKENS_PATH = REPO_ROOT / "scripts/UI/Theme/UiTokens.cs"
THEME_PATH = REPO_ROOT / "scripts/UI/Theme/UiThemeFactory.cs"
CHROME_PATH = REPO_ROOT / "scripts/UI/HUD/InventoryPanelChrome.cs"
BUTTON_PATH = REPO_ROOT / "scripts/UI/HUD/PixelButtonSkin.cs"
GLYPH_ATLAS_PATH = REPO_ROOT / "assets/graphics/ui/icons/ui_glyph_atlas.svg"
GLYPH_RESOLVER_PATH = REPO_ROOT / "scripts/UI/Theme/UiGlyphResolver.cs"
GLYPH_SMOKE_SCENE_PATH = REPO_ROOT / "tools/validation/ui_glyph_smoke_test.tscn"
GLYPH_SMOKE_SCRIPT_PATH = REPO_ROOT / "tools/validation/UiGlyphSmokeTest.cs"
ICON_RESOURCE_NAMES = ("str", "dex", "int", "vit", "spi", "def", "exit", "default_skill")
ATLAS_RESOURCE_PATH = "res://assets/graphics/ui/icons/ui_glyph_atlas.svg"


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
    for font_name in expected_fonts:
        assert (REPO_ROOT / f"assets/fonts/{font_name}.import").is_file()

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


def test_ui_glyph_atlas_uses_integer_24px_cells():
    assert GLYPH_ATLAS_PATH.is_file()
    assert Path(f"{GLYPH_ATLAS_PATH}.import").is_file()
    source = _source(GLYPH_ATLAS_PATH)

    svg_match = re.search(
        r'<svg\b[^>]*\bwidth="(\d+)"[^>]*\bheight="(\d+)"[^>]*\bviewBox="0 0 (\d+) (\d+)"',
        source,
    )
    assert svg_match is not None
    width, height, view_width, view_height = (int(value) for value in svg_match.groups())
    assert width == view_width
    assert height == view_height
    assert width > 0 and height > 0
    assert width % 24 == 0
    assert height % 24 == 0

    cell_origins = re.findall(r'<g\s+id="glyph-[^"]+"\s+transform="translate\(([-\d]+)\s+([-\d]+)\)"', source)
    assert cell_origins
    assert all(int(x) % 24 == 0 and int(y) % 24 == 0 for x, y in cell_origins)
    assert 'stroke-width="1.5"' in source
    assert 'class="highlight"' in source


def test_ui_glyph_atlas_geometry_stays_inside_24px_cells():
    root = ET.fromstring(_source(GLYPH_ATLAS_PATH))
    groups = [element for element in root if element.tag.endswith("}g")]
    assert groups

    for group in groups:
        transform = group.attrib["transform"]
        origin_match = re.fullmatch(r"translate\(([-\d.]+) ([-\d.]+)\)", transform)
        assert origin_match is not None
        assert all(float(value).is_integer() for value in origin_match.groups())

        for element in group.iter():
            if element is group:
                continue
            for attribute in ("x", "y", "cx", "cy"):
                if attribute in element.attrib:
                    value = float(element.attrib[attribute])
                    assert 0 <= value <= 24
            if "r" in element.attrib:
                center_x = float(element.attrib.get("cx", "0"))
                center_y = float(element.attrib.get("cy", "0"))
                radius = float(element.attrib["r"])
                assert radius <= center_x <= 24 - radius
                assert radius <= center_y <= 24 - radius
            if "width" in element.attrib:
                assert float(element.attrib["x"]) + float(element.attrib["width"]) <= 24
            if "height" in element.attrib:
                assert float(element.attrib["y"]) + float(element.attrib["height"]) <= 24
            if "d" in element.attrib:
                numbers = [float(value) for value in re.findall(r"[-+]?(?:\d*\.\d+|\d+\.?\d*)", element.attrib["d"])]
                assert numbers
                assert max(abs(value) for value in numbers) <= 24

    stroke_widths = [float(value) for value in re.findall(r'stroke-width="([^\"]+)"', _source(GLYPH_ATLAS_PATH))]
    assert stroke_widths
    assert all(1 <= value <= 2 for value in stroke_widths)


def test_all_runtime_navigation_glyphs_resolve():
    source = _source(GLYPH_RESOLVER_PATH)
    assert 'namespace AshesofaDyingWorld.UI.Theme' in source
    assert 'public enum UiGlyph' in source
    assert 'public static AtlasTexture Resolve(UiGlyph glyph)' in source
    assert 'ui_glyph_atlas.svg' in source
    assert 'Fallback' in source

    expected_glyphs = (
        "Menu", "Character", "Inventory", "Skills", "Quests", "Party", "Settings",
        "CategoryWeapon", "CategoryArmor", "CategoryConsumable", "CategoryMaterial",
        "CategoryQuest", "Target", "Strength", "Dexterity", "Intelligence",
        "Vitality", "Spirit", "Defense", "Exit",
    )
    for glyph in expected_glyphs:
        assert re.search(rf'\b{glyph}\b', source)
        assert re.search(rf'\[UiGlyph\.{glyph}\]\s*=\s*Cell\(', source)


def test_ui_glyph_resolver_has_non_null_missing_atlas_and_unknown_fallbacks():
    source = _source(GLYPH_RESOLVER_PATH)
    assert "private static ImageTexture _fallbackTexture;" in source
    assert "Image.CreateEmpty(CellSize, CellSize, false, Image.Format.Rgba8)" in source
    assert "ImageTexture.CreateFromImage(image)" in source
    assert "if (_atlas == null)" in source
    assert "return CreateFallbackTexture();" in source
    assert "Regions[UiGlyph.Fallback]" in source
    assert "new Rect2(0, 0, CellSize, CellSize)" in source


def test_ui_glyph_runtime_smoke_artifacts_exercise_fallbacks():
    assert GLYPH_SMOKE_SCENE_PATH.is_file()
    assert GLYPH_SMOKE_SCRIPT_PATH.is_file()

    resolver_source = _source(GLYPH_RESOLVER_PATH)
    assert "internal static void InjectAtlasForValidation(Texture2D atlas)" in resolver_source
    assert "internal static void ClearAtlasForValidation()" in resolver_source

    script_source = _source(GLYPH_SMOKE_SCRIPT_PATH)
    scene_source = _source(GLYPH_SMOKE_SCENE_PATH)
    assert "UiGlyphResolver.Resolve(UiGlyph.Menu)" in script_source
    assert "UiGlyphResolver.Resolve((UiGlyph)int.MaxValue)" in script_source
    assert "UiGlyphResolver.InjectAtlasForValidation(null)" in script_source
    assert "UiGlyphResolver.ClearAtlasForValidation()" in script_source
    assert "UI glyph smoke test passed" in script_source
    assert "GetTree().Quit(1)" in script_source
    assert 'path="res://tools/validation/UiGlyphSmokeTest.cs"' in scene_source


def test_data_stat_icons_use_clean_atlas_regions():
    atlas_paths = set()
    for icon_name in ICON_RESOURCE_NAMES:
        source = _source(REPO_ROOT / f"data/icons/{icon_name}.tres")
        assert ATLAS_RESOURCE_PATH in source
        assert "menu_action_icons_sheet.png" not in source
        assert "stat_icons_sheet.png" not in source
        atlas_paths.update(re.findall(r'path="([^"]+)"', source))

        region_match = re.search(
            r"region\s*=\s*Rect2\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)",
            source,
        )
        assert region_match is not None
        x, y, width, height = (float(value) for value in region_match.groups())
        assert all(value.is_integer() for value in (x, y, width, height))
        assert x % 24 == 0 and y % 24 == 0
        assert width == 24 and height == 24

    assert atlas_paths == {ATLAS_RESOURCE_PATH}
