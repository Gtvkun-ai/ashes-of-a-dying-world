"""Source-pixel-preserving Hyou cutout and modest-motion QA assets."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/graphics/characters/hyou/neutral_dial.png"
DEFAULT_OUTPUT = SOURCE.parent / "neutral_dial_rig_v1"


def polygon(size, points):
    image = Image.new("1", size)
    ImageDraw.Draw(image).polygon(points, fill=1)
    return np.asarray(image, dtype=bool)


def build_body(source):
    h, w = source.shape[:2]
    yy, xx = np.indices((h, w))
    fg = source[:, :, 3] >= 128
    labels = np.where(xx < 560, 0, 1).astype(np.int16)
    definitions = [
        dict(name="hair_back_screenleft", pivot=[415, 237], parent="head_pivot", role="rear hair"),
        dict(name="hair_back_screenright", pivot=[659, 246], parent="head_pivot", role="rear hair"),
    ]

    def add(name, points, pivot, parent, role):
        mask = polygon((w, h), points) if isinstance(points, list) else points
        labels[mask & fg] = len(definitions)
        definitions.append(dict(name=name, pivot=pivot, parent=parent, role=role))

    # Contours here follow the original visible occlusion edges, not image boxes.
    add("leg_back_thigh", [(612,750),(657,681),(681,651),(711,668),(724,711),(722,773),(713,829),(701,894),(683,963),(670,1017),(613,1030),(580,1010),(594,925),(612,838)], [665,690], "body", "back thigh")
    add("leg_back_calf", [(580,990),(674,988),(674,1040),(680,1067),(680,1138),(670,1180),(656,1199),(623,1245),(580,1212),(569,1166),(576,1105)], [629,1015], "leg_back_thigh", "back calf")
    add("leg_front_thigh", [(506,649),(548,662),(579,698),(605,746),(618,813),(626,886),(635,954),(636,989),(615,1026),(567,1020),(540,971),(545,955),(525,892),(505,842),(478,774),(470,711),(473,682)], [525,691], "body", "front thigh")
    add("leg_front_calf", [(551,956),(635,954),(636,989),(611,1027),(575,1078),(548,1129),(507,1177),(450,1150),(450,1121),(461,1080),(482,1048),(522,996)], [588,977], "leg_front_thigh", "front calf")
    # Boots retain their cyan crown, cuff and sole in one deformation unit.
    add("boot_back", [(561,1170),(583,1165),(626,1222),(657,1182),(694,1180),(708,1200),(681,1287),(655,1351),(631,1405),(662,1432),(666,1487),(620,1518),(573,1518),(525,1470),(525,1435),(548,1386),(559,1300),(572,1230)], [624,1219], "leg_back_calf", "boot")
    add("boot_front", [(415,1092),(453,1092),(491,1140),(531,1128),(556,1135),(560,1169),(519,1240),(482,1305),(484,1337),(496,1360),(497,1412),(466,1441),(429,1441),(399,1420),(385,1396),(378,1314),(399,1268),(414,1220)], [488,1162], "leg_front_calf", "boot")
    # Guarantee that the lower silhouette is fully assigned to boots.
    labels[fg & (yy >= 1250) & (xx < 530)] = 7
    labels[fg & (yy >= 1250) & (xx >= 530)] = 6

    add("body", [(473,341),(518,352),(552,382),(586,382),(602,394),(624,427),(650,454),(661,487),(642,524),(632,551),(652,604),(665,644),(664,676),(648,710),(627,738),(606,759),(590,730),(568,686),(542,660),(512,659),(476,686),(465,676),(482,647),(504,619),(508,590),(490,562),(469,537),(468,512),(494,484),(494,449),(478,411)], [552,630], "root", "torso and collar")
    add("arm_back_upper", [(551,381),(585,380),(603,393),(624,426),(646,451),(641,470),(610,451),(581,425),(560,409)], [581,395], "body", "upper arm")
    add("arm_back_sleeve", [(647,443),(659,447),(661,463),(672,459),(669,488),(659,496),(677,520),(697,546),(719,578),(739,609),(759,652),(765,681),(788,719),(779,740),(757,777),(762,831),(735,827),(719,794),(716,738),(701,700),(688,672),(671,651),(649,625),(643,598),(657,580),(647,565),(627,540),(631,521),(635,498),(650,477)], [653,495], "arm_back_upper", "sleeve")
    add("arm_front_upper", [(407,381),(446,374),(478,382),(490,420),(496,477),(486,495),(466,505),(428,504),(397,493),(394,415)], [441,402], "body", "upper arm")
    add("arm_front_sleeve", [(370,493),(394,492),(393,470),(416,477),(425,466),(447,492),(456,479),(468,490),(480,491),(480,511),(467,545),(461,591),(466,638),(472,686),(476,735),(484,810),(493,848),(469,872),(470,931),(466,950),(444,957),(444,999),(411,975),(376,947),(334,909),(337,891),(298,870),(309,839),(279,822),(274,802),(303,765),(340,722),(377,666),(394,612),(401,559),(398,541)], [433,517], "arm_front_upper", "sleeve")
    # Lavender skin classification is used only inside traced hand envelopes.
    rgb = source[:, :, :3].astype(np.int16)
    skin = (rgb[:,:,0] > 100) & (rgb[:,:,1] > 60) & (rgb[:,:,2] > 130) & (rgb[:,:,0] > rgb[:,:,1]+3)
    hand_front_box = polygon((w,h), [(378,807),(417,807),(425,841),(421,866),(431,892),(421,902),(405,882),(402,906),(395,925),(385,935),(372,915),(365,902),(362,878),(368,850)])
    hand_front = ndi.binary_dilation(skin & hand_front_box & fg, iterations=3) & hand_front_box
    add("hand_front", hand_front, [399,832], "arm_front_sleeve", "hand")
    hand_back_box = polygon((w,h), [(681,634),(699,637),(704,650),(692,665),(707,655),(716,666),(711,683),(719,691),(720,724),(705,735),(676,731),(654,710),(651,690),(662,667)])
    hand_back = ndi.binary_dilation(skin & hand_back_box & fg, iterations=3) & hand_back_box
    add("hand_back", hand_back, [705,691], "arm_back_sleeve", "hand resting on hip")
    cyan = (rgb[:,:,1] > rgb[:,:,0]+6) & (rgb[:,:,1] > 60) & (rgb[:,:,2] > rgb[:,:,0]+25)
    chest_envelope = polygon((w,h), [(505,397),(520,397),(525,382),(544,389),(553,404),(575,404),(561,420),(566,451),(547,434),(534,451),(527,431),(512,434),(521,416),(505,406)])
    add("ice_chest", cyan & chest_envelope, [541,416], "body", "chest crystal")
    hip_envelope = polygon((w,h), [(489,625),(526,622),(541,586),(551,613),(576,631),(553,648),(562,691),(531,669),(501,696),(503,658)])
    add("ice_hip", cyan & hip_envelope, [536,643], "body", "hip crystal")

    # Crown crystals overlap the calves in a V: use original palette regions,
    # not a horizontal boot cut, to keep each complete crown with its boot.
    for bounds, boot_id, calf_id in [((413,1087,563,1203),7,5),((560,1157,709,1260),6,3)]:
        x1,y1,x2,y2 = bounds
        region = np.zeros_like(fg)
        region[y1:y2,x1:x2] = True
        skin_seed = skin & region & fg
        boot_seed = region & fg & ((rgb[:,:,1] > rgb[:,:,0]+6) | ((rgb[:,:,0]<80)&(rgb[:,:,1]>rgb[:,:,0])))
        ds = ndi.distance_transform_edt(~skin_seed)
        db = ndi.distance_transform_edt(~boot_seed)
        eligible = region & fg & np.isin(labels,[0,1,calf_id,boot_id])
        labels[eligible & (ds<db)] = calf_id
        labels[eligible & (ds>=db)] = boot_id

    # The front cuff's upward spikes belong to the sleeve rather than shoulder.
    cuff = polygon((w,h),[(369,493),(393,493),(393,468),(416,477),(425,465),(443,480),(455,477),(469,485),(482,490),(480,515),(451,544),(406,541)])
    cuff_core = cyan & cuff & fg
    cuff_outline = ndi.binary_dilation(cuff_core,iterations=3) & cuff & fg
    labels[cuff_outline] = 12

    # Reattach detached dark contour fragments accidentally left in rear hair.
    # The small visible inner tress behind the front sleeve is intentionally kept.
    inner_tress = polygon((w,h),[(463,532),(492,563),(509,590),(505,626),(482,655),(463,646)])
    for hair_id in (0,1):
        components,count = ndi.label((labels==hair_id)&fg,structure=np.ones((3,3)))
        if count:
            sizes = np.bincount(components.ravel()); sizes[0]=0
            largest = int(sizes.argmax())
            fragments = (components!=0)&(components!=largest)&~inner_tress&(yy>=375)
            other = fg & (labels>=2)
            _, nearest = ndi.distance_transform_edt(~other,return_indices=True)
            rows,cols=np.nonzero(fragments)
            labels[rows,cols] = labels[nearest[0,rows,cols],nearest[1,rows,cols]]
    from hyou_body_cleanup import cleanup_body_labels
    labels = cleanup_body_labels(source, labels, fg)
    # This lavender source edge belongs to the thigh, even though the local
    # sleeve cleanup's purple predicate also matches its shadow color.
    thigh_edge = (labels==0) & fg & (xx==472) & (yy>=686) & (yy<=713)
    labels[thigh_edge] = 4
    return definitions, labels, fg


def make_pixels(source, mask):
    result = np.zeros_like(source)
    result[mask,:3] = source[mask,:3]
    result[mask,3] = 255
    return result


def compose(layers):
    canvas = Image.new("RGBA", (layers[0]["pixels"].shape[1], layers[0]["pixels"].shape[0]))
    for layer in layers:
        canvas.alpha_composite(Image.fromarray(layer["pixels"]))
    return canvas


def checker(size, tile=24, colors=((235,241,247),(212,222,232))):
    yy, xx = np.indices((size[1], size[0]))
    array = np.asarray(colors, dtype=np.uint8)[((xx//tile + yy//tile) % 2)]
    return Image.fromarray(array).convert("RGBA")


def add_hidden_overlap(layers, foreground, radius=12, fringe_radius=4, scalp_radius=6, source=None):
    """Extend only beneath later source ownership; the neutral pose stays exact."""
    later = np.zeros_like(foreground)
    for layer in reversed(layers):
        mask = layer["mask"]
        if layer.get("role") in {"rear hair", "back thigh", "back calf", "front thigh", "front calf", "upper arm", "torso and collar", "sleeve", "boot", "front_hair", "rear_scalp", "face_foundation", "accessory"}:
            role=layer.get("role")
            padding=fringe_radius if role in {"front_hair","accessory"} else (scalp_radius if role=="rear_scalp" else radius)
            if role=="face_foundation":
                padding=fringe_radius if fringe_radius>4 else 0
            extension = ndi.binary_dilation(mask, iterations=padding) & later
            extension &= ~mask
            if role=="face_foundation":
                extension &= ~layer.get("fill_mask",np.zeros_like(foreground))
            if extension.any():
                interior = ndi.binary_erosion(mask, iterations=4)
                if not interior.any():
                    interior = mask
                _, nearest = ndi.distance_transform_edt(~interior, return_indices=True)
                rows, cols = np.nonzero(extension)
                layer["pixels"][rows,cols,:3] = layer["pixels"][nearest[0,rows,cols],nearest[1,rows,cols],:3]
                layer["pixels"][rows,cols,3] = 255
                if role=="face_foundation" and source is not None:
                    hair_underpaint = extension & layer.get("hair_underpaint_mask",np.zeros_like(foreground))
                    layer["pixels"][hair_underpaint,:3] = source[hair_underpaint,:3]
                layer["fill_mask"] = layer.get("fill_mask",np.zeros_like(foreground)) | extension
        later |= mask


def remove_detached_hair_fill(layers):
    """A moving overlap must remain attached to original artwork, not float."""
    for layer in layers:
        if layer.get("role") not in {"rear hair","front_hair"}:
            continue
        components,count = ndi.label(layer["pixels"][:,:,3]>0,structure=np.ones((3,3)))
        attached = np.unique(components[layer["mask"]])
        orphan = (components>0) & ~np.isin(components,attached)
        layer["pixels"][orphan] = 0
        layer["fill_mask"] = layer.get("fill_mask",np.zeros_like(orphan)) & ~orphan
        layer["discarded_detached_fill_pixels"] = int(orphan.sum())


def write_preview(layers, output):
    image = compose(layers)
    image.save(output / "reassembled.png")
    backgrounds = []
    for bg in [checker(image.size), Image.new("RGBA",image.size,"#f8fcff"), Image.new("RGBA",image.size,"#17213a")]:
        bg.alpha_composite(image)
        backgrounds.append(bg.resize((384,576),Image.Resampling.NEAREST).convert("RGB"))
    strip = Image.new("RGB",(1152,576))
    for i,bg in enumerate(backgrounds):
        strip.paste(bg,(384*i,0))
    strip.save(output / "edge_check.png")
    rng = np.random.default_rng(7)
    palette = rng.integers(45,245,size=(len(layers),3),dtype=np.uint8)
    owner = np.full((image.height,image.width,4),255,dtype=np.uint8)
    owner[:,:,:3] = 246
    for i,layer in enumerate(layers):
        owner[layer["mask"],:3] = palette[i]
    Image.fromarray(owner).save(output / "ownership_map.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,default=DEFAULT_OUTPUT)
    parser.add_argument("--body-only",action="store_true")
    parser.add_argument("--joint-padding",type=int,default=12)
    parser.add_argument("--fringe-padding",type=int,default=4)
    parser.add_argument("--scalp-padding",type=int,default=6)
    args = parser.parse_args()
    if any(value<0 or value>64 for value in [args.joint_padding,args.fringe_padding,args.scalp_padding]):
        parser.error("Hidden overlap padding must be between 0 and 64 pixels")
    source = np.array(Image.open(SOURCE).convert("RGBA"))
    definitions, labels, fg = build_body(source)
    head_layers = []
    if not args.body_only:
        from hyou_head_layers import build_head_layers
        head_layers = build_head_layers(source)
        head_union = np.logical_or.reduce([l["mask"] for l in head_layers])
        labels[head_union] = -1
        # Reclaim the original dark accessory/crown border pixels that lie just
        # outside the head's traced polygons. They must follow the head, rather
        # than become detached chips on the moving long hair.
        owner = labels.copy()
        for j,head_layer in enumerate(head_layers):
            owner[head_layer["mask"]] = len(definitions)+j
        yy,xx = np.indices(fg.shape)
        for hair_id in (0,1):
            components,count = ndi.label((labels==hair_id)&fg,structure=np.ones((3,3)))
            if not count:
                continue
            sizes = np.bincount(components.ravel()); sizes[0]=0
            largest = int(sizes.argmax())
            remnants = (components!=0)&(components!=largest)&(yy<400)
            eligible_owner = fg & (owner>=2)
            _,nearest = ndi.distance_transform_edt(~eligible_owner,return_indices=True)
            rows,cols = np.nonzero(remnants)
            routed = owner[nearest[0,rows,cols],nearest[1,rows,cols]]
            for i in np.unique(routed):
                take = routed==i
                ry,rx = rows[take],cols[take]
                if i < len(definitions):
                    labels[ry,rx] = i
                else:
                    layer = head_layers[i-len(definitions)]
                    layer["mask"][ry,rx] = True
                    layer["pixels"][ry,rx,:3] = source[ry,rx,:3]
                    layer["pixels"][ry,rx,3] = 255
                    layer["fill_mask"][ry,rx] = False
                    labels[ry,rx] = -1
        # Cyan belongs to ice/costume accessories. Reattach the last tiny cyan
        # edge flecks so moving hair cannot carry an armor highlight with it.
        rgb = source[:,:,:3].astype(np.int16)
        cyan_edge = (rgb[:,:,1]>100)&(rgb[:,:,2]>150)&(rgb[:,:,1]-rgb[:,:,0]>15)&(rgb[:,:,2]-rgb[:,:,1]<110)
        owner = labels.copy()
        for j,head_layer in enumerate(head_layers):
            owner[head_layer["mask"]] = len(definitions)+j
        flecks = fg & np.isin(labels,[0,1]) & cyan_edge
        proper_ice = fg & (owner>=2) & cyan_edge
        if flecks.any():
            _,nearest = ndi.distance_transform_edt(~proper_ice,return_indices=True)
            rows,cols=np.nonzero(flecks)
            routed=owner[nearest[0,rows,cols],nearest[1,rows,cols]]
            for i in np.unique(routed):
                take=routed==i
                ry,rx=rows[take],cols[take]
                if i<len(definitions):
                    labels[ry,rx]=i
                else:
                    layer=head_layers[i-len(definitions)]
                    layer["mask"][ry,rx]=True
                    layer["pixels"][ry,rx,:3]=source[ry,rx,:3]
                    layer["pixels"][ry,rx,3]=255
                    layer["fill_mask"][ry,rx]=False
                    labels[ry,rx]=-1
    layers = []
    for i,definition in enumerate(definitions):
        mask = (labels == i) & fg
        layers.append(dict(**definition, mask=mask, pixels=make_pixels(source,mask)))
    layers += head_layers
    if head_layers:
        by_name={layer["name"]:layer for layer in layers}
        moving=by_name["hair_front_screenright"]
        scalp=by_name["head_scalp"]
        if moving["mask"][378,559]:
            moving["mask"][378,559]=False
            moving["pixels"][378,559]=0
            moving["fill_mask"][378,559]=False
            scalp["mask"][378,559]=True
            scalp["pixels"][378,559,:3]=source[378,559,:3]
            scalp["pixels"][378,559,3]=255
            scalp["fill_mask"][378,559]=False
    # Source ownership must be a partition, independent of hidden joint padding.
    coverage = np.sum([l["mask"].astype(np.uint8) for l in layers],axis=0)
    if not np.array_equal(coverage,fg.astype(np.uint8)):
        raise RuntimeError(f"Invalid source partition: {np.count_nonzero(coverage != fg)} pixels")
    add_hidden_overlap(layers,fg,args.joint_padding,args.fringe_padding,args.scalp_padding,source)
    remove_detached_hair_fill(layers)
    output = args.output.resolve()
    output.mkdir(parents=True,exist_ok=True)
    (output/"layers").mkdir(exist_ok=True)
    normalized = make_pixels(source,fg)
    normalized_image = Image.fromarray(normalized)
    normalized_image.save(output/"source_clean.png")
    # Do not ship the inferred masks as art: these are reproducibility data.
    entries = []
    for index,layer in enumerate(layers):
        if not np.array_equal(layer["pixels"][layer["mask"],:3],source[layer["mask"],:3]):
            raise RuntimeError(f"Source RGB changed in {layer['name']}")
        if not layer["mask"].any():
            raise RuntimeError(f"Empty source layer: {layer['name']}")
        filename = f"layers/{index:02d}_{layer['name']}.png"
        Image.fromarray(layer["pixels"]).save(output/filename)
        entries.append({k:layer[k] for k in ("name","pivot","parent","role")}|dict(file=filename,z_index=index,source_pixels=int(layer["mask"].sum()),hidden_fill_pixels=int(layer.get("fill_mask",np.zeros_like(fg)).sum()),discarded_detached_fill_pixels=layer.get("discarded_detached_fill_pixels",0)))
    reassembled = np.array(compose(layers))
    different = np.any(reassembled != normalized,axis=2)
    diff = int(different.sum())
    report = dict(source=str(SOURCE.relative_to(ROOT)).replace("\\","/"),source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),canvas=[source.shape[1],source.shape[0]],alpha_cleanup="Source alpha >=128 becomes255; RGB unchanged. Original internal transparent gaps preserved.",layer_order="back to front",coordinates="top-left canvas origin; screenleft/screenright describe the viewer's sides",joints={"root":{"pivot":[552,630],"parent":None},"head_pivot":{"pivot":[519,353],"parent":"body"}},layers=entries,verification=dict(source_foreground_pixels=int(fg.sum()),source_partition_errors=int(np.count_nonzero(coverage != fg)),neutral_rgba_pixel_differences=diff))
    (output/"rig_manifest.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    np.savez_compressed(output/"source_ownership.npz",names=np.array([l["name"] for l in layers]),owner=np.argmax(np.stack([l["mask"] for l in layers]),axis=0).astype(np.uint8),foreground=fg)
    readme = f"""# Hyou — bộ cắt để rig 2D

{len(layers)} layer, canvas gốc 1024 × 1536. Cắt bằng Python từ neutral_dial.png.
Giữ RGB của toàn bộ {int(fg.sum()):,} pixel nhân vật; alpha ≥ 128 được làm sạch
thành 255. Các khoảng hở giữa tóc và tay áo giữ nguyên.

## Mở bộ ảnh

- `neutral_dial_rig.psd`: ảnh nhiều layer, có vị trí gốc, để nhập vào phần mềm rig hỗ trợ PSD.
- `neutral_dial_rig.ora`: bản nhiều layer cho Krita/GIMP hoặc phần mềm hỗ trợ OpenRaster.
- `layers/`: PNG RGBA cùng canvas, chồng tất cả ở (0, 0) theo rig_manifest.json.
- `cropped/`: PNG cắt gọn; vị trí và pivot cục bộ có trong crop_manifest.json.
- `contact_sheet.png`: xem từng bộ phận; `edge_check.png`: kiểm tra trên ba nền.
- `motion_preview.gif`: thử tóc, tay áo và nhịp thở nhẹ, dùng nearest filtering.

Tên `screenleft` / `screenright` chỉ bên trái / phải khi nhìn vào ảnh.
Tọa độ pivot trong rig_manifest.json tính từ góc trên trái của canvas.
Layer được ghi từ sau ra trước. Khi dùng PNG đã crop, đặt theo offset trong
crop_manifest.json thay vì đặt tất cả về (0, 0).

## Cách dùng cho animation

Tóc dài, ba lọn tóc trước, mặt, lông mày, lòng mắt, iris, mi mắt, miệng,
phụ kiện, thân, tay, bàn tay, đùi, bắp chân và giày có layer riêng.
Dùng các pivot/hierarchy được gợi ý để bắt đầu dựng skeleton hoặc mesh.
Chọn nearest filtering để giữ nét pixel; đừng scale hoặc làm mịn ảnh trước khi rig.

Bộ này dành cho animation từ góc nhìn gốc: idle, thở, tóc đung đưa và cử động
nhẹ của đầu/tay. Các khớp có phần chồng được bù bằng màu gần đó, nằm dưới
pixel gốc. Pose lớn, đổi góc nhìn, đi/chạy hoặc xoay chân bắt chéo cần vẽ thêm
phần giải phẫu/trang phục vốn bị che trong ảnh gốc. Không có pose miệng mở
hoặc mi mắt nhắm được vẽ mới.

## Kiểm tra

Ghép layer ở pose gốc khớp tuyệt đối với source_clean.png: {diff} pixel RGBA khác.
Mỗi pixel gốc thuộc đúng một mask; phần bù có ghi số lượng trong manifest.
source_ownership.npz lưu nhãn pixel để kiểm tra và tái tạo bộ cắt.
Ảnh neutral_dial.png và bộ rig cũ được giữ tại vị trí ban đầu.

## Tái tạo

Chạy từ thư mục repo, với Python + Pillow + NumPy + SciPy:

```powershell
python tools/art/cut_hyou_rig.py
python tools/art/hyou_rig_package.py assets/graphics/characters/hyou/neutral_dial_rig_v1
python tools/art/hyou_rig_preview.py assets/graphics/characters/hyou/neutral_dial_rig_v1
```
"""
    (output/"README_VI.md").write_text(readme,encoding="utf-8")
    write_preview(layers,output)
    if diff:
        visual = Image.fromarray(np.dstack([different*255,np.zeros_like(different),np.zeros_like(different),np.full_like(different,255)]).astype(np.uint8))
        visual.save(output/"reassembly_difference.png")
        raise RuntimeError(f"Neutral reassembly differs at {diff} pixels")
    print(json.dumps(dict(output=str(output),layers=len(layers),**report["verification"]),indent=2))


if __name__ == "__main__":
    main()
