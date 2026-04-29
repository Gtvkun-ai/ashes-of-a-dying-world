using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.Menus
{
    public partial class InventoryPanel : Panel
    {
        private const int DefaultSlotCount = 40;
        private const int GridColumns = 8;

        private enum InventoryCategory
        {
            Items,
            Tools,
            Quests
        }

        [Export] public NodePath InventoryManagerPath { get; set; }

        private readonly Color _accentColor = new Color("#38bdf8");
        private readonly Color _subTextColor = new Color("#94a3b8");
        private readonly Color _themeBorderColor = new Color("#38bdf8");
        private readonly Color _btnNormalColor = new Color("#1e293b");
        private readonly Color _btnHoverColor = new Color("#334155");

        private InventoryManager _inventoryManager;
        private NinePatchRect _panelGlow;
        private NinePatchRect _panelFrame;
        private Button _itemsButton;
        private Button _toolsButton;
        private Button _questsButton;
        private Label _titleLabel;
        private GridContainer _grid;

        private InventoryCategory _currentCategory = InventoryCategory.Items;

        private readonly List<TextureRect> _slotIcons = new();
        private readonly List<Label> _slotLabels = new();
        private readonly List<Button> _slotButtons = new();

        public override void _Ready()
        {
            ApplyCharacterPanelSize();
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            SetupPanelFrame();
            BuildUI();
            FindInventoryManager();
            ShowCategory(InventoryCategory.Items);
        }

        public override void _ExitTree()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged -= RefreshGrid;
            }
        }

        private void ApplyCharacterPanelSize()
        {
            AnchorLeft = 0.1f;
            AnchorTop = 0.1f;
            AnchorRight = 0.9f;
            AnchorBottom = 0.9f;

            OffsetLeft = 0;
            OffsetTop = 0;
            OffsetRight = 0;
            OffsetBottom = 0;
        }

        private void SetupPanelFrame()
        {
            var frameTexture = GD.Load<Texture2D>("res://assets/sprites/button/khungPanel.png");
            if (frameTexture == null)
            {
                return;
            }

            _panelGlow = new NinePatchRect();
            _panelGlow.Texture = frameTexture;
            _panelGlow.SetAnchorsPreset(LayoutPreset.FullRect);
            _panelGlow.ZIndex = -1;
            _panelGlow.PatchMarginLeft = 40;
            _panelGlow.PatchMarginTop = 40;
            _panelGlow.PatchMarginRight = 40;
            _panelGlow.PatchMarginBottom = 40;
            _panelGlow.DrawCenter = false;
            _panelGlow.Modulate = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.8f);
            _panelGlow.Scale = new Vector2(1.005f, 1.005f);
            _panelGlow.Position = new Vector2(-2, -2);

            var glowShader = new Shader();
            glowShader.Code = @"
shader_type canvas_item;
render_mode blend_add, unshaded;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    COLOR = tex;
}
";
            var glowMaterial = new ShaderMaterial();
            glowMaterial.Shader = glowShader;
            _panelGlow.Material = glowMaterial;
            AddChild(_panelGlow);

            _panelFrame = new NinePatchRect();
            _panelFrame.Texture = frameTexture;
            _panelFrame.SetAnchorsPreset(LayoutPreset.FullRect);
            _panelFrame.ZIndex = 0;
            _panelFrame.PatchMarginLeft = 40;
            _panelFrame.PatchMarginTop = 40;
            _panelFrame.PatchMarginRight = 40;
            _panelFrame.PatchMarginBottom = 40;
            _panelFrame.DrawCenter = false;
            AddChild(_panelFrame);
        }

        private void BuildUI()
        {
            var root = new MarginContainer();
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            root.AddThemeConstantOverride("margin_left", 30);
            root.AddThemeConstantOverride("margin_top", 20);
            root.AddThemeConstantOverride("margin_right", 30);
            root.AddThemeConstantOverride("margin_bottom", 50);
            AddChild(root);

            var main = new HBoxContainer();
            main.AddThemeConstantOverride("separation", 18);
            main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            main.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(main);

            var leftMenu = new VBoxContainer();
            leftMenu.CustomMinimumSize = new Vector2(150, 0);
            leftMenu.AddThemeConstantOverride("separation", 8);
            leftMenu.SizeFlagsVertical = SizeFlags.ExpandFill;
            main.AddChild(leftMenu);

            var menuTitle = new Label();
            menuTitle.Text = "TÚI ĐỒ";
            menuTitle.HorizontalAlignment = HorizontalAlignment.Center;
            menuTitle.AddThemeFontSizeOverride("font_size", 18);
            menuTitle.AddThemeColorOverride("font_color", _accentColor);
            leftMenu.AddChild(menuTitle);

            _itemsButton = CreateCategoryButton("VẬT PHẨM", InventoryCategory.Items);
            _toolsButton = CreateCategoryButton("CÔNG CỤ", InventoryCategory.Tools);
            _questsButton = CreateCategoryButton("NHIỆM VỤ", InventoryCategory.Quests);

            leftMenu.AddChild(_itemsButton);
            leftMenu.AddChild(_toolsButton);
            leftMenu.AddChild(_questsButton);

            var contentPanel = new PanelContainer();
            contentPanel.SetAnchorsPreset(LayoutPreset.FullRect);
            contentPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            contentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            contentPanel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());
            main.AddChild(contentPanel);

            var contentMargin = new MarginContainer();
            contentMargin.AddThemeConstantOverride("margin_left", 15);
            contentMargin.AddThemeConstantOverride("margin_top", 10);
            contentMargin.AddThemeConstantOverride("margin_right", 15);
            contentMargin.AddThemeConstantOverride("margin_bottom", 10);
            contentPanel.AddChild(contentMargin);

            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 12);
            content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            content.SizeFlagsVertical = SizeFlags.ExpandFill;
            contentMargin.AddChild(content);

            _titleLabel = new Label();
            _titleLabel.Text = "KHO ĐỒ";
            _titleLabel.AddThemeFontSizeOverride("font_size", 36);
            _titleLabel.AddThemeColorOverride("font_color", Colors.White);
            content.AddChild(_titleLabel);

            var gridWrap = new CenterContainer();
            gridWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            gridWrap.SizeFlagsVertical = SizeFlags.ExpandFill;
            content.AddChild(gridWrap);

            _grid = new GridContainer();
            _grid.Columns = GridColumns;
            _grid.AddThemeConstantOverride("h_separation", 10);
            _grid.AddThemeConstantOverride("v_separation", 10);
            _grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _grid.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            gridWrap.AddChild(_grid);

            int slotCount = GetSlotCount();
            for (int i = 0; i < slotCount; i++)
            {
                CreateInventorySlot();
            }

            AddExitButton();
        }

        private Button CreateCategoryButton(string text, InventoryCategory category)
        {
            var button = new Button();
            button.Text = text;
            button.CustomMinimumSize = new Vector2(140, 38);
            button.FocusMode = FocusModeEnum.None;
            button.Pressed += () => ShowCategory(category);
            return button;
        }

        private void CreateInventorySlot()
        {
            var slot = new PanelContainer();
            slot.CustomMinimumSize = new Vector2(62, 62);

            var style = CreateSlotStyle();
            slot.AddThemeStyleboxOverride("panel", style);

            var center = new CenterContainer();
            slot.AddChild(center);

            var content = new VBoxContainer();
            content.Alignment = BoxContainer.AlignmentMode.Center;
            content.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            content.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            content.AddThemeConstantOverride("separation", 1);
            center.AddChild(content);

            var icon = new TextureRect();
            icon.CustomMinimumSize = new Vector2(32, 32);
            icon.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.Visible = false;
            content.AddChild(icon);

            var label = new Label();
            label.Text = "";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 0.95f, 0.85f));
            content.AddChild(label);

            var hoverButton = new Button();
            hoverButton.Text = "";
            hoverButton.SetAnchorsPreset(LayoutPreset.FullRect);
            hoverButton.FocusMode = FocusModeEnum.None;
            hoverButton.MouseDefaultCursorShape = CursorShape.PointingHand;
            hoverButton.AddThemeStyleboxOverride("normal", CreateTransparentButtonStyle());
            hoverButton.AddThemeStyleboxOverride("hover", CreateHoverButtonStyle());
            hoverButton.AddThemeStyleboxOverride("pressed", CreateHoverButtonStyle());
            slot.AddChild(hoverButton);

            _slotIcons.Add(icon);
            _slotLabels.Add(label);
            _slotButtons.Add(hoverButton);
            _grid.AddChild(slot);
        }

        private void AddExitButton()
        {
            var exitTexture = GD.Load<Texture2D>("res://assets/resources/data/icon/Exit.tres");

            var exitButton = new TextureButton();
            exitButton.TextureNormal = exitTexture;
            exitButton.CustomMinimumSize = new Vector2(50, 50);
            exitButton.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
            exitButton.IgnoreTextureSize = true;
            exitButton.SetAnchorsPreset(LayoutPreset.TopRight);
            exitButton.Position = new Vector2(-80, 30);
            exitButton.Pressed += Hide;

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(1f, 1f, 1f, 0.2f);
            hoverStyle.SetCornerRadiusAll(25);
            exitButton.AddThemeStyleboxOverride("hover", hoverStyle);

            AddChild(exitButton);
        }

        private void FindInventoryManager()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged -= RefreshGrid;
            }

            _inventoryManager = ResolveInventoryManager();

            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged += RefreshGrid;
            }
        }

        private InventoryManager ResolveInventoryManager()
        {
            if (InventoryManagerPath != null && !InventoryManagerPath.IsEmpty)
            {
                var exportedInventory = GetNodeOrNull<InventoryManager>(InventoryManagerPath);
                if (exportedInventory != null)
                {
                    return exportedInventory;
                }
            }

            var sceneManager = GetTree()?.Root?.GetNodeOrNull<SceneManager>("SceneManager");
            var playerFromSceneManager = sceneManager?.Player;
            if (playerFromSceneManager != null)
            {
                var inventoryFromPlayer = playerFromSceneManager.GetNodeOrNull<InventoryManager>("InventoryManager");
                if (inventoryFromPlayer != null)
                {
                    return inventoryFromPlayer;
                }
            }

            var playerNodes = GetTree()?.GetNodesInGroup("Player");
            if (playerNodes != null)
            {
                foreach (var node in playerNodes)
                {
                    if (node is Node playerNode)
                    {
                        var inventory = playerNode.GetNodeOrNull<InventoryManager>("InventoryManager");
                        if (inventory != null)
                        {
                            return inventory;
                        }
                    }
                }
            }

            return GetTree()?.Root?.GetNodeOrNull<InventoryManager>("InventoryManager");
        }

        private int GetSlotCount()
        {
            var inventory = ResolveInventoryManager();
            if (inventory == null || inventory.MaxSlots < 1)
            {
                return DefaultSlotCount;
            }

            return Mathf.Max(DefaultSlotCount, inventory.MaxSlots);
        }

        private void ShowCategory(InventoryCategory category)
        {
            _currentCategory = category;
            UpdateCategoryButtonStyle();
            RefreshGrid();
        }

        private void UpdateCategoryButtonStyle()
        {
            SetCategoryButtonSelected(_itemsButton, _currentCategory == InventoryCategory.Items);
            SetCategoryButtonSelected(_toolsButton, _currentCategory == InventoryCategory.Tools);
            SetCategoryButtonSelected(_questsButton, _currentCategory == InventoryCategory.Quests);
        }

        private void SetCategoryButtonSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            var normal = new StyleBoxFlat();
            normal.BgColor = selected ? new Color("#0f172a") : _btnNormalColor;
            normal.BorderColor = selected ? _accentColor : new Color("#64748b");
            normal.SetBorderWidthAll(selected ? 2 : 0);
            normal.SetCornerRadiusAll(5);
            normal.ContentMarginLeft = 15;
            normal.ContentMarginRight = 15;
            normal.ContentMarginTop = 8;
            normal.ContentMarginBottom = 8;

            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = _btnHoverColor;
            hover.BorderColor = _accentColor;
            hover.SetBorderWidthAll(2);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", hover);
            button.AddThemeColorOverride("font_color", selected ? _accentColor : _subTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", _accentColor);
        }

        private void RefreshGrid()
        {
            ClearSlots();

            switch (_currentCategory)
            {
                case InventoryCategory.Items:
                    FillItemSlots();
                    _titleLabel.Text = "KHO ĐỒ";
                    break;
                case InventoryCategory.Tools:
                    FillPlaceholderSlots("Công cụ");
                    _titleLabel.Text = "CÔNG CỤ";
                    break;
                case InventoryCategory.Quests:
                    FillPlaceholderSlots("Nhiệm vụ");
                    _titleLabel.Text = "NHIỆM VỤ";
                    break;
            }
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                _slotIcons[i].Texture = null;
                _slotIcons[i].Visible = false;
                _slotLabels[i].Text = "";
                _slotButtons[i].TooltipText = "";
                _slotButtons[i].Disabled = false;
            }
        }

        private void FillItemSlots()
        {
            if (_inventoryManager == null)
            {
                FindInventoryManager();
            }

            IReadOnlyList<EquipmentItemData> items = _inventoryManager?.Items;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count && i < _slotButtons.Count; i++)
            {
                EquipmentItemData item = items[i];
                if (item == null)
                {
                    continue;
                }

                _slotIcons[i].Texture = item.Icon;
                _slotIcons[i].Visible = item.Icon != null;
                _slotLabels[i].Text = item.Icon == null ? CompactItemName(item.ItemName) : "";
                _slotButtons[i].TooltipText = item.ItemName;
            }
        }

        private void FillPlaceholderSlots(string categoryName)
        {
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                _slotButtons[i].TooltipText = $"{categoryName} slot {i + 1}";
            }
        }

        private string CompactItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return "";
            }

            return itemName.Length <= 2 ? itemName.ToUpper() : itemName.Substring(0, 2).ToUpper();
        }

        private StyleBoxFlat GetCommonPanelStyle()
        {
            var style = new StyleBoxFlat();
            style.DrawCenter = true;
            style.BgColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.01f);
            style.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.9f);
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(8);
            style.ShadowColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.1f);
            style.ShadowSize = 8;
            style.ContentMarginLeft = 15;
            style.ContentMarginRight = 15;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            return style;
        }

        private StyleBoxFlat CreateSlotStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.85f);
            style.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.6f);
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(4);
            return style;
        }

        private StyleBoxFlat CreateTransparentButtonStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0);
            return style;
        }

        private StyleBoxFlat CreateHoverButtonStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(1f, 1f, 1f, 0.08f);
            style.SetCornerRadiusAll(4);
            return style;
        }
    }
}
