# Ghi chú patch HUD effect icon (tiếng Việt)

## Mục tiêu
Sửa phần icon hiệu lực skill đang active ở dưới HUD:
- bỏ nền xanh / viền xanh kiểu debug
- thay bằng khung nâu-vàng cùng hệ với HUD
- thêm hiệu ứng **thời gian tuột từ trên xuống** để nhìn đẹp và dễ đọc hơn

## File đã sửa
- `scripts/UI/HUD/CharacterUnitHUD.cs`
- `assets/graphics/ui/hud/status_effects/status_effect_icon_frame.png`

## Thay đổi chính
### 1) Khung icon mới
- Dùng texture `status_effect_icon_frame.png`
- khung 28x28
- tâm khung trong suốt để icon nằm bên dưới
- màu nâu/vàng đồng bộ với HUD hiện tại

### 2) Overlay thời gian
- Khi skill buff đang tồn tại, icon vẫn hiển thị.
- Một lớp tối phủ **từ trên xuống** theo thời gian đã trôi qua.
- Một vạch vàng mảnh ở mép lớp phủ giúp nhìn thấy nhịp tụt rõ hơn.

### 3) Hiển thị icon active
- Chỉ hiển thị icon khi skill timed buff còn thời gian (`remaining > 0`).
- Không còn phụ thuộc vào `overlayRatio > 0`, nên lúc mới kích hoạt effect cũng hiện ngay.

## Thông số UI hiện tại
- Badge size: `28x28`
- Icon inset: `4 px`
- Khoảng cách giữa badge: `4 px`
- Strip đặt ngay bên dưới HUD, căn trái theo portrait.

## Gợi ý nếu muốn chỉnh tiếp
Trong `CharacterUnitHUD.cs`:
- `StatusEffectBadgeSize` -> tăng/giảm kích thước khung
- `StatusEffectIconInset` -> tăng/giảm khoảng thở của icon trong khung
- `StatusEffectBadgeSpacing` -> khoảng cách giữa nhiều effect icon
- màu `overlay.Color` -> độ tối của lớp thời gian
- màu `overlayEdge.Color` -> màu vạch phân cách thời gian
