from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
UID_RE = re.compile(r'uid://([^\s"\]]+)')
VALID_UID_BODY_RE = re.compile(r'^[a-z0-9]+$')


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_registered_dialogic_timelines_are_non_empty():
    project = read('project.godot')
    timeline_paths = re.findall(r'"([^"]+\.dtl)"', project)
    assert timeline_paths, 'Dialogic should register at least one timeline'

    empty_timelines = [
        path for path in timeline_paths
        if path.startswith('res://')
        and (ROOT / path.removeprefix('res://')).exists()
        and (ROOT / path.removeprefix('res://')).stat().st_size == 0
    ]

    assert empty_timelines == []


def test_dialogic_custom_resources_use_valid_uid_text():
    files = [
        'data/dialogic_characters/hyou.dch',
        'scenes/ui/dialog/jrpg_style.tres',
        'scenes/ui/dialog/jrpg_textbox.tscn',
        'scenes/ui/dialog/jrpg_main_panel.tres',
        'scenes/ui/dialog/jrpg_name_panel.tres',
    ]

    invalid = []
    for rel in files:
        for uid_body in UID_RE.findall(read(rel)):
            if not VALID_UID_BODY_RE.fullmatch(uid_body):
                invalid.append(f'{rel}: uid://{uid_body}')

    assert invalid == []


def test_dialogic_runtime_text_uses_unicode_capable_font():
    prompt_hud = read('scripts/World/Interaction/InteractionPromptHud.cs')
    textbox_layer = read('scenes/ui/dialog/jrpg_textbox_layer.gd')
    regular = 'res://assets/fonts/BeVietnamPro-Regular.ttf'
    semibold = 'res://assets/fonts/BeVietnamPro-SemiBold.ttf'
    italic = 'res://assets/fonts/BeVietnamPro-Italic.ttf'
    semibold_italic = 'res://assets/fonts/BeVietnamPro-SemiBoldItalic.ttf'

    assert regular in prompt_hud
    assert 'AddThemeFontOverride("font"' in prompt_hud
    for font_path in (regular, semibold, italic, semibold_italic):
        assert font_path in textbox_layer
    assert 'add_theme_font_override(&"font"' in textbox_layer
    assert 'add_theme_font_override(&"normal_font", regular_font)' in textbox_layer
    assert 'add_theme_font_override(&"bold_font", semibold_font)' in textbox_layer
    assert 'add_theme_font_override(&"italics_font", italic_font)' in textbox_layer
    assert 'add_theme_font_override(&"bold_italics_font", semibold_italic_font)' in textbox_layer


def test_dialogic_choices_wrap_scroll_and_expand_past_legacy_width():
    layer = read('scenes/ui/dialog/jrpg_choice_layer.gd')
    scene = read('scenes/ui/dialog/jrpg_choice_layer.tscn')
    style = read('scenes/ui/dialog/jrpg_style.tres')
    smoke = read('tools/validation/_dialogic_layout_smoke_test.gd')

    assert 'centered_max_width: float = 520.0' in layer
    assert 'choices_max_height: float = 360.0' in layer
    assert 'right_reserved_min: float = 300.0' in layer
    assert 'right_reserved_max: float = 330.0' in layer
    assert 'hud_safe_right' in layer
    assert 'TextServer.AUTOWRAP_WORD_SMART' in layer
    assert 'TextServer.OVERRUN_NO_TRIMMING' in layer
    assert 'clip_text = false' in layer
    assert '%ChoiceScroll' in layer
    assert '[node name="ChoiceScroll" type="ScrollContainer" parent="."]' in scene
    assert '[node name="Choices" type="VBoxContainer" parent="ChoiceScroll"]' in scene
    assert 'vertical_scroll_mode = 1' in scene
    assert 'size_flags_vertical = 3' in scene
    assert 'alignment = 2' in scene
    assert 'BoxContainer.ALIGNMENT_END' in layer
    assert '"centered_max_width": "520.0"' in style
    assert '"choices_max_height": "360.0"' in style
    assert '"right_reserved_min": "300.0"' in style
    assert '"right_reserved_max": "330.0"' in style
    assert 'Sáu lựa chọn tiếng Việt rất dài' in smoke
    assert 'Vector2i(1280, 720)' in smoke
    assert 'Vector2i(1600, 900)' in smoke
    assert 'last_button.grab_focus()' in smoke
    assert 'choice_scroll.scroll_vertical > 0' in smoke
    assert 'Single choice is not aligned near the textbox' in smoke


def test_dialogic_focus_style_is_structurally_distinct_from_hover():
    hover = read('scenes/ui/dialog/jrpg_choice_hover.tres')
    focus = read('scenes/ui/dialog/jrpg_choice_focus.tres')

    assert focus != hover
    assert 'border_width_top = 1' in focus
    assert 'border_width_right = 1' in focus
    assert 'border_width_left = 2' in focus
    assert 'border_width_left = 3' not in focus
