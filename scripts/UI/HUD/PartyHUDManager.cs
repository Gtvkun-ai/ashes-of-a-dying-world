using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.NPC;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.UI.Theme;
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
        private const float CommandMenuMinWidth = 206f;
        private const float CommandMenuHeaderHeight = 28f;
        private const float CommandMenuControlHeight = UiTokens.ButtonHeightCompact;
        private const float CommandMenuButtonHeight = UiTokens.ButtonHeightCompact;
        private const float CommandMenuHudGap = 4f;

        private CharacterUnitHUD[] unitHUDs;
        private PanelContainer _contextMenu;
        private VBoxContainer _contextMenuItems;
        private PlayerStats _contextMember;
        private CharacterUnitHUD _contextSourceHud;

        public override void _Ready()
        {
            var container = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (container == null)
            {
                GD.PrintErr("[PartyHUD] VBoxContainer not found!");
                return;
            }
            UiThemeFactory.Apply(container);

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
            UiThemeFactory.Apply(_contextMenu);
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

        private static StyleBoxFlat CreateCommandMenuPanelStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = UiTokens.Background,
                BorderColor = UiTokens.BorderStrong,
                ContentMarginLeft = UiTokens.Space2,
                ContentMarginTop = UiTokens.Space2,
                ContentMarginRight = UiTokens.Space2,
                ContentMarginBottom = UiTokens.Space2
            };
            style.SetBorderWidthAll(UiTokens.BorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);
            return style;
        }

        private static StyleBoxFlat CreateCommandHeaderStyle()
        {
            StyleBoxFlat style = new()
            {
                BgColor = UiTokens.Surface,
                BorderColor = UiTokens.Border,
                BorderWidthBottom = 1
            };
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

            var glyph = new TextureRect
            {
                Texture = UiGlyphResolver.Resolve(UiGlyph.Party),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(UiTokens.IconSizeSmall, UiTokens.IconSizeSmall),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(glyph);

            Label name = new()
            {
                Text = characterName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Pass,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                TooltipText = characterName
            };
            UiThemeFactory.ApplyText(name, UiTextRole.Label);
            row.AddChild(name);

            Control spacer = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(spacer);

            Label mode = new()
            {
                Text = active ? "ĐANG CHỌN" : "MỆNH LỆNH",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            mode.AddThemeFontSizeOverride("font_size", UiTokens.MicroFontSize);
            mode.AddThemeColorOverride("font_color", active ? UiTokens.Accent : UiTokens.TextSecondary);
            row.AddChild(mode);
            return header;
        }

        private Button CreateControlButton(string characterName, bool active, System.Action onPressed)
        {
            Button button = CreateCommandMenuButton(
                active ? "Đang điều khiển" : "Điều khiển",
                active,
                active,
                UiGlyph.Character,
                onPressed);
            button.TooltipText = active
                ? $"Đang điều khiển {characterName}"
                : $"Chuyển điều khiển sang {characterName}";
            return button;
        }

        private Button CreateCommandRow(string text, bool selected, UiGlyph glyph, System.Action onPressed)
        {
            return CreateCommandMenuButton(text, selected, false, glyph, onPressed);
        }

        private Button CreateCommandMenuButton(
            string text,
            bool selected,
            bool disabled,
            UiGlyph glyph,
            System.Action onPressed)
        {
            Button button = new()
            {
                Text = text,
                Disabled = disabled,
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = disabled ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(CommandMenuMinWidth, selected && disabled ? CommandMenuControlHeight : CommandMenuButtonHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Flat = false,
                Alignment = HorizontalAlignment.Left,
                ClipText = true,
                Icon = UiGlyphResolver.Resolve(glyph),
                TooltipText = text
            };

            PixelButtonSkin.Apply(button,
                selected ? PixelButtonSkin.Variant.Primary : PixelButtonSkin.Variant.Secondary,
                selected && disabled ? CommandMenuControlHeight : CommandMenuButtonHeight,
                CommandMenuMinWidth);
            button.AddThemeFontSizeOverride("font_size", UiTokens.BodyFontSize);

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
                Color = UiTokens.Border,
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

                AddCommandButton("Theo sau", FollowCommandId, companion.CommandMode == CompanionCommandMode.Follow, UiGlyph.Target);
                AddCommandButton("Đứng yên", StayCommandId, companion.CommandMode == CompanionCommandMode.Stay, UiGlyph.Party);
                AddCommandButton("Bảo vệ", ProtectCommandId, companion.CommandMode == CompanionCommandMode.Protect, UiGlyph.Defense);
                AddCommandButton("Đi dạo", WanderCommandId, companion.CommandMode == CompanionCommandMode.Wander, UiGlyph.CategoryMap);
            }

            Vector2 mouse = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            CharacterUnitHUD sourceHud = unitHUDs != null && memberIndex < unitHUDs.Length
                ? unitHUDs[memberIndex]
                : null;
            ShowContextMenuForHud(sourceHud, mouse);
        }

        private void AddCommandButton(string label, int id, bool selected, UiGlyph glyph)
        {
            _contextMenuItems.AddChild(CreateCommandRow(label, selected, glyph, () => ExecuteContextMenuId(id)));
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

            _contextSourceHud = sourceHud;
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
            FocusFirstEnabledCommand();
        }

        private void FocusFirstEnabledCommand()
        {
            if (_contextMenuItems == null)
            {
                return;
            }

            foreach (Node child in _contextMenuItems.GetChildren())
            {
                if (child is Button button && button.Visible && !button.Disabled)
                {
                    button.GrabFocus();
                    return;
                }
            }
        }

        private void HideContextMenu()
        {
            if (_contextMenu?.Visible != true)
            {
                return;
            }

            _contextMenu.Hide();
            if (GodotObject.IsInstanceValid(_contextSourceHud) && _contextSourceHud.Visible)
            {
                _contextSourceHud?.GrabFocus();
            }
            _contextSourceHud = null;
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

            if (inputEvent.IsActionPressed("ui_cancel"))
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
