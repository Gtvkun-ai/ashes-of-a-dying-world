# Hikaru Focus — Deep Flow Auto Evasion V1

## Mục tiêu
Focus có mechanic signature thay vì chỉ cộng stat: trong thời gian buff, mỗi hit melee/projectile hợp lệ có 20% cơ hội kích hoạt phản xạ né tự động.

## Runtime
1. Hit thật sự chạm Hikaru và chuẩn bị resolve.
2. Nếu Focus đang active, roll đúng 1 lần cho hit đó.
3. Proc thành công:
   - hit bị reject với `HitRejectionReason.Evaded`;
   - action đang đánh của Hikaru bị cancel để ưu tiên né;
   - Hikaru lách ngang bằng external motion có collision;
   - có cửa sổ bảo vệ ngắn 0.18s để multi-hit cùng khoảnh khắc không bắt lại ngay.
4. Attacker vào `AttackRecovery` / kết thúc attack:
   - nếu player không nhập hướng di chuyển mới, Hikaru tự áp lại một bước ngắn;
   - nếu player đã tự di chuyển, auto-return hủy ngay. Player luôn có quyền cao nhất.

## Điều kiện không né
- đang block/perfect-parry;
- frozen/dead;
- true damage;
- self effect / hazard không đi qua melee hoặc projectile action;
- nguồn không hostile.

## Balance mặc định
- Chance: 20%
- Internal cooldown sau proc: 0.60s
- I-frame bảo vệ motion: 0.18s
- Dodge impulse: 230
- Re-engage window: 0.80s
- Re-engage impulse: 165

VFX mắt / sound cue chưa được thêm trong V1 theo yêu cầu; mechanic chạy độc lập và presentation có thể hook sau.
