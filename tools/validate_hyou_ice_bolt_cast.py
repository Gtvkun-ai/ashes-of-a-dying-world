#!/usr/bin/env python3
"""Kiểm tra contract cast Ice Bolt 2 giây và bộ sprite phép của Hyou."""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def require(path: str, pattern: str, message: str) -> None:
    text = (ROOT / path).read_text(encoding="utf-8")
    if re.search(pattern, text, flags=re.MULTILINE) is None:
        raise AssertionError(f"{message}: {path}")


def main() -> int:
    action = "assets/resources/data/combat/actions/hyou_ice_bolt.tres"
    skill = "assets/resources/data/combat/skills/hyou_ice_bolt.tres"
    scene = "assets/resources/data/characters/Hyou.tscn"
    visual = "src/Combat/Visuals/HyouCastVisual.cs"

    require(action, r"^ActiveStartFrame = 7$", "Projectile phải nhả ở frame cuối")
    require(action, r"^ActiveEndFrame = 7$", "Active window phải nằm ở frame cuối")
    require(action, r"^EndFrame = 7$", "Action phải kết thúc ở frame cuối")
    require(action, r"^PlaybackSpeedMultiplier = 0\.7$", "8 frame @ 5 FPS phải được hạ còn 3.5 FPS")
    require(action, r"^StartupSeconds = 2\.0$", "Fallback startup phải giữ đúng 2 giây")
    require(skill, r"^Cooldown = 2\.0$", "Mỗi Ice Bolt phải có nhịp hai giây")

    require(scene, r"^UsedFrames = 4$", "Sheet phép chỉ dùng bốn frame hữu hình")
    require(scene, r"^CastDurationSeconds = 2\.0$", "Visual phải đồng bộ cast hai giây")

    require(visual, r'x10 hyou up ice bolt\.png', "Thiếu sheet vòng tròn ma pháp")
    require(visual, r'x10 hyou ice up\.png', "Thiếu sheet lõi băng")
    require(visual, r"public int UsedFrames \{ get; set; \} = 4;", "Default visual đang đọc thừa frame alpha")
    require(visual, r"public float CastDurationSeconds \{ get; set; \} = 2f;", "Default cast visual không phải hai giây")
    require(visual, r"frameCount / Mathf\.Max\(0\.1f, CastDurationSeconds\)", "Animation speed chưa lấy duration làm nguồn sự thật")
    require(visual, r"_playingAction != currentAction", "Fallback process có nguy cơ restart animation mỗi frame")

    print("[OK] Hyou Ice Bolt: charge 2.0s, 4 frame phép, release ở frame cuối.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"[FAIL] {exc}", file=sys.stderr)
        raise SystemExit(1)
