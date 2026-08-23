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
    alpha = np.asarray(canopy.getchannel("A"), dtype=np.float32) / 255.0
    # Silhouette ngang của canopy giúp mép bóng không thành ellipse hình học hoàn hảo.
    x_profile = alpha.max(axis=0)
    prof = Image.fromarray(np.uint8(x_profile[None, :] * 255.0), "L").resize((112, 1), Image.Resampling.BOX)
    xprof = np.asarray(prof, dtype=np.float32)[0] / 255.0

    w, h = 112, 64
    out = np.zeros((h, w), dtype=np.float32)
    center = (w - 1) * 0.5
    for y in range(h):
        t = y / max(h - 1, 1)
        # Gốc rộng, đuôi thu nhẹ và fade. Không dùng oval parametric hoàn toàn.
        width_scale = 1.00 - 0.32 * (t ** 1.15)
        fade = (1.0 - t) ** 0.58
        core = 0.82 + 0.18 * math.exp(-((t - 0.18) / 0.22) ** 2)
        for x in range(w):
            src_x = center + (x - center) / max(width_scale, 0.05)
            if 0 <= src_x < w - 1:
                x0 = int(src_x)
                f = src_x - x0
                p = xprof[x0] * (1.0 - f) + xprof[min(x0 + 1, w - 1)] * f
                out[y, x] = p * fade * core

    # Penumbra mềm, nhưng giữ core foliage irregular.
    core_img = Image.fromarray(np.uint8(np.clip(out * 210.0, 0, 255)), "L")
    blur_img = core_img.filter(ImageFilter.GaussianBlur(radius=2.0))
    core_a = np.asarray(core_img, dtype=np.uint8)
    blur_a = np.asarray(blur_img, dtype=np.uint8)
    final_a = np.maximum(core_a, (blur_a.astype(np.float32) * 0.72).astype(np.uint8))

    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = np.array([9, 15, 10], dtype=np.uint8)
    rgba[..., 3] = final_a
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
