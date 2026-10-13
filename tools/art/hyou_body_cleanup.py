"""Reassign visible purple hair chips captured by Hyou's sleeve envelopes."""
from __future__ import annotations

import numpy as np
from PIL import Image, ImageDraw


def _polygon(size: tuple[int, int], points: list[tuple[int, int]]) -> np.ndarray:
    image = Image.new("1", size)
    ImageDraw.Draw(image).polygon(points, fill=1)
    return np.asarray(image, dtype=bool)


def cleanup_body_labels(
    source: np.ndarray, labels: np.ndarray, foreground: np.ndarray
) -> np.ndarray:
    """Return labels with only bounded, purple sleeve-edge pixels on rear hair.

    IDs match cut_hyou_rig.build_body: rear hair 0/1, back sleeve 10,
    front sleeve 12. Dark sleeve outlines remain with the sleeves. Source
    pixels and the foreground silhouette are never modified.
    """
    h, w = labels.shape
    rgb = source[:, :, :3].astype(np.int16)
    purple = (
        (rgb[:, :, 0] > 55)
        & (rgb[:, :, 0] > rgb[:, :, 1] + 12)
        & (rgb[:, :, 2] * 10 > rgb[:, :, 1] * 18)
    )
    front_edge = _polygon((w, h), [
        (268,797), (282,794), (315,833), (335,854), (354,883),
        (374,911), (372,944), (331,932), (298,886), (270,845),
    ]) | _polygon((w, h), [
        (457,588), (476,588), (485,752), (466,730),
    ])
    back_edge = _polygon((w, h), [
        (759,693), (791,696), (798,750), (779,789),
        (771,842), (719,841), (712,783), (741,749),
    ])
    result = labels.copy()
    result[(labels == 12) & foreground & purple & front_edge] = 0
    result[(labels == 10) & foreground & purple & back_edge] = 1
    return result
