# Combat spatial / friendly-fire

Bản này tách rõ **hostile target** và **damage thật**:

- `FactionRules.IsHostile(...)`: chỉ dùng cho AI chọn mục tiêu / threat.
- `FactionRules.CanDamage(...)`: dùng cho hit thật. Các thành viên party có thể đánh/bắn nhầm nhau.
- AI không bao giờ chủ động target đồng đội chỉ vì friendly fire đang bật.

## Line of fire

`CombatLineOfFireSensor` dùng `ShapeCast2D` hình tròn với **đúng Radius + collision masks của ProjectileSpecData**.
Vì vậy một Ice Bolt rộng 10 px không còn được AI kiểm tra bằng ray mảnh rồi tưởng rằng nó lọt qua mép cây.

Kết quả được phân loại:

- `Clear`: đường bắn tới target sạch.
- `World`: cây / đá / tường / collision map chắn.
- `Ally`: đồng đội nằm trước target, có nguy cơ friendly fire.
- `Hostile`: một enemy khác đứng chắn target hiện tại.
- `NeutralActor`: actor trung lập chắn đường.

Nếu đường bắn bị chặn, Hyou thử trước hai firing slot trái/phải và giữ side tốt hơn bằng hysteresis. Sau đó
`CombatSpacingController` + `CombatMovementSolver` mới chịu trách nhiệm di chuyển. Sensor không tự điều khiển nhân vật.

## Collision layers liên quan

- World map: mask/layer `8`.
- Hurtbox actor: layer `16`.
- Runtime melee hitbox: layer `32` (đã tách khỏi world layer `8`).

Ice Bolt dùng:

- `HurtboxCollisionMask = 16`
- `WorldCollisionMask = 8`

Projectile thật và AI prediction vì thế đọc cùng một geometry.

## Hành vi mong đợi

1. `Hyou -> cây -> slime`: Hyou không cast, ưu tiên đổi firing lane. Nếu đạn đã bay thì cây chặn thật.
2. `Hyou -> Hikaru -> slime`: Hyou thường không cast vì ally đang chắn corridor.
3. Hikaru chạy vào corridor sau khi Hyou đã release projectile: Hikaru có thể ăn Ice Bolt thật.
4. Hikaru quét kiếm trúng Hyou: Hyou nhận damage thật, nhưng AI vẫn xem Hikaru là ally chứ không đổi target sang Hikaru.

Lưu ý: đây là **line-of-fire + local obstacle awareness**. NavigationRegion2D toàn map vẫn là lớp riêng nếu sau này cần AI tìm đường vòng qua tường dài / mê cung.
