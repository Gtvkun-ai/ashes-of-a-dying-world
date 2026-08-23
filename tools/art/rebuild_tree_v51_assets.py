#!/usr/bin/env python3
"""
Tạo các layer runtime cho Tree Package V5.1 từ 2 ảnh master native-resolution.

Mục tiêu:
- Master là ảnh mà artist/user có thể chỉnh trực tiếp.
- Script tự tách canopy/trunk, sinh normal, AO và footprint bóng.
- Không scale sprite trong Godot để tránh lặp lại lỗi apple_tree 499x681 @ scale 0.3.

Yêu cầu: Python 3 + Pillow + numpy.
Chạy từ root project:
    python tools/art/rebuild_tree_v51_assets.py
"""
from __future__ import annotations

from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
TREE_DIR = ROOT / "assets/graphics/environment/trees/v5_1"
SHADOW_DIR = ROOT / "assets/graphics/environment/shadows/v5_1"

ASSETS = {
    "tree": {
        "master": TREE_DIR / "tree_v51_master.png",
        "shadow": SHADOW_DIR / "tree_footprint_v51.png",
    },
    "apple_tree": {
        "master": TREE_DIR / "apple_tree_v51_master.png",
        "shadow": SHADOW_DIR / "apple_tree_footprint_v51.png",
    },
}


def _dilate(mask: np.ndarray, rounds: int = 1) -> np.ndarray:
    """Dilation 8-neighbour thuần numpy để script không phụ thuộc scipy."""
    out = mask.copy()
    for _ in range(rounds):
        p = np.pad(out, 1, mode="constant")
        n = np.zeros_like(out)
        for dy in range(3):
            for dx in range(3):
                n |= p[dy:dy + out.shape[0], dx:dx + out.shape[1]]
        out = n
    return out


def _split_masks(img: Image.Image) -> tuple[np.ndarray, np.ndarray]:
    arr = np.asarray(img.convert("RGBA"), dtype=np.uint8)
    rgb = arr[..., :3].astype(np.float32)
    a = arr[..., 3]
    solid = a >= 64
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    h = arr.shape[0]
    yy = np.arange(h, dtype=np.float32)[:, None] / max(h - 1, 1)

    # Seed màu rõ. Táo đỏ vẫn thuộc canopy.
    green = solid & (g > r * 1.04) & (g > b * 1.08) & (g > 35)
    apple_red = solid & (r > g * 1.30) & (r > b * 1.10) & (yy < 0.72)
    brown = solid & (r > g * 1.10) & (r > b * 1.35) & (yy > 0.34)

    canopy = green | apple_red
    trunk = brown & ~canopy

    # Gán outline tối theo vùng màu gần nhất. Làm vài vòng là đủ vì outline chỉ 1-4 px.
    unresolved = solid & ~(canopy | trunk)
    for _ in range(8):
        if not unresolved.any():
            break
        can_near = _dilate(canopy, 1) & unresolved
        tr_near = _dilate(trunk, 1) & unresolved
        both = can_near & tr_near
        canopy |= can_near & ~tr_near
        trunk |= tr_near & ~can_near
        # Vùng giao: nửa trên ưu tiên canopy, nửa dưới ưu tiên trunk.
        canopy |= both & (yy < 0.64)
        trunk |= both & (yy >= 0.64)
        unresolved = solid & ~(canopy | trunk)

    # Nếu vẫn còn pixel tối cô lập, dùng vị trí làm fallback.
    canopy |= unresolved & (yy < 0.58)
    trunk |= unresolved & (yy >= 0.58)

    # Brown branch nằm trong crown phải thuộc trunk, giữ lại seed brown rõ dù ở trên.
    trunk |= brown & ~apple_red
    canopy &= ~brown

    return canopy, trunk


def _masked_rgba(img: Image.Image, mask: np.ndarray) -> Image.Image:
    arr = np.asarray(img.convert("RGBA"), dtype=np.uint8).copy()
    arr[..., 3] = np.where(mask, arr[..., 3], 0).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def _build_normal(canopy: Image.Image) -> Image.Image:
    arr = np.asarray(canopy.convert("RGBA"), dtype=np.float32) / 255.0
    alpha = arr[..., 3]
    luma = arr[..., 0] * 0.2126 + arr[..., 1] * 0.7152 + arr[..., 2] * 0.0722

    # Height shape lớn từ alpha blur + một ít authored detail từ luma.
    a_img = Image.fromarray(np.uint8(np.clip(alpha * 255.0, 0, 255)), "L")
    smooth = np.asarray(a_img.filter(ImageFilter.GaussianBlur(radius=3.0)), dtype=np.float32) / 255.0
    detail = (luma - 0.50) * 0.16
    height = np.clip(smooth * 0.92 + detail * alpha, 0.0, 1.0)

    gy, gx = np.gradient(height)
    strength = 3.3
    nx = -gx * strength
    ny = -gy * strength
    nz = np.ones_like(nx)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz) + 1e-6
    nx, ny, nz = nx / norm, ny / norm, nz / norm

    out = np.zeros_like(arr)
    out[..., 0] = nx * 0.5 + 0.5
    out[..., 1] = ny * 0.5 + 0.5
    out[..., 2] = nz * 0.5 + 0.5
    out[..., 3] = alpha
    return Image.fromarray(np.uint8(np.clip(out * 255.0, 0, 255)), "RGBA")


def _build_ao(canopy: Image.Image) -> Image.Image:
    arr = np.asarray(canopy.convert("RGBA"), dtype=np.float32) / 255.0
    alpha = arr[..., 3]
    luma = arr[..., 0] * 0.2126 + arr[..., 1] * 0.7152 + arr[..., 2] * 0.0722
    h = arr.shape[0]
    yy = np.arange(h, dtype=np.float32)[:, None] / max(h - 1, 1)

    # AO trắng = không tối thêm. Interior/lower crown tối vừa phải.
    lum_norm = np.clip((luma - 0.10) / 0.78, 0.0, 1.0)
    lower = np.clip((yy - 0.38) / 0.62, 0.0, 1.0)
    ao = 0.64 + 0.28 * lum_norm - 0.10 * lower
    ao = np.clip(ao, 0.48, 1.0)
    ao = np.where(alpha > 0.01, ao, 1.0)

    rgba = np.stack([ao, ao, ao, alpha], axis=-1)
    return Image.fromarray(np.uint8(np.clip(rgba * 255.0, 0, 255)), "RGBA")


def _build_footprint(canopy: Image.Image, out_path: Path) -> None:
    """
    V5.1 elegant footprint.

    Không còn lấy x_profile rồi extrude thành hình thang. Cách cũ khiến bóng cây đọc như
    một polygon/skew rectangle. Bản này giữ silhouette thật của canopy, ép nó xuống ground
    plane rồi nối nhẹ về root bằng một penumbra hữu cơ. Texture chỉ mang ALPHA; RGB trắng
    để ShadowCasterProfile.Tint là nơi duy nhất quyết định màu bóng.
    """
    alpha_img = canopy.getchannel("A")
    bbox = alpha_img.getbbox()
    if bbox is None:
        Image.new("RGBA", (112, 64), (255, 255, 255, 0)).save(out_path)
        return

    # Fill tiny leaf holes before projection, but keep the crown contour authored.
    crown = alpha_img.crop(bbox).filter(ImageFilter.MaxFilter(3))

    w, h = 112, 64
    crown_w, crown_h = 98, 40
    crown = crown.resize((crown_w, crown_h), Image.Resampling.LANCZOS)
    crown = crown.filter(ImageFilter.GaussianBlur(radius=0.65))

    crown_layer = np.zeros((h, w), dtype=np.float32)
    crown_arr = np.asarray(crown, dtype=np.float32) / 255.0
    x0 = (w - crown_w) // 2
    y0 = 16
    y1 = min(y0 + crown_h, h)
    crown_layer[y0:y1, x0:x0 + crown_w] = crown_arr[:y1 - y0]

    # Narrow root connection -> broad crown. This prevents a detached oval while avoiding
    # any straight trapezoid edges. It deliberately stays weaker than the canopy mass.
    xx = np.arange(w, dtype=np.float32)
    center_x = (w - 1) * 0.5
    bridge = np.zeros((h, w), dtype=np.float32)
    for y in range(h):
        t = np.clip(y / 34.0, 0.0, 1.0)
        sigma_x = 5.0 + 31.0 * (t ** 0.82)
        horizontal = np.exp(-0.5 * ((xx - center_x) / max(sigma_x, 0.01)) ** 2)
        vertical = math.exp(-0.5 * ((y - 18.0) / 14.5) ** 2)
        bridge[y] = horizontal * vertical * 0.72

    projected = np.maximum(crown_layer * 0.92, bridge)

    # Near the caster the shadow is stable; far edge gently loses density. No hard tail.
    yy = np.arange(h, dtype=np.float32) / max(h - 1, 1)
    distance_fade = 0.98 - 0.38 * (yy ** 1.45)
    projected *= distance_fade[:, None]

    # Controlled penumbra: core keeps pixel-art contour, blur only softens outer alpha.
    core = Image.fromarray(np.uint8(np.clip(projected * 205.0, 0, 255)), "L")
    penumbra = core.filter(ImageFilter.GaussianBlur(radius=1.8))
    core_a = np.asarray(core, dtype=np.float32)
    penumbra_a = np.asarray(penumbra, dtype=np.float32)
    final_a = np.maximum(core_a * 0.90, penumbra_a * 0.70)

    # Fade the very first/last rows so rotation never exposes a rectangular end-cap.
    final_a[:4] *= np.linspace(0.18, 0.92, 4, dtype=np.float32)[:, None]
    final_a[-7:] *= np.linspace(1.0, 0.18, 7, dtype=np.float32)[:, None]

    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = 255  # alpha-only art; tint comes from ShadowCasterProfile.
    rgba[..., 3] = np.uint8(np.clip(final_a, 0, 188))
    Image.fromarray(rgba, "RGBA").save(out_path)


def rebuild_one(name: str, cfg: dict) -> None:
    master = Image.open(cfg["master"]).convert("RGBA")
    canopy_mask, trunk_mask = _split_masks(master)
    canopy = _masked_rgba(master, canopy_mask)
    trunk = _masked_rgba(master, trunk_mask)

    canopy.save(TREE_DIR / f"{name}_canopy_v51.png")
    trunk.save(TREE_DIR / f"{name}_trunk_v51.png")
    _build_normal(canopy).save(TREE_DIR / f"{name}_canopy_normal_v51.png")
    _build_ao(canopy).save(TREE_DIR / f"{name}_canopy_ao_v51.png")
    _build_footprint(canopy, cfg["shadow"])

    # Metadata anchor để code/artist dễ kiểm tra khi thay master.
    a = np.asarray(master.getchannel("A"), dtype=np.uint8)
    ys, xs = np.where(a >= 64)
    root_y = int(ys.max()) if len(ys) else master.height - 1
    c_a = np.asarray(canopy.getchannel("A"), dtype=np.uint8)
    cys, _ = np.where(c_a >= 64)
    canopy_bottom = int(cys.max()) if len(cys) else root_y
    print(f"[{name}] size={master.size} root_y={root_y} canopy_bottom={canopy_bottom} ground_offset={root_y-canopy_bottom}")


def main() -> None:
    TREE_DIR.mkdir(parents=True, exist_ok=True)
    SHADOW_DIR.mkdir(parents=True, exist_ok=True)
    for name, cfg in ASSETS.items():
        if not cfg["master"].exists():
            raise SystemExit(f"Thiếu master: {cfg['master']}")
        rebuild_one(name, cfg)
    print("V5.1 tree layers rebuilt.")


if __name__ == "__main__":
    main()
