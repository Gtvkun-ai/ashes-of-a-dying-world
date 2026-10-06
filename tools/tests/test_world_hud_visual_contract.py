from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_world_hud_lanes_are_ordered_and_dimension_aware():
    source = read("scripts/UI/HUD/WorldHudLayout.cs")
    assert re.search(r"enum\s+WorldHudLane\s*\{\s*Target\s*,\s*Health\s*,\s*Feedback\s*,\s*Progression\s*\}", source, re.S)
    assert "Resolve(Node2D actor, WorldHudLane lane, Vector2 widgetSize" in source
    assert "widgetSize.Y" in source
    assert "LaneSpacing" in source
    assert "ReservedBandHeight" in source
    assert "GetFeedbackTravelLimit" in source


def test_world_hud_services_use_the_shared_layout():
    services = (
        ("scripts/UI/HUD/CompanionTargetIndicatorService.cs", "WorldHudLane.Target"),
        ("scripts/UI/HUD/EnemyHealthBarService.cs", "WorldHudLane.Health"),
        ("scripts/UI/HUD/DamageNumberService.cs", "WorldHudLane.Feedback"),
        ("scripts/UI/HUD/FloatingProgressionHudService.cs", "WorldHudLane.Progression"),
    )
    for path, lane in services:
        source = read(path)
        assert "WorldHudLayout.Resolve" in source
        assert lane in source


def test_enemy_health_uses_a_styled_control_bar_without_legacy_bitmap():
    source = read("scripts/UI/HUD/EnemyHealthBarService.cs")
    assert "enemy_hp_bar.png" not in source
    assert "TextureProgressBar" not in source
    assert "ProgressBar" in source
    assert "StyleBoxFlat" in source
    assert 'AddThemeStyleboxOverride("background"' in source
    assert 'AddThemeStyleboxOverride("fill"' in source
    assert "frame" in source.lower()


def test_target_and_combat_feedback_use_shared_glyphs_and_vietnamese_copy():
    target = read("scripts/UI/HUD/CompanionTargetIndicatorService.cs")
    damage = read("scripts/UI/HUD/DamageNumberService.cs")
    assert "UiGlyph.Target" in target
    assert "UiGlyphResolver.Resolve" in target
    assert "\u25bc" not in target
    assert "SHATTER" not in damage
    assert "VỠ ĐÁ" in damage


def test_world_hud_showcase_exercises_all_lanes_at_two_viewports():
    scene = read("tools/validation/ui_world_hud_showcase.tscn")
    script = read("tools/validation/WorldHudRuntimeValidation.cs")
    assert "WorldHudRuntimeValidation.cs" in scene
    assert "1600" in script and "900" in script
    assert "1280" in script and "720" in script
    for service in (
        "EnemyHealthBarService",
        "CompanionTargetIndicatorService",
        "DamageNumberService",
        "FloatingProgressionHudService",
    ):
        assert service in script
    assert "shattered: false" in script
    assert "shattered: true" in script
    assert "AssertNoOverlap" in script
    assert "Lifetime" in script
