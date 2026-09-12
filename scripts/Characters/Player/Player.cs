using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.World.Interaction;

/// <summary>
/// Adapter người chơi: chỉ đọc input và giữ các API inventory/save.
/// Toàn bộ movement/combat thật nằm trong CombatCharacter + CombatActionRunner.
/// </summary>
public partial class Player : CombatCharacter
{
    [Export] public bool UsePlayerInput { get; set; } = true;

    private InventoryManager _inventory;
    private InteractionSensor _interactionSensor;
    private static readonly string[] SkillSlotActions = { "skill_1", "skill_2", "skill_3", "skill_4" };

    protected override void OnCombatReady()
    {
        Faction = CombatFaction.Player;
        AddToGroup("Player");

        _inventory = GetNodeOrNull<InventoryManager>("InventoryManager");
        if (_inventory == null)
        {
            _inventory = new InventoryManager { Name = "InventoryManager" };
            // OnCombatReady được gọi từ _Ready của CharacterBody2D; attach deferred để
            // không va vào pha Godot đang dựng children.
            CallDeferred("add_child", _inventory);
        }

        _interactionSensor = GetNodeOrNull<InteractionSensor>("InteractionSensor");
        if (_interactionSensor == null)
        {
            _interactionSensor = new InteractionSensor
            {
                Name = "InteractionSensor",
                Actor = this,
                InputEnabled = () => UsePlayerInput && IsAlive
            };
            // Giống InventoryManager: attach deferred vì OnCombatReady chạy trong _Ready.
            CallDeferred("add_child", _interactionSensor);
        }
        else
        {
            _interactionSensor.Actor = this;
            _interactionSensor.InputEnabled = () => UsePlayerInput && IsAlive;
        }

        InteractionPromptHud.GetOrCreate(GetTree());

        // Dựng trạng thái kỹ năng runtime sau khi các component combat đã sẵn sàng.
        InitializeSkillCollection();
        InitializeFlowEvasion();
        SkillCooldownHudService.GetOrCreate(GetTree());
        FloatingProgressionHudService.GetOrCreate(GetTree());
    }

    protected override void UpdateControlSource(float delta)
    {
        if (!IsAlive)
        {
            ResetFlowEvasionRuntime();
            return;
        }

        // Đóng băng toàn bộ input khi Dialogic dialog đang chạy.
        if (AshesofaDyingWorld.World.Interaction.DialogicBridge.IsDialogActive())
        {
            UpdateFlowEvasion(delta, Vector2.Zero);
            return;
        }

        if (!UsePlayerInput)
        {
            UpdateFlowEvasion(delta, Vector2.Zero);
            return;
        }

        bool wantsBlock = Input.IsKeyPressed(Key.X)
            || (InputMap.HasAction("block") && Input.IsActionPressed("block"));
        SetBlocking(wantsBlock);

        Vector2 inpuGodot Engine v4.7.2.stable.mono.official.ed1daf0bf - https://godotengine.org
OpenGL API 3.3.0 NVIDIA 552.27 - Compatibility - Using Device: NVIDIA - NVIDIA GeForce RTX 3050 Laptop GPU

[SettingsManager] READY fullscreen=False resolution=1600x900 fps_cap=144 focused=True
[SettingsManager] Created runtime fallback at /root/SettingsManager
[AudioManager] READY sfx_pool=12 bgm=res://assets/audio/music/bgm/bg_02.mp3
[AudioManager] Created runtime fallback at /root/AudioManager
[EnvironmentDebug] ACTIVE | Ctrl+F9 time | Ctrl+F10 speed | Ctrl+F12 weather
[EnvironmentDebug] boot | day=0 hour=00.00 daylight=0.00 night=0.00 golden=0.00 wind=0.00 sunEl=1.00 sunE=0.00 moonEl=0.00 moonE=0.00 shadowKey=1.00 shadowDir=(0.00,1.00) shadowAngle=90deg shadowLen=0.00 rain=0.00 wet=0.00
[ShaderGlobalBridge] READY V5.0 | globals=19 | material_scan=OFF
[AudioManager] BGM playing: res://assets/audio/music/bgm/bg_02.mp3
  ERROR: Unicode parsing error: Invalid unicode codepoint (1ec7), cannot represent as ASCII/Latin-1
  ERROR: Unicode parsing error: Byte 69 is not a correct continuation byte after f3
[WorldCloudShadow2D] READY V7.2 | seed=random | speed=7.6px/s | broad_world_clouds=ON
[EnvironmentShadowSystem2D] READY V5.1 | footprint_casters=472 | shader_projection=OFF | mass_heuristic=OFF
[EnvironmentMassShadow2D] READY V5.1h | root=/root/Fields1 cluster_mass=2 border_mass=7
[EnvironmentBinder2D] READY V5.4 | gpu=global_uniforms | material_scan=OFF | lighting_owner=material | grass_canvas=456x474 | shadow=ground_footprint | mass_shadow=ON | profile=res://data/world/environment/whispering_fields.tres
[EnvironmentBinder2D] FIELD01_GRADE_V14 | noon=clean | golden=warmer | night=less_cyan
[EnvironmentBinder2D] TREE_GROUND_V14 | trees=267 painted=158 | source=456x474 scale=(4,4) world=1824x1896 | affects_path=ON
[WorldLighting2D] READY V5.4 fallback-only restrained sun/moon fill
[WorldAtmosphere2D] READY V5.6 directional forest beams + world-locked cloud/fog/rain
[CharacterDetailUI] Đã nạp ảnh HP: res://assets/graphics/ui/status/main_stats/hp_frame.png
[CharacterDetailUI] Đã nạp ảnh HP: res://assets/graphics/ui/status/main_stats/hp_fill.png
[CharacterDetailUI] Đã nạp ảnh MP: res://assets/graphics/ui/status/main_stats/mp_frame.png
[CharacterDetailUI] Đã nạp ảnh MP: res://assets/graphics/ui/status/main_stats/mp_fill.png
[CharacterDetailUI] Đã nạp ảnh STA: res://assets/graphics/ui/status/main_stats/stamina_frame.png
[CharacterDetailUI] Đã nạp ảnh STA: res://assets/graphics/ui/status/main_stats/stamina_fill.png
[SlimeBrain] READY build=v7-smoother-melee slime=enemy_slime instance=173828741025 combat_spawn_leash=False forget=360.0 provoked_forget=520.0
[SlimeBrain] READY build=v7-smoother-melee slime=enemy_slime instance=173979735978 combat_spawn_leash=False forget=360.0 provoked_forget=520.0
[DialogicBridge] Calling Dialogic.start('hyou_intro')...
[DialogicBridge] Dialogic.start returned: type=Object
[DialogicBridge] Dialogic.start OK → layout node = <CanvasLayer#1965383760278>
[Avatar] speaker_updated signal connected OK
[Avatar] speaker='Hyou' portraits=[]
--- Debugging process stopped ---
tDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        UpdateFlowEvasion(delta, inputDirection);

        bool wantsRun = Input.IsKeyPressed(Key.Shift)
            || (InputMap.HasAction("run") && Input.IsActionPressed("run"));
        SetMoveInput(inputDirection, wantsRun);

        if (InputMap.HasAction("attack") && Input.IsActionJustPressed("attack"))
        {
            RequestAttack();
        }

        for (int slot = 0; slot < SkillSlotActions.Length; slot++)
        {
            string actionName = SkillSlotActions[slot];
            if (InputMap.HasAction(actionName) && Input.IsActionJustPressed(actionName))
            {
                TryActivateSkillSlot(slot);
            }
        }
    }

    public AshesofaDyingWorld.Entities.Player.PlayerStats GetStatsNode() => Stats;
    public InventoryManager GetInventoryManager() => _inventory;
    public EquipmentManager GetEquipmentManager() => Equipment;

    public void ResetTransientStateAfterLoad()
    {
        ResetFlowEvasionRuntime();
        ResetCombatRuntime();
    }
}
