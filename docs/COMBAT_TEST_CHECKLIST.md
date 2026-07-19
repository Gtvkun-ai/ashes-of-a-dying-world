# Combat V2 - Runtime Test Checklist

## Boot và scene

- Project import không báo missing script/resource.
- Player spawn đúng body, camera và inventory.
- Hyou spawn đúng body, tự equip Wood Sword và không nhận input người chơi.
- Slime có health bar và không còn node `findzone`/`attackzone`.

## Player attack

- Không có vũ khí: light attack không chạy và không mất stamina.
- Có Wood Sword: đòn 1 dùng frame 0-4, hit ở frame 2-3.
- Buffer lần hai: chuyển sang đòn 2 frame 5-8, hit ở frame 6-7.
- Mỗi mục tiêu chỉ nhận một hit trong một active window.
- Bấm attack khi đang block không mất stamina.
- Thiếu stamina ở hit 2 không làm actor kẹt recovery.

## Block, guard và poise

- Block chỉ chặn mục tiêu ở trong guard arc.
- Đòn phía sau đi thẳng vào HP.
- Block tiêu cả guard và stamina.
- Không đủ guard/stamina tạo GuardBreak và xóa block command.
- Poise về 0 tạo Stagger.
- Guard/poise hồi sau đúng delay, không hồi trong state bị khóa.

## Faction

- Player không gây damage cho Hyou.
- Hyou không chọn Player làm hostile target.
- Hyou và Player cùng gây damage cho Slime.
- Slime không gây damage cho Slime khác.

## AI

- Hyou follow Player khi không có enemy.
- Hyou chase/attack enemy trong radius.
- Hyou guard khi enemy đang startup/active và hướng về Hyou.
- Slime wander quanh spawn thật, không đổi spawn sau mỗi lần ngừng chase.
- Slime bỏ target ngoài leash và quay về spawn.
- Slime chỉ gây damage khi runtime hitbox thật chạm hurtbox.

## Ability và save

- Focus tăng movement và Dexterity đúng 60 giây.
- Cooldown ngăn kích hoạt lại.
- Tắt buff không xóa modifier từ source khác.
- Save/load phục hồi skill, cooldown và thời gian buff còn lại.
- Save version 1 cũ vẫn load skill như timed buff.

## Death và load

- Enemy chết unregister health bar và queue free một lần.
- Player chết vào state Dead, không di chuyển/attack/block.
- Load game khi đang attack xóa action/hitbox/velocity.
- Load game với HP > 0 trở lại Locomotion.
