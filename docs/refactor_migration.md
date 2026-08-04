# Refactor migration notes

## Completed changes

- Moved C# source from `src/` to `scripts/`.
- Separated gameplay data into `data/`.
- Reorganized graphics by ownership and normalized names.
- Moved misplaced character and weapon scenes out of asset/data folders.
- Repaired the broken tree, stone, flower, and grass texture references.
- Removed the nested `src/UI.zip` backup and the unrelated `src/UI/HUD/j.json` dataset from the active tree.
- Quarantined unreferenced and duplicate graphics instead of deleting them irreversibly.
- Moved historical one-off validators under `tools/validation/legacy/`.

## Static-validation boundary

Godot and the .NET SDK were unavailable in the execution environment, and the supplied archive omitted the project files. References were therefore validated statically, not by importing the project in Godot.

## Path map

| Old path | New path |
|---|---|
| `assets/music/bgm/Bg1.mp3` | `assets/audio/music/bgm/bg_01.mp3` |
| `assets/music/bgm/Bg2.mp3` | `assets/audio/music/bgm/bg_02.mp3` |
| `assets/music/bgm/Bg3.mp3` | `assets/audio/music/bgm/bg_03.mp3` |
| `assets/music/sound_effect/foot_step/grass_step.mp3` | `assets/audio/sfx/footsteps/grass_step.mp3` |
| `assets/music/sound_effect/foot_step/normal_step.mp3` | `assets/audio/sfx/footsteps/normal_step.mp3` |
| `assets/music/sound_effect/foot_step/normal_step_01.mp3` | `assets/audio/sfx/footsteps/normal_step_01.mp3` |
| `assets/music/sound_effect/foot_step/normal_step_02.mp3` | `assets/audio/sfx/footsteps/normal_step_02.mp3` |
| `assets/music/sound_effect/hammer/hammer_slash_01.mp3` | `assets/audio/sfx/tools/hammer/hammer_slash_01.mp3` |
| `assets/music/sound_effect/sword/wooden_slash_01.mp3` | `assets/audio/sfx/weapons/sword/wooden_slash_01.mp3` |
| `assets/music/story_song/emotional_bgm.mp3` | `assets/audio/music/story/emotional_bgm.mp3` |
| `assets/music/story_song/wedding_bgm.mp3` | `assets/audio/music/story/wedding_bgm.mp3` |
| `assets/resources/data/audio/footsteps/normal_step_01.tres` | `data/audio/footsteps/normal_step_01.tres` |
| `assets/resources/data/audio/footsteps/normal_step_02.tres` | `data/audio/footsteps/normal_step_02.tres` |
| `assets/resources/data/characters/Hyou.tres` | `data/characters/hyou.tres` |
| `assets/resources/data/characters/Hyou.tscn` | `scenes/characters/companions/hyou.tscn` |
| `assets/resources/data/characters/Main.tres` | `data/characters/main.tres` |
| `assets/resources/data/combat/actions/hyou_ice_bolt.tres` | `data/combat/actions/hyou_ice_bolt.tres` |
| `assets/resources/data/combat/actions/slime_bite.tres` | `data/combat/actions/slime_bite.tres` |
| `assets/resources/data/combat/actions/wood_sword_light_1.tres` | `data/combat/actions/wood_sword_light_1.tres` |
| `assets/resources/data/combat/actions/wood_sword_light_2.tres` | `data/combat/actions/wood_sword_light_2.tres` |
| `assets/resources/data/combat/decision/classes/cryomancer.tres` | `data/combat/decision/classes/cryomancer.tres` |
| `assets/resources/data/combat/decision/doctrines/hyou_safe_control.tres` | `data/combat/decision/doctrines/hyou_safe_control.tres` |
| `assets/resources/data/combat/decision/personalities/hyou_calm_protective.tres` | `data/combat/decision/personalities/hyou_calm_protective.tres` |
| `assets/resources/data/combat/hit_profiles/hyou_ice_bolt.tres` | `data/combat/hit_profiles/hyou_ice_bolt.tres` |
| `assets/resources/data/combat/hit_profiles/slime_bite.tres` | `data/combat/hit_profiles/slime_bite.tres` |
| `assets/resources/data/combat/hit_profiles/wood_sword_light_1.tres` | `data/combat/hit_profiles/wood_sword_light_1.tres` |
| `assets/resources/data/combat/hit_profiles/wood_sword_light_2.tres` | `data/combat/hit_profiles/wood_sword_light_2.tres` |
| `assets/resources/data/combat/movesets/hyou_cryomancer.tres` | `data/combat/movesets/hyou_cryomancer.tres` |
| `assets/resources/data/combat/movesets/slime.tres` | `data/combat/movesets/slime.tres` |
| `assets/resources/data/combat/movesets/wood_sword.tres` | `data/combat/movesets/wood_sword.tres` |
| `assets/resources/data/combat/projectiles/hyou_ice_bolt.tres` | `data/combat/projectiles/hyou_ice_bolt.tres` |
| `assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres` | `data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres` |
| `assets/resources/data/combat/skills/hikaru_battle_trance.tres` | `data/combat/skills/hikaru_battle_trance.tres` |
| `assets/resources/data/combat/skills/hikaru_focus.tres` | `data/combat/skills/hikaru_focus.tres` |
| `assets/resources/data/combat/skills/hikaru_heavy_slash.tres` | `data/combat/skills/hikaru_heavy_slash.tres` |
| `assets/resources/data/combat/skills/hikaru_quick_slash.tres` | `data/combat/skills/hikaru_quick_slash.tres` |
| `assets/resources/data/combat/skills/hikaru_relentless_cut.tres` | `data/combat/skills/hikaru_relentless_cut.tres` |
| `assets/resources/data/combat/skills/hikaru_second_wind.tres` | `data/combat/skills/hikaru_second_wind.tres` |
| `assets/resources/data/combat/skills/hyou_cold_recovery.tres` | `data/combat/skills/hyou_cold_recovery.tres` |
| `assets/resources/data/combat/skills/hyou_crystal_step.tres` | `data/combat/skills/hyou_crystal_step.tres` |
| `assets/resources/data/combat/skills/hyou_frost_focus.tres` | `data/combat/skills/hyou_frost_focus.tres` |
| `assets/resources/data/combat/skills/hyou_frozen_will.tres` | `data/combat/skills/hyou_frozen_will.tres` |
| `assets/resources/data/combat/skills/hyou_ice_bolt.tres` | `data/combat/skills/hyou_ice_bolt.tres` |
| `assets/resources/data/combat/skills/hyou_ice_lance.tres` | `data/combat/skills/hyou_ice_lance.tres` |
| `assets/resources/data/icon/DEF.tres` | `data/icons/def.tres` |
| `assets/resources/data/icon/DEX.tres` | `data/icons/dex.tres` |
| `assets/resources/data/icon/Exit.tres` | `data/icons/exit.tres` |
| `assets/resources/data/icon/INT.tres` | `data/icons/int.tres` |
| `assets/resources/data/icon/SPI.tres` | `data/icons/spi.tres` |
| `assets/resources/data/icon/STR .tres` | `data/icons/str.tres` |
| `assets/resources/data/icon/VIT.tres` | `data/icons/vit.tres` |
| `assets/resources/data/icon/default_skill.tres` | `data/icons/default_skill.tres` |
| `assets/resources/data/quests/flowers_on_ashes.tres` | `data/quests/flowers_on_ashes.tres` |
| `assets/resources/data/quests/hyou_promise.tres` | `data/quests/hyou_promise.tres` |
| `assets/resources/data/quests/traces_in_the_wind.tres` | `data/quests/traces_in_the_wind.tres` |
| `assets/resources/data/races/human.tres` | `data/races/human.tres` |
| `assets/resources/data/races/spiritIce.tres` | `data/races/spirit_ice.tres` |
| `assets/resources/data/skill_trees/hikaru_skill_tree.tres` | `data/skill_trees/hikaru_skill_tree.tres` |
| `assets/resources/data/skill_trees/hyou_skill_tree.tres` | `data/skill_trees/hyou_skill_tree.tres` |
| `assets/resources/data/weapons/sword/WoodSword.tres` | `data/weapons/sword/wood_sword.tres` |
| `assets/resources/data/weapons/sword/woodSword.tscn` | `scenes/items/weapons/wood_sword.tscn` |
| `assets/shader/chroma_key.gdshader` | `assets/shaders/chroma_key.gdshader` |
| `assets/shader/outline.gdshader` | `assets/shaders/outline.gdshader` |
| `assets/sprites/BG/Hyou_BG.png` | `assets/graphics/backgrounds/hyou_ice_cavern.png` |
| `assets/sprites/UI_HUD/Inventory/category_all.png` | `assets/graphics/ui/inventory/category_all.png` |
| `assets/sprites/UI_HUD/Inventory/category_consumables.png` | `assets/graphics/ui/inventory/category_consumables.png` |
| `assets/sprites/UI_HUD/Inventory/category_equipment.png` | `assets/graphics/ui/inventory/category_equipment.png` |
| `assets/sprites/UI_HUD/Inventory/category_materials.png` | `assets/graphics/ui/inventory/category_materials.png` |
| `assets/sprites/UI_HUD/Inventory/category_more.png` | `assets/graphics/ui/inventory/category_more.png` |
| `assets/sprites/UI_HUD/Inventory/category_quest.png` | `assets/graphics/ui/inventory/category_quest.png` |
| `assets/sprites/UI_HUD/Inventory/frame_9slice.png` | `assets/graphics/ui/inventory/frame_9slice.png` |
| `assets/sprites/UI_HUD/Inventory/grain.png` | `assets/graphics/ui/inventory/grain.png` |
| `assets/sprites/UI_HUD/Inventory/icon_bag.png` | `assets/graphics/ui/inventory/icon_bag.png` |
| `assets/sprites/UI_HUD/Inventory/icon_coin.png` | `assets/graphics/ui/inventory/icon_coin.png` |
| `assets/sprites/UI_HUD/Main_HUD/MHUD.png` | `assets/graphics/ui/hud/main_hud.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/hp ic.png` | `assets/graphics/ui/status/main_stats/hp_frame.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/hp.png` | `assets/graphics/ui/status/main_stats/hp_fill.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/mp ic.png` | `assets/graphics/ui/status/main_stats/mp_frame.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/mp.png` | `assets/graphics/ui/status/main_stats/mp_fill.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/sta ic.png` | `assets/graphics/ui/status/main_stats/stamina_frame.png` |
| `assets/sprites/UI_HUD/Status_bar/3 main stat/sta.png` | `assets/graphics/ui/status/main_stats/stamina_fill.png` |
| `assets/sprites/UI_HUD/Status_bar/Hp.png` | `assets/graphics/ui/status/resource_frame_hp.png` |
| `assets/sprites/UI_HUD/Status_bar/Khung.png` | `assets/graphics/ui/status/resource_panel_frame.png` |
| `assets/sprites/UI_HUD/Status_bar/Mana.png` | `assets/graphics/ui/status/resource_frame_mana.png` |
| `assets/sprites/UI_HUD/Status_bar/Stamina.png` | `assets/graphics/ui/status/resource_frame_stamina.png` |
| `assets/sprites/UI_HUD/Status_bar/hp_enemy.png` | `assets/graphics/ui/status/enemy_hp_bar.png` |
| `assets/sprites/button/ButtonMain.png` | `assets/graphics/ui/icons/menu_action_icons_sheet.png` |
| `assets/sprites/button/characterbutton.png` | `assets/graphics/ui/buttons/character.png` |
| `assets/sprites/button/inventorybutton.png` | `assets/graphics/ui/buttons/inventory.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou bh ice .png` | `assets/graphics/characters/hyou/vfx/ice_bolt/back_ice.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou bh ice bolt.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/back_ice_bolt.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou body.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/body.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou ice bh.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/ice_behind.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou ice up.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/ice_up.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou up ice bolt.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/up_ice_bolt.png` |
| `assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou up ice.png` | `assets/graphics/characters/hyou/vfx/ice_bolt/up_ice.png` |
| `assets/sprites/char/Hyou/HyouIco.png` | `assets/graphics/characters/hyou/icon.png` |
| `assets/sprites/char/Hyou/Hyou_avt.ogv` | `assets/video/portraits/hyou.ogv` |
| `assets/sprites/char/Hyou/Hyou_body.tscn` | `scenes/characters/companions/hyou_body.tscn` |
| `assets/sprites/char/Hyou/Reira_run 4 h#U01b0#U1edbng c#U00f2n l#U1ea1i.png` | `assets/graphics/characters/hyou/run_4_direction.png` |
| `assets/sprites/char/Hyou/Reira_run.png` | `assets/graphics/characters/hyou/run.png` |
| `assets/sprites/char/Hyou/Reira_walk 4 h#U01b0#U1edbng c#U00f2n l#U1ea1i.png` | `assets/graphics/characters/hyou/walk_4_direction.png` |
| `assets/sprites/char/Hyou/Reira_walk.png` | `assets/graphics/characters/hyou/walk.png` |
| `assets/sprites/char/Hyou/toctim slash fix size.png` | `assets/graphics/characters/hyou/slash.png` |
| `assets/sprites/char/main/MainIco.png` | `assets/graphics/characters/player/icon.png` |
| `assets/sprites/char/main/Main_Body.tscn` | `scenes/characters/player/main_body.tscn` |
| `assets/sprites/char/main/Mainavt.ogv` | `assets/video/portraits/player.ogv` |
| `assets/sprites/char/main/animation slash.png` | `assets/graphics/characters/player/slash.png` |
| `assets/sprites/char/main/mainrun.png` | `assets/graphics/characters/player/run.png` |
| `assets/sprites/char/main/mainwalk.png` | `assets/graphics/characters/player/walk.png` |
| `assets/sprites/environment item/cay cao 01.png` | `assets/graphics/environment/trees/apple_tree.png` |
| `assets/sprites/environment item/cay.png` | `assets/graphics/environment/trees/tree.png` |
| `assets/sprites/environment item/grass1.png` | `assets/graphics/environment/flora/grass_patch.png` |
| `assets/sprites/environment item/stone 12345.png` | `assets/graphics/environment/rocks/stone_variants.png` |
| `assets/sprites/environment item/v#U1eadt th#U1ec3.png` | `assets/graphics/environment/flora/flora_sheet.png` |
| `assets/sprites/field1.png` | `assets/graphics/world/whispering_fields/field_01.png` |
| `assets/sprites/icon/statsIco.png` | `assets/graphics/ui/icons/stat_icons_sheet.png` |
| `assets/sprites/icon/swordwoodIco.png` | `assets/graphics/items/weapons/wood_sword_icon.png` |
| `assets/sprites/monster/slime/slime 2.png` | `assets/graphics/characters/enemies/slime/slime_sheet.png` |
| `assets/sprites/setting item/exitBtn.png` | `assets/graphics/ui/menus/login/exit_button.png` |
| `assets/sprites/setting item/loginBtn.png` | `assets/graphics/ui/menus/login/login_button.png` |
| `assets/sprites/setting item/loginForm.png` | `assets/graphics/ui/menus/login/form.png` |
| `assets/video/bgLogin.ogv` | `assets/video/login_background.ogv` |
| `scenes/actors/player/main_animation.tscn` | `scenes/characters/player/main_animation.tscn` |
| `scenes/actors/player/player.tscn` | `scenes/characters/player/player.tscn` |
| `scenes/main/screen_main.tscn` | `scenes/app/screen_main.tscn` |
| `scenes/ui/CharacterUnitHUD.tscn` | `scenes/ui/hud/character_unit_hud.tscn` |
| `scenes/ui/GameMenuButton.tscn` | `scenes/ui/menus/game_menu_button.tscn` |
| `scenes/ui/PartyHUD.tscn` | `scenes/ui/hud/party_hud.tscn` |
| `scenes/world/WhisperingFields/Field1.tscn` | `scenes/world/whispering_fields/field_01.tscn` |
| `scenes/world/WhisperingFields/slime_1.tscn` | `scenes/characters/enemies/slime_01.tscn` |
| `scenes/world/apple_tree.tscn` | `scenes/world/props/trees/apple_tree.tscn` |
| `scenes/world/blue_flower.tscn` | `scenes/world/props/flora/blue_flower.tscn` |
| `scenes/world/grass.tscn` | `scenes/world/props/flora/grass.tscn` |
| `scenes/world/grass1.tscn` | `scenes/world/props/flora/grass_patch.tscn` |
| `scenes/world/purple_flower.tscn` | `scenes/world/props/flora/purple_flower.tscn` |
| `scenes/world/red_flower.tscn` | `scenes/world/props/flora/red_flower.tscn` |
| `scenes/world/stone_1.tscn` | `scenes/world/props/rocks/stone_01.tscn` |
| `scenes/world/stone_2.tscn` | `scenes/world/props/rocks/stone_02.tscn` |
| `scenes/world/stone_3.tscn` | `scenes/world/props/rocks/stone_03.tscn` |
| `scenes/world/stone_4.tscn` | `scenes/world/props/rocks/stone_04.tscn` |
| `scenes/world/stone_5.tscn` | `scenes/world/props/rocks/stone_05.tscn` |
| `scenes/world/tree.tscn` | `scenes/world/props/trees/tree.tscn` |
| `src/App/SceneManager.cs` | `scripts/App/SceneManager.cs` |
| `src/App/ScreenMain.cs` | `scripts/App/ScreenMain.cs` |
| `src/App/SettingsManager.cs` | `scripts/App/SettingsManager.cs` |
| `src/Audio/AudioCueData.cs` | `scripts/Audio/AudioCueData.cs` |
| `src/Audio/AudioManager.cs` | `scripts/Audio/AudioManager.cs` |
| `src/Characters/Companion/NpcCharacter.cs` | `scripts/Characters/Companion/NpcCharacter.cs` |
| `src/Characters/Data/CharacterConfig.cs` | `scripts/Characters/Data/CharacterConfig.cs` |
| `src/Characters/Data/RPGTypes.cs` | `scripts/Characters/Data/RPGTypes.cs` |
| `src/Characters/Data/RaceData.cs` | `scripts/Characters/Data/RaceData.cs` |
| `src/Characters/Enemies/Slime1.cs` | `scripts/Characters/Enemies/Slime1.cs` |
| `src/Characters/Party/PlayerManager.cs` | `scripts/Characters/Party/PlayerManager.cs` |
| `src/Characters/Player/Player.Inventory.cs` | `scripts/Characters/Player/Player.Inventory.cs` |
| `src/Characters/Player/Player.Skills.cs` | `scripts/Characters/Player/Player.Skills.cs` |
| `src/Characters/Player/Player.cs` | `scripts/Characters/Player/Player.cs` |
| `src/Characters/Player/PlayerSkillCollection.cs` | `scripts/Characters/Player/PlayerSkillCollection.cs` |
| `src/Characters/Player/PlayerSkillState.cs` | `scripts/Characters/Player/PlayerSkillState.cs` |
| `src/Characters/Player/SkillCollectionResolver.cs` | `scripts/Characters/Player/SkillCollectionResolver.cs` |
| `src/Characters/Stats/PlayerStats.cs` | `scripts/Characters/Stats/PlayerStats.cs` |
| `src/Combat/AI/CombatSteering.cs` | `scripts/Combat/AI/CombatSteering.cs` |
| `src/Combat/AI/HyouAI.cs` | `scripts/Combat/AI/HyouAI.cs` |
| `src/Combat/AI/SlimeBrain.cs` | `scripts/Combat/AI/SlimeBrain.cs` |
| `src/Combat/Abilities/SkillData.cs` | `scripts/Combat/Abilities/SkillData.cs` |
| `src/Combat/Abilities/SkillTree/CharacterSkillTreeData.cs` | `scripts/Combat/Abilities/SkillTree/CharacterSkillTreeData.cs` |
| `src/Combat/Abilities/SkillTree/SkillTreeBranchData.cs` | `scripts/Combat/Abilities/SkillTree/SkillTreeBranchData.cs` |
| `src/Combat/Abilities/SkillTree/SkillTreeNodeData.cs` | `scripts/Combat/Abilities/SkillTree/SkillTreeNodeData.cs` |
| `src/Combat/Abilities/SkillTree/SkillTreeProgression.cs` | `scripts/Combat/Abilities/SkillTree/SkillTreeProgression.cs` |
| `src/Combat/Actors/CombatCharacter.cs` | `scripts/Combat/Actors/CombatCharacter.cs` |
| `src/Combat/Data/CombatActionData.cs` | `scripts/Combat/Data/CombatActionData.cs` |
| `src/Combat/Data/CombatActionEventData.cs` | `scripts/Combat/Data/CombatActionEventData.cs` |
| `src/Combat/Data/HitProfileData.cs` | `scripts/Combat/Data/HitProfileData.cs` |
| `src/Combat/Data/ProjectileSpecData.cs` | `scripts/Combat/Data/ProjectileSpecData.cs` |
| `src/Combat/Data/ProjectileVisualProfileData.cs` | `scripts/Combat/Data/ProjectileVisualProfileData.cs` |
| `src/Combat/Data/WeaponMovesetData.cs` | `scripts/Combat/Data/WeaponMovesetData.cs` |
| `src/Combat/Decision/Debug/CombatDecisionDebugOverlay.cs` | `scripts/Combat/Decision/Debug/CombatDecisionDebugOverlay.cs` |
| `src/Combat/Decision/Debug/CombatDecisionWorldDebugDraw.cs` | `scripts/Combat/Decision/Debug/CombatDecisionWorldDebugDraw.cs` |
| `src/Combat/Decision/Debug/DecisionTraceExporter.cs` | `scripts/Combat/Decision/Debug/DecisionTraceExporter.cs` |
| `src/Combat/Decision/Execution/CombatIntentExecutor.cs` | `scripts/Combat/Decision/Execution/CombatIntentExecutor.cs` |
| `src/Combat/Decision/Model/CombatDecisionEnums.cs` | `scripts/Combat/Decision/Model/CombatDecisionEnums.cs` |
| `src/Combat/Decision/Model/CombatIntent.cs` | `scripts/Combat/Decision/Model/CombatIntent.cs` |
| `src/Combat/Decision/Model/CombatSnapshot.cs` | `scripts/Combat/Decision/Model/CombatSnapshot.cs` |
| `src/Combat/Decision/Model/DecisionModels.cs` | `scripts/Combat/Decision/Model/DecisionModels.cs` |
| `src/Combat/Decision/Movement/CombatMovementModels.cs` | `scripts/Combat/Decision/Movement/CombatMovementModels.cs` |
| `src/Combat/Decision/Movement/CombatMovementSolver.cs` | `scripts/Combat/Decision/Movement/CombatMovementSolver.cs` |
| `src/Combat/Decision/Movement/CombatSpacingController.cs` | `scripts/Combat/Decision/Movement/CombatSpacingController.cs` |
| `src/Combat/Decision/Party/PartyTacticalDirector.cs` | `scripts/Combat/Decision/Party/PartyTacticalDirector.cs` |
| `src/Combat/Decision/Profiles/CombatClassProfile.cs` | `scripts/Combat/Decision/Profiles/CombatClassProfile.cs` |
| `src/Combat/Decision/Profiles/CombatDoctrineProfile.cs` | `scripts/Combat/Decision/Profiles/CombatDoctrineProfile.cs` |
| `src/Combat/Decision/Profiles/CombatPersonalityProfile.cs` | `scripts/Combat/Decision/Profiles/CombatPersonalityProfile.cs` |
| `src/Combat/Decision/Runtime/CombatBlackboard.cs` | `scripts/Combat/Decision/Runtime/CombatBlackboard.cs` |
| `src/Combat/Decision/Runtime/CombatDecisionAgent.cs` | `scripts/Combat/Decision/Runtime/CombatDecisionAgent.cs` |
| `src/Combat/Decision/Runtime/CombatPerception.cs` | `scripts/Combat/Decision/Runtime/CombatPerception.cs` |
| `src/Combat/Decision/Runtime/DecisionContracts.cs` | `scripts/Combat/Decision/Runtime/DecisionContracts.cs` |
| `src/Combat/Decision/Runtime/ResponseCurve.cs` | `scripts/Combat/Decision/Runtime/ResponseCurve.cs` |
| `src/Combat/Decision/Runtime/TacticalEvaluator.cs` | `scripts/Combat/Decision/Runtime/TacticalEvaluator.cs` |
| `src/Combat/Decision/Runtime/ThreatPredictor.cs` | `scripts/Combat/Decision/Runtime/ThreatPredictor.cs` |
| `src/Combat/Decision/Scheduling/CombatActionScheduler.cs` | `scripts/Combat/Decision/Scheduling/CombatActionScheduler.cs` |
| `src/Combat/Model/CombatEnums.cs` | `scripts/Combat/Model/CombatEnums.cs` |
| `src/Combat/Model/HitRequest.cs` | `scripts/Combat/Model/HitRequest.cs` |
| `src/Combat/Model/HitResult.cs` | `scripts/Combat/Model/HitResult.cs` |
| `src/Combat/Projectiles/CombatProjectile2D.cs` | `scripts/Combat/Projectiles/CombatProjectile2D.cs` |
| `src/Combat/Projectiles/CombatProjectileSpawner.cs` | `scripts/Combat/Projectiles/CombatProjectileSpawner.cs` |
| `src/Combat/Runtime/CombatAbilityRunner.cs` | `scripts/Combat/Runtime/CombatAbilityRunner.cs` |
| `src/Combat/Runtime/CombatActionEventDispatcher.cs` | `scripts/Combat/Runtime/CombatActionEventDispatcher.cs` |
| `src/Combat/Runtime/CombatActionRunner.cs` | `scripts/Combat/Runtime/CombatActionRunner.cs` |
| `src/Combat/Runtime/CombatHitbox.cs` | `scripts/Combat/Runtime/CombatHitbox.cs` |
| `src/Combat/Runtime/CombatResolver.cs` | `scripts/Combat/Runtime/CombatResolver.cs` |
| `src/Combat/Runtime/CombatStateMachine.cs` | `scripts/Combat/Runtime/CombatStateMachine.cs` |
| `src/Combat/Runtime/FactionRules.cs` | `scripts/Combat/Runtime/FactionRules.cs` |
| `src/Combat/Visuals/HyouCastVisual.cs` | `scripts/Combat/Visuals/HyouCastVisual.cs` |
| `src/Inventory/Data/EquipmentItemData.cs` | `scripts/Inventory/Data/EquipmentItemData.cs` |
| `src/Inventory/Runtime/EquipmentManager.cs` | `scripts/Inventory/Runtime/EquipmentManager.cs` |
| `src/Inventory/Runtime/InventoryManager.cs` | `scripts/Inventory/Runtime/InventoryManager.cs` |
| `src/Quests/Data/QuestData.cs` | `scripts/Quests/Data/QuestData.cs` |
| `src/Quests/Data/QuestObjectiveData.cs` | `scripts/Quests/Data/QuestObjectiveData.cs` |
| `src/Quests/Data/QuestRewardData.cs` | `scripts/Quests/Data/QuestRewardData.cs` |
| `src/Quests/Runtime/QuestManager.cs` | `scripts/Quests/Runtime/QuestManager.cs` |
| `src/Quests/Runtime/QuestRuntimeState.cs` | `scripts/Quests/Runtime/QuestRuntimeState.cs` |
| `src/Quests/Runtime/QuestService.cs` | `scripts/Quests/Runtime/QuestService.cs` |
| `src/Save/SaveGameData.cs` | `scripts/Save/SaveGameData.cs` |
| `src/Save/SaveManager.cs` | `scripts/Save/SaveManager.cs` |
| `src/Save/UserSettingsData.cs` | `scripts/Save/UserSettingsData.cs` |
| `src/UI/HUD/CharacterDetailUI.cs` | `scripts/UI/HUD/CharacterDetailUI.cs` |
| `src/UI/HUD/CharacterUnitHUD.cs` | `scripts/UI/HUD/CharacterUnitHUD.cs` |
| `src/UI/HUD/DamageNumberService.cs` | `scripts/UI/HUD/DamageNumberService.cs` |
| `src/UI/HUD/EnemyHealthBarService.cs` | `scripts/UI/HUD/EnemyHealthBarService.cs` |
| `src/UI/HUD/GameMenuButton.cs` | `scripts/UI/HUD/GameMenuButton.cs` |
| `src/UI/HUD/InventoryPanel.cs` | `scripts/UI/HUD/InventoryPanel.cs` |
| `src/UI/HUD/InventoryPanelChrome.cs` | `scripts/UI/HUD/InventoryPanelChrome.cs` |
| `src/UI/HUD/PartyHUDManager.cs` | `scripts/UI/HUD/PartyHUDManager.cs` |
| `src/UI/HUD/SettingsPanel.cs` | `scripts/UI/HUD/SettingsPanel.cs` |
| `src/UI/HUD/Skills/SkillIconResolver.cs` | `scripts/UI/HUD/Skills/SkillIconResolver.cs` |
| `src/UI/HUD/Skills/SkillViewModel.cs` | `scripts/UI/HUD/Skills/SkillViewModel.cs` |
| `src/UI/HUD/StatHexagonChart.cs` | `scripts/UI/HUD/StatHexagonChart.cs` |
| `src/UI/Menus/End.cs` | `scripts/UI/Menus/End.cs` |
| `src/UI/Menus/Start.cs` | `scripts/UI/Menus/Start.cs` |
| `src/UI/Party/PartyPanel.cs` | `scripts/UI/Party/PartyPanel.cs` |
| `src/UI/Quests/QuestJournalPanel.cs` | `scripts/UI/Quests/QuestJournalPanel.cs` |
| `src/UI/Quests/QuestTrackerHud.cs` | `scripts/UI/Quests/QuestTrackerHud.cs` |
| `src/UI/Skills/SkillTreeGraphView.cs` | `scripts/UI/Skills/SkillTreeGraphView.cs` |
| `src/UI/Skills/SkillTreeNodeView.cs` | `scripts/UI/Skills/SkillTreeNodeView.cs` |
| `src/UI/Skills/SkillTreePanel.cs` | `scripts/UI/Skills/SkillTreePanel.cs` |
| `src/World/Objects/GameLevel.cs` | `scripts/World/Objects/GameLevel.cs` |
| `src/World/Objects/SpawnPoint.cs` | `scripts/World/Objects/SpawnPoint.cs` |
| `src/World/Transitions/SceneTrigger.cs` | `scripts/World/Transitions/SceneTrigger.cs` |
| `tools/validate_combat_backbone_v8.py` | `tools/validation/legacy/validate_combat_backbone_v8.py` |
| `tools/validate_combat_decision_foundation.py` | `tools/validation/legacy/validate_combat_decision_foundation.py` |
| `tools/validate_combat_decision_phase2.py` | `tools/validation/legacy/validate_combat_decision_phase2.py` |
| `tools/validate_combat_decision_phase3.py` | `tools/validation/legacy/validate_combat_decision_phase3.py` |
| `tools/validate_combat_decision_phase4.py` | `tools/validation/legacy/validate_combat_decision_phase4.py` |
| `tools/validate_combat_refactor.py` | `tools/validation/legacy/validate_combat_refactor.py` |
| `tools/validate_hyou_cast_runtime_v3.py` | `tools/validation/legacy/validate_hyou_cast_runtime_v3.py` |
| `tools/validate_hyou_existing_kit_rhythm_v7.py` | `tools/validation/legacy/validate_hyou_existing_kit_rhythm_v7.py` |
| `tools/validate_hyou_ice_bolt_cast.py` | `tools/validation/legacy/validate_hyou_ice_bolt_cast.py` |
| `tools/validate_hyou_ice_bolt_vfx.py` | `tools/validation/legacy/validate_hyou_ice_bolt_vfx.py` |
| `tools/validate_hyou_projectile_and_slime_pursuit_v6.py` | `tools/validation/legacy/validate_hyou_projectile_and_slime_pursuit_v6.py` |
| `tools/validate_hyou_projectile_asset_v4.py` | `tools/validation/legacy/validate_hyou_projectile_asset_v4.py` |
| `tools/validate_party_panel.py` | `tools/validation/legacy/validate_party_panel.py` |
| `tools/validate_quest_journal.py` | `tools/validation/legacy/validate_quest_journal.py` |
| `tools/validate_skill_panel_redesign.py` | `tools/validation/legacy/validate_skill_panel_redesign.py` |
| `tools/validate_skill_tree_panel.py` | `tools/validation/legacy/validate_skill_tree_panel.py` |
| `tools/validate_slime_retaliation_targeting_v5.py` | `tools/validation/legacy/validate_slime_retaliation_targeting_v5.py` |
