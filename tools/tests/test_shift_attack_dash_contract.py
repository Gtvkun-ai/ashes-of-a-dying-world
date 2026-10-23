"""Kiểm tra cấu hình phím Shift tap/hold và điều kiện combat dash.

Đây là contract test kiểm tra wiring/source vì runner không có Godot/.NET.
Test gameplay động (animation, va chạm, thao tác phím thực tế) cần kiểm trong editor.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8")


def input_action_block(input_config: str, action: str) -> str:
    start = input_config.index(action + "={")
    end = input_config.index("\n}\n", start) + 2
    return input_config[start:end]


def test_space_cannot_open_focused_ui_button():
    config = source("project.godot")
    # Không dùng Space làm ui_accept hoặc ui_select; Dialogic vẫn có Space riêng.
    assert '"keycode":32' not in input_action_block(config, "ui_accept")
    assert '"physical_keycode":32' not in input_action_block(config, "ui_accept")
    assert '"keycode":32' not in input_action_block(config, "ui_select")
    assert '"keycode":32' in input_action_block(config, "dialogic_default_action")
    assert "\ndash={" not in config


def test_shift_tap_on_release_and_hold_run_not_space_dash():
    actor = source("scripts/Combat/Actors/CombatCharacter.cs")
    assert "Input.IsKeyPressed(Key.Shift)" in actor
    assert "else if (_shiftWasDown)" in actor
    assert "_shiftDownSeconds < Mathf.Max(0.05f, ShiftTapMaxSeconds)" in actor
    assert "return down && _shiftDownSeconds >= Mathf.Max(0.05f, ShiftTapMaxSeconds);" in actor
    assert "RequestAttackDash(direction);" in actor


def test_player_and_companion_share_shift_rules():
    for filename in ("scripts/Characters/Player/Player.cs", "scripts/Characters/Companion/NpcCharacter.cs"):
        actor = source(filename)
        assert "ReadShiftCombatInput(delta, inputDirection)" in actor
        assert 'IsActionJustPressed("dash")' not in actor
        assert "ResetShiftCombatInput();" in actor


def test_dash_is_queued_only_during_attack_and_waits_for_recovery():
    actor = source("scripts/Combat/Actors/CombatCharacter.cs")
    assert "Actions?.IsRunning == true && StateMachine?.IsAttackState == true" in actor
    assert "StateMachine.Current != CombatStateId.AttackRecovery" in actor
    assert "_attackDashQueued = true;" in actor
    assert "ResolveQueuedAttackDash();" in actor
    assert "TryStartDash(direction);" in actor
    assert "Actions.ActionFinished += OnCombatActionFinished;" in actor
    assert "if (completed && IsAlive)" in actor
    assert "(!canCancelRecovery && !justFinishedAttack)" in actor
