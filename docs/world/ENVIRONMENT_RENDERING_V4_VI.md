# Environment Rendering V4 — Whispering Fields

Mục tiêu V4 là bám ba target look đã chốt: **day rõ khối**, **golden hour có highlight vàng nhưng shadow vẫn xanh/cool**, và **night xanh trăng nhưng vẫn đọc được gameplay**.

## Thay đổi chính

- Material bus truyền thêm `key direction/color/elevation/strength`, `golden_hour` và `shadow_length` cho shader. Shader không còn đoán độ cao mặt trời từ vector 2D.
- Ambient hoàng hôn được giữ trung tính hơn. Màu vàng chuyển sang key light/highlight để tránh cả map thành olive.
- Foliage V4 relight theo pseudo-normal, tăng crown AO, golden rim và moon response.
- Trunk có shader riêng để root/trunk bắt sáng đồng bộ với canopy.
- Shadow tree dùng footprint V4 mới, rộng và mềm hơn; contact AO nhỏ hơn để không thành vạch đen dưới gốc.
- Ground có authored mask V4: macro variation, cavity gần cliff, moisture gần pond; thêm sparse pixel grass detail.
- Dirt path có material riêng cho relief/golden/night.
- Cliff/rock tăng plane separation + cavity detail + selective golden edge.
- Water depth mask được sinh lại bằng distance-to-shore; shader có shallow/deep, golden glint và moon glint riêng.
- Color grade có ba look thay vì một tint chung: day / golden / night.
- Direct `DirectionalLight2D` giảm lại để không làm asset AI bị cháy/neon; material shader chịu trách nhiệm tạo form.

## Asset V4 mới

- `assets/graphics/environment/shadows/tree_shadow_mask_v4.png`
- `assets/graphics/environment/shadows/apple_tree_shadow_mask_v4.png`
- `assets/graphics/environment/shadows/contact_ellipse_64x28_v4.png`
- `assets/graphics/world/whispering_fields/field_01_layers/05_cliff_contact_ao_v4.png`
- `assets/graphics/world/whispering_fields/field_01_layers/10_puddle_depth_mask_v4.png`
- `assets/graphics/world/whispering_fields/field_01_layers/11_ground_art_mask_v4.png`
- `assets/graphics/world/whispering_fields/field_01_layers/12_ground_detail_v4.png`

Asset-sheet AI tham khảo **không được import trực tiếp** vào game ở V4 vì một số module (đặc biệt stairs) sai hình học. V4 chỉ dùng ba render target làm art direction.

## Test nhanh

Giữ camera cố định và dùng `Ctrl+F9` để chụp 06:00 / 12:00 / 18:00 / 00:00. Log debug V4 có thêm `golden=...` để biết shader đang ở look nào.
