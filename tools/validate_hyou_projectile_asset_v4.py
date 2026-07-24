#!/usr/bin/env python3
"""Kiểm tra tĩnh projectile Ice Bolt dùng visual profile asset thật."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
VISUAL_CS = ROOT / "src/Combat/Data/ProjectileVisualProfileData.cs"
PROJECTILE_CS = ROOT / "src/Combat/Projectiles/CombatProjectile2D.cs"
SPEC_RESOURCE = ROOT / "assets/resources/data/combat/projectiles/hyou_ice_bolt.tres"
VISUAL_RESOURCE = ROOT / "assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres"
ASSET_DIR = ROOT / "assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1"
CORE = ASSET_DIR / "x10 hyou ice up.png"
UP_CORE = ASSET_DIR / "x10 hyou ice bh.png"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"[FAIL] {message}")
    print(f"[PASS] {message}")


visual_cs = VISUAL_CS.read_text(encoding="utf-8")
projectile = PROJECTILE_CS.read_text(encoding="utf-8")
spec_res = SPEC_RESOURCE.read_text(encoding="utf-8")
visual_res = VISUAL_RESOURCE.read_text(encoding="utf-8")

require("ProjectileVisualProfileData" in visual_cs, "Presentation tách khỏi gameplay spec")
require("SpriteSheetPath" in visual_cs, "Visual profile có sprite sheet data-driven")
require("UpSpriteSheetOverridePath" in visual_cs, "Visual profile có sheet override hướng up")
require("AtlasTexture" in projectile and "_visual" in projectile, "Projectile cắt atlas qua visual profile")
require("VisualProfile = ExtResource" in spec_res, "Projectile spec trỏ tới visual profile")
require("v8-visual-profile-action-events" in projectile, "Có build marker presentation v8")
require("x10 hyou ice up.png" in visual_res, "Core dùng asset hyou ice up")
require("x10 hyou ice bh.png" in visual_res, "Hướng up dùng asset hyou ice bh")
require("SpriteColumns = 8" in visual_res and "SpriteFrameWidth = 66" in visual_res,
        "Grid thật là 8x4, frame 66x64")
require("SpriteColumn = 2" in visual_res, "Projectile core dùng frame kết tinh cột 2")
require("UseProceduralFallback = false" in visual_res,
        "Ice Bolt không được rơi về viên tròn placeholder")

for path, rows in ((CORE, (0, 1, 2)), (UP_CORE, (3,))):
    require(path.exists(), f"Asset tồn tại: {path.name}")
    image = Image.open(path).convert("RGBA")
    require(image.size == (528, 256), f"Sheet đúng kích thước 528x256: {path.name}")
    for row in rows:
        frame = image.crop((2 * 66, row * 64, 3 * 66, (row + 1) * 64))
        require(frame.getchannel("A").getbbox() is not None,
                f"Frame projectile hữu hình ở {path.name}, row {row}, col 2")

print("HYOU PROJECTILE ASSET V4+: PASS")
