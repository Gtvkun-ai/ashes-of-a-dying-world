from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
runner = (ROOT / 'src/Combat/Runtime/CombatActionRunner.cs').read_text(encoding='utf-8')
visual = (ROOT / 'src/Combat/Visuals/HyouCastVisual.cs').read_text(encoding='utf-8')
ai = (ROOT / 'src/Combat/AI/HyouAI.cs').read_text(encoding='utf-8')
action = (ROOT / 'assets/resources/data/combat/actions/hyou_ice_bolt.tres').read_text(encoding='utf-8')
scene = (ROOT / 'assets/resources/data/characters/Hyou.tscn').read_text(encoding='utf-8')

checks = {
    'runner has initialization guard': 'private bool _initializingFrameAnimation;' in runner,
    'frame callback respects guard': 'if (_initializingFrameAnimation)' in runner,
    'animation is initialized before ActionStarted': runner.index('_initializingFrameAnimation = true;') < runner.index('ActionStarted?.Invoke(_currentAction, _actionFacing);'),
    'cast animation forced non-loop': 'SetAnimationLoop(animationName, false)' in runner,
    'legacy AI no longer calls StopCast': '_castVisual?.StopCast();' not in ai,
    'diagnostic build marker exists': 'v3-init-order' in visual or 'v8-action-events-debug-spine' in (ROOT / 'src/Combat/Decision/Runtime/CombatDecisionAgent.cs').read_text(encoding='utf-8'),
    'ice bolt releases at final frame': 'ActiveStartFrame = 7' in action and 'ActiveEndFrame = 7' in action and 'EndFrame = 7' in action,
    'cast ignores attack-speed scaling': 'ScalePlaybackWithAttackSpeed = false' in action,
    '2-second fallback exists': 'StartupSeconds = 2.0' in action,
    'visual duration is 2 seconds': 'CastDurationSeconds = 2.0' in scene,
}

failed = [name for name, ok in checks.items() if not ok]
for name, ok in checks.items():
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if failed:
    raise SystemExit('Validation failed: ' + ', '.join(failed))
print('[PASS] Hyou cast runtime v3 cumulative patch is structurally consistent.')
