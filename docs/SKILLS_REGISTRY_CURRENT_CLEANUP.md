# Skills Registry current cleanup

What was stale:
- `hyou_crystal_step`
- `hyou_frost_focus`
- Frost Ward existed in gameplay/skill tree but had no UID, so YARD did not index it.

Additional stale reference found:
- Hikaru's `main.tres` and `hikaru_skill_tree.tres` still referenced `hyou_crystal_step`.

Fix:
- assign Frost Ward stable UID `uid://ctd73bolw74oc`
- reuse the retired Frost Focus UID as a migration UID for Frost Ward
- retire the two legacy Hyou skill resources as non-SkillData tombstones
- registry now contains only current SkillData resources
- update Hyou references to Frost Ward UID
- remove the accidental Crystal Step references from Hikaru

No guess was made about which new Hikaru slash skill should replace Crystal Step in
Hikaru's active loadout/skill tree. That progression should be authored separately.
