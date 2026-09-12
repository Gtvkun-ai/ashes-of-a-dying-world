from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def test_spring_charge_curve_and_soft_slow_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    assert 'SpringMinChargeSeconds' in brain
    assert 'SpringMaxChargeSeconds' in brain
    assert 'ComputeRequiredCharge01' in brain
    assert 'charge01 * charge01' in brain
    # Charge tích theo delta thật; Slow chỉ tác động utility target, không nhân timer charge.
    assert '_springChargeElapsed += dt;' in brain
    assert '_springChargeElapsed += dt * (_character.Statuses?.MoveSpeedMultiplier' not in brain


def test_spring_hard_cc_interrupt_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    slime = read('scripts/Characters/Enemies/Slime1.cs')
    assert 'Statuses?.IsFrozen == true' in brain
    assert '(int)reaction >= (int)ImpactReactionType.Stagger' in brain
    assert 'NotifyCombatReaction' in brain
    assert 'NotifyCombatReaction(result.Reaction)' in slime


def test_spring_runtime_motion_multiplier_contract():
    runner = read('scripts/Combat/Runtime/CombatActionRunner.cs')
    assert '_currentMotionMultiplier' in runner
    assert '* _currentMotionMultiplier' in runner
    assert 'float motionMultiplier' in runner
    assert '_currentMotionMultiplier = 1f;' in runner


def test_slow_aware_utility_targeting_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    assert 'EvaluateTargetScore' in brain
    assert 'SlowTargetProximityMultiplier' in brain
    assert '_character.Statuses?.IsSlowed == true' in brain
    assert 'SpringTargetCommitFraction' in brain
    assert 'CanRetargetDuringSpringCharge' in brain



def test_charge_scales_impact_without_scaling_damage_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    runner = read('scripts/Combat/Runtime/CombatActionRunner.cs')
    request = read('scripts/Combat/Model/HitRequest.cs')
    resolver = read('scripts/Combat/Runtime/ImpactReactionResolver.cs')
    assert 'SpringMinImpactMultiplier' in brain
    assert 'ComputeSpringImpactMultiplier' in brain
    assert '_currentImpactMultiplier' in runner
    assert 'ImpactMultiplier' in request
    assert 'request.ImpactMultiplier' in resolver



def test_short_charge_is_mobility_and_deep_charge_is_pounce_contract():
    brain = read('scripts/Combat/AI/SlimeBrain.cs')
    assert 'SpringJumpAction' in brain
    assert 'SpringPounceChargeThreshold' in brain
    assert 'charge01 >= Mathf.Clamp(SpringPounceChargeThreshold' in brain
    mobility_path = ROOT / 'data/combat/actions/slime_spring_jump.tres'
    assert mobility_path.exists()
    mobility = mobility_path.read_text(encoding='utf-8')
    assert 'ActionId = "slime_spring_jump"' in mobility
    assert 'DeliveryMode = 2' in mobility
    assert 'HitProfile' not in mobility


def test_pounce_resource_releases_after_external_charge_contract():
    action = read('data/combat/actions/slime_pounce.tres')
    assert 'StartFrame = 1' in action
    assert 'slime_pounce_charge' not in action
    assert 'slime_pounce_land' in action


if __name__ == '__main__':
    tests = [
        test_spring_charge_curve_and_soft_slow_contract,
        test_spring_hard_cc_interrupt_contract,
        test_spring_runtime_motion_multiplier_contract,
        test_slow_aware_utility_targeting_contract,
        test_charge_scales_impact_without_scaling_damage_contract,
        test_short_charge_is_mobility_and_deep_charge_is_pounce_contract,
        test_pounce_resource_releases_after_external_charge_contract,
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
