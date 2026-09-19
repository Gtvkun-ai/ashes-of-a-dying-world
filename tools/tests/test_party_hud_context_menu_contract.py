from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_hyou_command_context_menu_uses_project_pixel_frame():
    asset = ROOT / 'assets/graphics/ui/hud/companion_command_menu_frame.png'
    assert asset.exists(), 'Missing Hyou command menu frame asset'
    assert asset.stat().st_size > 0, 'Hyou command menu frame asset is empty'

    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'CommandMenuFramePath = "res://assets/graphics/ui/hud/companion_command_menu_frame.png"' in script
    assert 'ApplyCommandMenuTheme();' in script
    assert 'StyleBoxTexture' in script
    assert 'AddThemeStyleboxOverride("panel"' in script


def test_hyou_command_context_menu_keeps_runtime_commands():
    script = read('scripts/UI/HUD/PartyHUDManager.cs')
    assert 'AddCommandItem("Theo sau", FollowCommandId' in script
    assert 'AddCommandItem("Đứng yên", StayCommandId' in script
    assert 'AddCommandItem("Bảo vệ", ProtectCommandId' in script
    assert 'AddCommandItem("Đi dạo", WanderCommandId' in script
    assert 'AddRadioCheckItem(label, id)' in script
