using AshesofaDyingWorld.Core.Managers;
using Godot;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class PartyHUDManager : CanvasLayer
    {
        private CharacterUnitHUD[] unitHUDs;

        public override void _Ready()
        {
            
            // Lấy VBoxContainer và tìm các CharacterUnitHUD con
            var container = GetNode<VBoxContainer>("VBoxContainer");
            if (container == null)
            {
                GD.PrintErr("[PartyHUD] VBoxContainer not found!");
                return;
            }

            // Lấy tất cả CharacterUnitHUD con
            var children = container.GetChildren();
            unitHUDs = new CharacterUnitHUD[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                unitHUDs[i] = children[i] as CharacterUnitHUD;
                if (unitHUDs[i] == null)
                {
                    GD.PrintErr($"[PartyHUD] Child {i} is not CharacterUnitHUD!");
                }
            }

            if (PlayerManager.Instance == null)
            {
                GD.PrintErr("[PartyHUD] PlayerManager.Instance is null!");
                return;
            }

            // Lắng nghe khi có thành viên mới được thêm vào nhóm
            PlayerManager.Instance.PartyUpdated += RefreshPartyUI;

            // Kết nối tín hiệu đổi nhân vật
            PlayerManager.Instance.ActiveCharacterChanged += UpdateSelection;

            // Khởi tạo giao diện ban đầu
            RefreshPartyUI();
        }

        private void UpdateSelection(int activeIndex)
        {
            if (unitHUDs == null) return;
            
            for (int i = 0; i < unitHUDs.Length; i++)
            {
                unitHUDs[i]?.ApplyHighlight(i == activeIndex);
            }
        }

        public void RefreshPartyUI()
        {
            if (PlayerManager.Instance == null || unitHUDs == null) 
            {
                GD.PrintErr("[PartyHUD] RefreshPartyUI: PlayerManager or unitHUDs is null");
                return;
            }

            
            var members = PlayerManager.Instance.PartyMembers; 

            for(int i = 0; i < unitHUDs.Length; i++)
            {
                if (unitHUDs[i] == null) 
                {
                    GD.PrintErr($"[PartyHUD] unitHUDs[{i}] is null!");
                    continue;
                }

                if(i < members.Count && members[i] != null)
                {
                    unitHUDs[i].SetTarget(members[i]);
                    unitHUDs[i].Show();
                    unitHUDs[i].ApplyHighlight(i == PlayerManager.Instance.ActiveCharacterIndex);
                }
                else
                {
                    unitHUDs[i].Hide();
                }
            }
        }

        public override void _ExitTree()
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PartyUpdated -= RefreshPartyUI;
                PlayerManager.Instance.ActiveCharacterChanged -= UpdateSelection;
            }
        }
    }
}
