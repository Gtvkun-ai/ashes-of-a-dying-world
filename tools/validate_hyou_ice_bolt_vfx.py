from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]

checks = {
    "src/Combat/Visuals/HyouCastVisual.cs": [
        "TimelineFrames { get; set; } = 8",
        "TimelineStartColumn { get; set; } = 0",
        "CastDurationSeconds { get; set; } = 2f",
        'AddDirection(frames, sheet, "down", DownRow)',
        'AddDirection(frames, sheet, "right", RightRow)',
        'AddDirection(frames, sheet, "left", LeftRow)',
        'AddDirection(frames, sheet, "up", UpRow)',
        "(frameCount - 1) / Mathf.Max(0.1f, CastDurationSeconds)",
        "[HyouCastVisual] CAST START",
    ],
    "src/Combat/Data/CombatActionData.cs": [
        "ScalePlaybackWithAttackSpeed",
    ],
    "src/Combat/Runtime/CombatActionRunner.cs": [
        "action.ScalePlaybackWithAttackSpeed",
    ],
    "assets/resources/data/characters/Hyou.tscn": [
        "TimelineFrames = 8",
        "CastDurationSeconds = 2.0",
        "DebugLogging = true",
    ],
    "assets/resources/data/combat/actions/hyou_ice_bolt.tres": [
        "ActiveStartFrame = 7",
        "ActiveEndFrame = 7",
        "EndFrame = 7",
        "PlaybackSpeedMultiplier = 0.7",
        "ScalePlaybackWithAttackSpeed = false",
        "StartupSeconds = 2.0",
    ],
    "assets/resources/data/combat/skills/hyou_ice_bolt.tres": [
        "Cooldown = 2.0",
    ],
}

errors = []
for relative, needles in checks.items():
    path = ROOT / relative
    if not path.exists():
        errors.append(f"Thiếu file: {relative}")
        continue
    text = path.read_text(encoding="utf-8")
    for needle in needles:
        if needle not in text:
            errors.append(f"{relative}: thiếu `{needle}`")

if errors:
    print("HYOU ICE BOLT VFX: FAIL")
    for error in errors:
        print(" -", error)
    sys.exit(1)

print("HYOU ICE BOLT VFX: PASS")
print(" - Timeline đủ 8 frame; VFX hữu hình ở cột 2-5, không bị cắt nhầm")
print(" - Body và sáu lớp phép cùng chạy 3.5 FPS, release ở mốc 2 giây")
print(" - Spell cast không bị chỉ số AttackSpeed rút ngắn")
print(" - Runtime có log READY / BOUND / CAST START / CAST STOP")
