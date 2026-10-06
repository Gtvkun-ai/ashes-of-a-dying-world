from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]
INVENTORY_PATH = REPO_ROOT / "scripts/UI/HUD/InventoryPanel.cs"
CHARACTER_PATH = REPO_ROOT / "scripts/UI/HUD/CharacterDetailUI.cs"
CHROME_PATH = REPO_ROOT / "scripts/UI/HUD/InventoryPanelChrome.cs"
SHOWCASE_SCENE_PATH = REPO_ROOT / "tools/validation/ui_screen_showcase.tscn"
SHOWCASE_SCRIPT_PATH = REPO_ROOT / "tools/validation/ui_screen_showcase.gd"


def _source(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def test_inventory_copy_is_vietnamese_and_has_no_fake_currency_or_png_categories():
    source = _source(INVENTORY_PATH)

    assert 'CreateLabel("TÚI ĐỒ"' in source
    for label in ("Tất cả", "Tiêu hao", "Nguyên liệu", "Trang bị", "Nhiệm vụ", "Khác"):
        assert f'CreateCategoryButton("{label}"' in source

    assert "12,345" not in source
    assert "CoinIconPath" not in source
    assert "category_" not in source
    assert "UiGlyphResolver.Resolve" in source
    assert "UiGlyph.CategoryConsumable" in source


def test_inventory_uses_content_driven_responsive_navigation_and_real_filter_state():
    source = _source(INVENTORY_PATH)
    chrome = _source(CHROME_PATH)

    assert "CustomMinimumSize = new Vector2(0, 36)" in source
    assert "GetCategoryButtonWidth" not in source
    assert "AutowrapMode" in source
    assert "ShowCategory(category)" in source
    assert "ScrollContainer" in chrome
    assert "HorizontalScrollMode = ScrollContainer.ScrollMode.Auto" in chrome


def test_inventory_category_glyphs_stay_at_native_size():
    source = _source(INVENTORY_PATH)
    method = source.split("private Button CreateCategoryButton", 1)[1].split(
        "private Button CreateCloseButton", 1
    )[0]

    assert "button.ExpandIcon = false" in method
    assert "button.AutowrapMode = TextServer.AutowrapMode.Off" in method
    assert 'AddThemeConstantOverride("icon_max_width"' not in method


def test_inventory_empty_state_is_intentional_and_visible_copy_is_translated():
    source = _source(INVENTORY_PATH)

    for english in (
        '"INVENTORY"',
        '"All"',
        '"Consumables"',
        '"Materials"',
        '"Equipment"',
        '"Quest"',
        '"More"',
        '"Select Item"',
        '"Choose an item to view its details."',
        '"Sort: ID"',
        '"Sort: Name"',
        '"Sort: Type"',
    ):
        assert english not in source

    assert "Chưa chọn vật phẩm" in source
    assert "Chọn một vật phẩm để xem chi tiết." in source
    assert "Không có vật phẩm trong mục này." in source


def test_character_replaces_bracket_placeholders_and_fake_filter_arrow():
    source = _source(CHARACTER_PATH)

    assert "[ ẢNH CHÂN DUNG ]" not in source
    assert "[ Nhân vật ]" not in source
    assert "Bộ lọc: Tất cả ▼" not in source
    assert 'Text = "+"' not in source
    assert "Chưa có ảnh chân dung" in source
    assert "Chưa có dữ liệu nhân vật" in source
    assert "Chưa có thành viên khác" in source
    assert "OptionButton" in source
    assert "ItemSelected" in source


def test_empty_character_resource_bars_start_empty():
    source = _source(CHARACTER_PATH)
    method = source.split("private Control CreateResourceBarRow", 1)[1].split(
        "private static StyleBoxFlat CreateResourceRowStyle", 1
    )[0]

    assert "MaxValue = 100" in method
    assert "Value = 0" in method
    assert not re.search(r"^\s*Value = 100,\s*$", method, re.MULTILINE)


def test_character_preserves_portrait_paths_and_manager_integration():
    source = _source(CHARACTER_PATH)

    assert "BackgroundImage" in source
    assert "config.Icon" in source
    assert "BodyScene" in source
    assert "EquipFromInventory" in source
    assert "UnequipToInventory" in source
    assert "SetActiveCharacter" in source


def test_character_resource_bars_use_native_shared_styles_not_large_png_frames():
    source = _source(CHARACTER_PATH)

    assert "main_stats" not in source
    assert "MainStatFrameNativeSize" not in source
    assert "TextureRect FrameRect" not in source
    assert "ProgressBar Progress" in source
    assert "UiTokens.Life" in source
    assert "UiTokens.Memory" in source
    assert "UiTokens.Accent" in source
    assert "UiTokens.Danger" in source
    assert '"HP"' in source
    assert '"MP"' in source
    assert '"STA"' in source


def test_inventory_and_character_interactions_keep_keyboard_focus():
    inventory = _source(INVENTORY_PATH)
    character = _source(CHARACTER_PATH)
    chrome = _source(CHROME_PATH)

    assert "FocusModeEnum.None" not in inventory
    assert "FocusModeEnum.None" not in character
    assert "FocusModeEnum.None" not in chrome
    assert inventory.count("FocusModeEnum.All") >= 5
    assert character.count("FocusModeEnum.All") >= 8
    assert "AddThemeStyleboxOverride(\"focus\", InventoryPanelChrome.CreateSlotHoverStyle())" in character


def test_character_null_state_never_displays_sample_identity_data():
    source = _source(CHARACTER_PATH)

    for sample_copy in ("Hikaru", "Con người", "Cấp 01", "0 / 100 XP"):
        assert sample_copy not in source

    assert "ShowEmptyCharacterState();" in source
    assert 'CreateLabel("Chưa có nhân vật"' in source
    assert 'CreateLabel("Cấp --"' in source
    assert 'CreateLabel("Chưa có dữ liệu kinh nghiệm"' in source


def test_character_only_shows_no_other_member_state_when_party_has_no_other_member():
    source = _source(CHARACTER_PATH)
    load_list = re.search(
        r"private void LoadCharacterList\(\)(.*?)(?=private void OnCharacterSelected)",
        source,
        re.DOTALL,
    ).group(1)

    assert "Chưa có thành viên khác" in load_list
    assert re.search(r"if \([^\n]*PartyMembers\.Count\s*<=\s*1\)", load_list)


def test_all_equipment_slot_names_have_vietnamese_fallbacks():
    inventory = _source(INVENTORY_PATH)
    character = _source(CHARACTER_PATH)

    for enum_name, caption in (("Head", "Đầu"), ("Body", "Áo"), ("Legs", "Quần")):
        assert f'EquipmentSlot.{enum_name} => "{caption}"' in inventory
        assert f'EquipmentSlot.{enum_name} => "{caption}"' in character


def test_showcase_instantiates_selected_runtime_panel_and_checks_focus():
    scene = _source(SHOWCASE_SCENE_PATH)
    script = _source(SHOWCASE_SCRIPT_PATH)

    assert "anchors_preset = 15" in scene
    assert "custom_minimum_size" not in scene
    assert '"inventory": ["res://scripts/UI/HUD/InventoryPanel.cs"' in script
    assert '"character": ["res://scripts/UI/HUD/CharacterDetailUI.cs"' in script
    assert "--showcase-panel=" in script
    assert "_validate_panel" in script
    assert "Control.FOCUS_ALL" in script
    assert 'set_meta("validation_passed", true)' in script
