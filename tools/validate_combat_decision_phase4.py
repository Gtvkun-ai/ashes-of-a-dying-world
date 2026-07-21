#!/usr/bin/env python3
"""Kiểm tra tĩnh Phase 4: motor physics mượt + formation follow + arena Ice Bolt.

Script không thay thế compile Godot/C#. Nó bắt đúng regression vừa xuất hiện trong log:
Decision trace báo move=(0,0) nhưng executor bí mật pursuit Player theo nhịp 0.12 giây.
"""
from __future__ import annotations

from pathlib import Path
import math
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def fail(message: str) -> None:
    print(f"[FAIL] {message}")
    raise SystemExit(1)


def read(path: str) -> str:
    file_path = ROOT / path
    if not file_path.is_file():
        fail(f"Thiếu file: {path}")
    return file_path.read_text(encoding="utf-8")


def require(path: str, *tokens: str) -> None:
    content = read(path)
    for token in tokens:
        if token not in content:
            fail(f"{path} thiếu token: {token}")


def check_balanced(path: str) -> None:
    content = read(path)
    for left, right in (("{", "}"), ("(", ")"), ("[", "]")):
        if content.count(left) != content.count(right):
            fail(f"Delimiter {left}{right} lệch trong {path}")


def parse_position(scene: str, node_name: str) -> tuple[float, float]:
    block = re.search(
        rf'\[node name="{re.escape(node_name)}"[^\]]*\](.*?)(?=\n\[node |\Z)',
        scene,
        re.S,
    )
    if not block:
        fail(f"Không tìm thấy node {node_name} trong test scene.")
    match = re.search(r"position\s*=\s*Vector2\(([-\d.]+),\s*([-\d.]+)\)", block.group(1))
    if not match:
        fail(f"Node {node_name} thiếu position trong test scene.")
    return float(match.group(1)), float(match.group(2))


def main() -> int:
    files = [
        "src/Combat/Actors/CombatCharacter.cs",
        "src/Combat/Decision/Execution/CombatIntentExecutor.cs",
        "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
        "src/Combat/Decision/Scheduling/CombatActionScheduler.cs",
        "scenes/tests/hyou_ice_bolt_test.tscn",
    ]
    for path in files[:4]:
        check_balanced(path)

    require(
        "src/Combat/Actors/CombatCharacter.cs",
        "float speedScale = 1f",
        "_moveSpeedScale",
        "_isActuallyRunning",
        "StateMachine.CanRegenerateStamina && !_isActuallyRunning",
    )
    require(
        "src/Combat/Decision/Execution/CombatIntentExecutor.cs",
        "TickMotor(",
        "TickFollowLeader(",
        "FollowRunEnterDistance",
        "FollowRunExitDistance",
        "FollowStopDistance",
        "FollowSlowDistance",
        "-2, // -2 = formation follow",
    )
    require(
        "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
        "Motor chạy mỗi physics frame",
        "_executor.TickMotor(",
        "motorMode",
        '"follow"',
    )
    require(
        "src/Combat/Decision/Scheduling/CombatActionScheduler.cs",
        '"same_idle"',
        '"idle_exit"',
    )

    executor = read("src/Combat/Decision/Execution/CombatIntentExecutor.cs")
    if "private bool ExecuteFollowLeader" in executor:
        fail("Fallback pursuit cũ vẫn còn; motor follow sẽ tiếp tục bị giấu khỏi trace.")

    scene = read("scenes/tests/hyou_ice_bolt_test.tscn")
    for token in (
        "res://assets/resources/data/characters/Hyou.tscn",
        "res://scenes/world/WhisperingFields/slime_1.tscn",
        "process_mode = 4",
    ):
        if token not in scene:
            fail(f"Test scene thiếu token: {token}")

    hyou_scene = read("assets/resources/data/characters/Hyou.tscn")
    radius_match = re.search(r"EnemySearchRadius\s*=\s*([\d.]+)", hyou_scene)
    if not radius_match or float(radius_match.group(1)) < 140.0:
        fail("Hyou EnemySearchRadius nhỏ hơn khoảng test, sẽ tiếp tục target=none.")

    hx, hy = parse_position(scene, "Hyou")
    sx, sy = parse_position(scene, "TargetSlime")
    distance = math.hypot(sx - hx, sy - hy)
    if not 105.0 <= distance <= 140.0:
        fail(f"Khoảng Hyou -> slime phải nằm trong preferred band, hiện là {distance:.1f}px")

    print("[OK] Tactical decision vẫn chạy nhịp thấp, motor đã chạy mỗi physics frame.")
    print("[OK] Follow dùng formation anchor, arrival slowdown và run hysteresis.")
    print("[OK] Stamina regen dựa trên chạy thật, không dựa trên một run command bị kẹt.")
    print(f"[OK] Arena Ice Bolt đặt Hyou cách target {distance:.1f}px trong preferred band.")
    print("[NOTE] Vẫn cần mở Godot để compile C# và chạy scene tests/hyou_ice_bolt_test.tscn.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
