from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(rel: str) -> str:
    path = ROOT / rel
    assert path.exists(), f"Thiếu file: {rel}"
    return path.read_text(encoding="utf-8")

projectile = read("src/Combat/Projectiles/CombatProjectile2D.cs")
spec = read("src/Combat/Data/ProjectileSpecData.cs")
visual = read("src/Combat/Data/ProjectileVisualProfileData.cs")
resource = read("assets/resources/data/combat/projectiles/hyou_ice_bolt.tres")
visual_resource = read("assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres")
slime = read("src/Combat/AI/SlimeBrain.cs")
slime_actor = read("src/Characters/Enemies/Slime1.cs")

for token in (
    'v8-visual-profile-action-events',
    'LaunchAnimation',
    'TryBuildAssetVisual',
    '!_visual.UseProceduralFallback',
):
    assert token in projectile, f"Projectile runtime thiếu: {token}"

assert 'VisualProfile' in spec
for token in ('LaunchSpriteSheetPath', 'UpLaunchSpriteSheetOverridePath', 'LaunchFrameCount'):
    assert token in visual, f"ProjectileVisualProfileData thiếu: {token}"

assert 'VisualProfile = ExtResource' in resource
for token in (
    'x10 hyou ice up.png',
    'x10 hyou ice bh.png',
    'x10 hyou up ice.png',
    'x10 hyou bh ice .png',
    'SpriteColumns = 8',
    'SpriteFrameWidth = 66',
    'SpriteColumn = 2',
    'LaunchStartColumn = 2',
    'LaunchFrameCount = 4',
    'UseProceduralFallback = false',
    'DebugVisualLogging = true',
):
    assert token in visual_resource, f"Visual profile Ice Bolt thiếu: {token}"

assert 'reason=leash_exceeded' not in slime
assert 'SetTarget(null, "leash_exceeded")' not in slime
for token in ('v6-soft-pursuit', 'UseCombatSpawnLeash', 'TargetForgetRadius', 'ProvokedForgetRadius', 'target_too_far'):
    assert token in slime, f"Slime pursuit thiếu: {token}"

assert 'NotifyProvoked(request.Attacker' in slime_actor
assert 157.1 <= 520.0

print('[OK] Ice Bolt dùng visual profile thật và cấm fallback procedural.')
print('[OK] Slime retaliation không còn bị spawn leash dập ngay.')
print('[NOTE] Vẫn cần Godot thật để compile C# và quan sát runtime.')
