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


def test_party_layout_scrolls_detail_and_does_not_stretch_member_cards():
    source = _source("scripts/UI/Party/PartyPanel.cs")
    detail = source.split("private Control BuildDetailSection()", 1)[1].split(
        "private void RebuildFormationCards()", 1
    )[0]

    assert 'Name = "DetailScroll"' in detail
    assert "VerticalScrollMode = ScrollContainer.ScrollMode.Auto" in detail
    assert "HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled" in detail
    assert "frame.AddChild(scroll)" in detail
    assert "scroll.AddChild(column)" in detail
    assert "_formationRow.SizeFlagsVertical = SizeFlags.ShrinkCenter" in source
    assert source.count("card.SizeFlagsVertical = SizeFlags.ShrinkCenter") == 2
    assert source.count("SetBar(_detail") >= 6
    assert source.count(", 0, 0);") == 3
    assert "if (maximum <= 0f)" in source
    assert 'label.Text = "0/0"' in source


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

    assert "anchors_preset = 15" in scene
    assert "custom_minimum_size" not in scene
    for panel_path in (
        "res://scripts/UI/Party/PartyPanel.cs",
        "res://scripts/UI/Quests/QuestJournalPanel.cs",
        "res://scripts/UI/Skills/SkillTreePanel.cs",
        "res://scripts/UI/HUD/SettingsPanel.cs",
    ):
        assert panel_path in script
    assert "_validate_panel" in script
    assert "node.focus_mode == Control.FOCUS_ALL" in script
    assert 'set_meta("validation_passed", true)' in script


def test_party_and_skill_names_share_the_bounded_long_text_policy():
    party = _source("scripts/UI/Party/PartyPanel.cs")
    skills = _source("scripts/UI/Skills/SkillTreePanel.cs")
    showcase = _source("tools/validation/ui_screen_showcase.gd")

    assert 'ConfigureWrappedLabel(_detailNameLabel, "PartyDetailName")' in party
    assert 'ConfigureEllipsisLabel(_characterNameLabel, "SkillHeaderCharacterName"' in skills
    assert 'ConfigureWrappedLabel(_detailNameLabel, "SkillDetailName")' in skills
    assert '"party": [["PartyDetailName", true]]' in showcase
    assert '"skills": [["SkillHeaderCharacterName", false]]' in showcase
