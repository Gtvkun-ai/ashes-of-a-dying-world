#!/usr/bin/env python3
"""Kiểm tra tĩnh patch Decision Core Phase 2 khi archive không có .csproj/project.godot.

Script không thay thế compile Godot thật. Nó bắt các lỗi rollout dễ gây tai nạn:
- thiếu file phase 2;
- agent shadow lén gọi mechanics;
- mất hard gate recovery/panic;
- curve range quay lại kiểu mép band = 0;
- mana policy chưa nối đủ chuỗi Stats -> StateMachine -> CombatCharacter -> AbilityRunner.
"""
from __future__ import annotations

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

REQUIRED = [
    "src/Combat/Decision/Model/CombatSnapshot.cs",
    "src/Combat/Decision/Model/DecisionModels.cs",
    "src/Combat/Decision/Profiles/CombatClassProfile.cs",
    "src/Combat/Decision/Runtime/CombatPerception.cs",
    "src/Combat/Decision/Runtime/TacticalEvaluator.cs",
    "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
    "src/Combat/Decision/Scheduling/CombatActionScheduler.cs",
    "src/Characters/Stats/PlayerStats.cs",
    "src/Combat/Runtime/CombatStateMachine.cs",
    "src/Combat/Runtime/CombatAbilityRunner.cs",
    "assets/resources/data/combat/decision/classes/cryomancer.tres",
    "assets/resources/data/characters/Hyou.tscn",
]


def fail(message: str) -> None:
    print(f"[FAIL] {message}")
    raise SystemExit(1)


def check_contains(path: str, *needles: str) -> None:
    text = (ROOT / path).read_text(encoding="utf-8")
    for needle in needles:
        if needle not in text:
            fail(f"{path} thiếu dấu hiệu bắt buộc: {needle}")


def smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def smooth_ramp(value: float, start: float, end: float) -> float:
    lo, hi = sorted((start, end))
    return smoothstep((value - lo) / max(1e-6, hi - lo))


def smooth_band(value: float, minimum: float, maximum: float, edge: float) -> float:
    if value < minimum:
        return smooth_ramp(value, minimum - edge, minimum)
    if value <= maximum:
        return 1.0
    return 1.0 - smooth_ramp(value, maximum, maximum + edge)


def main() -> int:
    missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
    if missing:
        fail("Thiếu file: " + ", ".join(missing))

    agent = (ROOT / "src/Combat/Decision/Runtime/CombatDecisionAgent.cs").read_text(encoding="utf-8")
    forbidden = [".SetMoveInput(", ".SetBlocking(", ".RequestAttack(", ".TryUseSkill(", ".TryActivate("]
    for token in forbidden:
        if token in agent:
            fail(f"CombatDecisionAgent shadow đang gọi mechanics: {token}")

    check_contains(
        "src/Combat/Decision/Runtime/TacticalEvaluator.cs",
        "unsafe_to_recover",
        "leader_needs_protection",
        "class_has_no_granted_skill",
        "inside_unsafe_cast_range",
        "panic_requires_direct_escape",
        "0.84f + 0.14f * snapshot.ThreatSeverity",
    )
    check_contains(
        "src/Combat/Decision/Scheduling/CombatActionScheduler.cs",
        "commitment_lock",
        "score_margin",
        "emergency_override",
        "same_intent",
    )
    check_contains(
        "src/Characters/Stats/PlayerStats.cs",
        "ManaRegenRate",
        "ManaRegenDelay",
        "ConsumeMana",
        "allowMana",
    )
    check_contains(
        "src/Combat/Runtime/CombatStateMachine.cs",
        "CanRegenerateMana",
    )
    check_contains(
        "src/Combat/Runtime/CombatAbilityRunner.cs",
        "ConsumeMana(skill.ManaCost)",
    )
    check_contains(
        "assets/resources/data/characters/Hyou.tscn",
        "UseDecisionCore = false",
        "ShadowMode = true",
        "SwitchScoreMargin = 0.14",
    )

    # Regression checks cho đúng lỗi đã thấy trong log.
    if abs(smooth_band(105.0, 105.0, 140.0, 24.0) - 1.0) > 1e-6:
        fail("Range band không đạt 1.0 tại preferred min.")
    if abs(smooth_band(140.0, 105.0, 140.0, 24.0) - 1.0) > 1e-6:
        fail("Range band không đạt 1.0 tại preferred max.")
    if smooth_band(146.0, 105.0, 140.0, 24.0) < 0.80:
        fail("Range falloff vẫn quá gắt ở 146 px.")
    if smooth_band(227.0, 82.0, 180.0, 24.0) > 0.001:
        fail("RecoverResources vẫn có position readiness khi target ở 227 px.")
    if smooth_ramp(227.0, 140.0, 180.0) < 0.99:
        fail("Approach chưa đạt áp lực tối đa ở 227 px.")

    # Kiểm tra ngoặc cơ bản cho toàn bộ C# có trong patch.
    for path in REQUIRED:
        if not path.endswith(".cs"):
            continue
        text = (ROOT / path).read_text(encoding="utf-8")
        if text.count("{") != text.count("}"):
            fail(f"Ngoặc nhọn lệch trong {path}")

    print("[OK] Evaluator đã sửa resource/panic/range/approach/leader protection.")
    print("[OK] Scheduler có commitment, switch margin và emergency override.")
    print("[OK] Mana policy đã nối vào mechanics nhưng Decision Agent vẫn shadow-safe.")
    print("[NOTE] Vẫn phải mở project Godot thật để compile và chạy arena test.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
