using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class PlayerManager : Node
    {
        public const int MaxPartySize = 3;

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

        private bool SwitchToCharacter(int index)
        {
            if (index < 0 || index >= PartyMembers.Count || index == ActiveCharacterIndex)
            {
                return false;
            }

            ActiveCharacterIndex = index;
            EmitSignal(SignalName.ActiveCharacterChanged, index);
            return true;
        }

        /// <summary>Đổi nhân vật đang điều khiển từ gameplay hoặc UI.</summary>
        public void SetActiveCharacter(int index)
        {
            SwitchToCharacter(index);
        }

        /// <summary>
        /// Đội trưởng hiện chính là nhân vật đang điều khiển. Dùng chung một state
        /// giúp HUD, phím tắt và panel Tổ đội không tự hiểu "leader" theo ba cách khác nhau.
        /// </summary>
        public bool SetPartyLeader(int index)
        {
            if (index < 0 || index >= PartyMembers.Count)
            {
                return false;
            }

            if (index == ActiveCharacterIndex)
            {
                return true;
            }

            return SwitchToCharacter(index);
        }

        /// <summary>
        /// Di chuyển thành viên trong đội hình và giữ nguyên người đang được điều khiển.
        /// </summary>
        public bool MoveMember(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= PartyMembers.Count
                || toIndex < 0 || toIndex >= PartyMembers.Count
                || fromIndex == toIndex)
            {
                return false;
            }

            PlayerStats activeMember = ActiveCharacterIndex >= 0 && ActiveCharacterIndex < PartyMembers.Count
                ? PartyMembers[ActiveCharacterIndex]
                : null;

            PlayerStats movingMember = PartyMembers[fromIndex];
            PartyMembers.RemoveAt(fromIndex);
            PartyMembers.Insert(toIndex, movingMember);

            int previousActiveIndex = ActiveCharacterIndex;
            ActiveCharacterIndex = activeMember != null
                ? Mathf.Max(0, PartyMembers.IndexOf(activeMember))
                : 0;

            EmitSignal(SignalName.PartyUpdated);
            if (previousActiveIndex != ActiveCharacterIndex)
            {
                EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
            }
            return true;
        }

        /// <summary>Lưu thứ tự party bằng ID ổn định thay vì vị trí node trong scene.</summary>
        public List<string> CapturePartyOrder()
        {
            var result = new List<string>();
            foreach (PlayerStats member in PartyMembers)
            {
                string id = member?.ConfigData?.ID;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        /// <summary>
        /// Khôi phục thứ tự party từ save. ID không còn tồn tại sẽ bị bỏ qua;
        /// thành viên mới chưa có trong save được nối vào cuối đội hình.
        /// </summary>
        public void RestorePartyOrder(IReadOnlyList<string> characterIds)
        {
            if (characterIds == null || characterIds.Count == 0 || PartyMembers.Count <= 1)
            {
                return;
            }

            var reordered = new List<PlayerStats>();
            foreach (string characterId in characterIds)
            {
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    continue;
                }

                PlayerStats match = PartyMembers.Find(member =>
                    member?.ConfigData != null
                    && member.ConfigData.ID == characterId
                    && !reordered.Contains(member));
                if (match != null)
                {
                    reordered.Add(match);
                }
            }

            foreach (PlayerStats member in PartyMembers)
            {
                if (member != null && !reordered.Contains(member))
                {
                    reordered.Add(member);
                }
            }

            if (reordered.Count != PartyMembers.Count)
            {
                return;
            }

            PartyMembers.Clear();
            PartyMembers.AddRange(reordered);
            ActiveCharacterIndex = Mathf.Clamp(ActiveCharacterIndex, 0, PartyMembers.Count - 1);
            EmitSignal(SignalName.PartyUpdated);
        }

        public void RegisterMember(PlayerStats member)
        {
            if (member == null)
            {
                return;
            }

            if (!PartyMembers.Contains(member) && PartyMembers.Count < MaxPartySize)
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
