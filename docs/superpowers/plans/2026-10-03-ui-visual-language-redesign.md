# Tàn Sinh UI Visual Language Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thay lớp visual UI/HUD mang cảm giác AI/generic bằng một hệ code-native thống nhất, hỗ trợ tiếng Việt, dễ đọc trong combat và phản ánh đối lập Sinh/Tàn mà không chỉnh artwork nhân vật.

**Architecture:** Dùng `UiTokens`, `UiThemeFactory` và một glyph atlas native làm giao diện chung; giữ API/layout hữu ích của `InventoryPanelChrome`, `PixelButtonSkin`, các panel và gameplay service hiện có. Chuyển từng subsystem theo thứ tự foundation -> HUD -> menu/panel -> dialog/docs, chỉ xóa asset cũ sau khi reference scan và runtime validation đều sạch.

**Tech Stack:** Godot 4.7.2 Mono, C#/.NET 8, GDScript cho Dialogic layout, Godot scene/resource files, pytest contract tests.

**Spec:** `docs/superpowers/specs/2026-10-03-ui-visual-language-redesign-design.md`

## Global Constraints

- Không chỉnh sửa portrait hoặc tranh minh họa Hikaru/Hyou.
- Không thay gameplay, combat balance, quest progression hoặc cấu trúc save data.
- World art vẫn được phép sáng và sống động; UI không áp tông hậu tận thế lên map.
- Palette nền dùng các token chính xác trong spec: `#120F0C`, `#1D1813`, `#29221B`, `#352C22`, `#5C4B38`, `#8A6B42`, `#C39A57`, `#F0E5D2`, `#BFAF98`.
- Grid cơ sở `4 px`; border `1 px`, selection tối đa `2 px`; radius thông thường `0-3 px`.
- Icon navigation/stat dùng cell native `24x24`; button cao `32/40/48 px`; status icon `24 px` trong frame `28 px`.
- Text UI runtime dùng Be Vietnam Pro Regular/Medium/SemiBold/Italic/SemiBoldItalic; không dùng Cinzel hoặc IM Fell khi chưa bundle và kiểm thử.
- Không bake chữ, số, portrait hoặc resource label vào texture.
- Giữ và tích hợp các thay đổi dialog đang có; không reset hoặc ghi đè worktree dirty.
- Mỗi task phải giữ `dotnet build` và các contract test liên quan chạy được trước khi commit.

## Review Focus

- Tên và mô tả tiếng Việt dài ở viewport `1280x720` phải wrap/ellipsis có tooltip, không clip im lặng; Task 6 và Task 7 có contract + visual test.
- Keyboard/gamepad focus phải khác hover trên button, tab và dialog choice; Task 1, Task 5 và Task 8 pin hành vi này.
- Ba party member cùng có status không được overlap; Task 3 có scene contract và screenshot case.
- Target marker + enemy HP/status + damage + XP cùng xuất hiện trên một actor không được đè nhau; Task 4 có lane-layout tests.
- Thiếu font, save snapshot hoặc optional icon không được crash/blank UI; Task 1, Task 2 và Task 5 có fallback tests.

---

### Task 1: Shared Tokens, Fonts, And Theme

**Files:**
- Create: `scripts/UI/Theme/UiTokens.cs`
- Create: `scripts/UI/Theme/UiThemeFactory.cs`
- Create: `assets/fonts/OFL.txt`
- Create: `assets/fonts/BeVietnamPro-Medium.ttf`
- Create: `assets/fonts/BeVietnamPro-SemiBold.ttf`
- Create: `assets/fonts/BeVietnamPro-Italic.ttf`
- Create: `assets/fonts/BeVietnamPro-SemiBoldItalic.ttf`
- Modify: `scripts/UI/HUD/InventoryPanelChrome.cs`
- Modify: `scripts/UI/HUD/PixelButtonSkin.cs`
- Create: `tools/tests/test_ui_visual_contract.py`

**Interfaces:**
- Produces: `UiTokens` constants for palette, spacing, radii, icon and control sizes.
- Produces: `UiTextRole { ScreenTitle, SectionTitle, Body, Label, Micro, CombatNumber }`.
- Produces in namespace `AshesofaDyingWorld.UI.Theme`: `UiThemeFactory.GetSharedTheme() -> Theme`, `UiThemeFactory.Apply(Control root)`, and `UiThemeFactory.ApplyText(Control control, UiTextRole role)`.
- Produces: existing public methods on `InventoryPanelChrome` and `PixelButtonSkin` remain source-compatible.

- [ ] **Step 1: Write failing token/font contract tests**

Add tests named `test_ui_tokens_match_approved_palette`, `test_active_text_tokens_meet_contrast_ratio`, `test_vietnamese_font_faces_are_bundled`, `test_shared_chrome_uses_theme_tokens`, and `test_pixel_button_skin_does_not_resize_ai_exports`. Assert the exact token hex values, WCAG ratio `>= 4.5` for active small text, five font files, use of `UiTokens`/`UiThemeFactory`, and absence of `MaximumSourceHeight`, `NormalizeSourceTexture`, and `Asset AI/export`.

- [ ] **Step 2: Run the focused tests and confirm failure**

Run: `python -m pytest tools/tests/test_ui_visual_contract.py -q`

Expected: FAIL because theme classes and font faces do not exist.

- [ ] **Step 3: Add official Be Vietnam Pro faces and license**

Fetch the static Medium, SemiBold, Italic and SemiBoldItalic TTF files plus `OFL.txt` from the official Be Vietnam Pro/Google Fonts source. Preserve `BeVietnamPro-Regular.ttf`; do not rename or subset the fonts.

- [ ] **Step 4: Implement `UiTokens` and `UiThemeFactory`**

Define the exact colors/dimensions from Global Constraints. `GetSharedTheme()` caches one `Theme`, assigns the five font faces to normal/medium/bold/italic/bold-italic roles and exposes the scale in `UiTextRole`; `Apply` assigns the shared theme without overwriting semantic component overrides.

- [ ] **Step 5: Move shared chrome and buttons onto the theme**

Update `InventoryPanelChrome` to consume `UiTokens`. Keep its public factory signatures. Change `PixelButtonSkin` to return `StyleBoxFlat` states built from tokens, with identical geometry for normal/hover/focus/pressed/disabled and no texture loading/downscaling.

- [ ] **Step 6: Run foundation contracts**

Run: `python -m pytest tools/tests/test_ui_visual_contract.py -q`

Expected: all focused tests PASS.

- [ ] **Step 7: Build the project**

Run: `dotnet build "Ashes of a Dying World.csproj"`

Expected: build succeeds with zero errors.

- [ ] **Step 8: Commit foundation**

```bash
git add assets/fonts scripts/UI/Theme scripts/UI/HUD/InventoryPanelChrome.cs scripts/UI/HUD/PixelButtonSkin.cs tools/tests/test_ui_visual_contract.py
git commit -m "feat: add shared Tàn Sinh UI theme"
```

### Task 2: Deterministic UI Glyph System

**Files:**
- Create: `assets/graphics/ui/icons/ui_glyph_atlas.svg`
- Create: `scripts/UI/Theme/UiGlyphResolver.cs`
- Modify: `data/icons/str.tres`
- Modify: `data/icons/dex.tres`
- Modify: `data/icons/int.tres`
- Modify: `data/icons/vit.tres`
- Modify: `data/icons/spi.tres`
- Modify: `data/icons/def.tres`
- Modify: `data/icons/exit.tres`
- Modify: `tools/tests/test_ui_visual_contract.py`

**Interfaces:**
- Consumes: `UiTokens.IconSize = 24` from Task 1.
- Produces: `UiGlyph` enum covering `Menu`, `Character`, `Inventory`, `Skills`, `Quests`, `Party`, `Settings`, category glyphs, target marker, six stats and exit.
- Produces in namespace `AshesofaDyingWorld.UI.Theme`: `UiGlyphResolver.Resolve(UiGlyph glyph) -> AtlasTexture`; callers tint through Control/Button theme colors rather than generating recolored textures.

- [ ] **Step 1: Add failing glyph atlas tests**

Add `test_ui_glyph_atlas_uses_integer_24px_cells`, `test_all_runtime_navigation_glyphs_resolve`, and `test_data_stat_icons_use_clean_atlas_regions`. Assert one atlas path, integer regions aligned to `24`, non-empty DEX mapping and no references to `menu_action_icons_sheet.png` or `stat_icons_sheet.png`.

- [ ] **Step 2: Run tests and confirm the old sheets fail the contract**

Run: `python -m pytest tools/tests/test_ui_visual_contract.py -q -k "glyph or stat_icons"`

Expected: FAIL on missing atlas/resolver and old sheet paths.

- [ ] **Step 3: Create the atlas and resolver**

Draw familiar single-color glyphs on a strict `24x24` cell grid, `1-2 px` visual stroke and a common top-left highlight rule. `UiGlyphResolver` owns the enum-to-region map and returns a fallback question-mark glyph when a key is missing.

- [ ] **Step 4: Remap stat resources**

Point the seven `data/icons/*.tres` resources at integer atlas regions. Preserve their resource paths so skill/quest consumers do not change.

- [ ] **Step 5: Verify glyph contracts and fallback**

Run: `python -m pytest tools/tests/test_ui_visual_contract.py tools/tests/test_skill_icon_contract.py -q`

- [ ] **Step 6: Build the glyph integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

Expected: PASS and zero build errors.

- [ ] **Step 7: Commit glyph system**

```bash
git add assets/graphics/ui/icons/ui_glyph_atlas.svg scripts/UI/Theme/UiGlyphResolver.cs data/icons tools/tests
git commit -m "feat: add deterministic UI glyph atlas"
```

### Task 3: Party HUD And Companion Command Menu

**Files:**
- Modify: `scenes/ui/hud/character_unit_hud.tscn`
- Modify: `scenes/ui/hud/party_hud.tscn`
- Modify: `scripts/UI/HUD/CharacterUnitHUD.cs`
- Modify: `scripts/UI/HUD/PartyHUDManager.cs`
- Modify: `tools/tests/test_party_hud_context_menu_contract.py`
- Create: `tools/tests/test_party_hud_visual_contract.py`
- Create: `tools/validation/ui_party_hud_showcase.tscn`
- Create: `tools/validation/ui_party_hud_showcase.gd`

**Interfaces:**
- Consumes: `UiThemeFactory`, `UiTokens`, `PixelButtonSkin`, and `UiGlyphResolver`.
- Preserves: exported `HealthBar`, `ManaBar`, `StaminaBar`, `NameLabel`, and `Portrait` NodePaths used by `CharacterUnitHUD`.
- Produces: a named `StatusStrip` inside each unit's `300x97` layout and code-native context menu styles.

- [ ] **Step 1: Write failing party HUD contracts**

Assert that the scene no longer references `unit_hud_frame.png`, `unit_hud_portrait_frame.png` or glossy fill PNGs; it contains `StatusStrip`; status holders use tooltip-capable mouse filtering; selected outline is at most `2`; and `PartyHUDManager` no longer loads old companion panel/button textures.

- [ ] **Step 2: Run tests and confirm failure**

Run: `python -m pytest tools/tests/test_party_hud_visual_contract.py tools/tests/test_party_hud_context_menu_contract.py -q`

Expected: FAIL on bitmap paths, missing internal status strip and legacy command assets.

- [ ] **Step 3: Rebuild `character_unit_hud.tscn` with native controls**

Use a themed `PanelContainer`, independent portrait frame, dynamic labels, three code-styled progress bars and an internal status row. Preserve the `300x97` minimum; allocate status space instead of positioning badges below the unit.

- [ ] **Step 4: Adapt `CharacterUnitHUD`**

Resolve named nodes once in `_Ready`, allow long names to ellipsize with tooltip, keep portrait data untouched, use semantic low-resource states and set badge holders to receive tooltip input while their icon/frame children ignore it.

- [ ] **Step 5: Reskin companion commands**

Remove `CommandMenuPanelPath` and old button texture handling. Build context menu/button states from shared theme while retaining `Theo sau`, `Đứng yên`, `Bảo vệ`, and `Đi dạo` behavior.

- [ ] **Step 6: Add and render the three-member showcase**

The validation scene displays three members, long Vietnamese names, active skills and all status combinations. Run it at `1600x900` and `1280x720`; verify no status overlap and no layout shift between states.

- [ ] **Step 7: Run party HUD contracts**

Run: `python -m pytest tools/tests/test_party_hud_visual_contract.py tools/tests/test_party_hud_context_menu_contract.py -q`

- [ ] **Step 8: Build the party HUD integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

Expected: PASS and zero build errors.

- [ ] **Step 9: Commit party HUD**

```bash
git add scenes/ui/hud scripts/UI/HUD/CharacterUnitHUD.cs scripts/UI/HUD/PartyHUDManager.cs tools/tests tools/validation
git commit -m "feat: rebuild party HUD with shared visual language"
```

### Task 4: World HUD Lanes And Enemy Health

**Files:**
- Create: `scripts/UI/HUD/WorldHudLayout.cs`
- Modify: `scripts/UI/HUD/EnemyHealthBarService.cs`
- Modify: `scripts/UI/HUD/CompanionTargetIndicatorService.cs`
- Modify: `scripts/UI/HUD/DamageNumberService.cs`
- Modify: `scripts/UI/HUD/FloatingProgressionHudService.cs`
- Modify: `scripts/App/ScreenMain.cs`
- Create: `tools/tests/test_world_hud_visual_contract.py`
- Create: `tools/validation/ui_world_hud_showcase.tscn`
- Create: `tools/validation/ui_world_hud_showcase.gd`

**Interfaces:**
- Consumes: shared theme and `UiGlyph.Target`.
- Produces: `WorldHudLane { Target, Health, Feedback, Progression }`.
- Produces: `WorldHudLayout.Resolve(Node2D actor, WorldHudLane lane, Vector2 widgetSize, Vector2 extraOffset = default) -> Vector2`.

- [ ] **Step 1: Write failing lane and enemy-bar tests**

Assert exact lane ordering, all four services calling `WorldHudLayout.Resolve`, no font triangle `▼`, no `enemy_hp_bar.png` dependency, a non-empty trough/frame style and Vietnamese `VỠ BĂNG` feedback replacing `SHATTER`.

- [ ] **Step 2: Run tests and confirm independent offsets fail**

Run: `python -m pytest tools/tests/test_world_hud_visual_contract.py -q`

Expected: FAIL because each service currently owns unrelated negative offsets.

- [ ] **Step 3: Implement lane resolution**

Centralize top-center actor anchoring and fixed vertical separation in `WorldHudLayout`; widget dimensions participate in placement so a larger HP/status row cannot collide with the next lane.

- [ ] **Step 4: Rebuild enemy health and target marker**

Replace the `51x3` bitmap with styled trough/fill/frame controls, keep status logic, use `UiGlyph.Target` for target feedback and preserve visibility timers/AI target semantics.

- [ ] **Step 5: Migrate damage and progression feedback**

Use Feedback and Progression lanes while preserving existing animation/event behavior. Translate visible English combat copy and leave debug-only strings out of the player layer.

- [ ] **Step 6: Render collision scenario**

The showcase must display one actor with target, HP, three statuses, damage and progression simultaneously at both target viewports; canvas-pixel inspection must show distinct occupied bands.

- [ ] **Step 7: Run world HUD contracts**

Run: `python -m pytest tools/tests/test_world_hud_visual_contract.py tools/tests/test_combat_stability_contract.py -q`

- [ ] **Step 8: Build the world HUD integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

Expected: PASS and zero build errors.

- [ ] **Step 9: Commit world HUD**

```bash
git add scripts/UI/HUD scripts/App/ScreenMain.cs tools/tests/test_world_hud_visual_contract.py tools/validation/ui_world_hud_showcase.*
git commit -m "feat: coordinate world HUD feedback lanes"
```

### Task 5: Main Screen And In-Game Menu

**Files:**
- Modify: `scenes/app/screen_main.tscn`
- Modify: `scripts/App/ScreenMain.cs`
- Modify: `scenes/ui/menus/game_menu_button.tscn`
- Modify: `scripts/UI/HUD/GameMenuButton.cs`
- Create: `tools/tests/test_main_menu_visual_contract.py`

**Interfaces:**
- Consumes: shared theme, button skin and navigation glyphs.
- Preserves: `StartGameFromSnapshotAsync(SaveGameData saveSnapshot) -> Task<Error>`.
- Produces: `RefreshMainMenuState(SaveGameData snapshot)` and `StartNewGameAsync()`; neither deletes save data.

- [ ] **Step 1: Write failing main/menu tests**

Assert no references to `ui/menus/login/*.png`, `main_hud.png`, `buttons/character.png` or `buttons/inventory.png`; primary label is `Tiếp tục` with save and `Bắt đầu` without save; New Game is conditional; Map/Achievements are hidden or disabled; all visible feature buttons have distinct glyph mappings.

- [ ] **Step 2: Run tests and confirm placeholder UI fails**

Run: `python -m pytest tools/tests/test_main_menu_visual_contract.py -q`

Expected: FAIL on baked English images, avatar menu button and dead-end panels.

- [ ] **Step 3: Rebuild `screen_main.tscn`**

Use responsive `Control` containers, the existing Whispering Fields art as a living-world background, a readable scrim, title text and themed Vietnamese buttons. Add visible keyboard focus and a settings panel using the existing Settings UI.

- [ ] **Step 4: Wire dynamic start actions**

`RefreshMainMenuState` maps the snapshot to labels/visibility. Continue passes the snapshot; new game passes `null`; exit keeps current behavior. Add a confirmation before starting fresh when a save exists, without deleting that save.

- [ ] **Step 5: Rebuild the in-game launcher/grid**

Use a `32-48 px` Menu glyph, shared scrim/panel, six live feature buttons and no empty Map/Achievements routes. Preserve panel refresh, quest capture/restore and Escape behavior.

- [ ] **Step 6: Run main-menu contracts**

Run: `python -m pytest tools/tests/test_main_menu_visual_contract.py -q`

Expected: all menu contracts PASS.

- [ ] **Step 7: Capture main-menu visual states**

Run both scenes with Godot movie mode at `1600x900` and `1280x720`; capture no-save, existing-save and keyboard-focus frames.

- [ ] **Step 8: Build the menu integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

Expected: build succeeds with zero errors.

- [ ] **Step 9: Commit menu rebuild**

```bash
git add scenes/app/screen_main.tscn scripts/App/ScreenMain.cs scenes/ui/menus/game_menu_button.tscn scripts/UI/HUD/GameMenuButton.cs tools/tests/test_main_menu_visual_contract.py
git commit -m "feat: replace placeholder menus with Tàn Sinh UI"
```

### Task 6: Inventory And Character Panels

**Files:**
- Modify: `scripts/UI/HUD/InventoryPanel.cs`
- Modify: `scripts/UI/HUD/CharacterDetailUI.cs`
- Modify: `scripts/UI/HUD/InventoryPanelChrome.cs`
- Modify: `tools/tests/test_ui_visual_contract.py`
- Create: `tools/tests/test_inventory_character_ui_contract.py`
- Create: `tools/validation/ui_screen_showcase.tscn`
- Create: `tools/validation/ui_screen_showcase.gd`

**Interfaces:**
- Consumes: shared theme, glyph resolver and existing panel chrome API.
- Preserves: inventory manager/equipment manager calls and current public refresh/open methods.
- Produces: real category `OptionButton`/filter behavior and intentional empty states.

- [ ] **Step 1: Add failing language/layout tests**

Assert visible inventory strings are Vietnamese, fixed mock money `12,345` is absent, category PNG paths are absent, category labels can exceed the old English widths, `[ ẢNH CHÂN DUNG ]`/`[ Nhân vật ]` placeholders are replaced by intentional empty states and the fake filter arrow label is absent.

- [ ] **Step 2: Run tests and confirm current English/placeholder failure**

Run: `python -m pytest tools/tests/test_inventory_character_ui_contract.py -q`

- [ ] **Step 3: Migrate inventory navigation and copy**

Use category glyphs from Task 2 and content-driven tab widths or horizontal scrolling. Remove the fixed `12,345`; hide the currency display until a real currency model is available. Translate visible labels without altering item IDs or data keys.

- [ ] **Step 4: Clean character empty states and filters**

Keep portrait artwork paths untouched. Replace fake controls with `OptionButton` or non-interactive text, and use a themed empty state when no portrait/body/member data exists.

- [ ] **Step 5: Validate long Vietnamese strings**

Add a showcase case containing `Vật phẩm tiêu hao`, a long item name and a long character name at `1280x720`; verify wrap/tooltip and no horizontal clipping.

- [ ] **Step 6: Run inventory/character contracts**

Run: `python -m pytest tools/tests/test_inventory_character_ui_contract.py tools/tests/test_ui_visual_contract.py -q`

- [ ] **Step 7: Build the inventory/character integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

- [ ] **Step 8: Commit inventory and character UI**

```bash
git add scripts/UI/HUD/InventoryPanel.cs scripts/UI/HUD/CharacterDetailUI.cs scripts/UI/HUD/InventoryPanelChrome.cs tools/validation/ui_screen_showcase.* tools/tests
git commit -m "feat: unify inventory and character UI"
```

### Task 7: Party, Quest, Skill, And Settings Panels

**Files:**
- Modify: `scripts/UI/Party/PartyPanel.cs`
- Modify: `scripts/UI/Quests/QuestJournalPanel.cs`
- Modify: `scripts/UI/Quests/QuestTrackerHud.cs`
- Modify: `scripts/UI/Skills/SkillTreeNodeView.cs`
- Modify: `scripts/UI/Skills/SkillTreeGraphView.cs`
- Modify: `scripts/UI/Skills/SkillTreePanel.cs`
- Modify: `scripts/UI/HUD/SettingsPanel.cs`
- Modify: `scripts/App/SettingsManager.cs`
- Modify: `scripts/Save/UserSettingsData.cs`
- Create: `scripts/UI/Theme/UiMotion.cs`
- Modify: `tools/validation/ui_screen_showcase.tscn`
- Modify: `tools/validation/ui_screen_showcase.gd`
- Create: `tools/tests/test_secondary_panels_ui_contract.py`

**Interfaces:**
- Consumes: shared theme/button/glyph APIs.
- Preserves: party commands, quest tracking/capture/restore, skill unlock logic and settings manager bindings.
- Produces: all player-facing copy in Vietnamese, themed native settings controls, persisted `UserSettingsData.ReducedMotion` and `UiMotion.ResolveDuration(float normalSeconds) -> float`.

- [ ] **Step 1: Write failing secondary-panel tests**

Assert Party builds the existing companion command section or removes misleading instruction copy; quest buttons use current glyphs; skill nodes use radius at most `3`, support two-line names and retain tooltip; Settings contains no player-facing `AUDIO`, `DISPLAY`, `CONTROLS`, `COMFORT`, `Apply`, renderer/GPU prose or asset filename copy; reduced motion persists and resolves UI transition durations to `0` when enabled.

- [ ] **Step 2: Run tests and confirm failure**

Run: `python -m pytest tools/tests/test_secondary_panels_ui_contract.py -q`

- [ ] **Step 3: Finish Party and Quest presentation**

Expose the already implemented companion command section or remove the dead helper; do not infer new gameplay roles. Keep quest state behavior, replace category art with glyphs and make map action disabled with a clear unavailable state until a map exists.

- [ ] **Step 4: Restyle skill nodes without changing progression**

Use shared border/radius/shadow values, two-line wrapping with full tooltip and character accent only for semantic state. Preserve graph coordinates, connections and unlock logic.

- [ ] **Step 5: Skin native settings controls and rewrite copy**

Apply shared styles to slider, checkbox, option controls and tabs. Replace developer-facing copy with short Vietnamese player copy; keep existing SettingsManager properties and persistence calls, add a `Giảm chuyển động` toggle and route new UI transition durations through `UiMotion.ResolveDuration`.

- [ ] **Step 6: Update and capture the secondary-panel showcase**

Render Party, Quest, Skill and Settings states in `ui_screen_showcase` at both target viewports; verify long names, focus, reduced-motion state and native controls stay inside their bounds.

- [ ] **Step 7: Run secondary-panel contracts**

Run: `python -m pytest tools/tests/test_secondary_panels_ui_contract.py -q`

- [ ] **Step 8: Build the secondary-panel integration**

Run: `dotnet build "Ashes of a Dying World.csproj"`

- [ ] **Step 9: Commit secondary-panel UI**

```bash
git add scripts/UI/Party scripts/UI/Quests scripts/UI/Skills scripts/UI/HUD/SettingsPanel.cs scripts/UI/Theme/UiMotion.cs scripts/App/SettingsManager.cs scripts/Save/UserSettingsData.cs tools/validation/ui_screen_showcase.* tools/tests/test_secondary_panels_ui_contract.py
git commit -m "feat: align secondary panels with shared UI theme"
```

### Task 8: Dialog Choices And Vietnamese Font Variants

**Files:**
- Modify carefully: `scenes/ui/dialog/jrpg_textbox_layer.gd`
- Modify carefully: `scenes/ui/dialog/jrpg_choice_layer.gd`
- Modify carefully: `scenes/ui/dialog/jrpg_choice_layer.tscn`
- Modify carefully: `scenes/ui/dialog/jrpg_choice_normal.tres`
- Modify carefully: `scenes/ui/dialog/jrpg_choice_hover.tres`
- Modify carefully: `scenes/ui/dialog/jrpg_choice_focus.tres`
- Modify: `tools/tests/test_dialogic_resource_contract.py`
- Modify: `tools/validation/_dialogic_layout_smoke_test.gd`

**Interfaces:**
- Consumes: bundled Be Vietnam Pro faces from Task 1.
- Preserves: current portrait animation, responsive anchoring and all user worktree edits.
- Produces: wrap-capable choices, distinct hover/focus states and real font variants.

- [ ] **Step 1: Record and review the existing dialog diff**

Run: `git diff -- scenes/ui/dialog` and save no generated patch into the repo. Identify the exact current user changes before editing; do not reset any file.

- [ ] **Step 2: Add failing dialog contracts**

Assert normal uses `BeVietnamPro-Regular.ttf`, bold uses `BeVietnamPro-SemiBold.ttf`, italic uses `BeVietnamPro-Italic.ttf`, and bold-italic uses `BeVietnamPro-SemiBoldItalic.ttf`; also assert choice autowrap is enabled, responsive width exceeds the old `240-280 px` cap when viewport permits, overflow uses scroll and focus style differs from hover.

- [ ] **Step 3: Run the current smoke/contract tests**

Run: `python -m pytest tools/tests/test_dialogic_resource_contract.py -q`

Run: Godot headless scene `res://tools/validation/_dialogic_layout_smoke_test.tscn`.

Expected: new assertions FAIL while the pre-existing smoke behavior remains intact.

- [ ] **Step 4: Implement responsive choice and font variants**

Apply changes on top of the current files. Width derives from viewport with a bounded maximum; buttons wrap text and container scrolls when choice count exceeds available height. Use focus styling that remains visible without color alone.

- [ ] **Step 5: Capture dialog at two viewports**

Use long Vietnamese choices with diacritics, keyboard focus and at least six options at `1600x900` and `1280x720`.

- [ ] **Step 6: Run dialog contracts and smoke test**

Run: `python -m pytest tools/tests/test_dialogic_resource_contract.py -q`

Run: Godot smoke scene; expected exit code `0` and `Dialogic layout smoke test passed`.

- [ ] **Step 7: Commit dialog changes without unrelated files**

```bash
git add scenes/ui/dialog tools/tests/test_dialogic_resource_contract.py tools/validation/_dialogic_layout_smoke_test.gd
git commit -m "feat: make dialog choices responsive and accessible"
```

### Task 9: Art Bible, Visual Showcase, And Legacy Cleanup

**Files:**
- Modify: `docs/visual_language.html`
- Modify: `docs/visual_language_doc.md`
- Replace: `docs/hud_preview.png`
- Create: `tools/validation/ui_visual_showcase.tscn`
- Create: `tools/validation/ui_visual_showcase.gd`
- Modify: `tools/tests/test_ui_visual_contract.py`
- Modify: `scripts/App/ScreenMain.cs`
- Modify: `scripts/Characters/Player/Player.cs`
- Delete only when reference scan confirms no consumer: `scripts/UI/HUD/SkillCooldownHudService.cs`
- Delete only when reference scan confirms no consumer: `scripts/UI/HUD/SkillCooldownHudService.cs.uid`
- Delete only when reference scan confirms no consumer: `scripts/UI/HUD/StatHexagonChart.cs`
- Delete only when reference scan confirms no consumer: `scripts/UI/HUD/StatHexagonChart.cs.uid`
- Delete only after clean reference scan: legacy UI assets listed in the spec

**Interfaces:**
- Consumes: every runtime component from Tasks 1-8.
- Produces: one showcase scene and documentation matching the actual build.

- [ ] **Step 1: Add failing documentation/legacy tests**

Assert all relative HTML/Markdown image references exist, no `.gemini` path remains, `world_concept_art.png` is the referenced filename, font claims match bundled files, the design states that living maps may be saturated and every approved legacy path has zero runtime references before deletion.

- [ ] **Step 2: Run full tests before cleanup**

Run: `python -m pytest tools/tests -q`

Expected: new documentation/legacy assertions FAIL; unrelated existing tests remain green.

- [ ] **Step 3: Build the showcase and capture documentation assets**

Show buttons/states, glyphs, party HUD, enemy HUD, panels and dialog on light/dark world samples. Capture at `1600x900` and replace `docs/hud_preview.png` with a runtime-derived preview; do not hand-compose or AI-generate the final preview.

- [ ] **Step 4: Rewrite the art bible**

Update HTML/Markdown with approved tokens, typography, shape/material rules, native sizes, value hierarchy, VFX/telegraph guidance, foreground/background rules, accessibility and do/don't examples. Remove software architecture principles unrelated to visual language and fix every local path.

- [ ] **Step 5: Remove legacy assets safely**

Run `rg` for each path first. Delete only files with no runtime consumer, update contract tests that intentionally pointed at old assets and keep unreferenced files when ownership is uncertain.

- [ ] **Step 6: Run complete automated verification**

Run: `python -m pytest tools/tests -q`

Run: `dotnet build "Ashes of a Dying World.csproj"`

Run Godot headless import and the structure/dialog/showcase validation scenes.

Expected: all tests PASS; build and scenes exit `0`.

- [ ] **Step 7: Perform final visual verification**

Capture main screen, party/world HUD, menu grid, Inventory, Character, Party, Quest, Skill, Settings and Dialog at `1600x900` and `1280x720`. Check nonblank output, native icon sharpness, text bounds, state geometry, focus visibility and the two overlay collision scenarios from Review Focus.

- [ ] **Step 8: Commit docs and cleanup**

```bash
git add docs assets/graphics/ui tools/validation tools/tests
git commit -m "docs: publish production UI visual language"
```

### Task 10: Whole-Branch Review And Release Gate

**Files:**
- Review only: all files changed by Tasks 1-9
- Modify only when fixing review findings

**Interfaces:**
- Consumes: completed implementation and verification evidence.
- Produces: reviewed branch with no unresolved P0/P1 findings.

- [ ] **Step 1: Inspect final diff and legacy references**

Run: `git diff --stat 2b23143...HEAD`, `git diff --check`, and `rg` for every removed asset/font/path.

- [ ] **Step 2: Run the full verification matrix once more**

Run pytest, `dotnet build`, Godot validation scenes and both viewport screenshot passes from a clean process.

- [ ] **Step 3: Request independent code/visual review**

Reviewer checks spec coverage, user-owned artwork preservation, dirty-dialog integration, focus/readability, runtime asset use and screenshot consistency. Fix all P0/P1 findings and rerun their owning tests.

- [ ] **Step 4: Commit review fixes if needed**

```bash
git add scripts/UI scripts/App scripts/Characters/Player scenes/ui tools/tests tools/validation docs assets/graphics/ui assets/fonts
git commit -m "fix: address UI redesign review findings"
```
