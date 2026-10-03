from pathlib import Path


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


def test_character_preserves_portrait_paths_and_manager_integration():
    source = _source(CHARACTER_PATH)

    assert "BackgroundImage" in source
    assert "config.Icon" in source
    assert "BodyScene" in source
    assert "EquipFromInventory" in source
    assert "UnequipToInventory" in source
    assert "SetActiveCharacter" in source


def test_showcase_covers_long_vietnamese_strings_at_1280x720():
    scene = _source(SHOWCASE_SCENE_PATH)
    script = _source(SHOWCASE_SCRIPT_PATH)

    assert "1280" in scene and "720" in scene
    assert "Vật phẩm tiêu hao" in script
    assert "Kiếm trường kiếm cổ đại có chuôi bọc da" in script
    assert "Nhân vật đồng hành có tên dài để kiểm tra" in script
    assert "tooltip_text" in script
    assert "autowrap_mode" in script
