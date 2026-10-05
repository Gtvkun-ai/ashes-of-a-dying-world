from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_character_unit_hud_uses_native_visuals_and_internal_status_strip():
    scene = read("scenes/ui/hud/character_unit_hud.tscn")

    for legacy_asset in (
        "unit_hud_frame.png",
        "unit_hud_portrait_frame.png",
        "unit_hud_hp_fill.png",
        "unit_hud_mp_fill.png",
        "unit_hud_stamina_fill.png",
    ):
        assert legacy_asset not in scene

    assert '[node name="StatusFrame" type="PanelContainer"' in scene
    assert '[node name="StatusStrip" type="HBoxContainer"' in scene
    assert 'custom_minimum_size = Vector2(60, 28)' in scene
    assert "custom_minimum_size = Vector2(300, 97)" in scene
    assert 'HealthBar = NodePath("' in scene
    assert 'ManaBar = NodePath("' in scene
    assert 'StaminaBar = NodePath("' in scene
    assert 'NameLabel = NodePath("' in scene
    assert 'Portrait = NodePath("' in scene
    assert 'LevelLabel = NodePath("' in scene

    for semantic_label in ("HealthLabel", "ManaLabel", "StaminaLabel", "LevelLabel"):
        assert f'[node name="{semantic_label}"' in scene

    for label, text, tooltip in (
        ("HealthLabel", "HP", "Sinh lực"),
        ("ManaLabel", "MP", "Năng lượng"),
        ("StaminaLabel", "STA", "Thể lực"),
    ):
        block = scene.split(f'[node name="{label}"', 1)[1].split("\n\n", 1)[0]
        assert f'text = "{text}"' in block
        assert f'tooltip_text = "{tooltip}"' in block


def test_character_unit_hud_layout_budget_fits_97_pixels_without_state_growth():
    scene = read("scenes/ui/hud/character_unit_hud.tscn")

    assert 'custom_minimum_size = Vector2(60, 60)' in scene
    assert 'custom_minimum_size = Vector2(60, 28)' in scene
    assert 'theme_override_constants/separation = 1' in scene
    assert 'theme_override_constants/margin_top = 4' in scene
    assert 'theme_override_constants/margin_bottom = 4' in scene
    status_block = scene.split('[node name="StatusFrame"', 1)[1].split(
        '[node name="StatsColumn"', 1
    )[0]
    assert 'custom_minimum_size = Vector2(0, 24)' not in status_block

    party_scene = read("scenes/ui/hud/party_hud.tscn")
    assert 'anchor_left = 1.0' in party_scene
    assert 'anchor_right = 1.0' in party_scene
    assert 'theme_override_constants/separation = 4' in party_scene


def test_character_unit_hud_status_holders_are_tooltip_targets_and_selection_is_thin():
    script = read("scripts/UI/HUD/CharacterUnitHUD.cs")

    assert "StatusStrip" in script
    assert "TooltipText" in script
    assert "MouseFilterEnum.Stop" in script
    assert "MouseFilterEnum.Ignore" in script
    assert "UiTokens.SelectionBorderWidth" in script
    assert "selected ? UiTokens.SelectionBorderWidth : UiTokens.BorderWidth" in script
    assert "isSelected ? 20.0f" not in script
    assert "ContextHitTarget" not in script
    assert "public override void _GuiInput(InputEvent inputEvent)" in script
    assert "NameLabel.MouseFilter = Control.MouseFilterEnum.Pass" in script
    assert "MouseFilter = Control.MouseFilterEnum.Pass" in script
    assert "ContentMarginTop = 0" in script
    assert "ContentMarginBottom = 0" in script


def test_character_unit_hud_uses_anchor_offsets_for_inset_badges():
    script = read("scripts/UI/HUD/CharacterUnitHUD.cs")

    assert "SetFullRectInsets" in script
    assert "background.Position =" not in script
    assert "background.Size =" not in script
    assert "icon.Position =" not in script
    assert "icon.Size =" not in script
    assert "clipRoot.Position =" not in script
    assert "clipRoot.Size =" not in script


def test_character_unit_hud_input_bubbles_from_children_and_supports_gamepad_accept():
    scene = read("scenes/ui/hud/character_unit_hud.tscn")
    script = read("scripts/UI/HUD/CharacterUnitHUD.cs")

    for node_name in (
        "Content",
        "Columns",
        "PortraitColumn",
        "PortraitFrame",
        "StatusFrame",
        "StatsColumn",
        "HeaderRow",
        "ResourceRows",
        "HealthRow",
        "ManaRow",
        "StaminaRow",
    ):
        block = scene.split(f'[node name="{node_name}"', 1)[1].split("\n\n", 1)[0]
        assert "mouse_filter = 1" in block, f"{node_name} must pass input to CharacterUnitHUD"

    assert 'inputEvent.IsActionPressed("ui_accept")' in script
    assert "InputEventJoypadButton" not in script


def test_character_unit_hud_has_level_and_color_independent_resource_semantics():
    script = read("scripts/UI/HUD/CharacterUnitHUD.cs")

    assert "[Export] public Label LevelLabel" in script
    assert 'LevelLabel.Text = $"Cấp {_targetStats.CurrentLevel:00}"' in script
    assert 'ConfigureResourceBar(StaminaBar, UiTokens.Accent)' in script
    assert 'SetResourceValue(StaminaBar, _targetStats.CurrentStamina, _targetStats.MaxStamina, UiTokens.Accent)' in script
    assert 'ConfigureResourceBar(StaminaBar, UiTokens.Ice)' not in script

    for vietnamese_status in ("Nhiễm lạnh", "Chậm", "Đóng băng"):
        assert f'"{vietnamese_status}"' in script

    for english_copy in ('"UNKNOWN"', '"Chill"', '"Slow"', '"Frozen"'):
        assert english_copy not in script


def test_party_hud_manager_keeps_commands_and_uses_shared_theme_glyph_apis():
    script = read("scripts/UI/HUD/PartyHUDManager.cs")

    for legacy_asset in (
        "companion_command_menu_panel.png",
        "companion_command_menu_button.png",
        "CommandMenuPanelPath",
        "CommandMenuButtonPath",
    ):
        assert legacy_asset not in script

    for api in ("UiThemeFactory", "UiTokens", "PixelButtonSkin", "UiGlyphResolver"):
        assert api in script

    for command in ("Theo sau", "Đứng yên", "Bảo vệ", "Đi dạo"):
        assert command in script

    assert "UiThemeFactory.Apply(this)" not in script
    assert "UiThemeFactory.Apply(container)" in script
    assert "UiThemeFactory.Apply(_contextMenu)" in script
    assert "FocusMode = Control.FocusModeEnum.All" in script
    assert ".GrabFocus()" in script
    assert '"ĐANG CHỌN"' in script
    assert '"MỆNH LỆNH"' in script
    assert '"ACTIVE"' not in script
    assert '"COMMAND"' not in script
    assert 'inputEvent.IsActionPressed("ui_cancel")' in script
    assert "_contextSourceHud" in script
    assert "_contextSourceHud?.GrabFocus()" in script


def test_party_hud_showcase_covers_three_members_and_runtime_layout_checks():
    scene_path = ROOT / "tools/validation/ui_party_hud_showcase.tscn"
    script_path = ROOT / "tools/validation/ui_party_hud_showcase.gd"
    assert scene_path.exists()
    assert script_path.exists()

    scene = scene_path.read_text(encoding="utf-8")
    script = script_path.read_text(encoding="utf-8")
    assert "ui_party_hud_showcase.gd" in scene
    assert "Nguyễn Ánh Dương" in script
    assert "Hyou" in script
    assert "Nhiễm lạnh" in script
    assert "Đóng băng" in script
    assert "get_combined_minimum_size" in script
    assert "EXPECTED_UNIT_WIDTH := 300.0" in script
    assert "EXPECTED_UNIT_HEIGHT := 97.0" in script
    assert "EXPECTED_VIEWPORTS" in script
    assert "ActiveSkillStrip" in script
    assert "StatusStrip" in script
    assert "assert_no_overlap" in script
    assert "assert_focus_round_trip" in script
    assert "Party HUD showcase validation passed" in script
    assert len(re.findall(r"PARTY_HUD_SCENE\.instantiate\(\)", script)) == 1
