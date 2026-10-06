from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_main_screen_replaces_placeholder_images_with_living_world_ui():
    scene = read("scenes/app/screen_main.tscn")
    source = read("scripts/App/ScreenMain.cs")
    assert "ui/menus/login/" not in scene
    assert "main_hud.png" not in scene
    assert "field_01.png" in scene
    assert "ColorRect" in scene
    assert "Bắt đầu" in scene
    assert 'hasSave ? "Tiếp tục" : "Bắt đầu"' in source
    assert 'text = "Trò chơi mới"' in scene


def test_main_screen_has_save_aware_actions_without_save_deletion():
    source = read("scripts/App/ScreenMain.cs")
    assert "RefreshMainMenuState(SaveGameData snapshot)" in source
    assert "StartNewGameAsync()" in source
    assert "StartGameFromSnapshotAsync(_currentSnapshot)" in source
    assert "StartGameFromSnapshotAsync(null)" in source
    assert "ConfirmationDialog" in source
    assert "Delete" not in source
    assert "Remove" not in source


def test_game_menu_has_six_live_features_and_no_dead_end_routes():
    scene = read("scenes/ui/menus/game_menu_button.tscn")
    source = read("scripts/UI/HUD/GameMenuButton.cs")
    assert "main_hud.png" not in scene
    assert "buttons/character.png" not in scene
    assert "buttons/inventory.png" not in scene
    for label in ("Nhân vật", "Kho đồ", "Kỹ năng", "Nhiệm vụ", "Đội", "Cài đặt"):
        assert label in scene
    assert "MapButton" not in scene
    assert "AchievementsButton" not in scene
    assert "UiGlyphResolver.Resolve" in source
    assert "UiGlyph.Menu" in source
    assert "UiGlyph.Character" in source
    assert "UiGlyph.Inventory" in source
    assert "UiGlyph.Skills" in source
    assert "UiGlyph.Quests" in source
    assert "UiGlyph.Party" in source
    assert "UiGlyph.Settings" in source
    assert "WorldHudLane" not in source


def test_menu_focus_and_settings_are_wired():
    scene = read("scenes/ui/menus/game_menu_button.tscn")
    source = read("scripts/UI/HUD/GameMenuButton.cs")
    assert "SettingsPanel" in scene
    assert "PixelButtonSkin.ApplySecondary" in source
    assert "FocusModeEnum.All" in source or "FocusMode" in source
    assert "Escape" in source
    assert "CharacterButton?.GrabFocus()" in source
    assert "MenuButton?.GrabFocus()" in source


def test_settings_panel_defers_runtime_manager_creation_until_scene_setup_finishes():
    source = read("scripts/UI/HUD/SettingsPanel.cs")
    assert "CallDeferred(nameof(InitializeSettings))" in source
    assert "private void InitializeSettings()" in source
    assert "_settings = SettingsManager.GetOrCreate(GetTree());" in source


def test_quest_journal_defers_runtime_manager_creation_until_scene_setup_finishes():
    source = read("scripts/UI/Quests/QuestJournalPanel.cs")
    assert "CallDeferred(nameof(InitializeQuestManager))" in source
    assert "private void InitializeQuestManager()" in source
    assert "_questManager = QuestManager.GetOrCreate(GetTree());" in source


def test_save_manager_loads_through_the_responsive_main_screen_controller():
    scene = read("scenes/app/screen_main.tscn")
    save_manager = read("scripts/Save/SaveManager.cs")
    assert '[node name="login" type="Button"' in scene
    assert "currentScene as ScreenMain" in save_manager
    assert "StartGameFromSnapshotAsync(snapshot)" in save_manager
    assert 'GetNodeOrNull<Button>("login")' not in save_manager


def test_visual_showcase_runs_actual_main_screen_and_game_menu():
    source = read("tools/validation/ui_visual_showcase.gd")

    assert 'preload("res://scenes/app/screen_main.tscn")' in source
    assert 'preload("res://scenes/ui/menus/game_menu_button.tscn")' in source
    assert 'const SUPPORTED_MODES := ["overview", "panels", "main", "menu"]' in source
    assert "_build_main_menu_sample" in source
    assert 'showcase_title.visible = _showcase_mode == "overview"' in source
    assert 'get_node_or_null("MainUi")' in source
    assert 'get_node_or_null("Control/MenuGridPanel")' in source
    for node_name in (
        "CharacterButton",
        "InventoryButton",
        "SkillsButton",
        "QuestsButton",
        "PartyButton",
        "SettingsButton",
    ):
        assert node_name in source
    assert "_control_fits_viewport" in source
