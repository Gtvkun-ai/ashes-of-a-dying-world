# Project architecture

## Separation of concerns

- `assets/` contains presentation files only: audio, graphics, shaders, and video.
- `data/` contains serialized gameplay configuration such as characters, combat actions, quests, skills, and weapons.
- `scenes/` contains composition and node hierarchy.
- `scripts/` contains executable C# logic.
- `addons/` is treated as vendored code and excluded from local naming rules.

## Asset ownership

Graphics are grouped first by system or world ownership, then by subject. For example:

- `assets/graphics/characters/hyou/vfx/ice_bolt/`
- `assets/graphics/ui/status/main_stats/`
- `assets/graphics/environment/rocks/`
- `assets/graphics/world/whispering_fields/`

This prevents filenames from having to encode their entire history, scale, character name, animation, and someone's late-night mood.

## Scene placement

Reusable world props live under `scenes/world/props/`. Character scenes live under `scenes/characters/`. A `.tscn` file should not be stored beside raw sprite sheets.
