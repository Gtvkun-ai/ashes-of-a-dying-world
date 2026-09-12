# Combat Impact / Reaction V1

## Mục tiêu

Pass này tách **damage** khỏi **cảm giác va chạm**. Một đòn gây nhiều damage không mặc định phải hất xa; ngược lại một cú pounce/ủi có thể damage vừa nhưng displacement mạnh.

Các biến chính:

- `BodyMass`: khối lượng tương đối của body. Human chuẩn = `1.0`.
- `ImpactPower`: sức va chạm authored trên từng hit profile.
- `BodyMassInfluence`: đòn này có quan tâm chênh trọng lượng đến đâu.
- `MomentumInfluence`: đòn này có quan tâm tốc độ đóng khoảng cách đến đâu.
- `ReferenceImpactSpeed`: tốc độ tham chiếu để normalize momentum.
- `Poise + Defense`: tạo Stability của target.
- `CanKnockdown / CanLaunch`: capability gate, không phải force effect.

## Công thức mass

```text
raw_mass = clamp(2 * attacker_mass / (attacker_mass + target_mass), 0.40, 1.60)
mass_factor = lerp(1.0, raw_mass, BodyMassInfluence)
```

Hai body bằng nhau cho `1.0`. Công thức này mềm hơn `attacker_mass / target_mass`, tránh boss hoặc quái nặng làm số bùng quá lớn.

## Momentum

```text
closing_speed = max(0, dot(attacker_velocity - target_velocity, impact_direction))
raw_momentum = clamp(0.80 + 0.40 * closing_speed / ReferenceImpactSpeed, 0.75, 1.40)
momentum_factor = lerp(1.0, raw_momentum, MomentumInfluence)
```

`CombatCharacter.ImpactVelocity` cố ý **không cộng external knockback cũ**. Nếu không, actor đang bị hất có thể vô tình mang tốc độ đó vào cú đánh kế tiếp.

Projectile/field hiện đặt `MomentumInfluence = 0` vì resolver đang nhìn velocity của caster, không phải velocity projectile. Sau này nếu cần projectile momentum thật thì truyền impact velocity riêng trong `HitRequest`.

## Stability

```text
base_stability = 40 + MaxPoise * 0.55 + Defense * 0.75
poise_state = lerp(0.68, 1.0, CurrentPoise / MaxPoise)
stability = base_stability * poise_state * state_factor
```

`MaxPoise` vốn đã nhận Vitality + Defense từ stat system. Defense chỉ cộng nhẹ lần nữa để không double-dip quá mạnh.

DEF còn có một lớp giảm displacement nhỏ với diminishing return:

```text
weighted_def = Defense * 0.6
control_resist = clamp(weighted_def / (100 + weighted_def), 0, 0.25)
```

Cap 25%. DEF không còn trực tiếp tạo 75% knockback resistance như trước.

## Reaction bands

```text
ImpactRatio = EffectiveImpact / Stability

< 0.45       Absorb
0.45 - 0.75  Flinch
0.75 - 1.00  Shove
1.00 - 1.30  Knockback
1.30 - 1.65  Stagger
1.65 - 2.05  Knockdown
>= 2.05      Launch
```

Poise break vẫn nâng reaction tối thiểu lên `Stagger`.

`ForceStagger` được giữ chỉ để tương thích resource/scripted hit cũ. Profile mới nên để resolver tự quyết định.

`CanKnockdown` và `CanLaunch` là cổng thật. Ví dụ profile có `LaunchHeight = 16` nhưng `CanLaunch = false` thì target không bị nhấc lên.

## Knockback

`KnockbackForce` giữ nguyên tên để không gãy `.tres`, nhưng runtime hiểu nó là **initial knockback velocity (px/s)**:

```text
kb_speed = KnockbackForce
         * mass_factor
         * momentum_factor
         * shatter_factor
         * (1 - control_resist)
         * reaction_scale
```

`CharacterBody2D` sau đó dùng `_externalVelocity` và `ExternalForceDecay` để tạo quãng trượt.

## Slime sau pass này

### Attack thường / shove

`slime_bite` giữ ID để không gãy registry/audio/moveset, nhưng presentation giờ dùng sheet bite/ủi mới.

```text
BodyMass slime        = 0.70
ImpactPower           = 95
KnockbackForce        = 150
BodyMassInfluence     = 0.85
MomentumInfluence     = 0.45
ReferenceImpactSpeed  = 60
```

Với Hikaru mass `1.0`, DEF/VIT level đầu hiện tại, cú này thường rơi vào vùng **Shove**: trượt thấy rõ nhưng không bay.

### Pounce

Pounce dùng bốn hàng jump cũ của `slime_sheet.png` và action riêng `slime_pounce`.

```text
ImpactPower           = 175
KnockbackForce        = 285
BodyMassInfluence     = 1.0
MomentumInfluence     = 1.0
CanKnockdown          = true
CanLaunch             = true
```

AI chỉ cân nhắc pounce ở khoảng cách vừa, có cooldown + decision interval để tránh roll random mỗi physics frame. Với target human bình thường và cú lao đủ closing speed, pounce có thể chạm ngưỡng Launch; target nặng hơn hoặc đang chạy cùng hướng có thể chỉ nhận Knockback/Stagger.

## Sprite slime

- `at_right/left/up/down`: chuyển sang `slime_die_bite_padded.png`, 5 frame, active hit chỉ frame 3.
- `pounce_right/left/up/down`: dùng bốn row jump cũ của `slime_sheet.png`.
- `death`: row 0 của sheet bite padded (đã slice nhưng chưa nối death playback vì slime hiện queue_free ngay khi chết).
- `go_*`: loop thật.

`slime_die_bite_padded.png` là bản **giữ nguyên pixel**, chỉ thêm 32 px transparent phía trên mỗi cell 112x80 để thành cell 112x112. Baseline vì thế khớp `slime_sheet.png`; không dùng resample hay AI redraw.

## BodyMass hiện tại

- Hikaru: `1.00`
- Hyou: `0.92`
- Slime basic: `0.70`

BodyMass không scale theo level. Nếu có big slime/ogre/boss, đặt mass theo body/species/profile thay vì tăng từ STR/DEF.

## File chính

- `scripts/Combat/Runtime/ImpactReactionResolver.cs`
- `scripts/Combat/Model/ImpactResolution.cs`
- `scripts/Combat/Model/CombatEnums.cs`
- `scripts/Combat/Data/HitProfileData.cs`
- `scripts/Characters/Stats/PlayerStats.cs`
- `scripts/Characters/Data/CombatStatProfileData.cs`
- `scripts/Characters/Data/CharacterConfig.cs`
- `scripts/Combat/AI/SlimeBrain.cs`
- `scenes/characters/enemies/slime_01.tscn`

## Gợi ý tune sau khi chạy game

Không tune `ImpactPower` và `KnockbackForce` cùng lúc nếu chưa biết lỗi nằm ở đâu:

1. Sai loại phản ứng (`Flinch/Shove/Stagger/...`) -> tune `ImpactPower`, mass/momentum influence, Stability.
2. Loại phản ứng đúng nhưng trượt quá xa/gần -> tune `KnockbackForce` hoặc `ExternalForceDecay`.
3. Pounce quá hay xảy ra -> tune `PounceChance`, `PounceCooldown`, distance band.
4. Pounce đúng reaction nhưng launch nhìn quá cao/thấp -> tune `LaunchHeight`, không tăng ImpactPower.
