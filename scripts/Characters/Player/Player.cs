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
        SkillCooldownHudService.GetOrCreate(GetTree());
        FloatingProgressionHudService.GetOrCreate(GetTree());
    }

    protected override void UpdateControlSource(float delta)
    {
        if (!UsePlayerInput || !IsAlive)
        {
            return;
        }

        bool wantsBlock = Input.IsKeyPressed(Key.X)
            || (InputMap.HasAction("block") && Input.IsActionPressed("block"));
        SetBlocking(wantsBlock);

        Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
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
        ResetCombatRuntime();
    }
}
