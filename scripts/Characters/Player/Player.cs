using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

/// <summary>
/// Adapter người chơi: chỉ đọc input và giữ các API inventory/save.
/// Toàn bộ movement/combat thật nằm trong CombatCharacter + CombatActionRunner.
/// </summary>
public partial class Player : CombatCharacter
{
    [Export] public bool UsePlayerInput { get; set; } = true;

    private InventoryManager _inventory;
    private const string SkillSlot1Action = "skill_1";

    protected override void OnCombatReady()
    {
        Faction = CombatFaction.Player;
        AddToGroup("Player");

        _inventory = GetNodeOrNull<InventoryManager>("InventoryManager");
        if (_inventory == null)
        {
            _inventory = new InventoryManager { Name = "InventoryManager" };
            AddChild(_inventory);
        }

        // Dựng trạng thái kỹ năng runtime sau khi các component combat đã sẵn sàng.
        InitializeSkillCollection();
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

        if (InputMap.HasAction(SkillSlot1Action) && Input.IsActionJustPressed(SkillSlot1Action))
        {
            TryActivateSkillSlot(0);
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
