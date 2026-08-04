from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
required = [
    "src/UI/Party/PartyPanel.cs",
    "src/UI/HUD/GameMenuButton.cs",
    "src/Characters/Party/PlayerManager.cs",
    "src/Save/SaveGameData.cs",
    "src/Save/SaveManager.cs",
    "scenes/ui/GameMenuButton.tscn",
]

missing = [item for item in required if not (root / item).is_file()]
if missing:
    print("[FAIL] Thiếu file:")
    for item in missing:
        print(" -", item)
    sys.exit(1)

checks = {
    "src/UI/Party/PartyPanel.cs": ["class PartyPanel", "SetPartyLeader", "MoveMember"],
    "src/Characters/Party/PlayerManager.cs": ["MaxPartySize", "CapturePartyOrder", "RestorePartyOrder"],
    "src/Save/SaveGameData.cs": ["PartyOrderCharacterIds", "Version { get; set; } = 5"],
    "src/Save/SaveManager.cs": ["CapturePartyOrder", "RestorePartyOrder", "Version = 5"],
    "scenes/ui/GameMenuButton.tscn": ["src/UI/Party/PartyPanel.cs", 'script = ExtResource("13_party_panel")'],
}

for relative, needles in checks.items():
    content = (root / relative).read_text(encoding="utf-8")
    for needle in needles:
        if needle not in content:
            print(f"[FAIL] {relative} thiếu mối nối: {needle}")
            sys.exit(1)

print("[PASS] Panel Tổ đội có đủ file và các mối nối chính.")
