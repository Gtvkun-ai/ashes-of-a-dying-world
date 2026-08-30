# Hikaru Focus V2 + skill cleanup

## Skill cleanup
Retired from production SkillData/Registry because nothing in gameplay or progression references them:
- hikaru_quick_slash
- hikaru_heavy_slash
- hikaru_relentless_cut

The old paths are left as plain Resource tombstones so a file-change-only patch can safely overwrite an existing repo. YARD will no longer index them as SkillData.

## Focus V2
Focus is now a tempo/recovery tool instead of a barely-visible stat buff:
- instant: restore 20 stamina
- duration: 14s
- +10% movement speed
- +35% Dexterity
- cooldown: 40s

At Hikaru's base 10 DEX this rounds to +4 DEX, which noticeably improves attack cadence/stamina economy without directly multiplying physical damage.

## Architecture cleanup
`Player.Skills.cs` no longer owns a second hard-coded Focus definition. If Hikaru's config is missing Focus, it loads the canonical `data/combat/skills/hikaru_focus.tres` resource.

`CombatAbilityRunner` now supports one-shot stamina/guard restoration on TimedBuff activation using the fields SkillData already had, so no Focus-only special case was added.
