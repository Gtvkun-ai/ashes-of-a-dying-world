# Hyou Ice Bolt — Soft Homing V1

Mục tiêu:
- hình sprite ngang và CombatCenter/collision không còn khiến Ice Bolt hụt khó chịu;
- viên đạn có "lia" theo đúng target Hyou đã khóa;
- target vẫn có thể né, không biến thành missile auto-hit.

Thiết kế:
- `HomingStrength = 0.60`
- turn rate gốc `260 deg/s`, strength 60% => tối đa khoảng `156 deg/s`
- chỉ track target gốc của action, không tự đổi sang enemy khác
- aim vào `CombatCenter` thật
- gần target dưới 10 px thì ngừng bẻ lái để tránh orbit/rung
- visual pivot quay theo hướng bay mới, collision và hình không tách hướng
- Radius Ice Bolt: 10 -> 12 world px để khớp cảm giác sprite scale hiện tại hơn

Quan trọng:
`60%` là độ mạnh của soft-homing trên turn-rate, KHÔNG phải 60% xác suất auto-hit.
Do turn-rate bị giới hạn, target chạy/lách vẫn né được.
