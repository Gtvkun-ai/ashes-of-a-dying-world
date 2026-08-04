#!/usr/bin/env python3
"""Kiểm tra nhanh các mối nối chính của bản nâng cấp panel kỹ năng.

Script này không thay thế bước compile trong Godot, nhưng phát hiện được trường hợp
chép thiếu file hoặc dùng lẫn Player.Skills.cs mới với SkillData.cs cũ.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

REQUIRED_TOKENS = {
    "src/Combat/Abilities/SkillData.cs": [
        "enum SkillCategory",
        "enum SkillElement",
        "public int MaxLevel",
        "public bool DefaultUnlocked",
    ],
    "src/Characters/Player/PlayerSkillState.cs": [
        "class PlayerSkillState",
        "EquippedSlot",
    ],
    "src/Characters/Player/PlayerSkillCollection.cs": [
        "class PlayerSkillCollection",
        "TryEquip",
        "RestoreStates",
    ],
    "src/Characters/Player/Player.Skills.cs": [
        "InitializeSkillCollection",
        "CaptureSkillStates",
        "RestoreSavedSkills",
    ],
    "src/Save/SaveGameData.cs": [
        "class SkillStateSaveData",
        "UnspentSkillPoints",
    ],
    "src/UI/HUD/CharacterDetailUI.cs": [
        "DANH SÁCH KỸ NĂNG",
        "TRANG BỊ VÀO SLOT 1",
        "BuildSkillViewModel",
    ],
    "src/UI/HUD/Skills/SkillViewModel.cs": [
        "class SkillViewModel",
        "DamageText",
    ],
    "assets/resources/data/combat/skills/hyou_ice_bolt.tres": [
        'Icon = ExtResource("3_icon")',
        "Element = 2",
    ],
}


def main() -> int:
    errors: list[str] = []
    for relative_path, tokens in REQUIRED_TOKENS.items():
        path = ROOT / relative_path
        if not path.exists():
            errors.append(f"Thiếu file: {relative_path}")
            continue

        content = path.read_text(encoding="utf-8")
        for token in tokens:
            if token not in content:
                errors.append(f"{relative_path} thiếu nội dung: {token}")

    if errors:
        print("[FAIL] Bản nâng cấp chưa được chép đầy đủ:")
        for error in errors:
            print(f" - {error}")
        return 1

    print("[PASS] Các file và mối nối chính của panel kỹ năng đều có mặt.")
    print("[NOTE] Vẫn cần mở Godot để compile C# và kiểm tra layout ở độ phân giải thật.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
