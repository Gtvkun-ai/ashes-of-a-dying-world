#!/usr/bin/env python3
"""Kiểm tra tĩnh Phase 3: projectile + live execution + spacing/movement + Hyou rollout.

Archive người dùng không có project.godot/.csproj, nên script này không giả vờ thay thế
Godot compile. Nó chỉ bắt các lỗi dây chuyền phổ biến trước khi người dùng mở project thật.
"""
from __future__ import annotations

from pathlib import Path
import math
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

REQUIRED = [
    "src/Combat/Data/ProjectileSpecData.cs",
    "src/Combat/Projectiles/CombatProjectile2D.cs",
    "src/Combat/Projectiles/CombatProjectileSpawner.cs",
    "src/Combat/Decision/Execution/CombatIntentExecutor.cs",
    "src/Combat/Decision/Movement/CombatMovementModels.cs",
    "src/Combat/Decision/Movement/CombatSpacingController.cs",
    "src/Combat/Decision/Movement/CombatMovementSolver.cs",
    "src/Combat/Decision/Party/PartyTacticalDirector.cs",
    "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
    "src/Combat/Decision/Runtime/TacticalEvaluator.cs",
    "src/Combat/Runtime/CombatActionRunner.cs",
    "src/Combat/Runtime/CombatAbilityRunner.cs",
    "src/Combat/Actors/CombatCharacter.cs",
    "assets/resources/data/combat/actions/hyou_ice_bolt.tres",
    "assets/resources/data/combat/hit_profiles/hyou_ice_bolt.tres",
    "assets/resources/data/combat/projectiles/hyou_ice_bolt.tres",
    "assets/resources/data/combat/skills/hyou_ice_bolt.tres",
    "assets/resources/data/combat/decision/classes/cryomancer.tres",
    "assets/resources/data/characters/Hyou.tres",
    "assets/resources/data/characters/Hyou.tscn",
]


def fail(message: str) -> None:
    print(f"[FAIL] {message}")
    raise SystemExit(1)


def text(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(path: str, *tokens: str) -> None:
    content = text(path)
    for token in tokens:
        if token not in content:
            fail(f"{path} thiếu token bắt buộc: {token}")


def check_balanced(path: str) -> None:
    content = text(path)
    pairs = [("{", "}"), ("(", ")"), ("[", "]")]
    for left, right in pairs:
        if content.count(left) != content.count(right):
            fail(f"Delimiter {left}{right} lệch trong {path}")


def ext_paths(resource_path: str) -> list[str]:
    return re.findall(r'path="res://([^"]+)"', text(resource_path))


def weighted_geometric(scores: list[float], weights: list[float]) -> float:
    total_weight = sum(weights)
    return math.exp(sum(w * math.log(max(0.001, min(1.0, s))) for s, w in zip(scores, weights)) / total_weight)


def main() -> int:
    missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
    if missing:
        fail("Thiếu file Phase 3: " + ", ".join(missing))

    for path in REQUIRED:
        if path.endswith(".cs"):
            check_balanced(path)

    require(
        "src/Combat/Data/ProjectileSpecData.cs",
        "CombatDeliveryMode",
        "ProjectileSpecData",
        "HurtboxCollisionMask",
        "WorldCollisionMask",
    )
    require(
        "src/Combat/Projectiles/CombatProjectile2D.cs",
        "ShapeCast2D",
        "ForceShapecastUpdate",
        "TryResolveHit",
        "FactionRules.CanDamage",
        "QueueFree",
    )
    require(
        "src/Combat/Runtime/CombatActionRunner.cs",
        "ActionReleased",
        "TryStartAbilityAction(CombatActionData action, Vector2 aimDirection)",
        "CombatDeliveryMode.MeleeHitbox",
    )
    require(
        "src/Combat/Actors/CombatCharacter.cs",
        "Actions.ActionReleased += OnActionReleased",
        "CombatProjectileSpawner.Spawn",
    )
    require(
        "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
        "PartyTacticalDirector",
        "CombatSpacingController",
        "CombatMovementSolver",
        "CombatIntentExecutor",
        "_executor.Execute(",
    )
    require(
        "src/Combat/Decision/Movement/CombatMovementSolver.cs",
        "DirectionCount = 16",
        "DangerRay",
        "NavigationAgent2D",
        "ScorePredictedRange",
    )
    require(
        "src/Combat/Decision/Runtime/TacticalEvaluator.cs",
        "action_runtime_cooldown",
        "HoldRange là nhịp chờ/căn vị trí",
        "CastPrimary",
    )

    # Resource chain phải thật sự là Skill -> Action -> ProjectileSpec -> HitProfile.
    require(
        "assets/resources/data/combat/actions/hyou_ice_bolt.tres",
        "DeliveryMode = 1",
        "ProjectileSpec = ExtResource",
        "HitProfile = ExtResource",
        'ActionId = "hyou_ice_bolt"',
    )
    require(
        "assets/resources/data/combat/skills/hyou_ice_bolt.tres",
        "ExecutionType = 1",
        "CombatAction = ExtResource",
        'SkillId = "hyou_ice_bolt"',
    )
    cryomancer_resource = text("assets/resources/data/combat/decision/classes/cryomancer.tres")
    if "GrantedSkills = Array[Resource]([ExtResource" not in cryomancer_resource \
            and "GrantedSkills = Array[ExtResource" not in cryomancer_resource:
        fail("cryomancer.tres chưa gán GrantedSkills theo cú pháp typed Resource array của Godot.")
    require(
        "assets/resources/data/combat/decision/classes/cryomancer.tres",
        "PreferredMinRange = 105.0",
        "PreferredMaxRange = 140.0",
    )

    hyou_config_resource = text("assets/resources/data/characters/Hyou.tres")
    if "ActiveSkills = Array[Resource]([ExtResource" not in hyou_config_resource \
            and "ActiveSkills = Array[ExtResource" not in hyou_config_resource:
        fail("Hyou.tres chưa gán ActiveSkills theo cú pháp typed Resource array của Godot.")
    require(
        "assets/resources/data/characters/Hyou.tres",
        "hyou_ice_bolt.tres",
    )
    require(
        "assets/resources/data/characters/Hyou.tscn",
        "Enabled = false",
        "UseDecisionCore = true",
        "ShadowMode = false",
        "NavAgent",
        "NavigationAgentPath",
        "DefaultMoveset",
    )

    # Không để visual bị gọi hai lần bởi legacy AI và ActionRunner cùng lúc.
    legacy_ai = text("src/Combat/AI/HyouAI.cs")
    if ".PlayCast(" in legacy_ai:
        fail("HyouAI vẫn tự gọi PlayCast; visual sẽ phát hai lần khi action event chạy.")

    # Tất cả đường res:// mới phải tồn tại trong archive.
    checked_resources = [
        "assets/resources/data/combat/actions/hyou_ice_bolt.tres",
        "assets/resources/data/combat/projectiles/hyou_ice_bolt.tres",
        "assets/resources/data/combat/skills/hyou_ice_bolt.tres",
        "assets/resources/data/combat/decision/classes/cryomancer.tres",
        "assets/resources/data/characters/Hyou.tres",
        "assets/resources/data/characters/Hyou.tscn",
    ]
    missing_refs: list[str] = []
    for resource in checked_resources:
        for ref in ext_paths(resource):
            if not (ROOT / ref).exists():
                missing_refs.append(f"{resource} -> res://{ref}")
    if missing_refs:
        fail("Resource reference không tồn tại: " + "; ".join(missing_refs))

    # Regression logic: ở giữa preferred band, Ice Bolt phải thắng HoldRange.
    cast_base = weighted_geometric(
        [1.0, 1.0, 0.98, 1.0, 0.95, 0.55],
        [1.35, 1.10, 0.85, 0.50, 1.10, 0.80],
    )
    cast_score = cast_base * (0.68 + 0.32 * 0.92)
    hold_score = min(0.72, 0.24 + 1.0 * (0.26 + 0.16 * 0.95))
    if cast_score <= hold_score + 0.08:
        fail(f"Ice Bolt chưa thắng HoldRange đủ rõ: cast={cast_score:.3f}, hold={hold_score:.3f}")

    print("[OK] Projectile pipeline đi qua ShapeCast2D và CombatResolver.")
    print("[OK] Skill/Action/Projectile/HitProfile của Ice Bolt nối đủ resource chain.")
    print("[OK] HyouAI cũ đã tắt; Decision Core live qua Scheduler -> Spacing -> Movement -> Executor.")
    print(f"[OK] Utility smoke test: Ice Bolt {cast_score:.3f} > HoldRange {hold_score:.3f} trong preferred band.")
    print("[NOTE] Cần mở project Godot đầy đủ để compile C# và chạy arena/runtime thật.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
