# Environment Rendering V4.2 — Shadow Geometry Fix

V4.1 đã lộ đúng bug hình học: bóng cây thành vệt kim/ tam giác dài.

Nguyên nhân:
- `ArtDirectedFootprint` vẫn dùng phép biến đổi phi tuyến theo 4 vertex của cả quad.
- Compatibility rasterizer chỉ nội suy giữa 4 vertex, nên footprint mềm bị kéo thành lưỡi dao dài.

V4.2:
- Model 3 chuyển sang affine tuyến tính hoàn toàn.
- Footprint PNG tự chứa fade: gần gốc đậm, đầu xa mềm.
- Bỏ tail fade bằng `UV.y` trong fragment.
- Tree max length 126 -> 78, Apple 140 -> 90.
- Giảm opacity cast shadow và giữ contact AO rất nhỏ.

Mục tiêu: bóng đọc như một mảng canopy trên mặt đất, không như vệt bút.
