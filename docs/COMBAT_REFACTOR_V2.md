# Combat Refactor V2

## Mục tiêu

Combat V2 thay toàn bộ pipeline chiến đấu cũ bằng một lõi dùng chung cho Player, companion và enemy. Actor không còn tự tính damage, tự bắt hitbox hoặc tự giữ các boolean chiến đấu riêng.

## Cấu trúc mới

```text
src/
├── Combat/
│   ├── Actors/CombatCharacter.cs
│   ├── AI/HyouAI.cs
│   ├── AI/SlimeBrain.cs
│   ├── Abilities/SkillData.cs
│   ├── Data/
│   │   ├── CombatActionData.cs
│   │   ├── HitProfileData.cs
│   │   └── WeaponMovesetData.cs
│   ├── Model/
│   │   ├── CombatEnums.cs
│   │   ├── HitRequest.cs
│   │   └── HitResult.cs
│   └── Runtime/
│       ├── CombatAbilityRunner.cs
│       ├── CombatActionRunner.cs
│       ├── CombatHitbox.cs
│       ├── CombatResolver.cs
│       ├── CombatStateMachine.cs
│       └── FactionRules.cs
├── Characters/
│   ├── Player/
│   ├── Companion/
│   ├── Enemies/
│   ├── Stats/
│   ├── Data/
│   └── Party/
├── Inventory/
├── Audio/
├── Save/
├── App/
├── UI/
└── World/
```

Các thư mục `src/Core`, `src/Entities` và `src/Scenes` cũ đã bị loại bỏ hoàn toàn. Những module không thuộc combat được chuyển sang thư mục theo chức năng nhưng giữ namespace tương thích để hạn chế làm gãy UI/save hiện có.

## Pipeline runtime

```text
Player input / AI intent
        ↓
CombatCharacter command API
        ↓
CombatStateMachine
        ↓
CombatActionRunner / CombatAbilityRunner
        ↓
CombatActionData + WeaponMovesetData
        ↓
CombatHitbox
        ↓
HitRequest
        ↓
CombatResolver
        ↓
HitResult
        ↓
HP / Guard / Poise / Stamina + state cưỡng bức
        ↓
Signal và presentation
```

## Những thay đổi chính

### Actor chung

`CombatCharacter` là lõi duy nhất cho Player, Hyou và Slime. Nó quản lý movement, facing, state, action, ability, hitbox runtime, nhận hit, knockback và death flow. Player và AI chỉ gửi intent qua các API như `SetMoveInput`, `SetBlocking`, `RequestAttack`.

### State machine thật

Combat state không còn là cụm boolean rời rạc. Các state hiện có:

- `Locomotion`
- `Blocking`
- `AttackStartup`
- `AttackActive`
- `AttackRecovery`
- `BlockStun`
- `Hitstun`
- `Stagger`
- `GuardBreak`
- `Dead`

Attack không thể đồng thời block. Forced state có timer và tự trở lại locomotion.

### Combat data-driven

Đòn đánh không còn hardcode trong Player. Mỗi đòn là một `CombatActionData`, chứa animation, frame window, fallback timing, stamina cost, lunge và `HitProfileData`.

`WeaponMovesetData` quản lý combo và guard của từng vũ khí. Wood Sword hiện có combo hai đòn bằng resource. Animation guard cũng nằm trong moveset, không còn chuỗi `block_woodSword_*` đóng đinh trong actor.

### Một hitbox pipeline

Hitbox cũ trong scene vũ khí, `Findzone`, `Attackzone`, callback damage riêng của Player/NPC/Slime đã bị xóa. `CombatHitbox` được tạo runtime, mở đúng active window, scan overlap và khử trùng mục tiêu trong một action.

### Damage resolver trung tâm

`CombatResolver` là nơi duy nhất xử lý:

- faction và friendly fire;
- attack power;
- armor curve;
- directional block;
- guard damage;
- stamina guard cost;
- guard break;
- poise và stagger;
- hitstun;
- knockback resistance;
- lethal result.

### Faction

- Player và Companion là đồng minh.
- Enemy là đồng minh với Enemy.
- Neutral không gây hoặc nhận damage mặc định.

Friendly fire không còn phụ thuộc việc từng class có nhớ viết một nhánh `if` hay không.

### Ability runner

Skill không còn bị mặc định xem là timed buff. `CombatAbilityRunner` hỗ trợ:

- timed buff;
- combat action;
- heal;
- restore stamina/guard;
- cooldown;
- mana/stamina cost;
- save/restore trạng thái skill.

Temporary stat modifier được phân theo source ID để nhiều buff không xóa nhầm nhau.

### AI

- `HyouAI` là companion FSM thật: follow, chase, attack, guard, reposition.
- `SlimeBrain` quản lý wander, aggro, leash, chase và attack.
- Cả hai chỉ phát intent và tìm target qua faction, không gọi trực tiếp damage method của class khác.

### Save

Save schema được nâng lên version 2. Skill save hỗ trợ execution type, combat action, heal và resource restore. Dữ liệu version 1 vẫn có default hợp lệ vì execution type mặc định là timed buff.

## Resource combat hiện có

```text
assets/resources/data/combat/
├── actions/
│   ├── wood_sword_light_1.tres
│   ├── wood_sword_light_2.tres
│   └── slime_bite.tres
├── hit_profiles/
│   ├── wood_sword_light_1.tres
│   ├── wood_sword_light_2.tres
│   └── slime_bite.tres
└── movesets/
    ├── wood_sword.tres
    └── slime.tres
```

## Cách thêm vũ khí

1. Tạo một hoặc nhiều `HitProfileData`.
2. Tạo `CombatActionData` cho từng đòn.
3. Tạo `WeaponMovesetData` và nối combo.
4. Gán moveset vào `EquipmentItemData.Moveset`.
5. Không sửa Player, HyouAI hay CombatResolver.

## Cách thêm enemy

1. Tạo scene root kế thừa `CombatCharacter` hoặc một subclass mỏng.
2. Thêm `PlayerStats` với manual profile hoặc CharacterConfig.
3. Gán faction Enemy và một default moveset.
4. Viết brain chỉ phát intent qua command API.
5. Hurtbox dùng collision layer 16, mask 8.

## Cách thêm skill gây damage

1. Tạo `CombatActionData` riêng cho skill.
2. Tạo `SkillData` với `ExecutionType = CombatAction`.
3. Gán `CombatAction` vào skill.
4. Ability runner sẽ kiểm tra cooldown/resource và đưa action vào cùng state/hitbox/resolver pipeline.

## Phần đã xóa

- `Player.Combat.cs`
- `Player.Damage.cs`
- `Player.Equipment.cs` cũ
- Player monolith cũ
- NPC damage callback riêng
- Slime monolith cũ
- `Findzone.cs`, `Attackzone.cs`
- `findzone.tscn`, `attackzone.tscn`
- hitbox tĩnh trong `woodSword.tscn`
- toàn bộ layout `src/Core`, `src/Entities`, `src/Scenes` cũ

## Lưu ý kiểm thử

Gói nguồn được cung cấp không chứa `project.godot`, `.csproj` hoặc `.sln`, vì vậy không thể build Godot C# trong môi trường xử lý này. Script `tools/validate_combat_refactor.py` kiểm tra cấu trúc, resource path, UID, frame window và delimiter C# trước khi đóng gói. Kiểm thử runtime cần thực hiện trong project Godot đầy đủ.
