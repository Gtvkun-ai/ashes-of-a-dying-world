# Combat Refactor V2 - Manifest bàn giao

## Phạm vi

Bản này thay toàn bộ lõi combat cũ bằng kiến trúc data-driven dùng chung cho Player, companion và enemy. Ba thư mục cũ `src/Core`, `src/Entities`, `src/Scenes` đã bị loại bỏ; mã nguồn được tổ chức lại theo chức năng dưới `src/App`, `src/Audio`, `src/Characters`, `src/Combat`, `src/Inventory`, `src/Save`, `src/UI`, `src/World`.

## Lõi mới

- `CombatCharacter`: actor chiến đấu chung, không còn mỗi loại nhân vật tự tính damage/block/knockback.
- `CombatStateMachine`: quản lý locomotion, attack startup/active/recovery, block, hitstun, stagger, guard break và dead.
- `CombatActionRunner`: chạy combo từ resource, có buffer, stamina cost, frame window và fallback theo thời gian.
- `CombatHitbox`: một pipeline hitbox runtime duy nhất, có dedupe mục tiêu theo action.
- `CombatResolver`: nơi duy nhất xử lý faction, armor, guard, stamina, poise, hitstun, knockback và tử vong.
- `CombatAbilityRunner`: skill buff, action combat, hồi máu, hồi stamina/guard, cooldown và save/restore.
- `WeaponMovesetData`, `CombatActionData`, `HitProfileData`: moveset và hit profile không còn hardcode trong Player.
- `HyouAI`, `SlimeBrain`: AI đưa intent vào cùng API combat, không gọi nhánh riêng của từng enemy.

## Nội dung cũ đã xóa

- Các partial combat cũ của Player: `Player.Combat.cs`, `Player.Damage.cs`, `Player.Equipment.cs`, `Player.Skills.cs`.
- `Findzone`, `Attackzone` và scene tương ứng.
- Hitbox tĩnh gắn trong scene Wood Sword.
- Damage path riêng `ReceivePlayerAttack`/`ReceiveMeleeHit`.
- Layout `src/Core`, `src/Entities`, `src/Scenes`.

## Resource và scene mới

- Wood Sword có moveset hai đòn bằng resource.
- Slime có action/hit profile/moveset riêng.
- Player, Hyou và Slime đều có faction và combatant identity.
- Player scene được chuyển sang `scenes/actors/player/player.tscn`.
- Save schema tăng lên version 2, vẫn có đường đọc dữ liệu skill version 1.

## Kiểm tra đã chạy

```text
Combat Refactor V2: PASS
 - Cấu trúc cũ đã bị loại bỏ
 - Resource/script/scene references hợp lệ
 - UID và load_steps hợp lệ
 - Action frame windows hợp lệ
 - Delimiter C# cân bằng
```

Ngoài ra `git diff --check` không phát hiện lỗi whitespace. Bộ nguồn người dùng gửi không có `project.godot`, `.csproj` hoặc `.sln`, và môi trường bàn giao không có Godot/.NET, nên chưa thể compile hay chạy scene thật. Checklist runtime nằm trong `docs/COMBAT_TEST_CHECKLIST.md`.
