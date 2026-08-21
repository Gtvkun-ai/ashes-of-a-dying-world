# Balance rework

Các registry nên mở trong YARD:

- `res://data/registries/balance_registry.tres`: công thức derived stat toàn game.
- `res://data/registries/stat_profiles_registry.tres`: stat trực tiếp của enemy/NPC, hiện có slime.
- `res://data/registries/skills_registry.tres`: cooldown, mana/stamina cost, damage multiplier.
- `res://data/registries/combat_actions_registry.tres`: stamina cost và timing của action.
- `res://data/registries/hit_profiles_registry.tres`: base damage, power scaling, guard/poise damage.

Baseline hiện tại:

- Hikaru Lv1 + kiếm gỗ: 188 HP, 106 stamina, ~32 Physical Power, ~18.8 stamina/s.
- Hyou Lv1: 142 MP, ~46 Magic Power, ~2.1 mana/s.
- Slime cơ bản: 200 HP, 18 Physical Power.
- Kiếm gỗ: light 1 = 6 stamina, light 2 = 7, heavy = 14.
- Ice Bolt: 14 MP, 2.4s cooldown, x1.0 damage.
- Ice Lance: 26 MP, 5.0s cooldown, x1.55 damage.

`SkillData.DamageMultiplier` đã được nối vào hit runtime cho cả melee và projectile. Hit profile có `PowerScaling`: Physical hoặc Magic. Damage Magic/Ice dùng Magic Resistance; damage vật lý dùng Armor.

Progression / XP:

- `core_power.tres`: `BaseExperienceToNextLevel = 100`, `ExperienceGrowthMultiplier = 1.12`, làm tròn theo bước 5 XP.
- `slime_basic.tres`: `ExperienceReward = 20` XP. Với curve hiện tại, Lv1 -> Lv2 cần 5 slime cơ bản.
- XP được chia đầy đủ cho các thành viên party có `CharacterConfig`, không phụ thuộc ai tung đòn kết liễu.
- HUD party có ribbon `LV | XP bar | XP nữa` cho nhân vật đang điều khiển. Khi level-up, nhãn bên phải hiện `LÊN CẤP!` trong khoảng 1.1 giây.
- Save version 7 lưu cả level + XP hiện tại của Hikaru và companion; save cũ tải với XP = 0.
