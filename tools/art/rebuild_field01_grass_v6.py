#!/usr/bin/env python3
"""Rebuild Field 01 grass art on the map's logical 456x474 pixel grid.

Goals:
- one art pixel = one logical map pixel (scene scales the layer 4x with nearest)
- calm base grass, not painterly/noisy
- authored broad light/shade composition in a real macro mask
- sparse 1-3 px grass micro accents
- useful soft normal map instead of a flat normal
- explicit path-edge grass overlay instead of relying on shader noise

The pass is deterministic so future tweaks stay reproducible.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, binary_dilation, distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
LAYERS = ROOT / "assets/graphics/world/whispering_fields/field_01_layers"
W, H = 456, 474
RNG = np.random.default_rng(50625)


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
    # Authored sunlit clearings / soft shadow masses. These are fixed composition,
    # not runtime procedural FBM, so the scene reads the same every frame.
    m = np.zeros((H, W), dtype=np.float32)

    # Sunlit ribbons / clearings, arranged diagonally like broken canopy light.
    for args, gain in [
        ((72, 72, 92, 31, -28), 0.82),
        ((210, 120, 108, 38, -24), 0.58),
        ((338, 126, 88, 34, -18), 0.48),
        ((148, 262, 118, 44, -25), 0.64),
        ((306, 330, 124, 48, -20), 0.74),
        ((126, 415, 92, 38, -18), 0.46),
    ]:
        m += ellipse_gaussian(*args) * gain

    # Broad cool shade near forest/map edges and a couple of visual-rest regions.
    for args, gain in [
        ((228, -30, 270, 80, 0), -0.62),
        ((-28, 255, 92, 220, 0), -0.38),
        ((482, 245, 86, 230, 0), -0.30),
        ((380, 236, 120, 90, -15), -0.34),
        ((72, 345, 95, 92, 10), -0.28),
    ]:
        m += ellipse_gaussian(*args) * gain

    # Tiny fixed low-frequency breakup so large ellipses never look airbrushed.
    n = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 28.0)
    n = norm01(n) * 2.0 - 1.0
    m += n * 0.075

    # Compress extremes; 128 remains exact neutral for the shader.
    m = np.tanh(m * 1.08) * 0.92
    out = np.clip(128.0 + m * 132.0, 38, 222).astype(np.uint8)
    return out


def build_base(macro_u8: np.ndarray):
    # Calm natural field. Low-frequency hue/value variation is intentionally weak;
    # lighting composition lives in the macro mask instead of being baked here.
    low = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 21.0)
    mid = gaussian_filter(RNG.normal(0, 1, (H, W)).astype(np.float32), 6.5)
    low = norm01(low) - 0.5
    mid = norm01(mid) - 0.5

    base = np.array([69.0, 108.0, 25.0], dtype=np.float32)
    rgb = np.empty((H, W, 3), dtype=np.float32)
    rgb[:] = base
    rgb += low[..., None] * np.array([9.0, 11.0, 4.0])
    rgb += mid[..., None] * np.array([3.0, 4.0, 1.5])

    # A slight cool/dark bias in authored shadow zones and warmer/yellower bias in
    # clearings, but only ~20% of the final lighting effect is baked into albedo.
    ms = (macro_u8.astype(np.float32) - 128.0) / 92.0
    pos = np.clip(ms, 0, 1)[..., None]
    neg = np.clip(-ms, 0, 1)[..., None]
    rgb += pos * np.array([4.0, 4.5, 0.5])
    rgb += neg * np.array([-4.0, -5.0, 1.0])

    # Quantize slightly to keep a pixel-art surface instead of smooth painted noise.
    rgb = np.round(rgb / 2.0) * 2.0
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    rgba = np.dstack([rgb, np.full((H, W), 255, dtype=np.uint8)])

    # Sparse readable grass marks. Most of the field is intentionally untouched.
    micro = np.zeros((H, W, 4), dtype=np.uint8)
    draw_base = Image.fromarray(rgba, 'RGBA')
    draw_micro = Image.fromarray(micro, 'RGBA')
    db = ImageDraw.Draw(draw_base)
    dm = ImageDraw.Draw(draw_micro)

    # Lower density in bright open clearings; slightly higher in resting/shaded zones.
    candidates = []
    for _ in range(1100):
        x = int(RNG.integers(4, W - 4)); y = int(RNG.integers(4, H - 4))
        macro = (int(macro_u8[y, x]) - 128) / 90.0
        accept = 0.58 - max(macro, 0) * 0.20 + max(-macro, 0) * 0.10
        if RNG.random() < accept:
            candidates.append((x, y))
        if len(candidates) >= 520:
            break

    dark = (52, 91, 22, 255)
    dark2 = (61, 98, 22, 255)
    light = (91, 132, 34, 255)
    light2 = (103, 143, 39, 255)

    for x, y in candidates:
        kind = RNG.choice(5, p=[0.31, 0.27, 0.20, 0.15, 0.07])
        c = light if RNG.random() < 0.55 else dark2
        # Each tuft is 1-3 logical pixels; at 4x nearest this becomes readable but
        # stays smaller/softer than characters and props.
        if kind == 0:  # single short blade
            db.point((x, y), fill=c)
            if RNG.random() < 0.45: db.point((x, y-1), fill=light)
            dm.point((x, y), fill=(198 if c == light else 72,)*3 + (145,))
        elif kind == 1:  # diagonal two-pixel blade
            s = -1 if RNG.random() < 0.5 else 1
            db.point((x, y), fill=dark)
            db.point((x+s, y-1), fill=c)
            dm.point((x+s, y-1), fill=(194,194,194,155))
        elif kind == 2:  # tiny V
            db.point((x, y), fill=dark)
            db.point((x-1, y-1), fill=c)
            db.point((x+1, y-1), fill=light)
            dm.point((x-1, y-1), fill=(80,80,80,125))
            dm.point((x+1, y-1), fill=(194,194,194,125))
        elif kind == 3:  # 3px tuft
            db.point((x, y), fill=dark)
            db.point((x, y-1), fill=c)
            db.point((x-1, y-1), fill=dark2)
            db.point((x+1, y-2), fill=light)
            dm.point((x+1, y-2), fill=(202,202,202,150))
        else:  # rare brighter accent; never larger than 3x3
            db.point((x, y), fill=dark)
            db.point((x-1, y-1), fill=light2)
            db.point((x, y-2), fill=light)
            db.point((x+1, y-1), fill=light2)
            dm.point((x, y-2), fill=(208,208,208,175))

    return draw_base, draw_micro



def build_field_detail() -> Image.Image:
    """Medium-scale authored tufts: visible accents, not a tiled carpet."""
    path = np.asarray(Image.open(LAYERS / '03_dirt_path.png').convert('RGBA'))[..., 3] > 40
    cliff = np.asarray(Image.open(LAYERS / '05_cliff_wall.png').convert('RGBA'))[..., 3] > 40
    blocked = binary_dilation(path | cliff, iterations=5)

    out = Image.new('RGBA', (W, H), (0,0,0,0))
    d = ImageDraw.Draw(out)
    centers = [(58,92),(154,86),(286,82),(382,132),(88,224),(232,218),(356,252),(154,344),(300,350),(390,394),(82,422)]
    pts=[]
    for cx,cy in centers:
        count=int(RNG.integers(2,5))
        for _ in range(count):
            x=int(np.clip(cx+RNG.normal(0,24),8,W-8)); y=int(np.clip(cy+RNG.normal(0,18),8,H-8))
            if blocked[y,x]:
                continue
            if any((x-px)**2+(y-py)**2<90 for px,py in pts):
                continue
            pts.append((x,y))
    # isolated accents
    attempts=0
    while len(pts)<42 and attempts<500:
        attempts+=1
        x=int(RNG.integers(8,W-8)); y=int(RNG.integers(8,H-8))
        if blocked[y,x] or any((x-px)**2+(y-py)**2<145 for px,py in pts):
            continue
        pts.append((x,y))

    c_dark=(46,84,19,245); c_mid=(62,105,24,250); c_light=(96,139,37,245); c_hi=(111,151,43,225)
    for x,y in pts:
        variant=int(RNG.integers(0,5))
        flip=-1 if RNG.random()<0.5 else 1
        if variant==0:
            pix=[(0,0,c_dark),(-1,-1,c_mid),(1,-1,c_light),(0,-2,c_hi)]
        elif variant==1:
            pix=[(0,0,c_dark),(-1,0,c_mid),(1,0,c_mid),(-2,-1,c_light),(0,-2,c_light),(2,-1,c_hi)]
        elif variant==2:
            pix=[(0,1,c_dark),(0,0,c_mid),(-1,-1,c_light),(1,-2,c_hi),(2,-1,c_light)]
        elif variant==3:
            pix=[(-1,1,c_dark),(0,1,c_dark),(1,1,c_dark),(-2,0,c_mid),(-1,-1,c_light),(1,-1,c_light),(2,0,c_hi),(0,-2,c_hi)]
        else:
            pix=[(0,1,c_dark),(-1,0,c_mid),(1,0,c_mid),(-2,-1,c_light),(0,-1,c_light),(2,-1,c_light),(-1,-2,c_hi),(1,-2,c_hi)]
        for dx,dy,c in pix:
            xx=x+dx*flip; yy=y+dy
            if 0<=xx<W and 0<=yy<H:
                d.point((xx,yy),fill=c)
    return out

def build_normal(macro_u8: np.ndarray, micro_img: Image.Image) -> Image.Image:
    m = (macro_u8.astype(np.float32) - 128.0) / 92.0
    # Mostly low form; micro alpha adds just enough local roll to avoid a flat normal.
    alpha = np.asarray(micro_img, dtype=np.uint8)[..., 3].astype(np.float32) / 255.0
    h = gaussian_filter(m, 2.4) * 0.34 + gaussian_filter(alpha, 0.9) * 0.10
    gy, gx = np.gradient(h)
    nx = -gx * 3.0
    ny = -gy * 3.0
    nz = np.ones_like(nx)
    inv = 1.0 / np.sqrt(nx*nx + ny*ny + nz*nz)
    nx *= inv; ny *= inv; nz *= inv
    rgb = np.stack([(nx*0.5+0.5)*255, (ny*0.5+0.5)*255, (nz*0.5+0.5)*255], axis=-1)
    return Image.fromarray(np.clip(rgb,0,255).astype(np.uint8), 'RGB')


def build_edge_detail() -> Image.Image:
    path = np.asarray(Image.open(LAYERS / '03_dirt_path.png').convert('RGBA'))
    pa = path[..., 3] > 48
    ring1 = binary_dilation(pa, iterations=1) & (~pa)
    ring3 = binary_dilation(pa, iterations=3) & (~pa)
    dist, inds = distance_transform_edt(~pa, return_indices=True)

    out = Image.new('RGBA', (W, H), (0,0,0,0))
    d = ImageDraw.Draw(out)

    # Broken dark fringe: gives the dirt/grass boundary a deliberate pixel-art seam
    # without becoming a uniform outline.
    ys,xs=np.where(ring1)
    for y,x in zip(ys,xs):
        selector=(x*17+y*31+(x*y)%13)%11
        if selector not in (0,1,2):
            d.point((int(x),int(y)), fill=(45,82,18,220 if selector<8 else 185))

    # Sparse blade tips extending farther into grass.
    ys, xs = np.where(ring3 & (dist >= 1.1) & (dist <= 2.9))
    order = RNG.permutation(len(xs))
    chosen = []
    for idx in order:
        x, y = int(xs[idx]), int(ys[idx])
        if any((x-cx)**2 + (y-cy)**2 < 34 for cx,cy in chosen[-90:]):
            continue
        if RNG.random() > 0.42:
            continue
        chosen.append((x,y))
        if len(chosen) >= 210:
            break

    colors = [(48,86,20,235),(56,98,22,245),(82,124,31,235),(100,141,38,225)]
    for x,y in chosen:
        py, px = int(inds[0,y,x]), int(inds[1,y,x])
        vx, vy = x-px, y-py
        mag = max(math.hypot(vx,vy), 1e-6)
        vx, vy = vx/mag, vy/mag
        length = 1 if RNG.random() < .48 else 2
        tx = int(round(x + vx*length)); ty = int(round(y + vy*length))
        d.point((x,y), fill=colors[int(RNG.integers(0,2))])
        d.point((tx,ty), fill=colors[int(RNG.integers(2,4))])
        if RNG.random() < 0.22:
            sx = int(round(x - vy)); sy = int(round(y + vx))
            d.point((sx,sy), fill=colors[1])
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

    print('rebuilt:', *(p.name for p in [
        LAYERS/'00_ground_base.png', LAYERS/'10_grass_macro_mask.png',
        LAYERS/'11_grass_micro_detail.png', LAYERS/'13_grass_normal_soft.png',
        LAYERS/'14_grass_edge_detail.png', LAYERS/'15_grass_field_detail.png']))

if __name__ == '__main__':
    main()
