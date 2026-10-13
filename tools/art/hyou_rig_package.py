#!/usr/bin/env python3
"""Package Hyou's already-cut RGBA rig without repainting any source pixels.

Usage:
    python tools/art/hyou_rig_package.py PATH_TO_NEUTRAL_DIAL_RIG_V1

The folder must contain rig_manifest.json and its referenced RGBA layer PNGs.
The manifest's layers are ordered back to front; that order is authoritative.
Outputs are cropped PNGs, crop_manifest.json, a transparent flattened PNG,
an editable 8-bit RGB+alpha PSD, OpenRaster, and a pale-checker contact sheet.
Only Pillow and the Python standard library are required.

Format references:
https://www.adobe.com/devnet-apps/photoshop/fileformatashtml/
https://www.openraster.org/baseline/file-layout-spec.html
https://www.openraster.org/baseline/layer-stack-spec.html
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import io
import json
import math
from pathlib import Path
import struct
import sys
from typing import Any
import xml.etree.ElementTree as ET
import zipfile

from PIL import Image, ImageDraw, ImageFont


STEM = "neutral_dial_rig"


@dataclass
class Layer:
    entry: dict[str, Any]
    bbox: tuple[int, int, int, int]
    alpha_bbox: tuple[int, int, int, int] | None
    image: Image.Image
    crop_file: str

    @property
    def name(self) -> str:
        return self.entry["name"]


def load_layers(folder: Path) -> tuple[list[Layer], Image.Image]:
    """Read and validate every source before creating any package output."""
    manifest = json.loads((folder / "rig_manifest.json").read_text(encoding="utf-8-sig"))
    entries = manifest.get("layers")
    if not isinstance(entries, list) or not entries or len(entries) > 32767:
        raise ValueError("rig_manifest.json must contain 1..32767 layers in back-to-front order")
    layers: list[Layer] = []
    names: set[str] = set()
    filenames: set[str] = set()
    composite: Image.Image | None = None
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            raise ValueError(f"Layer {index} must be an object")
        name, filename, pivot = entry.get("name"), entry.get("file"), entry.get("pivot")
        if not isinstance(name, str) or not name or name in names:
            raise ValueError(f"Layer {index} needs a unique, nonempty name")
        if not isinstance(filename, str) or not filename:
            raise ValueError(f"Layer {name!r} needs a relative PNG file path")
        source = (folder / filename).resolve()
        if Path(filename).is_absolute() or not source.is_relative_to(folder):
            raise ValueError(f"Layer {name!r} must reference a file inside the rig folder")
        if source.suffix.lower() != ".png":
            raise ValueError(f"Layer {name!r} must reference a PNG")
        if not isinstance(pivot, list) or len(pivot) != 2 or not all(
            isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
            for value in pivot
        ):
            raise ValueError(f"Layer {name!r} needs a finite [x, y] pivot")
        crop_file = f"cropped/{source.name}"
        if crop_file.casefold() in filenames:
            raise ValueError(f"Layer PNG filenames must be unique: {source.name}")
        with Image.open(source) as image:
            if image.mode != "RGBA":
                raise ValueError(f"Layer {name!r} is {image.mode}, expected exact 8-bit RGBA")
            image.load()
            if composite is None:
                if not (0 < image.width <= 30000 and 0 < image.height <= 30000):
                    raise ValueError("The canvas is outside Photoshop PSD's 1..30000 pixel limit")
                composite = Image.new("RGBA", image.size, (0, 0, 0, 0))
            elif image.size != composite.size:
                raise ValueError(f"Layer {name!r} has canvas {image.size}, expected {composite.size}")
            alpha_bbox = image.getchannel("A").getbbox()
            # Empty optional parts remain named, editable 1x1 transparent layers.
            bbox = alpha_bbox or (0, 0, 1, 1)
            crop = image.crop(bbox)
            composite = Image.alpha_composite(composite, image)
        layers.append(Layer(dict(entry), bbox, alpha_bbox, crop, crop_file))
        names.add(name)
        filenames.add(crop_file.casefold())
    assert composite is not None
    return layers, composite


def crop_manifest(layers: list[Layer], canvas: tuple[int, int]) -> dict[str, Any]:
    entries = []
    for layer in layers:
        left, top, right, bottom = layer.bbox
        entry = dict(layer.entry)
        entry.update({
            "source_file": layer.entry["file"],
            "file": layer.crop_file,
            "bbox": [left, top, right, bottom],
            "offset": [left, top],
            "size": [right - left, bottom - top],
            "pivot_local": [layer.entry["pivot"][0] - left, layer.entry["pivot"][1] - top],
            "alpha_bbox": list(layer.alpha_bbox) if layer.alpha_bbox is not None else None,
            "empty": layer.alpha_bbox is None,
        })
        entries.append(entry)
    return {
        "schema_version": 1,
        "source_manifest": "rig_manifest.json",
        "canvas": list(canvas),
        "layer_order": "back_to_front",
        "bbox_convention": "[left, top, right, bottom], right and bottom exclusive",
        "layers": entries,
    }


def png_bytes(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", compress_level=9)
    return buffer.getvalue()


def packbits(row: bytes) -> bytes:
    """Encode one independent PSD RLE scanline (TIFF/Macintosh PackBits)."""
    encoded = bytearray()
    position = 0
    while position < len(row):
        run = 1
        while position + run < len(row) and run < 128 and row[position + run] == row[position]:
            run += 1
        if run >= 3:
            encoded.extend((257 - run, row[position]))
            position += run
            continue
        start = position
        position += run
        while position < len(row) and position - start < 128:
            run = 1
            while position + run < len(row) and run < 3 and row[position + run] == row[position]:
                run += 1
            if run >= 3:
                break
            position += min(run, 128 - (position - start))
        literal = row[start:position]
        encoded.append(len(literal) - 1)
        encoded.extend(literal)
    return bytes(encoded)


def rle_rows(channel: Image.Image) -> list[bytes]:
    pixels = channel.tobytes()
    rows = [packbits(pixels[y * channel.width:(y + 1) * channel.width]) for y in range(channel.height)]
    if any(len(row) > 65535 for row in rows):
        raise ValueError("An RLE scanline exceeds Photoshop PSD's 16-bit byte-count limit")
    return rows


def rle_channel(channel: Image.Image) -> bytes:
    rows = rle_rows(channel)
    counts = struct.pack(f">{len(rows)}H", *(len(row) for row in rows))
    return struct.pack(">H", 1) + counts + b"".join(rows)


def layer_name_data(name: str) -> bytes:
    legacy = name.encode("mac_roman", errors="replace")[:255]
    pascal = bytes([len(legacy)]) + legacy
    pascal += b"\0" * (-len(pascal) % 4)
    unicode_name = name.encode("utf-16-be")
    unicode_data = struct.pack(">I", len(unicode_name) // 2) + unicode_name
    tagged = b"8BIMluni" + struct.pack(">I", len(unicode_data)) + unicode_data
    tagged += b"\0" * (-len(unicode_data) % 2)
    # Zero-length mask and blend-range data precede the two name encodings.
    return struct.pack(">II", 0, 0) + pascal + tagged


def write_psd(path: Path, layers: list[Layer], composite: Image.Image) -> None:
    """Write a layered Photoshop PSD with exact, unassociated RGBA samples."""
    records = []
    channel_images = []
    # PSD records, unlike OpenRaster XML, run bottom to top. Photoshop's layer
    # palette displays that stored list in reverse, with the front part on top.
    for layer in layers:
        red, green, blue, alpha = layer.image.split()
        channels = [rle_channel(channel) for channel in (alpha, red, green, blue)]
        left, top, right, bottom = layer.bbox
        record = struct.pack(">4iH", top, left, bottom, right, 4)
        record += b"".join(struct.pack(">hI", channel_id, len(data))
                           for channel_id, data in zip((-1, 0, 1, 2), channels))
        record += b"8BIMnorm" + bytes((255, 0, 8, 0))
        extra = layer_name_data(layer.name)
        record += struct.pack(">I", len(extra)) + extra
        records.append(record)
        channel_images.extend(channels)
    # Negative count marks the fourth merged channel as document transparency.
    layer_info = struct.pack(">h", -len(layers)) + b"".join(records) + b"".join(channel_images)
    layer_info += b"\0" * (-len(layer_info) % 2)
    layer_and_mask = struct.pack(">I", len(layer_info)) + layer_info + struct.pack(">I", 0)
    # Photoshop's compatibility preview stores color matted against white and
    # its independent transparency channel. Editable layers stay straight RGBA.
    # Supplying straight RGB here makes PSD readers unmatte it a second time.
    preview = Image.alpha_composite(Image.new("RGBA", composite.size, (255, 255, 255, 255)), composite)
    preview.putalpha(composite.getchannel("A"))
    merged_rows = [row for channel in preview.split() for row in rle_rows(channel)]
    merged = struct.pack(">H", 1)
    merged += struct.pack(f">{len(merged_rows)}H", *(len(row) for row in merged_rows))
    merged += b"".join(merged_rows)
    header = struct.pack(">4sH6sHIIHH", b"8BPS", 1, b"\0" * 6, 4,
                         composite.height, composite.width, 8, 3)
    with path.open("wb") as output:
        output.write(header)
        output.write(struct.pack(">II", 0, 0))  # Color-mode data and image resources.
        output.write(struct.pack(">I", len(layer_and_mask)))
        output.write(layer_and_mask)
        output.write(merged)


def write_openraster(path: Path, layers: list[Layer], composite: Image.Image) -> None:
    root = ET.Element("image", version="0.0.6", w=str(composite.width),
                      h=str(composite.height), name="Hyou neutral dialogue rig")
    stack = ET.SubElement(root, "stack")
    files = []
    for index, layer in enumerate(reversed(layers)):
        source = f"data/layer{index:03d}.png"
        ET.SubElement(stack, "layer", name=layer.name, src=source,
                      x=str(layer.bbox[0]), y=str(layer.bbox[1]), opacity="1.0",
                      visibility="visible", **{"composite-op": "svg:src-over"})
        files.append((source, png_bytes(layer.image)))
    thumbnail = composite.copy()
    thumbnail.thumbnail((256, 256), Image.Resampling.NEAREST)
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("mimetype", b"image/openraster", compress_type=zipfile.ZIP_STORED)
        archive.writestr("stack.xml", ET.tostring(root, encoding="utf-8", xml_declaration=True))
        for source, data in files:
            archive.writestr(source, data)
        archive.writestr("mergedimage.png", png_bytes(composite))
        archive.writestr("Thumbnails/thumbnail.png", png_bytes(thumbnail))


def font(size: int) -> ImageFont.ImageFont:
    bundled = Path(__file__).resolve().parents[2] / "assets/fonts/BeVietnamPro-Regular.ttf"
    try:
        return ImageFont.truetype(str(bundled), size)
    except OSError:
        return ImageFont.load_default(size=size)


def contact_sheet(layers: list[Layer]) -> Image.Image:
    columns = min(4, len(layers))
    rows = math.ceil(len(layers) / columns)
    cell_w, cell_h, margin, title_h = 256, 340, 20, 60
    sheet = Image.new("RGBA", (margin * 2 + cell_w * columns, title_h + margin + cell_h * rows))
    draw = ImageDraw.Draw(sheet)
    checker = 16
    for y in range(0, sheet.height, checker):
        for x in range(0, sheet.width, checker):
            color = (243, 247, 247, 255) if ((x // checker + y // checker) % 2 == 0) else (227, 236, 237, 255)
            draw.rectangle((x, y, x + checker - 1, y + checker - 1), fill=color)
    label_font = font(14)
    draw.text((margin, 13), "Hyou | neutral dialogue rig layers", font=font(22), fill=(30, 55, 67, 255))
    draw.text((margin, 41), "Back to front; previews fitted to each cell", font=font(12), fill=(60, 81, 91, 255))
    for index, layer in enumerate(layers):
        x = margin + (index % columns) * cell_w
        y = title_h + (index // columns) * cell_h
        preview = layer.image.copy()
        preview.thumbnail((cell_w - 24, cell_h - 80), Image.Resampling.NEAREST)
        sheet.alpha_composite(preview, (x + (cell_w - preview.width) // 2,
                                         y + (cell_h - 80 - preview.height) // 2))
        draw.rectangle((x + 4, y + 4, x + cell_w - 5, y + cell_h - 7), outline=(181, 198, 203, 255))
        words = f"{index + 1:02d}  {layer.name}".split()
        lines = [""]
        for word in words:
            candidate = (lines[-1] + " " + word).strip()
            if draw.textlength(candidate, font=label_font) > cell_w - 24 and lines[-1]:
                lines.append(word)
            else:
                lines[-1] = candidate
        draw.multiline_text((x + 12, y + cell_h - 70), "\n".join(lines), font=label_font,
                            fill=(30, 55, 67, 255), spacing=2)
        draw.text((x + 12, y + cell_h - 24), f"{layer.image.width} x {layer.image.height} px",
                  font=font(11), fill=(60, 81, 91, 255))
    return sheet


def package(folder: Path) -> dict[str, Any]:
    folder = folder.resolve()
    if not folder.is_dir():
        raise ValueError(f"Rig folder does not exist: {folder}")
    layers, composite = load_layers(folder)
    manifest = crop_manifest(layers, composite.size)
    (folder / "cropped").mkdir(exist_ok=True)
    for layer in layers:
        (folder / layer.crop_file).write_bytes(png_bytes(layer.image))
    (folder / "crop_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (folder / f"{STEM}_flattened.png").write_bytes(png_bytes(composite))
    write_psd(folder / f"{STEM}.psd", layers, composite)
    write_openraster(folder / f"{STEM}.ora", layers, composite)
    (folder / "contact_sheet.png").write_bytes(png_bytes(contact_sheet(layers)))
    return {"folder": str(folder), "layers": len(layers), "canvas": list(composite.size),
            "outputs": ["cropped/", "crop_manifest.json", f"{STEM}_flattened.png",
                        f"{STEM}.psd", f"{STEM}.ora", "contact_sheet.png"]}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("output_directory", type=Path, help="Rig folder containing rig_manifest.json and layer PNGs")
    args = parser.parse_args()
    try:
        result = package(args.output_directory)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f"Package failed: {error}\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
