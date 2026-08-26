#!/usr/bin/env python3
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, binary_dilation, distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
LAYERS = ROOT / 'assets/graphics/world/whispering_fields/field_01_layers'
W, H = 456, 474
RNG = np.random.default_rng(50626)


def norm01(a: np.ndarray) -> np.ndarray:
    lo, hi = np.percentile(a, (2, 98))
    return np.clip((a - lo) / max(hi - lo, 1e-6), 0.0, 1.0)


def ellipse_gaussian(cx, cy, rx, ry, angle_deg=0.0):
    yy, xx = np.mgrid[0:H, 0:W]
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    dx, dy = xx - cx, yy - cy
    xr = dx * ca + dy * sa
    yr = -dx * sa + dy * ca
    return np.exp(-0.5 * ((xr / rx) ** 2 + (yr / ry) ** 2))


def build_macro() -> np.ndarray:
    m = np.zeros((H, W), dtype=np.float32)
    # Stronger authored light ribbons / clearings.
    for args, gain in [
        ((62, 68, 94, 30, -28), 0.95),
        ((188, 114, 112, 42, -25), 0.62),
        ((332, 104, 92, 35, -18), 0.52),
        ((144, 252, 128, 50, -25), 0.76),
        ((312, 312, 132, 52, -21), 0.88),
        ((118, 406, 96, 38, -18), 0.58),
        ((248, 390, 78, 28, -15), 0.38),
    ]:
        m += ellipse_gaussian(*args) * gain

    # Broad visual-rest and cooler edge masses.
    for args, gain in [
        ((228, -26, 280, 82, 0), -0.72),
        ((-24, 252, 96, 220, 0), -0.44),
        ((478, 238, 88, 225, 0), -0.36),
        ((376, 234, 124, 94, -15), -0.42),
        ((78, 352, 110, 102, 10), -0.34),
        ((270, 190, 62, 44, 10), -0.18),
    ]:
        m += ellipse_gaussian(*args) * gain

    n = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 24.0)
    n = norm01(n) * 2.0 - 1.0
    m += n * 0.09

    m = np.tanh(m * 1.18) * 0.95
    out = np.clip(128.0 + m * 136.0, 34, 226).astype(np.uint8)
    return out


def build_base(macro_u8: np.ndarray):
    low = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 21.0)
    mid = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 7.5)
    low = norm01(low) - 0.5
    mid = norm01(mid) - 0.5

    base = np.array([72.0, 112.0, 26.0], dtype=np.float32)
    rgb = np.empty((H, W, 3), dtype=np.float32)
    rgb[:] = base
    rgb += low[..., None] * np.array([10.0, 13.0, 4.5])
    rgb += mid[..., None] * np.array([4.0, 5.0, 2.0])

    ms = (macro_u8.astype(np.float32) - 128.0) / 96.0
    pos = np.clip(ms, 0, 1)[..., None]
    neg = np.clip(-ms, 0, 1)[..., None]
    rgb += pos * np.array([7.0, 7.5, 0.8])
    rgb += neg * np.array([-5.5, -6.5, 1.5])

    # Local warmth / coolness pockets so the field is not too polite.
    warmth = np.zeros((H, W), dtype=np.float32)
    cool = np.zeros((H, W), dtype=np.float32)
    for args, gain in [
        ((82, 86, 52, 28, -18), 1.0),
        ((190, 250, 68, 34, -12), 0.8),
        ((316, 332, 72, 38, -18), 1.0),
        ((124, 412, 54, 26, -10), 0.7),
    ]:
        warmth += ellipse_gaussian(*args) * gain
    for args, gain in [
        ((44, 308, 58, 54, 0), 0.9),
        ((366, 176, 72, 42, 0), 0.8),
        ((426, 364, 62, 42, 0), 0.65),
    ]:
        cool += ellipse_gaussian(*args) * gain

    rgb += warmth[..., None] * np.array([5.0, 4.0, -0.3])
    rgb += cool[..., None] * np.array([-2.0, -3.0, 1.0])

    # A few richer grassy islands and a few quieter rest zones.
    lush = np.zeros((H, W), dtype=np.float32)
    calm = np.zeros((H, W), dtype=np.float32)
    for args, gain in [
        ((86, 98, 26, 18, -10), 1.0),
        ((232, 218, 34, 20, 10), 0.9),
        ((368, 254, 30, 20, 0), 0.9),
        ((312, 356, 28, 20, -10), 0.8),
    ]:
        lush += ellipse_gaussian(*args) * gain
    for args, gain in [
        ((286, 84, 44, 22, 0), 0.7),
        ((128, 332, 40, 24, 0), 0.7),
    ]:
        calm += ellipse_gaussian(*args) * gain
    rgb += lush[..., None] * np.array([2.0, 5.0, 1.0])
    rgb += calm[..., None] * np.array([-1.0, -2.5, -0.5])

    rgb = np.round(rgb / 2.0) * 2.0
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    rgba = np.dstack([rgb, np.full((H, W), 255, dtype=np.uint8)])

    micro = np.zeros((H, W, 4), dtype=np.uint8)
    draw_base = Image.fromarray(rgba, 'RGBA')
    draw_micro = Image.fromarray(micro, 'RGBA')
    db = ImageDraw.Draw(draw_base)
    dm = ImageDraw.Draw(draw_micro)

    cluster_centers = [(70,86),(146,98),(316,114),(96,222),(236,214),(358,254),(146,340),(300,346),(118,420),(382,392)]
    candidates = []
    for cx, cy in cluster_centers:
        count = int(RNG.integers(26, 44))
        for _ in range(count):
            x = int(np.clip(cx + RNG.normal(0, 24), 4, W - 4))
            y = int(np.clip(cy + RNG.normal(0, 18), 4, H - 4))
            macro = (int(macro_u8[y, x]) - 128) / 90.0
            accept = 0.70 - max(macro, 0) * 0.18 + max(-macro, 0) * 0.08
            if RNG.random() < accept:
                candidates.append((x, y))
    while len(candidates) < 760:
        x = int(RNG.integers(4, W - 4)); y = int(RNG.integers(4, H - 4))
        macro = (int(macro_u8[y, x]) - 128) / 90.0
        accept = 0.40 - max(macro, 0) * 0.12 + max(-macro, 0) * 0.06
        if RNG.random() < accept:
            candidates.append((x, y))

    dark = (50, 88, 21, 255)
    dark2 = (60, 98, 23, 255)
    light = (95, 137, 37, 255)
    light2 = (110, 151, 43, 255)

    for x, y in candidates[:760]:
        kind = RNG.choice(6, p=[0.23, 0.22, 0.19, 0.14, 0.14, 0.08])
        c = light if RNG.random() < 0.56 else dark2
        if kind == 0:
            db.point((x, y), fill=c)
            if RNG.random() < 0.55: db.point((x, y - 1), fill=light)
            dm.point((x, y), fill=(198 if c == light else 72,) * 3 + (150,))
        elif kind == 1:
            s = -1 if RNG.random() < 0.5 else 1
            db.point((x, y), fill=dark)
            db.point((x + s, y - 1), fill=c)
            dm.point((x + s, y - 1), fill=(194, 194, 194, 160))
        elif kind == 2:
            db.point((x, y), fill=dark)
            db.point((x - 1, y - 1), fill=c)
            db.point((x + 1, y - 1), fill=light)
            dm.point((x - 1, y - 1), fill=(80, 80, 80, 135))
            dm.point((x + 1, y - 1), fill=(194, 194, 194, 135))
        elif kind == 3:
            db.point((x, y), fill=dark)
            db.point((x, y - 1), fill=c)
            db.point((x - 1, y - 1), fill=dark2)
            db.point((x + 1, y - 2), fill=light)
            dm.point((x + 1, y - 2), fill=(202, 202, 202, 155))
        elif kind == 4:
            db.point((x, y), fill=dark)
            db.point((x - 1, y - 1), fill=light2)
            db.point((x, y - 2), fill=light)
            db.point((x + 1, y - 1), fill=light2)
            dm.point((x, y - 2), fill=(208, 208, 208, 180))
        else:
            db.point((x, y), fill=dark)
            db.point((x - 1, y), fill=dark2)
            db.point((x + 1, y), fill=dark2)
            db.point((x, y - 1), fill=light)
            db.point((x, y - 2), fill=light2)
            dm.point((x, y - 1), fill=(205, 205, 205, 160))

    return draw_base, draw_micro


def build_field_detail() -> Image.Image:
    path = np.asarray(Image.open(LAYERS / '03_dirt_path.png').convert('RGBA'))[..., 3] > 40
    cliff = np.asarray(Image.open(LAYERS / '05_cliff_wall.png').convert('RGBA'))[..., 3] > 40
    blocked = binary_dilation(path | cliff, iterations=5)

    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    pts = []
    centers = [
        (56, 86), (150, 80), (278, 84), (376, 130),
        (88, 228), (228, 216), (356, 250),
        (154, 338), (298, 350), (388, 396), (84, 420),
        (204, 410), (346, 420)
    ]
    for cx, cy in centers:
        count = int(RNG.integers(3, 6))
        for _ in range(count):
            x = int(np.clip(cx + RNG.normal(0, 18), 8, W - 8))
            y = int(np.clip(cy + RNG.normal(0, 16), 8, H - 8))
            if blocked[y, x]:
                continue
            if any((x - px) ** 2 + (y - py) ** 2 < 72 for px, py in pts):
                continue
            pts.append((x, y))

    # A few accents hugging safe path/pond sides so the map feels less empty.
    attempts = 0
    while len(pts) < 58 and attempts < 1000:
        attempts += 1
        x = int(RNG.integers(8, W - 8)); y = int(RNG.integers(8, H - 8))
        if blocked[y, x] or any((x - px) ** 2 + (y - py) ** 2 < 120 for px, py in pts):
            continue
        if RNG.random() > 0.22:
            continue
        pts.append((x, y))

    c_dark = (46, 84, 19, 245); c_mid = (62, 105, 24, 250); c_light = (98, 141, 38, 245); c_hi = (116, 156, 45, 232)
    patch_fill = (74, 118, 28, 62)
    patch_hi = (104, 145, 41, 52)

    # Soft tiny mats under a subset of clusters.
    for i, (x, y) in enumerate(pts):
        if i % 5 != 0:
            continue
        rx = int(RNG.integers(4, 8)); ry = int(RNG.integers(3, 6))
        for yy in range(max(0, y - ry), min(H, y + ry + 1)):
            for xx in range(max(0, x - rx), min(W, x + rx + 1)):
                if ((xx - x) / max(rx,1)) ** 2 + ((yy - y) / max(ry,1)) ** 2 <= 1.0:
                    alpha = 44 if (xx + yy) % 2 == 0 else 30
                    d.point((xx, yy), fill=(patch_fill[0], patch_fill[1], patch_fill[2], alpha))
        d.point((x, y - max(1, ry // 2)), fill=patch_hi)

    for x, y in pts:
        variant = int(RNG.integers(0, 6))
        flip = -1 if RNG.random() < 0.5 else 1
        if variant == 0:
            pix = [(0,0,c_dark),(-1,-1,c_mid),(1,-1,c_light),(0,-2,c_hi)]
        elif variant == 1:
            pix = [(0,0,c_dark),(-1,0,c_mid),(1,0,c_mid),(-2,-1,c_light),(0,-2,c_light),(2,-1,c_hi)]
        elif variant == 2:
            pix = [(0,1,c_dark),(0,0,c_mid),(-1,-1,c_light),(1,-2,c_hi),(2,-1,c_light)]
        elif variant == 3:
            pix = [(-1,1,c_dark),(0,1,c_dark),(1,1,c_dark),(-2,0,c_mid),(-1,-1,c_light),(1,-1,c_light),(2,0,c_hi),(0,-2,c_hi)]
        elif variant == 4:
            pix = [(0,1,c_dark),(-1,0,c_mid),(1,0,c_mid),(-2,-1,c_light),(0,-1,c_light),(2,-1,c_light),(-1,-2,c_hi),(1,-2,c_hi)]
        else:
            pix = [(0,2,c_dark),(-1,1,c_mid),(1,1,c_mid),(-2,0,c_light),(0,0,c_light),(2,0,c_light),(-1,-1,c_hi),(1,-2,c_hi),(3,-1,c_hi)]
        for dx, dy, c in pix:
            xx = x + dx * flip; yy = y + dy
            if 0 <= xx < W and 0 <= yy < H:
                d.point((xx, yy), fill=c)
    return out


def build_normal(macro_u8: np.ndarray, micro_img: Image.Image) -> Image.Image:
    m = (macro_u8.astype(np.float32) - 128.0) / 92.0
    alpha = np.asarray(micro_img, dtype=np.uint8)[..., 3].astype(np.float32) / 255.0
    h = gaussian_filter(m, 2.2) * 0.40 + gaussian_filter(alpha, 0.85) * 0.14
    gy, gx = np.gradient(h)
    nx = -gx * 3.2
    ny = -gy * 3.2
    nz = np.ones_like(nx)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    nx *= inv; ny *= inv; nz *= inv
    rgb = np.stack([(nx * 0.5 + 0.5) * 255, (ny * 0.5 + 0.5) * 255, (nz * 0.5 + 0.5) * 255], axis=-1)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), 'RGB')


def build_edge_detail() -> Image.Image:
    path = np.asarray(Image.open(LAYERS / '03_dirt_path.png').convert('RGBA'))
    pa = path[..., 3] > 48
    ring1 = binary_dilation(pa, iterations=1) & (~pa)
    ring4 = binary_dilation(pa, iterations=4) & (~pa)
    dist, inds = distance_transform_edt(~pa, return_indices=True)

    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)

    ys, xs = np.where(ring1)
    for y, x in zip(ys, xs):
        selector = (x * 17 + y * 31 + (x * y) % 13) % 11
        if selector in (0, 1, 2, 6):
            d.point((int(x), int(y)), fill=(44, 80, 18, 224 if selector != 6 else 180))

    ys, xs = np.where(ring4 & (dist >= 1.0) & (dist <= 3.8))
    order = RNG.permutation(len(xs))
    chosen = []
    for idx in order:
        x, y = int(xs[idx]), int(ys[idx])
        if any((x - cx) ** 2 + (y - cy) ** 2 < 24 for cx, cy in chosen[-140:]):
            continue
        if RNG.random() > 0.48:
            continue
        chosen.append((x, y))
        if len(chosen) >= 320:
            break

    colors = [(48, 86, 20, 235), (58, 100, 23, 245), (86, 128, 33, 235), (104, 146, 40, 225)]
    for x, y in chosen:
        py, px = int(inds[0, y, x]), int(inds[1, y, x])
        vx, vy = x - px, y - py
        mag = max(math.hypot(vx, vy), 1e-6)
        vx, vy = vx / mag, vy / mag
        length = int(RNG.choice([1, 2, 2, 3]))
        d.point((x, y), fill=colors[int(RNG.integers(0, 2))])
        for step in range(1, length + 1):
            tx = int(round(x + vx * step)); ty = int(round(y + vy * step))
            if 0 <= tx < W and 0 <= ty < H:
                d.point((tx, ty), fill=colors[min(3, 1 + step)])
        if RNG.random() < 0.24:
            sx = int(round(x - vy)); sy = int(round(y + vx))
            if 0 <= sx < W and 0 <= sy < H:
                d.point((sx, sy), fill=colors[1])
    return out


def main():
    macro = build_macro()
    base, micro = build_base(macro)
    field_detail = build_field_detail()
    normal = build_normal(macro, micro)
    edge = build_edge_detail()

    base.save(LAYERS / '00_ground_base.png', optimize=True)
    Image.fromarray(macro, 'L').save(LAYERS / '10_grass_macro_mask.png', optimize=True)
    micro.save(LAYERS / '11_grass_micro_detail.png', optimize=True)
    normal.save(LAYERS / '13_grass_normal_soft.png', optimize=True)
    edge.save(LAYERS / '14_grass_edge_detail.png', optimize=True)
    field_detail.save(LAYERS / '15_grass_field_detail.png', optimize=True)
    print('rebuilt v8 grass layers')

if __name__ == '__main__':
    main()
