# Environment Rendering V5.1 — Native Tree Asset Package

## Mục tiêu
V5.1 thay hai loại cây chính bằng asset native-resolution cùng một ngôn ngữ hình ảnh.
Không còn `apple_tree` 499x681 rồi scale `0.3`. Hai scene đều render gần 1 texel = 1 world pixel.

## Asset mới
`assets/graphics/environment/trees/v5_1/`
- `tree_v51_master.png` — master chỉnh tay, 144x176.
- `apple_tree_v51_master.png` — master chỉnh tay, 176x200.
- `*_canopy_v51.png` — layer foliage runtime.
- `*_trunk_v51.png` — layer trunk/runtime.
- `*_canopy_normal_v51.png` — pseudo normal authored từ silhouette.
- `*_canopy_ao_v51.png` — AO foliage.

`assets/graphics/environment/shadows/v5_1/`
- `tree_footprint_v51.png`
- `apple_tree_footprint_v51.png`

## Scene
`tree.tscn` và `apple_tree.tscn` dùng package V5.1.
Apple tree bỏ hoàn toàn `scale = Vector2(0.3, 0.3)`. Collision được đổi sang kích thước world tương đương bản cũ.
Ground anchor được giữ gần vị trí cũ để không làm layout Field 1 nhảy tung.

## Shader
`foliage_wind.gdshader` có thêm authored AO sampler. `authored_ao_strength` mặc định bằng 0 nên các flora cũ không bị đổi.
Tree V5.1 dùng AO khoảng 0.18 và giảm dynamic relight so với V4 để không cộng sáng quá mạnh lên albedo mới.

## Nếu muốn sửa ảnh cây
Chỉ cần sửa / thay:
- `tree_v51_master.png`
- `apple_tree_v51_master.png`

Sau đó chạy từ root project:
```bash
python tools/art/rebuild_tree_v51_assets.py
```
Script sẽ tự tách canopy/trunk và rebuild normal/AO/shadow footprint. Cần Python + Pillow + numpy.

## Test đề nghị
Giữ camera cố định và chụp 06:00 / 12:00 / 18:00 / 00:00.
Kiểm tra trước: kích thước cây so với character, root có nằm đúng ground không, collision có lệch path không.
