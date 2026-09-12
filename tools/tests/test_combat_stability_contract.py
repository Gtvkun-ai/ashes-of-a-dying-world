from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_reaction_interrupt_policy_contract():
    policy_path = ROOT / 'scripts/Combat/Runtime/CombatReactionPolicy.cs'
    assert policy_path.exists(), 'Thiếu CombatReactionPolicy.cs'
    src = policy_path.read_text(encoding='utf-8')

    # Contract: Stagger+ luôn interrupt; Knockback chỉ interrupt action thường;
    # Flinch/Shove không được cancel một action đang chạy.
    assert '(int)reaction >= (int)ImpactReactionType.Stagger' in src
    assert 'reaction == ImpactReactionType.Knockback' in src
    assert '!actionUninterruptible' in src
    assert 'return false;' in src

    actor = read('scripts/Combat/Actors/CombatCharacter.cs')
    assert 'CombatReactionPolicy.ShouldInterruptActiveAction' in actor
    assert 'Actions?.Cancel();\n                StateMachine.EnterHitstun(result.HitstunSeconds);' not in actor


def test_slime_damage_does_not_force_target_swap_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')

    # Contract: damage challenger có hysteresis + burst window; không còn SetTarget(attacker)
    # vô điều kiện như v10.
    assert 'ThreatBurstDamageThreshold' in brain
    # Slime basic có 200 HP; một Ice Bolt Hyou đầu game xấp xỉ 30 damage không được cướp aggro một phát.
    assert 'ThreatBurstDamageThreshold { get; set; } = 55f;' in brain
    assert 'ThreatBurstWindowSeconds' in brain
    assert 'TargetCommitSeconds' in brain
    assert '_threatChallengerDamage' in brain
    assert 'challengerClearlyCloser' in brain
    assert 'burstThreat' in brain
    assert 'if (attacker == _target)' in brain
    assert 'SetTarget(attacker, hpDamage > 0f ? "damaged" : "provoked");' not in brain


def test_zero_damage_challenger_cannot_steal_current_target_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    # Một status/field 0 HP damage có thể acquire khi slime chưa có target,
    # nhưng không được cướp target đang hợp lệ.
    assert 'hpDamage <= 0f' in brain
    assert 'zero_damage_ignored' in brain


if __name__ == '__main__':
    tests = [
        test_reaction_interrupt_policy_contract,
        test_slime_damage_does_not_force_target_swap_contract,
        test_zero_damage_challenger_cannot_steal_current_target_contract,
    ]
    failures = []
    for test in tests:
        try:
            test()
            print(f'PASS {test.__name__}')
        except Exception as exc:
            failures.append((test.__name__, exc))
            print(f'FAIL {test.__name__}: {exc}')
    if failures:
        raise SystemExit(1)
