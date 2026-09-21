from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_hyou_command_context_menu_uses_separate_panel_and_button_assets():
    panel = ROOT / 'assets/graphics/ui/hud/companion_command_menu_panel.png'
    button = ROOT / 'assets/graphics/ui/hud/companion_command_menu_button.png'
    assert panel.exists(), 'Missing Hyou command menu panel frame asset'
    assert panel.stat().st_size > 0, 'Hyou command menu panel frame asset is empty'
    assert button.exists(), 'Missing Hyou command menu button frame asset'
    assert button.stat().st_size > 0, 'Hyou command menu button frame asset is empty'

    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'CommandMenuPanelPath = "res://assets/graphics/ui/hud/companion_command_menu_panel.png"' in script
    assert 'CommandMenuButtonPath = "res://assets/graphics/ui/hud/companion_command_menu_button.png"' in script
    assert 'StyleBoxTexture' in script
    assert 'PanelContainer _contextMenu' in script
    assert 'VBoxContainer _contextMenuItems' in script
    assert 'GetCombinedMinimumSize()' in script
    assert 'PopupMenu' not in script


def test_hyou_command_context_menu_keeps_runtime_commands():
    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'AddCommandButton("Theo sau", FollowCommandId' in script
    assert 'AddCommandButton("Đứng yên", StayCommandId' in script
    assert 'AddCommandButton("Bảo vệ", ProtectCommandId' in script
    assert 'AddCommandButton("Đi dạo", WanderCommandId' in script
    assert 'CreateCommandMenuButton' in script
