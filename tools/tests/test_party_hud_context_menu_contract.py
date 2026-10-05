from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_hyou_command_context_menu_uses_shared_code_native_visuals():
    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'companion_command_menu_panel' not in script
    assert 'companion_command_menu_button' not in script
    assert 'StyleBoxTexture' not in script
    assert 'UiThemeFactory' in script
    assert 'UiTokens' in script
    assert 'PixelButtonSkin' in script
    assert 'UiGlyphResolver' in script
    assert 'PanelContainer _contextMenu' in script
    assert 'VBoxContainer _contextMenuItems' in script
    assert 'GetCombinedMinimumSize()' in script
    assert 'PopupMenu' not in script


def test_hyou_command_context_menu_keeps_runtime_commands():
    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'AddCommandButton("Theo sau", FollowCommandId, companion.CommandMode == CompanionCommandMode.Follow, UiGlyph.Target)' in script
    assert 'AddCommandButton("Đứng yên", StayCommandId, companion.CommandMode == CompanionCommandMode.Stay, UiGlyph.Party)' in script
    assert 'AddCommandButton("Bảo vệ", ProtectCommandId, companion.CommandMode == CompanionCommandMode.Protect, UiGlyph.Defense)' in script
    assert 'AddCommandButton("Đi dạo", WanderCommandId, companion.CommandMode == CompanionCommandMode.Wander, UiGlyph.CategoryMap)' in script
    assert 'CreateCommandMenuButton' in script


def test_hyou_command_context_menu_round_trips_keyboard_and_gamepad_focus():
    script = read('scripts/UI/HUD/PartyHUDManager.cs')

    assert 'FocusFirstEnabledCommand()' in script
    assert 'button.GrabFocus()' in script
    assert '_contextSourceHud = sourceHud' in script
    assert '_contextSourceHud?.GrabFocus()' in script
    assert 'inputEvent.IsActionPressed("ui_cancel")' in script
