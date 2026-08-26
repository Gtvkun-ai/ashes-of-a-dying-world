# Hyou Frost Ward — elegant scale continuity v1.1

Problem:
- Hyou root is scale x2.
- CastCircle is a child of Hyou, so EndScale 1.62 became 3.24 in world space.
- CombatField2D is spawned at world root, so VisualScale 1.62 remained 1.62.
- Result: the ward visibly shrank by half exactly when it transitioned into the persistent field.

Fix:
- `CombatActionData.ResolveFieldSpec()` exposes the same SpawnField spec to presentation code.
- `HyouFrostWardCastVisual` now resolves the field's authoritative WORLD scale.
- Cast growth is expressed as a ratio of that final field size.
- Local scale compensates inherited Hyou transform per-axis.
- Frost Ward `VisualScale` is 3.24, matching the existing cast-end world size that was already visually approved.
- At t=1 pulse is exactly zero, so the last cast frame and first field frame have the same size.

Changed files:
- scripts/Combat/Data/CombatActionData.cs
- scripts/Combat/Visuals/HyouFrostWardCastVisual.cs
- data/combat/fields/hyou_frost_ward.tres
- scenes/characters/companions/hyou.tscn
