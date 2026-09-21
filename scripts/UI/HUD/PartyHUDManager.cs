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
        // Menu lệnh V2: dùng khung native-size mảnh hơn thay vì ép asset 384 px xuống còn ~170 px.
        // Các hàng lệnh dùng StyleBox phẳng để giảm cảm giác "khung trong khung" và giữ pixel sạch.
        private const string CommandMenuPanelPath = "res://assets/graphics/ui/hud/redesign/companion_command_menu_panel_v2.png";
        private const int CommandMenuPanelPatchMargin = 12;
        private const float CommandMenuMinWidth = 206f;
        private const float CommandMenuHeaderHeight = 28f;
        private const float CommandMenuControlHeight = 30f;
        private const float CommandMenuButtonHeight = 25f;
        private const float CommandMenuHudGap = 4f;

        // Accent xanh chỉ dành cho trạng thái đang chọn/đang điều khiển Hyou.
        // Như vậy menu vẫn cùng họ nâu-vàng của HUD nhưng có một điểm nhận diện riêng cho Hyou.
        private static readonly Color CommandBlueAccent = new("#79B3F2");
        private static readonly Color CommandBlueSurface = new("#1B2B39");
        private static readonly Color CommandSurface = new("#21150F");
        private static readonly Color CommandHoverSurface = new("#342319");
        private static readonly Color CommandDividerColor = new(0.35f, 0.24f, 0.15f, 0.55f);

        private CharacterUnitHUD[] unitHUDs;
        private PanelContainer _contextMenu;
        private VBoxContainer _contextMenuItems;
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
            _contextMenu = new PanelContainer
            {
                Name = "CharacterCommandContextMenu",
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 1000
            };
            _contextMenu.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _contextMenu.AddThemeStyleboxOverride("panel", CreateCommandMenuPanelStyle());
            AddChild(_contextMenu);
            MoveChild(_contextMenu, GetChildCount() - 1);

            _contextMenuItems = new VBoxContainer
            {
                Name = "CommandRows",
                CustomMinimumSize = new Vector2(CommandMenuMinWidth, 0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            // V2 chủ động để các row sát nhau; khoảng thở được tạo bằng header/divider rõ ràng
            // thay vì khoảng trống chen giữa hàng loạt nút có khung vàng.
            _contextMenuItems.AddThemeConstantOverride("separation", 3);
            _contextMenu.AddChild(_contextMenuItems);
        }

        private StyleBox CreateCommandMenuPanelStyle()
        {
            Texture2D frame = InventoryPanelChrome.TryLoadTexture(CommandMenuPanelPath);
            if (frame != null)
            {
                return new StyleBoxTexture
                {
                    Texture = frame,
                    DrawCenter = true,
                    TextureMarginLeft = CommandMenuPanelPatchMargin,
                    TextureMarginTop = CommandMenuPanelPatchMargin,
                    TextureMarginRight = CommandMenuPanelPatchMargin,
                    TextureMarginBottom = CommandMenuPanelPatchMargin,
                    ContentMarginLeft = 9,
                    ContentMarginTop = 9,
                    ContentMarginRight = 9,
                    ContentMarginBottom = 9
                };
            }

            StyleBoxFlat fallback = InventoryPanelChrome.CreateWindowStyle();
            fallback.ContentMarginLeft = 10;
            fallback.ContentMarginTop = 10;
            fallback.ContentMarginRight = 10;
            fallback.ContentMarginBottom = 10;
            return fallback;
        }

        private static StyleBoxFlat CreateCommandHeaderStyle()
        {
            StyleBoxFlat style = new()
            {
                BgColor = new Color(0.15f, 0.09f, 0.055f, 0.92f),
                BorderColor = new Color(InventoryPanelChrome.BorderColor.R, InventoryPanelChrome.BorderColor.G, InventoryPanelChrome.BorderColor.B, 0.62f),
                BorderWidthBottom = 1
            };
            return style;
        }

        private static StyleBoxFlat CreateControlButtonStyle(bool active, bool hover = false, bool pressed = false)
        {
            Color bg = active
                ? CommandBlueSurface
                : pressed
                    ? CommandSurface.Darkened(0.08f)
                    : hover
                        ? CommandHoverSurface
                        : CommandSurface;

            Color border = active
                ? new Color(CommandBlueAccent.R, CommandBlueAccent.G, CommandBlueAccent.B, 0.78f)
                : hover
                    ? new Color(InventoryPanelChrome.AccentColor.R, InventoryPanelChrome.AccentColor.G, InventoryPanelChrome.AccentColor.B, 0.78f)
                    : new Color(InventoryPanelChrome.BorderColor.R, InventoryPanelChrome.BorderColor.G, InventoryPanelChrome.BorderColor.B, 0.76f);

            StyleBoxFlat style = new()
            {
                BgColor = bg,
                BorderColor = border,
                ContentMarginLeft = 10,
                ContentMarginTop = 4,
                ContentMarginRight = 10,
                ContentMarginBottom = 4
            };
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(1);
            return style;
        }

        private static StyleBoxFlat CreateCommandRowStyle(bool selected, bool hover = false, bool pressed = false)
        {
            Color bg = selected
                ? CommandBlueSurface
                : pressed
                    ? CommandSurface.Darkened(0.10f)
                    : hover
                        ? CommandHoverSurface
                        : new Color(CommandSurface.R, CommandSurface.G, CommandSurface.B, 0.36f);

            StyleBoxFlat style = new()
            {
                BgColor = bg,
                BorderColor = selected
                    ? CommandBlueAccent
                    : new Color(InventoryPanelChrome.AccentColor.R, InventoryPanelChrome.AccentColor.G, InventoryPanelChrome.AccentColor.B, 0.72f),
                BorderWidthLeft = selected ? 3 : hover ? 2 : 0,
                ContentMarginLeft = selected ? 8 : 11,
                ContentMarginTop = 3,
                ContentMarginRight = 8,
                ContentMarginBottom = 3
            };
            style.SetCornerRadiusAll(1);
            return style;
        }

        private Control CreateCommandMenuHeader(string characterName, bool active)
        {
            PanelContainer header = new()
            {
                Name = "CommandHeader",
                CustomMinimumSize = new Vector2(0f, CommandMenuHeaderHeight),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            header.AddThemeStyleboxOverride("panel", CreateCommandHeaderStyle());

            MarginContainer margin = new();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_top", 3);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 3);
            header.AddChild(margin);

            HBoxContainer row = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Begin
            };
            row.AddThemeConstantOverride("separation", 7);
            margin.AddChild(row);

            Label name = new()
            {
                Text = characterName.ToUpperInvariant(),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            name.AddThemeFontSizeOverride("font_size", 16);
            name.AddThemeColorOverride("font_color", InventoryPanelChrome.MainTextColor);
            row.AddChild(name);

            Control spacer = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(spacer);

            Label mode = new()
            {
                Text = active ? "ACTIVE" : "COMMAND",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            mode.AddThemeFontSizeOverride("font_size", 9);
            mode.AddThemeColorOverride("font_color", active ? CommandBlueAccent : InventoryPanelChrome.MutedTextColor);
            row.AddChild(mode);
            return header;
        }

        private Button CreateControlButton(string characterName, bool active, System.Action onPressed)
        {
            Button button = new()
            {
                Text = active ? "ĐANG ĐIỀU KHIỂN" : $"ĐIỀU KHIỂN {characterName}",
                Disabled = active,
                FocusMode = Control.FocusModeEnum.None,
                MouseDefaultCursorShape = active ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(CommandMenuMinWidth, CommandMenuControlHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Flat = false,
                Alignment = HorizontalAlignment.Left
            };

            button.AddThemeStyleboxOverride("normal", CreateControlButtonStyle(active));
            button.AddThemeStyleboxOverride("hover", CreateControlButtonStyle(false, hover: true));
            button.AddThemeStyleboxOverride("pressed", CreateControlButtonStyle(false, pressed: true));
            button.AddThemeStyleboxOverride("disabled", CreateControlButtonStyle(true));
            button.AddThemeColorOverride("font_color", InventoryPanelChrome.MainTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", new Color(0.82f, 0.91f, 1f, 0.95f));
            button.AddThemeFontSizeOverride("font_size", 13);

            if (!active && onPressed != null)
            {
                button.Pressed += onPressed;
            }

            return button;
        }

        private Button CreateCommandRow(string text, bool selected, System.Action onPressed)
        {
            Button button = new()
            {
                Text = selected ? $"◆  {text}" : $"   {text}",
                FocusMode = Control.FocusModeEnum.None,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(CommandMenuMinWidth, CommandMenuButtonHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Flat = false,
                Alignment = HorizontalAlignment.Left
            };

            button.AddThemeStyleboxOverride("normal", CreateCommandRowStyle(selected));
            button.AddThemeStyleboxOverride("hover", CreateCommandRowStyle(selected, hover: true));
            button.AddThemeStyleboxOverride("pressed", CreateCommandRowStyle(selected, pressed: true));
            button.AddThemeColorOverride("font_color", selected ? new Color(0.86f, 0.94f, 1f, 1f) : InventoryPanelChrome.MainTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 14);

            if (onPressed != null)
            {
                button.Pressed += onPressed;
            }

            return button;
        }

        private static ColorRect CreateCommandDivider()
        {
            return new ColorRect
            {
                Color = CommandDividerColor,
                CustomMinimumSize = new Vector2(0f, 1f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
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
            ClearContextMenuItems();

            bool active = memberIndex == manager.ActiveCharacterIndex;
            string characterName = member.ConfigData?.Name ?? $"Thành viên {memberIndex + 1}";

            // Header thay cho nút "MỆNH LỆNH HYOU" cũ: phân cấp thị giác rõ hơn, không còn
            // hai nút tiêu đề giống hệt nút thao tác.
            _contextMenuItems.AddChild(CreateCommandMenuHeader(characterName, active));
            _contextMenuItems.AddChild(CreateControlButton(
                characterName,
                active,
                () => ExecuteContextMenuId(SwitchCharacterId)));

            if (manager.GetCombatCharacter(member) is NpcCharacter companion && companion.IsRecruited)
            {
                _contextMenuItems.AddChild(CreateCommandDivider());

                AddCommandButton("Theo sau", FollowCommandId, companion.CommandMode == CompanionCommandMode.Follow);
                AddCommandButton("Đứng yên", StayCommandId, companion.CommandMode == CompanionCommandMode.Stay);
                AddCommandButton("Bảo vệ", ProtectCommandId, companion.CommandMode == CompanionCommandMode.Protect);
                AddCommandButton("Đi dạo", WanderCommandId, companion.CommandMode == CompanionCommandMode.Wander);
            }

            Vector2 mouse = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            CharacterUnitHUD sourceHud = unitHUDs != null && memberIndex < unitHUDs.Length
                ? unitHUDs[memberIndex]
                : null;
            ShowContextMenuForHud(sourceHud, mouse);
        }

        private void AddCommandButton(string label, int id, bool selected)
        {
            // Không dùng radio ○/● nữa. Trạng thái selected được biểu diễn bằng nền xanh tối,
            // vạch xanh bên trái và diamond nhỏ; các hàng còn lại giữ nền tối nhẹ.
            _contextMenuItems.AddChild(CreateCommandRow(label, selected, () => ExecuteContextMenuId(id)));
        }

        private void ClearContextMenuItems()
        {
            if (_contextMenuItems == null)
            {
                return;
            }

            foreach (Node child in _contextMenuItems.GetChildren())
            {
                _contextMenuItems.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void ShowContextMenuForHud(CharacterUnitHUD sourceHud, Vector2 fallbackPosition)
        {
            if (_contextMenu == null)
            {
                return;
            }

            _contextMenu.Show();
            _contextMenu.Size = _contextMenu.GetCombinedMinimumSize();

            Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            Vector2 menuSize = _contextMenu.Size;
            float x = fallbackPosition.X;
            float y = fallbackPosition.Y;

            if (sourceHud != null && sourceHud.IsInsideTree())
            {
                Rect2 hudRect = sourceHud.GetGlobalRect();
                float rightX = hudRect.Position.X + hudRect.Size.X + CommandMenuHudGap;
                float leftX = hudRect.Position.X - menuSize.X - CommandMenuHudGap;

                // HUD party hiện nằm sát cạnh phải màn hình, vì vậy menu thường bung sang trái.
                // Nếu layout sau này đổi, code vẫn ưu tiên cạnh còn đủ chỗ thay vì đè lên portrait/stat.
                if (viewportSize.X <= 0f || rightX + menuSize.X <= viewportSize.X)
                {
                    x = rightX;
                }
                else if (leftX >= 0f)
                {
                    x = leftX;
                }

                y = hudRect.Position.Y + 2f;
            }

            if (viewportSize.X > 0f)
            {
                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, viewportSize.X - menuSize.X));
            }

            if (viewportSize.Y > 0f)
            {
                y = Mathf.Clamp(y, 0f, Mathf.Max(0f, viewportSize.Y - menuSize.Y));
            }

            _contextMenu.Position = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        private void HideContextMenu()
        {
            _contextMenu?.Hide();
        }

        private void ExecuteContextMenuId(int id)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || _contextMember == null)
            {
                HideContextMenu();
                return;
            }

            int memberIndex = manager.PartyMembers.IndexOf(_contextMember);
            if (memberIndex < 0)
            {
                HideContextMenu();
                return;
            }

            if (id == SwitchCharacterId)
            {
                manager.SetActiveCharacter(memberIndex);
                HideContextMenu();
                return;
            }

            if (manager.GetCombatCharacter(_contextMember) is not NpcCharacter companion)
            {
                HideContextMenu();
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
                HideContextMenu();
                return;
            }

            HideContextMenu();
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            if (_contextMenu?.Visible != true)
            {
                return;
            }

            if (inputEvent is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                HideContextMenu();
                GetViewport()?.SetInputAsHandled();
                return;
            }

            if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
            {
                if (!_contextMenu.GetGlobalRect().HasPoint(mouse.Position))
                {
                    HideContextMenu();
                }
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

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.PartyUpdated -= RefreshPartyUI;
                PlayerManager.Instance.ActiveCharacterChanged -= UpdateSelection;
            }
        }
    }
}
