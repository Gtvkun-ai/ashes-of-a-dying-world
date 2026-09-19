using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.NPC;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.Shared;
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
        private const string CommandMenuFramePath = "res://assets/graphics/ui/hud/companion_command_menu_frame.png";
        private const int CommandMenuPatchMargin = 34;

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
            ApplyCommandMenuTheme();
            _contextMenu.IdPressed += OnContextMenuIdPressed;
            AddChild(_contextMenu);
        }

        private void ApplyCommandMenuTheme()
        {
            if (_contextMenu == null)
            {
                return;
            }

            _contextMenu.AddThemeStyleboxOverride("panel", CreateCommandMenuPanelStyle());
            _contextMenu.AddThemeStyleboxOverride("hover", CreateCommandMenuHoverStyle());
            _contextMenu.AddThemeStyleboxOverride("separator", CreateCommandMenuSeparatorStyle());
            _contextMenu.AddThemeStyleboxOverride("labeled_separator_left", CreateCommandMenuSeparatorStyle());
            _contextMenu.AddThemeStyleboxOverride("labeled_separator_right", CreateCommandMenuSeparatorStyle());

            _contextMenu.AddThemeColorOverride("font_color", InventoryPanelChrome.MainTextColor);
            _contextMenu.AddThemeColorOverride("font_hover_color", Colors.White);
            _contextMenu.AddThemeColorOverride("font_disabled_color", new Color(0.82f, 0.74f, 0.63f, 0.48f));
            _contextMenu.AddThemeColorOverride("font_separator_color", InventoryPanelChrome.AccentColor);
            _contextMenu.AddThemeColorOverride("font_accelerator_color", InventoryPanelChrome.MutedTextColor);

            _contextMenu.AddThemeConstantOverride("h_separation", 8);
            _contextMenu.AddThemeConstantOverride("v_separation", 5);
            _contextMenu.AddThemeConstantOverride("item_start_padding", 12);
            _contextMenu.AddThemeConstantOverride("item_end_padding", 14);
            _contextMenu.AddThemeConstantOverride("indent", 8);
            _contextMenu.AddThemeFontSizeOverride("font_size", 15);
        }

        private StyleBox CreateCommandMenuPanelStyle()
        {
            Texture2D frame = InventoryPanelChrome.TryLoadTexture(CommandMenuFramePath);
            if (frame != null)
            {
                return new StyleBoxTexture
                {
                    Texture = frame,
                    DrawCenter = true,
                    TextureMarginLeft = CommandMenuPatchMargin,
                    TextureMarginTop = CommandMenuPatchMargin,
                    TextureMarginRight = CommandMenuPatchMargin,
                    TextureMarginBottom = CommandMenuPatchMargin,
                    ContentMarginLeft = 18,
                    ContentMarginTop = 16,
                    ContentMarginRight = 18,
                    ContentMarginBottom = 16
                };
            }

            StyleBoxFlat fallback = InventoryPanelChrome.CreateWindowStyle();
            fallback.ContentMarginLeft = 14;
            fallback.ContentMarginTop = 10;
            fallback.ContentMarginRight = 14;
            fallback.ContentMarginBottom = 10;
            return fallback;
        }

        private static StyleBoxFlat CreateCommandMenuHoverStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(
                    InventoryPanelChrome.AccentColor.R,
                    InventoryPanelChrome.AccentColor.G,
                    InventoryPanelChrome.AccentColor.B,
                    0.18f),
                BorderColor = new Color(
                    InventoryPanelChrome.AccentColor.R,
                    InventoryPanelChrome.AccentColor.G,
                    InventoryPanelChrome.AccentColor.B,
                    0.42f)
            };
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            style.ContentMarginLeft = 4;
            style.ContentMarginTop = 2;
            style.ContentMarginRight = 4;
            style.ContentMarginBottom = 2;
            return style;
        }

        private static StyleBoxFlat CreateCommandMenuSeparatorStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(
                    InventoryPanelChrome.AccentColor.R,
                    InventoryPanelChrome.AccentColor.G,
                    InventoryPanelChrome.AccentColor.B,
                    0.35f)
            };
            style.ContentMarginTop = 1;
            style.ContentMarginBottom = 1;
            return style;
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
