using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class PlayerManager : Node
    {
        public static PlayerManager Instance { get; private set; }
        public List<PlayerStats> PartyMembers = new List<PlayerStats>();

        [Signal] public delegate void PartyUpdatedEventHandler();
        // Signal mới: Thông báo khi nhân vật đang hoạt động thay đổi
        [Signal] public delegate void ActiveCharacterChangedEventHandler(int index);

        public int ActiveCharacterIndex { get; private set; } = 0;

        public override void _Ready()
        {
            Instance = this;

            
        }

        public override void _Input(InputEvent @event)
        {
            // Xử lý phím tắt 1, 2, 3 để đổi nhân vật
            if (@event.IsActionPressed("digit1")) SwitchToCharacter(0);
            if (@event.IsActionPressed("digit2")) SwitchToCharacter(1);
            if (@event.IsActionPressed("digit3")) SwitchToCharacter(2);
        }

        private void SwitchToCharacter(int index)
        {
            if (index >= PartyMembers.Count || index == ActiveCharacterIndex) return;

            ActiveCharacterIndex = index;
            
            // Thông báo cho HUD và các nhân vật khác
            EmitSignal(SignalName.ActiveCharacterChanged, index);
            
            GD.Print($"Đang điều khiển: {PartyMembers[index].ConfigData.Name}");
            
        }

        // Method public để UI có thể gọi
        public void SetActiveCharacter(int index)
        {
            SwitchToCharacter(index);
        }

        public void RegisterMember(PlayerStats member)
        {
            if (!PartyMembers.Contains(member) && PartyMembers.Count < 3)
            {
                PartyMembers.Add(member);
                EmitSignal(SignalName.PartyUpdated);
            }
        }
    }
}