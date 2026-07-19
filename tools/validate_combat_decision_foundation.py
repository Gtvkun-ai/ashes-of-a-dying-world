#!/usr/bin/env python3
"""Kiểm tra tĩnh patch Combat Decision Foundation khi archive không có .csproj/project.godot."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "src/Combat/Decision/Model/CombatDecisionEnums.cs",
    "src/Combat/Decision/Model/CombatIntent.cs",
    "src/Combat/Decision/Model/CombatSnapshot.cs",
    "src/Combat/Decision/Model/DecisionModels.cs",
    "src/Combat/Decision/Profiles/CombatClassProfile.cs",
    "src/Combat/Decision/Profiles/CombatDoctrineProfile.cs",
    "src/Combat/Decision/Profiles/CombatPersonalityProfile.cs",
    "src/Combat/Decision/Runtime/CombatBlackboard.cs",
    "src/Combat/Decision/Runtime/ResponseCurve.cs",
    "src/Combat/Decision/Runtime/DecisionContracts.cs",
    "src/Combat/Decision/Runtime/ThreatPredictor.cs",
    "src/Combat/Decision/Runtime/CombatPerception.cs",
    "src/Combat/Decision/Runtime/TacticalEvaluator.cs",
    "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
    "assets/resources/data/combat/decision/classes/cryomancer.tres",
    "assets/resources/data/combat/decision/doctrines/hyou_safe_control.tres",
    "assets/resources/data/combat/decision/personalities/hyou_calm_protective.tres",
    "assets/resources/data/characters/Hyou.tscn",
]


def fail(message: str) -> None:
    print(f"[FAIL] {message}")
    raise SystemExit(1)


def require_contains(path: str, *tokens: str) -> None:
    text = (ROOT / path).read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            fail(f"{path} thiếu token bắt buộc: {token}")


def check_braces(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    if text.count("{") != text.count("}"):
        fail(f"Ngoặc nhọn không cân bằng: {path.relative_to(ROOT)}")


def main() -> int:
    missing = [path for path in REQUIRED_FILES if not (ROOT / path).is_file()]
    if missing:
        fail("Thiếu file: " + ", ".join(missing))

    for relative in REQUIRED_FILES:
        path = ROOT / relative
        if path.suffix == ".cs":
            check_braces(path)

    require_contains(
        "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
        "UseDecisionCore",
        "ShadowMode",
        "BuildSnapshot",
        "DecisionTrace",
    )

    agent_text = (ROOT / "src/Combat/Decision/Runtime/CombatDecisionAgent.cs").read_text(encoding="utf-8")
    forbidden_execution_calls = [
        ".SetMoveInput(",
        ".SetBlocking(",
        ".RequestAttack(",
        ".TryActivate(",
    ]
    for token in forbidden_execution_calls:
        if token in agent_text:
            fail(f"Foundation shadow mode không được thực thi mechanics: tìm thấy {token}")

    require_contains(
        "assets/resources/data/characters/Hyou.tscn",
        "CombatDecisionAgent",
        "LoSRay",
        "UseDecisionCore = false",
        "ShadowMode = true",
        "cryomancer.tres",
        "hyou_safe_control.tres",
        "hyou_calm_protective.tres",
        "DebugLogging = true",
    )

    print("[OK] Combat Decision Foundation đầy đủ.")
    print("[OK] Hyou chạy shadow mode; HyouAI cũ vẫn giữ quyền điều khiển.")
    print("[OK] Agent foundation không gọi movement/block/attack/ability mechanics.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
