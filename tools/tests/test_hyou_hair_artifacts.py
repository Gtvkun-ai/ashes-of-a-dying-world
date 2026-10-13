"""Regression checks for reproduced colored chips and underpaint on Hyou hair."""
from pathlib import Path
import json

import numpy as np
import pytest
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "assets/graphics/characters/hyou/neutral_dial_rig_v1/dialogue_idle"


@pytest.fixture(scope="module")
def rig():
    manifest = json.loads((ART / "rig_manifest.json").read_text(encoding="utf-8"))
    ownership = np.load(ART / "source_ownership.npz")
    return manifest, ownership, np.array(Image.open(ROOT / manifest["source"]))


def owner_name(rig, x, y):
    return str(rig[1]["names"][rig[1]["owner"][y, x]])


def test_socket_pixels_follow_the_eye_instead_of_the_moving_bang(rig):
    for y in (264, 265):
        name = owner_name(rig, 519, y)
        assert name.startswith("eye_") and name.endswith("screenleft"), name


def test_original_purple_bang_edge_follows_its_lock(rig):
    for y in range(261, 266):
        assert owner_name(rig, 467, y) == "hair_front_screenleft"


def test_thigh_edge_does_not_sway_with_rear_hair(rig):
    assert owner_name(rig, 472, 697) == "leg_front_thigh"


def test_moving_hair_does_not_contain_disconnected_hidden_fill(rig):
    manifest, ownership, _ = rig
    for index, layer in enumerate(manifest["layers"]):
        if layer["role"] not in {"rear hair", "front_hair"}:
            continue
        pixels = np.array(Image.open(ART / layer["file"]))
        components, count = ndimage.label(pixels[:, :, 3] > 0, structure=np.ones((3, 3)))
        original = ownership["foreground"] & (ownership["owner"] == index)
        attached = set(np.unique(components[original]))
        orphan_ids = set(range(1, count + 1)) - attached
        assert not orphan_ids, f"{layer['name']}: detached hidden-fill components {orphan_ids}"


def test_ear_side_underpaint_retains_hair_color(rig):
    manifest, _, source = rig
    face = next(layer for layer in manifest["layers"] if layer["name"] == "head_face")
    pixels = np.array(Image.open(ART / face["file"]))
    for x, y in ((429, 271), (430, 279)):
        assert np.array_equal(pixels[y, x, :3], source[y, x, :3]), (x, y, pixels[y, x].tolist())


def test_left_bang_seam_underpaint_keeps_hair_material(rig):
    manifest, _, source = rig
    face = next(layer for layer in manifest["layers"] if layer["name"] == "head_face")
    pixels = np.array(Image.open(ART / face["file"]))
    for x, y in ((465, 245), (466, 258), (467, 264), (467, 268), (467, 271),
                 (467, 276), (468, 279), (477, 300), (474, 306), (480, 307)):
        assert np.array_equal(pixels[y, x, :3], source[y, x, :3]), (x, y, pixels[y, x].tolist())


def test_legitimate_revealed_skin_remains_under_the_bangs(rig):
    manifest, _, _ = rig
    face = next(layer for layer in manifest["layers"] if layer["name"] == "head_face")
    pixels = np.array(Image.open(ART / face["file"]))
    for x, y in ((487, 242),):
        red, green, blue, alpha = pixels[y, x]
        assert red >= 218 and green >= 195 and blue >= 235 and alpha == 255


def test_hair_cleanup_preserves_the_original_neutral_art(rig):
    manifest, _, source = rig
    clean = np.zeros_like(source)
    foreground = source[:, :, 3] >= 128
    clean[foreground, :3] = source[foreground, :3]
    clean[foreground, 3] = 255
    composite = Image.new("RGBA", (source.shape[1], source.shape[0]))
    for layer in manifest["layers"]:
        composite.alpha_composite(Image.open(ART / layer["file"]))
    assert np.array_equal(np.asarray(composite), clean)
