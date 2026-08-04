#!/usr/bin/env python3
"""Kiểm tra tĩnh các file và mối nối chính của panel Nhiệm vụ."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

REQUIRED = [
    "src/Quests/Data/QuestData.cs",
    "src/Quests/Data/QuestObjectiveData.cs",
    "src/Quests/Data/QuestRewardData.cs",
    "src/Quests/Runtime/QuestRuntimeState.cs",
    "src/Quests/Runtime/QuestManager.cs",
    "src/Quests/Runtime/QuestService.cs",
    "src/UI/Quests/QuestJournalPanel.cs",
    "src/UI/Quests/QuestTrackerHud.cs",
    "assets/resources/data/quests/traces_in_the_wind.tres",
    "assets/resources/data/quests/flowers_on_ashes.tres",
    "assets/resources/data/quests/hyou_promise.tres",
    "scenes/ui/GameMenuButton.tscn",
]

CHECKS = {
    "scenes/ui/GameMenuButton.tscn": [
        "QuestJournalPanel.cs",
        "QuestTrackerHud.cs",
        'script = ExtResource("10_quest_journal")',
        "QuestTrackerHud",
    ],
    "src/UI/HUD/GameMenuButton.cs": [
        "QuestJournalPanel",
        "CaptureQuestProgress",
        "RestoreQuestProgress",
    ],
    "src/Save/SaveGameData.cs": [
        "Version { get; set; } = 4",
        "QuestProgressSaveData",
        "TrackedQuestId",
    ],
    "src/Save/SaveManager.cs": [
        "CaptureQuestProgress",
        "RestoreQuestProgress",
        "Version = 4",
    ],
}

# Các asset này đã có trong project gốc và không cần đóng gói lại trong patch.
PREEXISTING_ASSETS = {
    "assets/sprites/UI_HUD/Inventory/category_quest.png",
    "assets/resources/data/icon/VIT.tres",
    "assets/resources/data/icon/INT.tres",
    "assets/resources/data/icon/SPI.tres",
}

errors = []
for relative in REQUIRED:
    if not (ROOT / relative).is_file():
        errors.append(f"Thiếu file: {relative}")

for relative, tokens in CHECKS.items():
    path = ROOT / relative
    if not path.is_file():
        continue
    text = path.read_text(encoding="utf-8")
    for token in tokens:
        if token not in text:
            errors.append(f"{relative}: thiếu mối nối `{token}`")

# Kiểm tra reference res:// trong ba resource mẫu.
for path in (ROOT / "assets/resources/data/quests").glob("*.tres"):
    text = path.read_text(encoding="utf-8")
    for line in text.splitlines():
        if 'path="res://' not in line:
            continue
        resource_path = line.split('path="res://', 1)[1].split('"', 1)[0]
        if not (ROOT / resource_path).exists() and resource_path not in PREEXISTING_ASSETS:
            errors.append(f"{path.relative_to(ROOT)} tham chiếu file không tồn tại: {resource_path}")

if errors:
    print("[FAIL] Panel Nhiệm vụ còn lỗi tĩnh:")
    for error in errors:
        print(f" - {error}")
    sys.exit(1)

print("[PASS] Panel Nhiệm vụ có đủ file, resource mẫu và mối nối save/UI chính.")
