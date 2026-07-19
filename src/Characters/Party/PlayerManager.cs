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
            
            
        }

        // Method public để UI có thể gọi
        public void SetActiveCharacter(int index)
        {
            SwitchToCharacter(index);
        }

        public void RegisterMember(PlayerStats member)
        {
            if (member == null)
            {
                return;
            }

            if (!PartyMembers.Contains(member) && PartyMembers.Count < 3)
            {
                PartyMembers.Add(member);
                EmitSignal(SignalName.PartyUpdated);
            }
        }

        public void UnregisterMember(PlayerStats member)
        {
            if (member == null)
            {
                return;
            }

            if (!PartyMembers.Remove(member))
            {
                return;
            }

            if (PartyMembers.Count == 0)
            {
                ActiveCharacterIndex = 0;
            }
            else if (ActiveCharacterIndex >= PartyMembers.Count)
            {
                ActiveCharacterIndex = PartyMembers.Count - 1;
            }

            EmitSignal(SignalName.PartyUpdated);
            EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
        }

        public void ResetParty()
        {
            PartyMembers.Clear();
            ActiveCharacterIndex = 0;
            EmitSignal(SignalName.PartyUpdated);
            EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
        }
    }
}
