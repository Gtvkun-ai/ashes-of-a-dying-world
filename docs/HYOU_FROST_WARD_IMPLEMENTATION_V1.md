# Hyou Frost Ward — implementation v1

## Runtime flow

1. Hyou starts `hyou_frost_ward`.
2. Cast lasts **2.0 seconds** using the existing `ice_bolt_{dir}` body cast animation at 0.7 playback speed.
3. During the cast only `frost_ward_circle.png` is shown under Hyou's feet, charging from faint/small to bright/full size.
4. At frame 7 the action emits `SpawnField`.
5. The persistent field appears at the cast position; the center crystal rises for ~0.24 s.
6. After the crystal has fully risen, the field becomes active for **7 seconds**.
7. Enemy entering the radius is repelled outward and gains **Chill +1** + slow.
8. A target must leave the field and enter again to trigger it again. No per-frame Chill spam.
9. Chill uses the same `CombatStatusController` path as Ice Bolt. At 3 stacks it freezes for 0.95 s.

## Current tuning

- Cast: 2.0 s
- Mana: 24
- Cooldown: 12 s
- Radius: 56 px
- Active duration: 7 s
- Knockback: 172
- Slow: 18% for 2.4 s
- Chill: +1 for 6 s
- Freeze threshold: 3 stacks
- Freeze duration: 0.95 s
- Damage: 0

## AI

Cryomancer now exposes a secondary skill. Hyou considers Frost Ward in a pre-emptive setup band just outside PanicRange, because a 2-second defensive cast should not begin when an enemy is already on top of him.

## Test scene

Run:

`res://scenes/tests/hyou_frost_ward_test.tscn`

Useful logs:

- `CastDefensive(hyou_frost_ward)`
- `[CombatActionEvent] ... type=SpawnField`
- `[CombatField] ARMED ...`
- `[CombatField] ACTIVE ...`
- `[CombatField] TRIGGER ... chill=1 knockback=172`
