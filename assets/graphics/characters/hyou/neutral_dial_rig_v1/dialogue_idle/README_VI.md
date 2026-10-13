# Hyou — rig idle và sửa tay

Đây là bộ rig đang dùng trong **Godot 4 + Dialogic**. Gồm 34 layer PNG,
canvas nguồn 1024 × 1536. Loop idle dài 3,84 giây.

## Chọn đúng file

| File / thư mục | Dùng để làm gì |
| --- | --- |
| `layers/` | PNG cùng canvas để sửa pixel bằng tay. Đây là bản sửa chính. |
| `cropped/` | PNG game đang đọc, tự tạo lại từ `layers/`. |
| `neutral_dial_rig.psd`, `.ora` | Artwork nhiều layer để chỉnh trong phần mềm vẽ. |
| `contact_sheet.png` | Xem nhanh tên và hình từng bộ phận. |
| `motion_dialogue.gif` | Xem loop idle. |
| `rig_manifest.json` | Thứ tự layer, điểm xoay `pivot` và bộ phận cha `parent`. |
| `crop_manifest.json` | Vị trí PNG crop trong canvas gốc, tự tạo khi đóng gói. |
| `neutral_dial_rig_flattened.png` | Bản ghép neutral, công cụ preview cần đọc. |
| `source_ownership.npz` | Dữ liệu kiểm tra pixel nguồn; giữ cùng bộ rig. |

`screenleft` / `screenright` là trái / phải khi nhìn vào ảnh.
Tọa độ pivot tính từ góc trên trái canvas gốc.

## Sửa pixel, tóc hoặc phần bù khớp

1. Mở PNG cần sửa trong `layers/`, chẳng hạn `31_hair_front_screenleft.png`.
2. Giữ canvas **1024 × 1536**, RGBA, tên file và vị trí bộ phận; sửa bằng bút pixel.
3. Chạy hai lệnh sau từ thư mục repo:

```powershell
$hyouRigDir = "assets/graphics/characters/hyou/neutral_dial_rig_v1/dialogue_idle"
python -B tools/art/hyou_rig_package.py $hyouRigDir
python -B tools/art/hyou_rig_preview.py $hyouRigDir --profile dialogue
```

Lệnh đầu cập nhật PNG runtime, crop/offset, PSD/ORA, bản ghép và contact sheet.
Lệnh sau cập nhật GIF và tạo ảnh/report kiểm tra chuyển động.
Đợi Godot import lại PNG, rồi chạy game và mở đoạn thoại có Hyou để xem.

Nếu sửa PSD/ORA, xuất từng layer về PNG trong `layers/` với **canvas đầy đủ,
tọa độ và tên file gốc**, rồi chạy hai lệnh trên. Godot đọc PNG.

**Đừng chạy lại `cut_hyou_rig.py` sau khi sửa tay:** nó cắt lại từ ảnh nguồn và
ghi đè sửa chữa. File trong `cropped/` cũng sẽ được tạo lại khi đóng gói.

## Chỉnh chuyển động

Scene: `scenes/ui/dialog/hyou_rig/hyou_dialogue_idle.tscn`.
Chọn node gốc trong Inspector:

- `intensity`: 0 đến 1,25; mặc định 1. Giảm để lắc nhẹ hơn.
- `motion_enabled`: bật/tắt idle.

Điểm xoay nằm trong `rig_manifest.json`; sau khi đổi, chạy lệnh package ở trên.
Tốc độ và biên độ từng bộ phận nằm trong `hyou_dialogue_idle.gd`, tại
`LOOP_SECONDS` và `_apply_idle_pose()`.

Khớp và chuyển động hiện được dựng bằng script. Editor hiển thị pose neutral;
chạy game để xem chuyển động. Bộ này chưa có timeline kéo xương/keyframe bằng chuột.

Ảnh gốc là [neutral_dial.png](../../neutral_dial.png). Bộ này dành cho idle ở
góc nhìn gốc; pose lớn cần thêm phần artwork vốn bị che. Chưa có mi mắt nhắm
hoặc miệng mở được vẽ mới.
