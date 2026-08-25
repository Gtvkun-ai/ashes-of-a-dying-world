# Environment Grass V6.0 — Field 01

Mục tiêu: đưa ground của `field_01` về cùng art scale với map pixel-art và chuyển quyền quyết định composition từ procedural shader sang authored art.

## Rule bắt buộc

- Logical map art: `456x474`.
- Layer pixel-art hiển thị bằng `scale = Vector2(4, 4)` + nearest.
- Không dùng runtime FBM/noise để tự phát minh mảng sáng/tối lớn trên ground.
- Macro light/shade phải nằm trong `10_grass_macro_mask.png`.
- `00_ground_base.png` phải yên; detail trung bình nằm ở `15_grass_field_detail.png`; fringe quanh path nằm ở `14_grass_edge_detail.png`.

## Layer V6

- `00_ground_base.png`: albedo cỏ sạch, 456x474, micro baked rất thưa.
- `10_grass_macro_mask.png`: authored clearing/shade, 128 = neutral.
- `11_grass_micro_detail.png`: micro response alpha-safe.
- `12_grass_contact_mask.png`: contact quanh path/cliff/pond, giữ từ pass trước.
- `13_grass_normal_soft.png`: normal có form thật, không còn flat `(128,128,255)`.
- `14_grass_edge_detail.png`: grass fringe dọc path.
- `15_grass_field_detail.png`: tuft trung bình, phân cụm thưa, không rải đều.

## Runtime

`ground_lighting.gdshader` chỉ relight authored masks theo daylight/weather. Macro composition không thay đổi ngẫu nhiên theo runtime.

`field_01.tscn` giảm cụm `grass_patch` hình lưới từ 62 xuống 41 instance và phá grid `6x3/6x4` thành cluster irregular có scale variation.

## Rebuild art

Chạy:

```bash
python tools/art/rebuild_field01_grass_v6.py
```

Script deterministic để việc tuning sau này không làm asset drift ngẫu nhiên.
