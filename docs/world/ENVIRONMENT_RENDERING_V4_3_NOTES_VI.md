# Environment Rendering V4.3 — Ground Footprint Fix

V4.2 vẫn cho bóng cây đọc như một đường mảnh vì phép affine dùng quad đầy đủ, trong khi alpha footprint chỉ chiếm một vùng nhỏ ở đáy texture. V4.3 truyền alpha-bounds center/depth thật từ C# sang shader và chiếu đúng vùng footprint.

## Thay đổi
- `ShadowCaster2D.cs`: truyền `caster_footprint_center_y_local` + `caster_footprint_depth_local`; projected/contact shadow dùng Linear filter riêng.
- `projected_shadow_v2.gdshader`: model 3 dùng ground-plane footprint thật; near edge chạm gốc, far edge đi theo `shadow_direction`; noon vẫn là oval chứ không sụp thành line.
- Tree mask / apple mask: tạo lại dạng canopy mass có far penumbra + core gần thân.
- Tree profile: noon depth 15/17 px, low-sun depth 88/98 px; contact AO giảm.
- `GroundCanopyShadow` tắt vì `02_canopy_shadow.png` thực tế chứa terrain/cliff pixels cũ, không phải shadow mask sạch.
