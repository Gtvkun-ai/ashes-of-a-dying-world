from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]


def _source(relative: str) -> str:
    return (REPO_ROOT / relative).read_text(encoding="utf-8")


def test_party_keeps_companion_commands_and_keyboard_focus():
    source = _source("scripts/UI/Party/PartyPanel.cs")

    assert "BuildCompanionCommandSection" in source
    for command in ("THEO SAU", "ĐỨNG YÊN", "BẢO VỆ", "ĐI DẠO"):
        assert command in source
    assert "FocusModeEnum.None" not in source
    assert "CreateTransparentButtonStyle" in source


def test_quest_uses_glyph_and_has_explicit_unavailable_map_state():
    source = _source("scripts/UI/Quests/QuestJournalPanel.cs")
    tracker = _source("scripts/UI/Quests/QuestTrackerHud.cs")

    assert "category_quest.png" not in source
    assert "UiGlyphResolver.Resolve" in source
    assert "UiGlyph.Quests" in source
    assert "UiGlyph.Quests" in tracker
    assert "FocusModeEnum.None" not in source
    assert "Bản đồ chưa khả dụng" in source
    assert "Disabled" in source


def test_skill_nodes_are_compact_wrapping_tooltip_controls():
    node = _source("scripts/UI/Skills/SkillTreeNodeView.cs")
    panel = _source("scripts/UI/Skills/SkillTreePanel.cs")

    assert "SetCornerRadiusAll(3)" in node
    assert "SetCornerRadiusAll(10)" not in node
    assert "AutowrapMode" in node
    assert "TooltipText" in node
    assert "FocusModeEnum.All" in node
    assert "FocusModeEnum.None" not in panel


def test_settings_copy_and_controls_are_player_facing_and_persist_motion():
    panel = _source("scripts/UI/HUD/SettingsPanel.cs")
    manager = _source("scripts/App/SettingsManager.cs")
    data = _source("scripts/Save/UserSettingsData.cs")
    motion = _source("scripts/UI/Theme/UiMotion.cs")

    for forbidden in ('"AUDIO"', '"DISPLAY"', '"CONTROLS"', '"COMFORT"', '"Apply"', "GPU", "renderer"):
        assert forbidden not in panel
    assert "StyleOptionButton" in panel
    assert "Giảm chuyển động" in panel
    assert "ReducedMotion" in data
    assert "SetReducedMotion" in manager
    assert "ReducedMotion" in manager
    assert "UiMotion.ResolveDuration" in panel
    assert re.search(r"ResolveDuration\s*\(\s*float normalSeconds\s*\)", motion)


def test_secondary_showcase_mounts_real_panels_and_native_controls():
    scene = _source("tools/validation/ui_screen_showcase.tscn")
    script = _source("tools/validation/ui_screen_showcase.gd")

    assert "1280" in scene and "720" in scene
    for panel_path in (
        "res://scripts/UI/Party/PartyPanel.cs",
        "res://scripts/UI/Quests/QuestJournalPanel.cs",
        "res://scripts/UI/Skills/SkillTreePanel.cs",
        "res://scripts/UI/HUD/SettingsPanel.cs",
    ):
        assert panel_path in script
    assert "_validate_secondary_focus" in script
    assert "ReducedMotion" in script
