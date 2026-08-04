#!/usr/bin/env python3
"""Kiểm tra tĩnh cho Combat Refactor V2 khi chưa có project.godot/.csproj."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def require(relative: str) -> None:
    if not (ROOT / relative).exists():
        fail(f"Thiếu file bắt buộc: {relative}")


def forbid(relative: str) -> None:
    if (ROOT / relative).exists():
        fail(f"File/thư mục cũ vẫn còn: {relative}")


def strip_csharp(text: str) -> str:
    output: list[str] = []
    i = 0
    state = "code"
    while i < len(text):
        char = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""
        if state == "code":
            if char == "/" and nxt == "/":
                state = "line"
                output.extend("  ")
                i += 2
            elif char == "/" and nxt == "*":
                state = "block"
                output.extend("  ")
                i += 2
            elif char == '"':
                state = "string"
                output.append(" ")
                i += 1
            elif char == "'":
                state = "char"
                output.append(" ")
                i += 1
            else:
                output.append(char)
                i += 1
        elif state == "line":
            if char == "\n":
                state = "code"
                output.append("\n")
            else:
                output.append(" ")
            i += 1
        elif state == "block":
            if char == "*" and nxt == "/":
                state = "code"
                output.extend("  ")
                i += 2
            else:
                output.append("\n" if char == "\n" else " ")
                i += 1
        else:
            quote = '"' if state == "string" else "'"
            if char == "\\":
                output.extend("  ")
                i += 2
            elif char == quote:
                state = "code"
                output.append(" ")
                i += 1
            else:
                output.append("\n" if char == "\n" else " ")
                i += 1
    return "".join(output)


def check_csharp_delimiters() -> None:
    pairs = (("(", ")"), ("[", "]"), ("{", "}"))
    for path in ROOT.joinpath("src").rglob("*.cs"):
        text = strip_csharp(path.read_text(encoding="utf-8", errors="replace"))
        for opening, closing in pairs:
            stack: list[int] = []
            line = 1
            for char in text:
                if char == "\n":
                    line += 1
                elif char == opening:
                    stack.append(line)
                elif char == closing:
                    if not stack:
                        fail(f"{path.relative_to(ROOT)}: dư {closing} ở dòng {line}")
                        break
                    stack.pop()
            if stack:
                fail(f"{path.relative_to(ROOT)}: thiếu {closing}, mở ở dòng {stack[-1]}")


def check_resource_paths_and_uids() -> None:
    files = list(ROOT.rglob("*.tscn")) + list(ROOT.rglob("*.tres"))
    pattern = re.compile(
        r'\[ext_resource type="(Script|Resource|PackedScene)"'
        r'(?: uid="([^"]+)")? path="res://([^"]+)"'
    )
    for owner in files:
        text = owner.read_text(encoding="utf-8", errors="replace")
        for resource_type, uid, relative in pattern.findall(text):
            target = ROOT / relative
            if not target.exists():
                fail(f"{owner.relative_to(ROOT)} trỏ tới {resource_type} không tồn tại: {relative}")
                continue
            uid_file = Path(str(target) + ".uid")
            if uid and uid_file.exists() and uid_file.read_text().strip() != uid:
                fail(f"UID lệch: {owner.relative_to(ROOT)} -> {relative}")


def read_int_property(text: str, property_name: str) -> int | None:
    match = re.search(rf"^{re.escape(property_name)}\s*=\s*(-?\d+)$", text, re.MULTILINE)
    return int(match.group(1)) if match else None


def check_action_windows() -> None:
    for path in ROOT.joinpath("assets/resources/data/combat/actions").glob("*.tres"):
        text = path.read_text(encoding="utf-8")
        values = [
            read_int_property(text, "StartFrame"),
            read_int_property(text, "ActiveStartFrame"),
            read_int_property(text, "ActiveEndFrame"),
            read_int_property(text, "EndFrame"),
        ]
        if any(value is None for value in values):
            fail(f"Action thiếu frame window: {path.relative_to(ROOT)}")
        elif values != sorted(values):
            fail(f"Frame window sai thứ tự: {path.relative_to(ROOT)} = {values}")


def check_load_steps() -> None:
    for path in list(ROOT.rglob("*.tscn")) + list(ROOT.rglob("*.tres")):
        text = path.read_text(encoding="utf-8", errors="replace")
        first = text.splitlines()[0] if text else ""
        match = re.search(r"load_steps=(\d+)", first)
        if not match:
            continue
        actual = len(re.findall(r"^\[ext_resource ", text, re.MULTILINE))
        actual += len(re.findall(r"^\[sub_resource ", text, re.MULTILINE)) + 1
        if int(match.group(1)) != actual:
            fail(f"load_steps sai: {path.relative_to(ROOT)} ghi {match.group(1)}, thực tế {actual}")


def check_migration_contracts() -> None:
    for path in [
        "src/Combat/Actors/CombatCharacter.cs",
        "src/Combat/Runtime/CombatResolver.cs",
        "src/Combat/Runtime/CombatStateMachine.cs",
        "src/Combat/Runtime/CombatActionRunner.cs",
        "src/Combat/Runtime/CombatAbilityRunner.cs",
        "src/Combat/Runtime/CombatHitbox.cs",
        "src/Combat/Runtime/FactionRules.cs",
        "src/Combat/AI/HyouAI.cs",
        "src/Combat/AI/SlimeBrain.cs",
        "assets/resources/data/combat/movesets/wood_sword.tres",
        "assets/resources/data/combat/movesets/slime.tres",
        "scenes/actors/player/player.tscn",
    ]:
        require(path)

    for path in [
        "src/Core",
        "src/Entities",
        "src/Scenes",
        "scenes/world/WhisperingFields/Findzone.cs",
        "scenes/world/WhisperingFields/Attackzone.cs",
        "scenes/world/WhisperingFields/Slime1.cs",
        "scenes/world/WhisperingFields/findzone.tscn",
        "scenes/world/WhisperingFields/attackzone.tscn",
    ]:
        forbid(path)

    slime_scene = ROOT.joinpath("scenes/world/WhisperingFields/slime_1.tscn").read_text()
    if "findzone" in slime_scene.lower() or "attackzone" in slime_scene.lower():
        fail("slime_1.tscn vẫn còn node zone cũ")

    weapon_scene = ROOT.joinpath("assets/resources/data/weapons/sword/woodSword.tscn").read_text()
    if 'name="Hitbox"' in weapon_scene or "HitboxShape" in weapon_scene:
        fail("woodSword.tscn vẫn còn hitbox tĩnh cũ")

    save_data = ROOT.joinpath("src/Save/SaveGameData.cs").read_text()
    if "Version { get; set; } = 2" not in save_data:
        fail("SaveGameData chưa nâng schema version 2")

    forbidden_text = ("res://scripts/Core", "res://scripts/Entities", "res://scripts/Scenes")
    for path in list(ROOT.rglob("*.cs")) + list(ROOT.rglob("*.tscn")) + list(ROOT.rglob("*.tres")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for token in forbidden_text:
            if token in text:
                fail(f"Còn resource path cũ {token} trong {path.relative_to(ROOT)}")


def main() -> int:
    check_migration_contracts()
    check_resource_paths_and_uids()
    check_action_windows()
    check_load_steps()
    check_csharp_delimiters()

    if ERRORS:
        print(f"Combat Refactor V2: FAIL ({len(ERRORS)} lỗi)")
        for error in ERRORS:
            print(f" - {error}")
        return 1

    print("Combat Refactor V2: PASS")
    print(" - Cấu trúc cũ đã bị loại bỏ")
    print(" - Resource/script/scene references hợp lệ")
    print(" - UID và load_steps hợp lệ")
    print(" - Action frame windows hợp lệ")
    print(" - Delimiter C# cân bằng")
    return 0


if __name__ == "__main__":
    sys.exit(main())
