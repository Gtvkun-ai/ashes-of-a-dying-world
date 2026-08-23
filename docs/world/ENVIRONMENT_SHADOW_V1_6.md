# Environment Shadow V1.6 — real directional projected shadow

## Mục tiêu
Sửa 2 lỗi lớn của V1.5:

1. Bóng mới chỉ là silhouette bị ép dẹt xuống đất.
2. Bóng chưa đọc ra rõ chuyện **sáng/chiều đổi phía**.

## Ý tưởng
V1.6 vẫn dùng **asset thật làm bóng**, nhưng thay vì chỉ deform ở vertex, shader làm:

- nhận một pixel đang được render trên mặt đất;
- suy ngược pixel đó về **pixel nào trong asset gốc**;
- nếu pixel nguồn có alpha thì tô shadow tại pixel mặt đất đó.

Nói ngắn gọn: đây là **inverse projection**.

## Hệ quả
- cây sẽ có bóng theo đúng silhouette của cây;
- sáng và chiều sẽ lật trái/phải theo `KeyLightDirection.x`;
- trưa bóng co ngắn hơn;
- không còn kiểu “cái cây bị ép nằm xuống đất theo một hướng cố định”.

## File đổi
- `scripts/World/Environment/ProjectedShadow2D.cs`
- `assets/shaders/world/projected_asset_shadow.gdshader`

## Ghi chú kỹ thuật
- `ProjectedShadow2D` mở rộng quad render bằng padding, để bóng dài không bị cắt tại biên texture gốc.
- `shadow_direction` dùng **cùng hướng tia sáng trên mặt phẳng 2D** do `CelestialSolver` trả về.
  Vì solver đã thay đổi dấu trục X theo thời gian trong ngày, bóng sẽ tự đổi phía.
- `projection_mode = GroundPlane` dùng phép chiếu thật hơn cho cây/đá/hoa/nhân vật.
- `projection_mode = RigidDrop` vẫn giữ cho cliff/wall vì layer map lớn không thể ép mọi pixel về một “chân” chung.
