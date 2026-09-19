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
    font_path = 'res://addons/dialogic/Example Assets/Fonts/Roboto-Regular.ttf'

    assert font_path in prompt_hud
    assert 'AddThemeFontOverride("font"' in prompt_hud
    assert font_path in textbox_layer
    assert 'add_theme_font_override(&"font"' in textbox_layer
    assert 'add_theme_font_override(&"normal_font"' in textbox_layer
