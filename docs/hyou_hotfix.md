# Hyou runtime hotfix

## Root cause

`data/characters/hyou.tres` declares this hard dependency:

```text
res://assets/graphics/backgrounds/hyou_ice_cavern.png
```

The uploaded repository did not contain that PNG. Godot therefore failed to parse the entire
`hyou.tres` resource, not only the optional background field. As a consequence:

- `PlayerStats.ConfigData` was not loaded.
- Hyou fell back to the manual stat profile, whose mana maximum is `0`.
- Decision traces reported `mp=none`.
- `CastPrimary` was rejected because Ice Bolt costs 8 MP and no mana pool existed.

At level 1, the restored Spirit race config gives Hyou 18 Intelligence, so the normal mana
maximum is `18 * 8 + 50 = 194`.

## Changes

- Restored `assets/graphics/backgrounds/hyou_ice_cavern.png` from the original source asset.
- Preserved its Godot resource UID through the matching `.import` metadata.
- Disabled verbose Hyou decision/VFX logging by default. It can still be enabled in the inspector.
- Strengthened `tools/validation/validate_structure.py` to validate hard Godot references and the
  complete Hyou skill/action/projectile chain.

## Verification

Run:

```bash
python3 tools/validation/validate_structure.py
```

Expected result:

```text
PASS: structure, hard references, runtime paths, naming, duplicates, and Hyou resource chain
```

After copying the files into the actual Godot project, close and reopen the editor so the PNG is
imported. Clear the Output panel before testing. The previous `Resource file not found` and
`Failed loading resource: res://data/characters/hyou.tres` errors should be gone.

To inspect combat decisions, temporarily enable `DebugLogging` on `CombatDecisionAgent`. A healthy
Hyou snapshot should show a real mana pool such as `mp=194.0/194.0`, and `CastPrimary` should become
feasible while the target is in range and visible.

The `NativeCalls.cs` messages shown after stopping the debugger concern Godot Mono generated source
lookup and are separate from the missing Hyou resource failure.
