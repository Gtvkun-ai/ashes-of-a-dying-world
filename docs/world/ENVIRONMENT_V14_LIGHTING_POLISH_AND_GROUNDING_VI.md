# Field 01 V14 - Lighting polish + grounding refinement

## Mục tiêu
Sau khi V13.1 đã fix đúng hệ tọa độ tree-ground influence, V14 chuyển qua polish:
- noon giữ sạch và readable
- 18:00 ấm hơn, rõ golden hour hơn
- 00:00 bớt cyan wash
- grounding của cây organic hơn, bớt đọc như oval đều

## Thay đổi chính
### 1) Color grade runtime polish
`EnvironmentBinder2D` set trực tiếp shader params cho `WorldPostFX/ColorGrade`:
- golden mạnh hơn một chút
- night_blue_strength giảm
- night_exposure giảm nhẹ
- green_tame / cohesion tune lại nhẹ

### 2) Grounding mask shape
- tăng nhẹ strength cho ground influence
- path influence giữ nhưng tint yếu hơn để tránh blob vàng/nâu quá rõ
- thêm `lowerBed` lobe trong `PaintInfluence()` để vùng đất dưới rễ mang cảm giác mass đất nén, không chỉ ellipse đều

## File đổi
- `scripts/World/Environment/EnvironmentBinder2D.cs`
