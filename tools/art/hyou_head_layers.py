"""Pixel-preserving head cutouts for Hyou's 1024 x 1536 neutral portrait.

Coordinates and pivots use the source canvas, with screen-side names.  ``mask``
records ownership of original pixels; ``fill_mask`` records sampled, hidden
repairs.  Source ownership is exclusive.  Returned layers are in draw order.
No original portrait or existing rig asset is modified by this module.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def _polygon(shape: tuple[int, int], points: list[tuple[int, int]]) -> np.ndarray:
    image = Image.new("1", (shape[1], shape[0]))
    ImageDraw.Draw(image).polygon(points, fill=1)
    return np.array(image, dtype=bool)


def _grow(mask: np.ndarray, radius: int) -> np.ndarray:
    image = Image.fromarray(mask.astype(np.uint8) * 255)
    return np.asarray(image.filter(ImageFilter.MaxFilter(radius * 2 + 1))) > 0


def _sample_skin(source: np.ndarray, face: np.ndarray, repair: np.ndarray) -> np.ndarray:
    """Fill extracted features using nearest visible, unextracted skin pixels."""
    rgb = source[..., :3].astype(np.int16)
    # Exclude nearby feature shadows and eye-white fringes as well as the
    # features themselves.  Those are pale enough to resemble skin, but using
    # them as seeds would leave stripe-shaped remnants in the repair patch.
    skin = (face & ~_grow(repair, 8) & (rgb[..., 0] >= 218)
            & (rgb[..., 1] >= 195) & (rgb[..., 2] >= 235)
            & (rgb[..., 0] - rgb[..., 1] > 8)
            & (rgb[..., 0] - rgb[..., 1] < 26))
    samples = np.argwhere(skin)
    if not len(samples):
        raise ValueError("The source has no skin samples inside the face mask.")
    out = np.zeros_like(source)
    for y, x in np.argwhere(repair):
        delta = samples - (y, x)
        nearest = samples[np.argmin(np.sum(delta * delta, axis=1))]
        out[y, x, :3] = source[tuple(nearest)][:3]
        out[y, x, 3] = 255
    return out


def build_head_layers(source: np.ndarray) -> list[dict]:
    """Return head layers with full-canvas RGBA pixels and provenance masks."""
    if source.shape != (1536, 1024, 4) or source.dtype != np.uint8:
        raise ValueError("Expected an uint8 RGBA 1024 x 1536 source portrait.")
    shape = source.shape[:2]
    foreground = source[..., 3] >= 128
    rgb = source[..., :3].astype(np.int16)
    # Saturated purple includes bright pink-purple highlights.  Do not cap R:
    # the portrait's highlights can be brighter than the main hair palette.
    hair_color = (rgb[..., 1] < 110) & (rgb[..., 2] > 40) & (rgb[..., 2] > rgb[..., 0] + 15)

    # The bottom follows the visible chin/neck and the two short front locks.
    # Long rear strands, shoulder skin and the dark collar are outside this cut.
    head_region = _polygon(shape, [
        (415,139),(442,112),(489,97),(543,96),(581,110),(610,134),
        (638,177),(662,224),(672,301),(656,330),(630,349),(591,361),
        (562,377),(529,354),(513,361),(492,358),(481,355),(478,365),
        (455,377),(435,347),(424,319),(414,290),(399,272),(380,250),
        (372,227),(412,232),(409,198),
    ]) & foreground

    band_region = _polygon(shape, [
        (415,90),(441,76),(462,74),(468,62),(486,50),(502,51),
        (516,37),(544,35),(561,49),(566,65),(580,60),(597,62),
        (613,77),(615,99),(631,103),(637,123),(648,133),(648,155),
        (657,168),(653,194),(639,196),(628,165),(620,149),(605,133),
        (589,122),(573,110),(558,102),(526,98),(497,99),(470,109),
        (447,122),(429,142),(421,142),(418,124),
    ])
    band_light = band_region & foreground & (rgb[..., 0] > 140) & (rgb[..., 1] > 95) & (rgb[..., 2] > 150)
    band = _grow(band_light, 3) & band_region & foreground
    crystal_left = _polygon(shape, [(415,78),(423,155),(446,181),(318,226),(316,219)]) & foreground
    crystal_right = _polygon(shape, [(672,143),(698,228),(715,289),(642,242),(638,211)]) & foreground

    # The internal seams follow existing dark hair streaks.  These are source
    # cuts, not generated locks; hidden skin behind the bangs is not invented.
    front_left = _polygon(shape, [
        (433,184),(461,154),(488,138),(495,141),(486,199),(464,238),
        (468,282),(481,307),(495,316),(478,313),(479,347),(488,377),
        (474,365),(451,338),(433,307),(429,268),(430,218),
    ]) & head_region & hair_color
    front_center = _polygon(shape, [
        (492,140),(526,137),(552,158),(579,190),(587,218),(590,249),
        (582,269),(570,286),(553,300),(533,296),(521,275),(517,250),
        (519,225),(520,200),(501,224),(482,249),(467,255),(473,224),(481,203),
    ]) & head_region & hair_color
    front_right = _polygon(shape, [
        (560,141),(578,148),(600,171),(620,183),(642,222),(654,260),
        (663,296),(660,317),(645,332),(626,335),(618,354),(589,372),
        (560,378),(569,361),(586,342),(597,328),(607,301),(607,263),
        (604,234),(592,213),(587,190),(574,171),
    ]) & head_region & hair_color

    # These two shaded socket pixels are beneath the bang, not part of it.
    # The narrow original purple edge on the left belongs to that moving lock.
    front_center[264:266,519] = False
    front_left[261:266,467] = head_region[261:266,467] & hair_color[261:266,467]

    face_region = _polygon(shape, [
        (470,251),(491,225),(520,201),(531,202),(536,245),(548,278),
        (572,251),(592,242),(603,275),(593,307),(578,324),(555,340),
        (527,353),(513,362),(490,351),(475,343),(462,315),(466,277),
    ]) | _polygon(shape, [(374,228),(414,234),(435,245),(434,282),(426,287),(397,260)])
    face_region &= head_region

    eye_left = _polygon(shape, [
        (463,249),(476,249),(492,255),(514,262),(534,271),(537,278),
        (526,289),(511,296),(490,295),(478,289),(474,277),(469,264),
    ]) & head_region
    eye_left[264:266,519] = head_region[264:266,519] & foreground[264:266,519]
    eye_right = _polygon(shape, [
        (544,282),(575,278),(601,271),(611,266),(609,281),(592,298),
        (578,311),(562,314),(549,308),
    ]) & head_region
    brow_left = _polygon(shape, [(487,227),(502,229),(531,240),(533,250),(521,247),(490,238)])
    brow_right = _polygon(shape, [(574,260),(593,259),(595,268),(577,270)])
    # Include the pale feathered pixels around each brow, not just its dark
    # core.  Otherwise hiding the brow leaves its original lavender shadow.
    brow_left &= face_region
    brow_right &= face_region
    mouth = _polygon(shape, [(526,327),(539,327),(539,333),(526,333)]) & face_region & (rgb[..., 0] < 205) & (rgb[..., 1] < 190)

    # Resolve source ownership before constructing any repaired textures.
    candidates = [
        ("headband", band, (532,102), "accessory"),
        ("head_crystal_screenleft", crystal_left, (416,160), "accessory"),
        ("head_crystal_screenright", crystal_right, (658,225), "accessory"),
        ("hair_front_screenleft", front_left, (473,166), "front_hair"),
        ("hair_front_center", front_center, (530,167), "front_hair"),
        ("hair_front_screenright", front_right, (591,171), "front_hair"),
        ("eye_open_screenleft", eye_left, (500,276), "eye"),
        ("eye_open_screenright", eye_right, (568,296), "eye"),
        ("eyebrow_screenleft", brow_left, (507,234), "eyebrow"),
        ("eyebrow_screenright", brow_right, (584,265), "eyebrow"),
        ("mouth_neutral", mouth, (532,330), "mouth"),
        ("head_face", face_region, (519,353), "face_foundation"),
    ]
    claimed = np.zeros(shape, dtype=bool)
    layers_by_name = {}
    for name, candidate, pivot, role in candidates:
        owned = candidate & foreground & ~claimed
        claimed |= owned
        pixels = np.zeros_like(source)
        pixels[owned] = source[owned]
        pixels[owned, 3] = 255
        layers_by_name[name] = dict(name=name, pixels=pixels, mask=owned,
                                    fill_mask=np.zeros(shape, dtype=bool),
                                    pivot=pivot, parent="head_pivot", role=role)
    scalp = head_region & ~claimed
    scalp_pixels = np.zeros_like(source)
    scalp_pixels[scalp] = source[scalp]
    scalp_pixels[scalp, 3] = 255
    layers_by_name["head_scalp"] = dict(name="head_scalp", pixels=scalp_pixels,
        mask=scalp, fill_mask=np.zeros(shape,dtype=bool), pivot=(520,157),
        parent="head_pivot", role="rear_scalp")

    feature_names = ["eye_open_screenleft", "eye_open_screenright",
                     "eyebrow_screenleft", "eyebrow_screenright", "mouth_neutral"]
    repairs = np.zeros(shape, dtype=bool)
    for name in feature_names:
        repairs |= layers_by_name[name]["mask"]
    face = layers_by_name["head_face"]
    repair_pixels = _sample_skin(source, face["mask"], repairs)
    face["pixels"][repairs] = repair_pixels[repairs]
    face["fill_mask"] = repairs

    # Provide a narrow hidden seam overlap beneath the three moving locks.
    # Every fill coordinate is owned by a later opaque front-hair layer, so
    # these repairs cannot change the neutral composite.  The scalp uses the
    # source color at the same coordinate; the face extends sampled skin only.
    front_union = np.zeros(shape, dtype=bool)
    for name in ("hair_front_screenleft", "hair_front_center", "hair_front_screenright"):
        front_union |= layers_by_name[name]["mask"]
    scalp_layer = layers_by_name["head_scalp"]
    scalp_overlap = _grow(scalp_layer["mask"], 5) & front_union
    scalp_layer["pixels"][scalp_overlap] = source[scalp_overlap]
    scalp_layer["pixels"][scalp_overlap, 3] = 255
    scalp_layer["fill_mask"] = scalp_overlap
    face_overlap = _grow(face["mask"] | repairs, 4) & front_union
    fringe_repair_pixels = _sample_skin(source, face["mask"], face_overlap | repairs)
    # Eye-repair dilation reaches across the adjacent hair seam. Outside the
    # traced face and beside the ear, its backing material is original hair.
    # Keep actual newly revealed skin inside the anatomical face region.
    hair_material = ((np.arange(shape[1])[None,:] < 450)
                     | (layers_by_name["hair_front_screenleft"]["mask"] & ~face_region))
    # Follow the source-hair boundary below the lash. These 44 hidden backing
    # pixels otherwise become detached pale dots/strips during the idle loop;
    # none connects to original visible face/eye skin across the 48 poses.
    for x, top, bottom in (
        (467,268,277),(468,277,283),(469,283,285),(470,285,287),
        (471,287,289),(472,289,291),(473,291,293),(474,293,295),
        (475,295,297),(476,297,299),(477,299,301),(478,301,304),
        (479,303,306),(480,305,308),(474,306,307),(475,307,308),
    ):
        hair_material[top:bottom,x] = True
    hair_underpaint = face_overlap & hair_material
    fringe_repair_pixels[hair_underpaint,:3] = source[hair_underpaint,:3]
    fringe_repair_pixels[hair_underpaint,3] = 255
    face["pixels"][face_overlap] = fringe_repair_pixels[face_overlap]
    face["fill_mask"] |= face_overlap
    face["hair_underpaint_mask"] = hair_material

    # Split only original, unambiguous eye colors.  The cool blue gradient
    # includes the darker blue pupil; near-black original pixels are the lash.
    # Pale boundary/socket pixels remain in eye_open_* to avoid inventing iris
    # geometry or creating a new eye silhouette.  Children share its transform.
    blue_eye = ((rgb[..., 2] > rgb[..., 0] + 30)
                & (rgb[..., 2] > rgb[..., 1] + 20)
                & (rgb[..., 1] > rgb[..., 0] + 8)
                & (rgb[..., 1] > 15))
    dark_lash = (rgb[..., 0] < 40) & (rgb[..., 1] < 40) & (rgb[..., 2] < 65)
    for side, iris_pivot, lash_pivot in (
        ("screenleft", (499,278), (500,264)),
        ("screenright", (564,299), (576,284)),
    ):
        eye_name = "eye_open_" + side
        eye = layers_by_name[eye_name]
        iris = eye["mask"] & blue_eye
        lash = eye["mask"] & dark_lash & ~iris
        for name, owned, pivot, role in (
            ("eye_iris_" + side, iris, iris_pivot, "eye_iris"),
            ("eye_lash_" + side, lash, lash_pivot, "eye_lash"),
        ):
            pixels = np.zeros_like(source)
            pixels[owned] = source[owned]
            pixels[owned,3] = 255
            layers_by_name[name] = dict(name=name, pixels=pixels, mask=owned,
                fill_mask=np.zeros(shape,dtype=bool), pivot=pivot,
                parent=eye_name, role=role)
        eye["mask"] &= ~(iris | lash)
        eye["pixels"][iris | lash] = 0
        eye["role"] = "eye_white_patch"

    order = ["head_scalp", "head_face", "headband",
             "head_crystal_screenleft", "head_crystal_screenright",
             "eyebrow_screenleft", "eyebrow_screenright",
             "eye_open_screenleft", "eye_iris_screenleft", "eye_lash_screenleft",
             "eye_open_screenright", "eye_iris_screenright", "eye_lash_screenright", "mouth_neutral",
             "hair_front_screenleft", "hair_front_center", "hair_front_screenright"]
    return [layers_by_name[name] for name in order]


def summarize_head_layers(layers: list[dict]) -> dict:
    ownership = np.zeros(layers[0]["mask"].shape, dtype=np.uint8)
    fill_union = np.zeros(ownership.shape, dtype=bool)
    for layer in layers:
        ownership += layer["mask"].astype(np.uint8)
        fill_union |= layer["fill_mask"]
    return dict(source_mask=ownership > 0, fill_mask=fill_union,
                source_pixels=int((ownership > 0).sum()),
                fill_pixels=int(fill_union.sum()),
                duplicate_source_pixels=int((ownership > 1).sum()),
                layers=[dict(name=l["name"], source_pixels=int(l["mask"].sum()),
                             fill_pixels=int(l["fill_mask"].sum()),
                             pivot=list(l["pivot"]), parent=l["parent"], role=l["role"])
                        for l in layers])


def _checker(size: tuple[int,int], cell: int=16) -> Image.Image:
    y, x = np.indices((size[1], size[0]))
    values = np.where((x // cell + y // cell) % 2 == 0, 42, 66).astype(np.uint8)
    rgba = np.stack([values,values,values,np.full_like(values,255)],axis=-1)
    return Image.fromarray(rgba)


def write_review(source: np.ndarray, layers: list[dict], output: Path) -> None:
    output.mkdir(parents=True, exist_ok=True)
    metrics = summarize_head_layers(layers)
    clean_metrics = {k:v for k,v in metrics.items() if k not in {"source_mask","fill_mask"}}
    (output / "metrics.json").write_text(json.dumps(clean_metrics,indent=2), encoding="utf-8")
    np.save(output / "head_source_mask.npy", metrics["source_mask"])
    np.save(output / "head_fill_mask.npy", metrics["fill_mask"])
    composed = Image.new("RGBA", (1024,1536))
    crop = (300,25,730,395)
    cell_width, cell_height = 300,286
    rows = (len(layers) + 2 + 3) // 4
    sheet = Image.new("RGBA", (cell_width * 4, cell_height * rows), (23,27,35,255))
    draw = ImageDraw.Draw(sheet)
    review_items = []
    for layer in layers:
        image = Image.fromarray(layer["pixels"])
        image.save(output / (layer["name"] + ".png"))
        composed = Image.alpha_composite(composed,image)
        review_items.append((layer["name"],image))
    review_items = [("source",Image.fromarray(source)),("head composite",composed)] + review_items
    for index,(name,image) in enumerate(review_items):
        tile = _checker((cell_width,252))
        thumb = image.crop(crop).resize((282,243),Image.Resampling.NEAREST)
        tile.alpha_composite(thumb,(9,4))
        x,y = (index % 4) * cell_width,(index // 4) * cell_height
        sheet.alpha_composite(tile,(x,y+25))
        draw.text((x+8,y+7),name,fill=(230,236,244,255))
    sheet.save(output / "contact_sheet.png")
    composed.save(output / "head_recomposed.png")
    masked_source = np.zeros_like(source)
    mask = metrics["source_mask"]
    masked_source[mask] = source[mask]
    masked_source[mask,3] = 255
    masked = Image.fromarray(masked_source)
    masked.save(output / "head_source_expected.png")
    comparison = _checker((860,740))
    comparison.alpha_composite(masked.crop(crop).resize((430,370),Image.Resampling.NEAREST),(0,0))
    comparison.alpha_composite(composed.crop(crop).resize((430,370),Image.Resampling.NEAREST),(430,0))
    comparison.alpha_composite(Image.fromarray(layers[1]["pixels"]).crop(crop).resize((430,370),Image.Resampling.NEAREST),(0,370))
    ImageDraw.Draw(comparison).text((440,380),"Foundation has sampled skin beneath feature patches.",fill=(240,240,240,255))
    comparison.save(output / "head_review.png")
    print(json.dumps(clean_metrics,indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source",type=Path,default=Path("assets/graphics/characters/hyou/neutral_dial.png"))
    parser.add_argument("--output",type=Path,default=Path("tmp/hyou-head-review"))
    args = parser.parse_args()
    portrait = np.asarray(Image.open(args.source).convert("RGBA"))
    write_review(portrait,build_head_layers(portrait),args.output)
