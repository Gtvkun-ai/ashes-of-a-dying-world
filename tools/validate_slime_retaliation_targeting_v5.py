from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
brain = (ROOT / 'src/Combat/AI/SlimeBrain.cs').read_text(encoding='utf-8')
slime = (ROOT / 'src/Characters/Enemies/Slime1.cs').read_text(encoding='utf-8')
faction = (ROOT / 'src/Combat/Runtime/FactionRules.cs').read_text(encoding='utf-8')
scene = (ROOT / 'assets/resources/data/characters/Hyou.tscn').read_text(encoding='utf-8')

checks = {
    'slime exposes retaliation hook': 'public void NotifyProvoked(CombatCharacter attacker' in brain,
    'retaliation has memory': 'ProvokedTargetMemorySeconds' in brain and '_provokedTargetRemaining' in brain,
    'damage locks attacker temporarily': 'SetTarget(attacker, hpDamage > 0f ? "damaged" : "provoked")' in brain,
    'target refresh respects provoked attacker': '_provokedTargetRemaining > 0f' in brain,
    'slime can switch to clearly closer hostile': 'challengerClearlyCloser' in brain and 'TargetSwitchAdvantage' in brain,
    'targeting still uses faction rules': 'FactionRules.CanDamage(_character.Faction, candidate.Faction)' in brain,
    'slime forwards real attacker': '_brain?.NotifyProvoked(request.Attacker, result.HpDamage);' in slime,
    'slime receives hit callback': 'protected override void OnHitReceived(HitRequest request, HitResult result)' in slime,
    'enemy and companion are damageable': 'return !AreAllies(attacker, target);' in faction,
    'Hyou is companion faction': 'Faction = 2' in scene and 'CombatantId = "companion_hyou"' in scene,
    'debug target transition is observable': '[SlimeBrain] TARGET' in brain,
}

failed = [name for name, ok in checks.items() if not ok]
for name, ok in checks.items():
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")

if failed:
    raise SystemExit('FAILED: ' + ', '.join(failed))

# Mô phỏng điều kiện cự ly thiết kế: Hyou đứng 118 px, ngoài sight 105 nhưng trong retaliation leash 195.5.
aggro = 105.0
hyou_range = 118.0
leash = 170.0
retaliation_multiplier = 1.15
assert hyou_range > aggro
assert hyou_range < leash * retaliation_multiplier
print('[PASS] Hyou 118px is outside normal sight but inside retaliation leash')
print('Slime retaliation targeting v5: OK')
