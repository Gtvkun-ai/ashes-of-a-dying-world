#!/usr/bin/env python3
"""Render parent-aware motion checks and a looping dialogue idle for Hyou.

Usage:
    python tools/art/hyou_rig_preview.py PATH_TO_NEUTRAL_DIAL_RIG_V1

Reads rig_manifest.json, full-canvas RGBA layer PNGs, and the packaging helper's
neutral_dial_rig_flattened.png. Writes motion_preview.gif and motion_extremes.png.
All transforms and resizing use nearest sampling. Source parts are never saved
or repainted. The GIF has a fixed palette to avoid frame-to-frame color flicker.
Requires Pillow and NumPy. There is no invented blinking or facial deformation.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import sys
from typing import Any

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


FRAME_COUNT = 32
DURATION_MS = 80
PREVIEW_SIZE = (512, 768)
IDENTITY = np.eye(3, dtype=np.float64)


@dataclass
class Node:
    name: str
    parent: str | None
    pivot: tuple[float, float]
    role: str
    image: Image.Image | None = None


@dataclass
class Rig:
    nodes: dict[str, Node]
    layers: list[str]
    canvas: tuple[int, int]
    expected: Image.Image
    source_hashes: dict[str, str]


def load_rig(folder: Path) -> Rig:
    folder = folder.resolve()
    manifest = json.loads((folder / "rig_manifest.json").read_text(encoding="utf-8-sig"))
    entries = manifest.get("layers")
    if not isinstance(entries, list) or not entries:
        raise ValueError("rig_manifest.json needs back-to-front layers")
    nodes: dict[str, Node] = {
        "root": Node("root", None, (0.0, 0.0), "virtual root"),
        "head_pivot": Node("head_pivot", "body", (520.0, 355.0), "virtual head pivot"),
    }
    layers = []
    source_hashes = {}
    canvas = None
    for entry in entries:
        name = entry["name"]
        if not isinstance(name, str) or not name or name in nodes:
            raise ValueError(f"Layer name is reserved or duplicated: {name!r}")
        source = (folder / entry["file"]).resolve()
        if not source.is_relative_to(folder) or source.suffix.lower() != ".png":
            raise ValueError(f"Layer {name!r} needs a PNG inside the rig folder")
        pivot = entry.get("pivot")
        if not isinstance(pivot, list) or len(pivot) != 2 or not all(
            isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
            for value in pivot
        ):
            raise ValueError(f"Layer {name!r} needs a finite absolute [x, y] pivot")
        with Image.open(source) as image:
            if image.mode != "RGBA":
                raise ValueError(f"Layer {name!r} must be RGBA")
            if canvas is None:
                canvas = image.size
            elif image.size != canvas:
                raise ValueError(f"Layer {name!r} is not on the common canvas")
            pixels = image.copy()
        parent = entry.get("parent") or "root"
        nodes[name] = Node(name, parent, tuple(pivot), str(entry.get("role", "")), pixels)
        layers.append(name)
        source_hashes[entry["file"]] = hashlib.sha256(source.read_bytes()).hexdigest()
    # Older body-only manifests refer to 'head'; the final head cuts use
    # 'head_pivot'. Both follow the same body node and absolute neck pivot.
    if "body" not in nodes:
        nodes["body"] = Node("body", "root", (552.0, 630.0), "virtual body")
    if "head" not in nodes:
        nodes["head"] = Node("head", "head_pivot", (520.0, 355.0), "virtual head alias")
    for node in nodes.values():
        if node.parent is not None and node.parent not in nodes:
            raise ValueError(f"Unknown parent {node.parent!r} for {node.name!r}")
    expected_path = folder / "neutral_dial_rig_flattened.png"
    with Image.open(expected_path) as expected:
        if expected.mode != "RGBA" or expected.size != canvas:
            raise ValueError("The flattened reference must be RGBA on the common canvas")
        reference = expected.copy()
    assert canvas is not None
    rig = Rig(nodes, layers, canvas, reference, source_hashes)
    pose_matrices(rig, 0.0)  # Validate the complete parent graph, including cycles.
    return rig


def pivot_rotation(pivot: tuple[float, float], angle: float) -> np.ndarray:
    radians = math.radians(angle)
    cosine, sine = math.cos(radians), math.sin(radians)
    x, y = pivot
    return np.array([[cosine, -sine, x - cosine * x + sine * y],
                     [sine, cosine, y - sine * x - cosine * y],
                     [0.0, 0.0, 1.0]], dtype=np.float64)


def sway(phase: float, amplitude: float, offset: float = 0.0) -> float:
    """A phase-offset cycle bounded by amplitude and anchored at exact neutral."""
    return amplitude * (math.sin(phase + offset) - math.sin(offset)) / (1.0 + abs(math.sin(offset)))


def local_motion(node: Node, phase: float, profile: str = "calm") -> np.ndarray:
    transform = IDENTITY.copy()
    name, role = node.name.lower(), node.role.lower()
    dialogue = profile == "dialogue"
    if name == "body":
        transform[1, 2] = -(3.0 if dialogue else 1.0) * (1.0 - math.cos(phase))
        if dialogue:
            transform[0, 2] = 7.0 * math.sin(phase)
            transform = transform @ pivot_rotation(node.pivot, 0.35 * math.sin(phase))
    angle = 0.0
    right = "screenright" in name
    if "hair_back" in name or role == "rear hair":
        angle = sway(phase, 1.8 if dialogue else 0.65, math.pi / 7.0 if right else 0.0)
    elif "hair_front" in name or role == "front_hair":
        offset = math.pi / 5.0 if right else (math.pi / 9.0 if "center" in name else 0.0)
        angle = sway(phase, 0.4 if dialogue else 0.2, offset)
    elif "sleeve" in name or role == "sleeve":
        amplitude = (0.2 if "back" in name else 0.4) if dialogue else 0.15
        angle = sway(phase, amplitude, math.pi / 5.0 if "back" in name else 0.0)
    if abs(angle) > 1e-12:
        transform = transform @ pivot_rotation(node.pivot, angle)
    return transform


def pose_matrices(rig: Rig, phase: float, profile: str = "calm") -> dict[str, np.ndarray]:
    """Absolute pivots live in the source canvas; inherit each parent once."""
    matrices: dict[str, np.ndarray] = {}
    pending: set[str] = set()

    def resolve(name: str) -> np.ndarray:
        if name in matrices:
            return matrices[name]
        if name in pending:
            raise ValueError(f"Parent cycle at {name!r}")
        pending.add(name)
        node = rig.nodes[name]
        parent = resolve(node.parent) if node.parent is not None else IDENTITY
        matrices[name] = parent @ local_motion(node, phase, profile)
        pending.remove(name)
        return matrices[name]

    for name in rig.nodes:
        resolve(name)
    return matrices


def render_pose(rig: Rig, matrices: dict[str, np.ndarray] | None = None,
                padding: int = 0) -> Image.Image:
    canvas = (rig.canvas[0] + 2 * padding, rig.canvas[1] + 2 * padding)
    composite = Image.new("RGBA", canvas, (0, 0, 0, 0))
    padding_matrix = IDENTITY.copy()
    padding_matrix[:2, 2] = padding
    for name in rig.layers:
        pixels = rig.nodes[name].image
        assert pixels is not None
        matrix = matrices[name] if matrices is not None else IDENTITY
        matrix = padding_matrix @ matrix
        # A neutral pose must not go through resampling, even for hidden RGB.
        if np.allclose(matrix, IDENTITY, rtol=0.0, atol=1e-12):
            moved = pixels
        else:
            inverse = np.linalg.inv(matrix)
            moved = pixels.transform(canvas, Image.Transform.AFFINE,
                                     tuple(inverse[:2].ravel()),
                                     resample=Image.Resampling.NEAREST,
                                     fillcolor=(0, 0, 0, 0))
        composite.alpha_composite(moved)
    return composite


def checker(size: tuple[int, int]) -> Image.Image:
    y, x = np.indices((size[1], size[0]))
    palette = np.array([(243, 247, 247, 255), (227, 236, 237, 255)], dtype=np.uint8)
    return Image.fromarray(palette[(x // 24 + y // 24) % 2])


def suspect_gaps(rig: Rig, pose: Image.Image, interior: Image.Image,
                 matrices: dict[str, np.ndarray], padding: int = 0) -> dict[str, Any]:
    # Ignore a 16px boundary around both the silhouette and every original
    # transparent gap. Only newly exposed pixels deep inside the expected
    # foreground are flagged; silhouette sway is expected, not a seam defect.
    padding_matrix = IDENTITY.copy()
    padding_matrix[:2, 2] = padding
    inverse = np.linalg.inv(padding_matrix @ matrices["body"])
    shifted = interior.transform(pose.size, Image.Transform.AFFINE,
                                 tuple(inverse[:2].ravel()),
                                 resample=Image.Resampling.NEAREST, fillcolor=0)
    mask = (np.asarray(shifted) >= 128) & (np.asarray(pose.getchannel("A")) < 128)
    count = int(mask.sum())
    if count:
        y, x = np.nonzero(mask)
        bbox = [int(x.min()), int(y.min()), int(x.max()) + 1, int(y.max()) + 1]
    else:
        bbox = None
    return {"suspect_internal_gap_pixels": count, "suspect_bbox": bbox}


def font(size: int) -> ImageFont.ImageFont:
    path = Path(__file__).resolve().parents[2] / "assets/fonts/BeVietnamPro-Regular.ttf"
    try:
        return ImageFont.truetype(str(path), size)
    except OSError:
        return ImageFont.load_default(size=size)


def make_previews(folder: Path, profile: str = "calm", output_directory: Path | None = None) -> dict[str, Any]:
    folder = folder.resolve()
    if profile not in {"calm", "dialogue"}:
        raise ValueError(f"Unknown motion profile: {profile}")
    output = folder if output_directory is None else output_directory.resolve()
    output.mkdir(parents=True, exist_ok=True)
    frame_count = 48 if profile == "dialogue" else FRAME_COUNT
    padding = 64 if profile == "dialogue" else 0
    excluded_boundary = 32 if profile == "dialogue" else 16
    gif_name = "motion_dialogue.gif" if profile == "dialogue" else "motion_preview.gif"
    sheet_name = "motion_dialogue_extremes.png" if profile == "dialogue" else "motion_extremes.png"
    rig = load_rig(folder)
    neutral = render_pose(rig)
    different = np.any(np.asarray(neutral) != np.asarray(rig.expected), axis=2)
    difference_count = int(different.sum())
    if difference_count:
        raise ValueError(f"Identity pose differs from flattened reference at {difference_count} RGBA pixels")
    foreground = rig.expected.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    interior = foreground.filter(ImageFilter.MinFilter(excluded_boundary * 2 + 1))
    background = checker((rig.canvas[0] + 2 * padding, rig.canvas[1] + 2 * padding))
    frames = []
    checks = []
    canvas_edge_pixels = 0
    for index in range(frame_count):
        phase = 2.0 * math.pi * index / frame_count
        matrices = pose_matrices(rig, phase, profile)
        pose = render_pose(rig, matrices, padding)
        check = suspect_gaps(rig, pose, interior, matrices, padding)
        check["frame"] = index
        checks.append(check)
        preview = background.copy()
        preview.alpha_composite(pose)
        scale = min(PREVIEW_SIZE[0] / preview.width, PREVIEW_SIZE[1] / preview.height)
        thumbnail = preview.resize((round(preview.width * scale), round(preview.height * scale)), Image.Resampling.NEAREST)
        frame = Image.new("RGB", PREVIEW_SIZE, "#f3f7f7")
        frame.paste(thumbnail.convert("RGB"), ((PREVIEW_SIZE[0] - thumbnail.width) // 2,
                                             (PREVIEW_SIZE[1] - thumbnail.height) // 2))
        frames.append(frame)
        alpha = np.asarray(pose.getchannel("A"))
        canvas_edge_pixels = max(canvas_edge_pixels, int((alpha[0,:]>0).sum() + (alpha[-1,:]>0).sum()
                                  + (alpha[:,0]>0).sum() + (alpha[:,-1]>0).sum()))
    first = render_pose(rig, pose_matrices(rig, 0.0, profile), padding)
    closing = render_pose(rig, pose_matrices(rig, 2.0 * math.pi, profile), padding)
    loop_difference = int(np.any(np.asarray(first) != np.asarray(closing), axis=2).sum())
    if loop_difference or canvas_edge_pixels:
        raise ValueError(f"Loop/edge check failed: {loop_difference} loop pixels, {canvas_edge_pixels} canvas-edge pixels")
    fixed_palette = frames[0].quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=fixed_palette, dither=Image.Dither.NONE) for frame in frames]
    indexed[0].save(output / gif_name, save_all=True, append_images=indexed[1:],
                    duration=DURATION_MS, loop=0, disposal=2, optimize=False)
    selected = [(0, "Neutral"), (frame_count//4, "Sway +"), (frame_count//2, "Breath peak"), (3*frame_count//4, "Sway -")]
    sheet = Image.new("RGB", (PREVIEW_SIZE[0] * len(selected), PREVIEW_SIZE[1] + 44), "#f3f7f7")
    draw = ImageDraw.Draw(sheet)
    for column, (index, title) in enumerate(selected):
        x = column * PREVIEW_SIZE[0]
        sheet.paste(frames[index], (x, 44))
        draw.text((x + 12, 5), title, font=font(17), fill="#1e3743")
        draw.text((x + 12, 27), f"Interior gap check: {checks[index]['suspect_internal_gap_pixels']} px",
                  font=font(11), fill="#3c515b")
    sheet.save(output / sheet_name)
    for filename, expected_hash in rig.source_hashes.items():
        if hashlib.sha256((folder / filename).read_bytes()).hexdigest() != expected_hash:
            raise ValueError(f"Source layer changed during preview: {filename}")
    with Image.open(output / gif_name) as animation:
        written_frames = animation.n_frames
        total_duration = 0
        for index in range(written_frames):
            animation.seek(index)
            total_duration += animation.info.get("duration", 0)
    worst = max(checks, key=lambda check: check["suspect_internal_gap_pixels"])
    result = {
        "folder": str(folder), "layers": len(rig.layers), "canvas": list(rig.canvas),
        "profile": profile, "output_directory": str(output),
        "loop_closing_rgba_pixel_differences": loop_difference,
        "canvas_edge_foreground_pixels": canvas_edge_pixels, "render_padding_px": padding,
        "neutral_rgba_pixel_differences": difference_count, "source_layers_unchanged": True,
        "frames_requested": frame_count, "frames_written": written_frames,
        "duration_per_frame_ms": DURATION_MS, "loop_duration_ms": total_duration,
        "preview_size": list(PREVIEW_SIZE), "filtering": "nearest", "blink_enabled": False,
        "motion_limits": {"rear_hair_degrees": 1.8 if profile=="dialogue" else 0.65,
                          "front_locks_degrees": 0.4 if profile=="dialogue" else 0.2,
                          "front_sleeve_degrees": 0.4 if profile=="dialogue" else 0.15,
                          "back_sleeve_degrees": 0.2 if profile=="dialogue" else 0.15,
                          "body_x_pixels": [-7,7] if profile=="dialogue" else [0,0],
                          "body_y_pixels": [-6,0] if profile=="dialogue" else [-2,0],
                          "body_rotation_degrees": 0.35 if profile=="dialogue" else 0.0},
        "gap_check": {"foreground_boundary_excluded_px": excluded_boundary, "worst_frame": worst,
                      "frames_with_suspect_gaps": [check for check in checks if check["suspect_internal_gap_pixels"]]},
        "outputs": [gif_name, sheet_name],
    }
    report_name = "motion_dialogue_report.json" if profile=="dialogue" else "motion_report.json"
    (output / report_name).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("rig_directory", type=Path)
    parser.add_argument("--profile", choices=("calm", "dialogue"), default="calm")
    parser.add_argument("--output-directory", type=Path)
    args = parser.parse_args()
    try:
        result = make_previews(args.rig_directory, args.profile, args.output_directory)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f"Preview failed: {error}\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
