# Hyou Ice Bolt — Close-range Aim V2

## Vấn đề thật

V1 có soft-homing nhưng vẫn có thể trông ngu ở cự ly gần.

Hai nguyên nhân:
1. `SpawnOffset = 20` có thể đặt projectile quá xa về phía trước khi target đang ở rất gần.
   Viên đạn có thể bắt đầu sát hoặc thậm chí phía sau vùng hit của target.
2. Turn-rate cố định không có đủ thời gian sửa sai ở cự ly 30–70 px vì Ice Bolt bay 320 px/s.

## V2

- SpawnOffset 20 -> 10.
- Runtime tự clamp SpawnOffset theo khoảng cách target, không bao giờ dùng offset presentation
  để teleport viên đạn xuyên qua target.
- Radius 12 -> 14.
- Homing vẫn là 60%.
- Base max-turn 320 deg/s.
- Trong 96 px, turn-rate tăng dần tới x2.25 khi ở rất gần.
- Chỉ dừng steering dưới 2 px thay vì 10 px.

Đây vẫn là steering, không phải teleport-to-target và không đảm bảo auto-hit:
target chạy/lách đủ nhanh vẫn né được.

Mục tiêu gameplay:
Nếu Hyou và slime đứng sát/nhìn rõ là đang đối diện nhau, Ice Bolt không được hụt chỉ vì
CastOrigin, sprite scale và hurtbox của top-down character nằm ở các cao độ Y khác nhau.
