#!/usr/bin/env python3
"""Kiểm tra tĩnh gói panel cây kỹ năng trước khi mở Godot.

Script không thay thế bước build C# trong Godot, nhưng bắt được các lỗi đóng gói
thường gặp: thiếu file, sai res:// path, thiếu script trên SkillsPanel, ngoặc C# lệch,
NodeId trùng và prerequisite trỏ tới node không tồn tại.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "src/Combat/Abilities/SkillData.cs",
    "src/Combat/Abilities/SkillTree/SkillTreeNodeData.cs",
    "src/Combat/Abilities/SkillTree/SkillTreeBranchData.cs",
    "src/Combat/Abilities/SkillTree/CharacterSkillTreeData.cs",
    "src/Combat/Abilities/SkillTree/SkillTreeProgression.cs",
    "src/Characters/Data/CharacterConfig.cs",
    "src/Characters/Player/PlayerSkillState.cs",
    "src/Characters/Player/PlayerSkillCollection.cs",
    "src/Characters/Player/SkillCollectionResolver.cs",
    "src/Characters/Player/Player.Skills.cs",
    "src/Characters/Player/Player.cs",
    "src/UI/Skills/SkillTreeNodeView.cs",
    "src/UI/Skills/SkillTreeGraphView.cs",
    "src/UI/Skills/SkillTreePanel.cs",
    "src/UI/HUD/CharacterDetailUI.cs",
    "src/UI/HUD/Skills/SkillIconResolver.cs",
    "src/UI/HUD/Skills/SkillViewModel.cs",
    "src/UI/HUD/GameMenuButton.cs",
    "src/Save/SaveGameData.cs",
    "src/Save/SaveManager.cs",
    "scenes/ui/GameMenuButton.tscn",
    "assets/resources/data/icon/default_skill.tres",
    "assets/resources/data/skill_trees/hikaru_skill_tree.tres",
    "assets/resources/data/skill_trees/hyou_skill_tree.tres",
    "assets/resources/data/characters/Main.tres",
    "assets/resources/data/characters/Hyou.tres",
]


def fail(message: str) -> None:
    print(f"[FAIL] {message}")
    raise SystemExit(1)


def read(relative: str) -> str:
    path = ROOT / relative
    if not path.is_file():
        fail(f"Thiếu file: {relative}")
    return path.read_text(encoding="utf-8")


def check_required_files() -> None:
    for relative in REQUIRED_FILES:
        if not (ROOT / relative).is_file():
            fail(f"Thiếu file bắt buộc: {relative}")
    print(f"[OK] Có đủ {len(REQUIRED_FILES)} file lõi.")


def check_res_paths() -> None:
    pattern = re.compile(r'path="res://([^"]+)"')
    checked = 0
    external_dependencies: set[str] = set()
    candidates = [ROOT / item for item in REQUIRED_FILES if Path(item).suffix in {".tres", ".tscn"}]
    candidates += list((ROOT / "assets/resources/data/combat/skills").glob("hikaru_*.tres"))
    candidates += list((ROOT / "assets/resources/data/combat/skills").glob("hyou_*.tres"))

    # Validator chạy được ở cả hai tình huống:
    # 1) nằm trong project đầy đủ;
    # 2) nằm trong ZIP overlay chỉ chứa file thay đổi.
    # Vì vậy asset gốc của project có thể không nằm trong ZIP và được báo là dependency,
    # còn mọi file do gói này sở hữu vẫn bắt buộc phải có.
    owned_prefixes = (
        "src/UI/Skills/",
        "src/UI/HUD/Skills/",
        "src/Combat/Abilities/SkillTree/",
        "assets/resources/data/skill_trees/",
    )
    owned_exact = set(REQUIRED_FILES)
    owned_exact.update(
        str(path.relative_to(ROOT)).replace("\\", "/")
        for path in candidates
        if path.is_file() and "assets/resources/data/combat/skills" in str(path)
    )

    for path in candidates:
        text = path.read_text(encoding="utf-8", errors="replace")
        for relative in pattern.findall(text):
            checked += 1
            if (ROOT / relative).exists():
                continue

            owned = relative in owned_exact or relative.startswith(owned_prefixes)
            if owned:
                fail(f"{path.relative_to(ROOT)} trỏ tới file thuộc gói nhưng bị thiếu: res://{relative}")
            external_dependencies.add(relative)

    print(f"[OK] Đã đọc {checked} đường dẫn res://; mọi dependency thuộc gói đều có mặt.")
    if external_dependencies:
        print(f"[NOTE] {len(external_dependencies)} dependency dùng asset/project gốc, không đóng gói lặp lại.")


def strip_csharp_strings_and_comments(text: str) -> str:
    text = re.sub(r'@?"(?:""|\\.|[^"\\])*"', '""', text)
    text = re.sub(r"//.*", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return text


def check_csharp_braces() -> None:
    for relative in [item for item in REQUIRED_FILES if item.endswith(".cs")]:
        path = ROOT / relative
        text = strip_csharp_strings_and_comments(path.read_text(encoding="utf-8"))
        balance = 0
        for char in text:
            if char == "{":
                balance += 1
            elif char == "}":
                balance -= 1
                if balance < 0:
                    fail(f"Ngoặc nhọn đóng dư trong {relative}")
        if balance != 0:
            fail(f"Ngoặc nhọn chưa cân bằng trong {relative}: {balance}")
    print("[OK] Ngoặc nhọn của các file C# trong gói cân bằng.")


def check_scene_wiring() -> None:
    sources = {
        "GameMenuButton.tscn": read("scenes/ui/GameMenuButton.tscn"),
        "GameMenuButton.cs": read("src/UI/HUD/GameMenuButton.cs"),
        "CharacterConfig.cs": read("src/Characters/Data/CharacterConfig.cs"),
        "PlayerSkillCollection.cs": read("src/Characters/Player/PlayerSkillCollection.cs"),
        "CharacterDetailUI.cs": read("src/UI/HUD/CharacterDetailUI.cs"),
        "SaveManager.cs": read("src/Save/SaveManager.cs"),
    }
    required_fragments = {
        "GameMenuButton.tscn": [
            'path="res://scripts/UI/Skills/SkillTreePanel.cs"',
            'script = ExtResource("8_skill_tree")',
            'icon = ExtResource("9_skill_icon")',
        ],
        "GameMenuButton.cs": ["SkillsPanel is SkillTreePanel", "RefreshFromCurrentParty"],
        "CharacterConfig.cs": ["CharacterSkillTreeData SkillTree"],
        "PlayerSkillCollection.cs": ["RegisterDefinitionsFromTree", "TryUnlock", "IsUnlocked"],
        "CharacterDetailUI.cs": ["SkillCollectionResolver.Resolve", "_displayedSkillCollection.IsUnlocked"],
        "SaveManager.cs": ["CapturePartySkillProgress", "RestorePartySkillProgress", "Version = 3"],
    }
    for source_name, fragments in required_fragments.items():
        for fragment in fragments:
            if fragment not in sources[source_name]:
                fail(f"{source_name} thiếu mối nối: {fragment}")
    print("[OK] Menu, state runtime, Character panel và save/load đã nối cây kỹ năng.")


def check_tree_graphs() -> None:
    for relative in [
        "assets/resources/data/skill_trees/hikaru_skill_tree.tres",
        "assets/resources/data/skill_trees/hyou_skill_tree.tres",
    ]:
        text = read(relative)
        node_ids = re.findall(r'^NodeId = "([^"]+)"', text, flags=re.M)
        if not node_ids:
            fail(f"{relative} không có NodeId.")
        if len(node_ids) != len(set(node_ids)):
            fail(f"{relative} có NodeId trùng.")

        known = set(node_ids)
        requirement_groups = re.findall(
            r'^RequiredNodeIds = Array\[String\]\(\[(.*?)\]\)', text, flags=re.M
        )
        for group in requirement_groups:
            for requirement in re.findall(r'"([^"]+)"', group):
                if requirement not in known:
                    fail(f"{relative}: prerequisite không tồn tại: {requirement}")
        print(f"[OK] {relative}: {len(node_ids)} node, prerequisite hợp lệ.")


def main() -> int:
    check_required_files()
    check_res_paths()
    check_csharp_braces()
    check_scene_wiring()
    check_tree_graphs()
    print("[PASS] Gói panel cây kỹ năng đã đủ file và các mối nối tĩnh chính.")
    print("[NOTE] Vẫn cần mở project đầy đủ trong Godot để import Resource và build C# runtime.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
