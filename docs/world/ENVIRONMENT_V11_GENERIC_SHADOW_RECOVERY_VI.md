# Field 01 V11 - Generic Shadow Recovery

## Root cause đã sửa
`ShadowCaster2D.cs` trỏ tới:

- `res://assets/graphics/environment/shadows/v5/soft_footprint_v5.png`
- `res://assets/graphics/environment/shadows/v5/contact_ao_v5.png`

nhưng hai asset không có trong repo. Tree vẫn có bóng vì tree dùng footprint override riêng ở `v5_1`; player/Hyou/rock/flora generic vì vậy bị `Texture=null` và `geometryReady=false`.

## V11 sửa

1. phục hồi 2 generic texture trên
2. thêm runtime generated fallback trong `ShadowCaster2D` nếu texture lại bị thiếu trong tương lai
3. retry geometry tối đa 90 frame để hỗ trợ Player tạo `Body` bằng `CallDeferred`
4. giữ `_lastEnvironmentState` và apply ngay khi geometry trở nên sẵn sàng
5. hạ nhẹ profile V10 vì bây giờ footprint thật sự đã hiện, tránh bóng quá đen

## Expected runtime
- player và Hyou: có contact shadow ở chân + footprint đổi hướng/độ dài theo 06/12/18/00
- rock small/medium/large: có contact AO và bóng đọc được trên grass
- tree shadow V9/V10 không thay đổi

## File thay đổi
- `assets/graphics/environment/shadows/v5/soft_footprint_v5.png`
- `assets/graphics/environment/shadows/v5/contact_ao_v5.png`
- `scripts/World/Environment/ShadowCaster2D.cs`
- `data/world/shadows/character.tres`
- `data/world/shadows/rock_small.tres`
- `data/world/shadows/rock_medium.tres`
- `data/world/shadows/rock_large.tres`
