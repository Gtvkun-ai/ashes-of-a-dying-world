#!/usr/bin/env python3
"""Static checks for Hyou Ice Bolt projectile visuals."""

from pathlib import Path
import sys
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parents[1]
VISUAL_CS = ROOT / "src/Combat/Data/ProjectileVisualProfileData.cs"
PROJECTILE_CS = ROOT / "src/Combat/Projectiles/CombatProjectile2D.cs"
SPEC_RESOURCE = ROOT / "assets/resources/data/combat/projectiles/hyou_ice_bolt.tres"
VISUAL_RESOURCE = ROOT / "assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres"
ASSET_DIR = ROOT / "assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1"
CORE = ASSET_DIR / "x10 hyou up ice.png"
UP_CORE = ASSET_DIR / "x10 hyou bh ice .png"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"[FAIL] {message}")
    print(f"[PASS] {message}")


visual_cs = VISUAL_CS.read_text(encoding="utf-8")
projectile = PROJECTILE_CS.read_text(encoding="utf-8")
spec_res = SPEC_RESOURCE.read_text(encoding="utf-8")
visual_res = VISUAL_RESOURCE.read_text(encoding="utf-8")

require("ProjectileVisualProfileData" in visual_cs, "presentation is data-driven")
require("SpriteSheetPath" in visual_cs, "visual profile has core sprite path")
require("UpSpriteSheetOverridePath" in visual_cs, "visual profile has up core override")
require("AtlasTexture" in projectile and "_visual" in projectile, "projectile cuts atlas through visual profile")
require("VisualProfile = ExtResource" in spec_res, "projectile spec links visual profile")
require("v8-visual-profile-action-events" in projectile, "projectile runtime build marker is present")
require("x10 hyou up ice.png" in visual_res, "flying core uses ice projectile asset")
require("x10 hyou bh ice .png" in visual_res, "up flying core uses behind ice projectile asset")
require("x10 hyou ice bh.png" not in visual_res, "spell-center asset must not fly with projectile")
require("SpriteColumns = 11" in visual_res and "SpriteFrameWidth = 48" in visual_res,
        "projectile uses the 11x4 cast grid, 48x64 per frame")
require("SpriteColumn = 5" in visual_res, "projectile core uses the fully formed ice frame")
require('LaunchSpriteSheetPath = ""' in visual_res
        and 'UpLaunchSpriteSheetOverridePath = ""' in visual_res
        and "LaunchFrameCount = 0" in visual_res,
        "flying projectile carries no launch or spell-center layer")
require("UseProceduralFallback = false" in visual_res, "Ice Bolt does not fall back to placeholder geometry")

for path, rows in ((CORE, (0, 1, 2)), (UP_CORE, (3,))):
    require(path.exists(), f"asset exists: {path.name}")
    image = Image.open(path).convert("RGBA")
    require(image.size == (528, 256), f"sheet is 528x256: {path.name}")
    for row in rows:
        frame = image.crop((5 * 48, row * 64, 6 * 48, (row + 1) * 64))
        require(frame.getchannel("A").getbbox() is not None,
                f"projectile frame has pixels: {path.name}, row {row}, col 5")

print("HYOU PROJECTILE ASSET V4+: PASS")
