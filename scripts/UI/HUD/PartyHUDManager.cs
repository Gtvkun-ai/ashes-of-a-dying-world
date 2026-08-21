using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.NPC;
using AshesofaDyingWorld.Entities.Player;
using Godot;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class PartyHUDManager : CanvasLayer
    {
        private const int SwitchCharacterId = 10;
        private const int FollowCommandId = 101;
        private const int StayCommandId = 102;
        private const int ProtectCommandId = 103;
        private const int WanderCommandId = 104;

        private CharacterUnitHUD[] unitHUDs;
        private PopupMenu _contextMenu;
        private PlayerStats _contextMember;

        public override void _Ready()
        {
            var container = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (container == null)
            {
                GD.PrintErr("[PartyHUD] VBoxContainer not found!");
                return;
            }

            var children = container.GetChildren();
            unitHUDs = new CharacterUnitHUD[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                unitHUDs[i] = children[i] as CharacterUnitHUD;
                if (unitHUDs[i] == null)
                {
                    GD.PrintErr($"[PartyHUD] Child {i} is not CharacterUnitHUD!");
                    continue;
                }

                unitHUDs[i].ContextMenuRequested += OnCharacterContextRequested;
            }

            BuildContextMenu();

            PlayerManager manager = PlayerManager.GetOrCreate(GetTree());
            if (manager == null)
            {
                GD.PrintErr("[PartyHUD] Không tạo được PlayerManager.");
                return;
            }

            manager.PartyUpdated += RefreshPartyUI;
            manager.ActiveCharacterChanged += UpdateSelection;
            RefreshPartyUI();
        }

        private void BuildContextMenu()
        {
            _contextMenu = new PopupMenu
            {
                Name = "CharacterCommandContextMenu"
            };
            _contextMenu.IdPressed += OnContextMenuIdPressed;
            AddChild(_contextMenu);
        }

        private void OnCharacterContextRequested(PlayerStats member)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || member == null)
            {
                return;
            }

            int memberIndex = manager.PartyMembers.IndexOf(member);
            if (memberIndex < 0)
            {
                return;
            }

            _contextMember = member;
            _contextMenu.Clear();

            bool active = memberIndex == manager.ActiveCharacterIndex;
            string characterName = member.ConfigData?.Name ?? $"Thành viên {memberIndex + 1}";
            _contextMenu.AddItem(active ? $"ĐANG ĐIỀU KHIỂN: {characterName}" : $"ĐIỀU KHIỂN {characterName}", SwitchCharacterId);
            _contextMenu.SetItemDisabled(_contextMenu.ItemCount - 1, active);

            if (manager.GetCombatCharacter(member) is NpcCharacter companion && companion.IsRecruited)
            {
                _contextMenu.AddSeparator();
                _contextMenu.AddItem("MỆNH LỆNH HYOU");
                _contextMenu.SetItemDisabled(_contextMenu.ItemCount - 1, true);

                AddCommandItem("Theo sau", FollowCommandId, companion.CommandMode == CompanionCommandMode.Follow);
                AddCommandItem("Đứng yên", StayCommandId, companion.CommandMode == CompanionCommandMode.Stay);
                AddCommandItem("Bảo vệ", ProtectCommandId, companion.CommandMode == CompanionCommandMode.Protect);
                AddCommandItem("Đi dạo", WanderCommandId, companion.CommandMode == CompanionCommandMode.Wander);
            }

            Vector2 mouse = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            _contextMenu.Position = new Vector2I(Mathf.RoundToInt(mouse.X), Mathf.RoundToInt(mouse.Y));
            _contextMenu.Popup();
        }

        private void AddCommandItem(string label, int id, bool selected)
        {
            _contextMenu.AddRadioCheckItem(label, id);
            int index = _contextMenu.ItemCount - 1;
            _contextMenu.SetItemChecked(index, selected);
        }

        private void OnContextMenuIdPressed(long id)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || _contextMember == null)
            {
                return;
            }

            int memberIndex = manager.PartyMembers.IndexOf(_contextMember);
            if (memberIndex < 0)
            {
                return;
            }

            if (id == SwitchCharacterId)
            {
                manager.SetActiveCharacter(memberIndex);
                return;
            }

            if (manager.GetCombatCharacter(_contextMember) is not NpcCharacter companion)
            {
                return;
            }

            CompanionCommandMode? mode = id switch
            {
                FollowCommandId => CompanionCommandMode.Follow,
                StayCommandId => CompanionCommandMode.Stay,
                ProtectCommandId => CompanionCommandMode.Protect,
                WanderCommandId => CompanionCommandMode.Wander,
                _ => null
            };

            if (mode.HasValue)
            {
                companion.SetCommandMode(mode.Value);
                GD.Print($"[PartyHUD] command character={_contextMember.ConfigData?.ID ?? "companion"} mode={mode.Value}");
            }
        }

        private void UpdateSelection(int activeIndex)
        {
            if (unitHUDs == null)
            {
                return;
            }

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
            for (int i = 0; i < unitHUDs.Length; i++)
            {
                if (unitHUDs[i] == null)
                {
                    continue;
                }

                if (i < members.Count && members[i] != null)
                {
                    unitHUDs[i].SetTarget(members[i]);
                    unitHUDs[i].Show();
                    unitHUDs[i].ApplyHighlight(i == PlayerManager.Instance.ActiveCharacterIndex);
                }
                else
                {
                    unitHUDs[i].SetTarget(null);
                    unitHUDs[i].Hide();
                }
            }
        }

        public override void _ExitTree()
        {
            if (unitHUDs != null)
            {
                foreach (CharacterUnitHUD hud in unitHUDs)
                {
                    if (hud != null)
                    {
                        hud.ContextMenuRequested -= OnCharacterContextRequested;
                    }
                }
            }

            if (_contextMenu != null)
            {
                _contextMenu.IdPressed -= OnContextMenuIdPressed;
            }

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PartyUpdated -= RefreshPartyUI;
                PlayerManager.Instance.ActiveCharacterChanged -= UpdateSelection;
            }
        }
    }
}
