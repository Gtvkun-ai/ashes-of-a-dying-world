using Godot;
using System.Collections.Generic;
using System.Linq;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.Menus
{
    /// <summary>
    /// Kho đồ theo hướng "code-first": layout, chữ, trạng thái và khoảng cách được dựng bằng Control.
    /// Ảnh chỉ nên dùng cho icon vật phẩm hoặc lớp skin nhỏ sau này, không bake cả màn hình thành PNG.
    ///
    /// Mục tiêu thiết kế:
    /// - Giữ tinh thần UI cũ: nền nâu tối, điểm nhấn vàng, grid trái và chi tiết phải.
    /// - Giảm khung lồng khung, bỏ chữ hành động nổi trên ô đồ và không đổi selection khi chỉ hover.
    /// - Dùng typography/spacing thống nhất để UI vẫn sạch khi chưa có asset minh họa cuối cùng.
    /// </summary>
    public partial class InventoryPanel : Panel
    {
        private const int DefaultSlotCount = 40;
        private const int GridColumns = 8;

        // Kích thước chốt cho viewport 1600 x 900.
        // Cửa sổ đủ lớn để đọc, nhưng không còn phủ gần kín cả màn hình.
        private const float PanelWidth = 1220f;
        private const float PanelHeight = 680f;
        private const float SlotSize = 68f;
        private const float DetailPanelWidth = 350f;

        private enum InventoryCategory
        {
            All,
            Consumables,
            Materials,
            Equipment,
            Quest,
            Others
        }

        private enum InventorySortMode
        {
            Id,
            Name,
            Category
        }

        private sealed class InventoryEntry
        {
            public string Key { get; init; }
            public EquipmentItemData Item { get; init; }
            public int Count { get; set; }
            public int FirstIndex { get; init; }
        }

        [Export] public NodePath InventoryManagerPath { get; set; }

        // ---------------------------------------------------------------------
        // Design tokens: gom màu vào một chỗ để sau này chuyển sang Theme.tres dễ hơn.
        // ---------------------------------------------------------------------
        private readonly Color _windowColor = new("#15110d");
        private readonly Color _surfaceColor = new("#211a14");
        private readonly Color _raisedSurfaceColor = new("#2b221a");
        private readonly Color _deepSurfaceColor = new("#17130f");
        private readonly Color _slotSurfaceColor = new("#191510");
        private readonly Color _borderColor = new("#594631");
        private readonly Color _strongBorderColor = new("#85653f");
        private readonly Color _accentColor = new("#d8ad49");
        private readonly Color _mainTextColor = new("#f3ead9");
        private readonly Color _mutedTextColor = new("#b8aa91");
        private readonly Color _categoryColor = new("#7eb35d");
        private readonly Color _equipColor = new("#5e823f");
        private readonly Color _dropColor = new("#a6544c");
        private readonly Color _dangerColor = new("#6f332d");

        private InventoryManager _inventoryManager;
        private Player _player;

        private Button _allButton;
        private Button _consumablesButton;
        private Button _materialsButton;
        private Button _equipmentButton;
        private Button _questButton;
        private Button _othersButton;
        private Button _sortButton;
        private Button _primaryActionButton;
        private Button _dropButton;

        private Label _capacityLabel;
        private Label _detailNameLabel;
        private Label _detailCategoryLabel;
        private Label _detailSlotLabel;
        private Label _detailDamageValueLabel;
        private Label _detailValueValueLabel;
        private Label _detailDescriptionLabel;
        private TextureRect _detailIcon;
        private GridContainer _grid;

        private Texture2D _bagIcon;
        private Texture2D _coinIcon;
        private readonly Dictionary<InventoryCategory, Texture2D> _categoryIcons = new();

        private InventoryCategory _currentCategory = InventoryCategory.All;
        private InventorySortMode _sortMode = InventorySortMode.Id;
        private readonly List<InventoryEntry> _visibleEntries = new();
        private string _selectedEntryKey;

        private readonly List<PanelContainer> _slotFrames = new();
        private readonly List<TextureRect> _slotIcons = new();
        private readonly List<Label> _slotFallbackLabels = new();
        private readonly List<Label> _slotCounts = new();
        private readonly List<Button> _slotButtons = new();

        public override void _Ready()
        {
            ApplyPanelSize();
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            BuildUI();
            FindInventoryManager();
            ShowCategory(InventoryCategory.All);
        }

        public override void _ExitTree()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged -= RefreshInventoryView;
            }
        }

        /// <summary>
        /// Căn giữa theo viewport 1600 x 900 bằng kích thước tuyệt đối.
        /// Tránh dùng anchor 8%-92% vì nó khiến cửa sổ phình quá lớn và tạo nhiều khoảng trống chết.
        /// </summary>
        private void ApplyPanelSize()
        {
            AnchorLeft = 0.5f;
            AnchorTop = 0.5f;
            AnchorRight = 0.5f;
            AnchorBottom = 0.5f;

            OffsetLeft = -PanelWidth * 0.5f;
            OffsetTop = -PanelHeight * 0.5f;
            OffsetRight = PanelWidth * 0.5f;
            OffsetBottom = PanelHeight * 0.5f;

            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
            GrowHorizontal = GrowDirection.Both;
            GrowVertical = GrowDirection.Both;
        }

        private void BuildUI()
        {
            var window = new PanelContainer();
            window.SetAnchorsPreset(LayoutPreset.FullRect);
            window.AddThemeStyleboxOverride("panel", CreateWindowStyle());
            AddChild(window);

            var outerMargin = new MarginContainer();
            outerMargin.AddThemeConstantOverride("margin_left", 14);
            outerMargin.AddThemeConstantOverride("margin_top", 12);
            outerMargin.AddThemeConstantOverride("margin_right", 14);
            outerMargin.AddThemeConstantOverride("margin_bottom", 12);
            window.AddChild(outerMargin);

            var root = new VBoxContainer();
            root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            root.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 6);
            outerMargin.AddChild(root);

            root.AddChild(BuildHeader());
            root.AddChild(BuildCategoryTabs());
            root.AddChild(BuildBody());
        }

        private Control BuildHeader()
        {
            var headerPanel = new PanelContainer();
            headerPanel.CustomMinimumSize = new Vector2(0, 52);
            headerPanel.AddThemeStyleboxOverride("panel", CreateHeaderStyle());

            var headerMargin = new MarginContainer();
            headerMargin.AddThemeConstantOverride("margin_left", 14);
            headerMargin.AddThemeConstantOverride("margin_top", 7);
            headerMargin.AddThemeConstantOverride("margin_right", 8);
            headerMargin.AddThemeConstantOverride("margin_bottom", 7);
            headerPanel.AddChild(headerMargin);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 10);
            headerMargin.AddChild(header);

            // Icon nhỏ, không thêm badge lồng quanh icon để tránh cảm giác "box trong box".
            var bagIcon = new TextureRect();
            bagIcon.Texture = _bagIcon ??= CreateBagIcon();
            bagIcon.CustomMinimumSize = new Vector2(28, 28);
            bagIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            bagIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            header.AddChild(bagIcon);

            var title = CreateLabel("INVENTORY", 22, _mainTextColor);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            title.VerticalAlignment = VerticalAlignment.Center;
            header.AddChild(title);

            var coinRow = new HBoxContainer();
            coinRow.AddThemeConstantOverride("separation", 6);
            coinRow.Alignment = BoxContainer.AlignmentMode.Center;
            header.AddChild(coinRow);

            var coinIcon = new TextureRect();
            coinIcon.Texture = _coinIcon ??= CreateCoinIcon();
            coinIcon.CustomMinimumSize = new Vector2(18, 18);
            coinIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            coinIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            coinRow.AddChild(coinIcon);

            var coinLabel = CreateLabel("12,345", 17, new Color("#f1c85a"));
            coinLabel.VerticalAlignment = VerticalAlignment.Center;
            coinRow.AddChild(coinLabel);

            header.AddChild(CreateCloseButton());
            return headerPanel;
        }

        private Control BuildCategoryTabs()
        {
            var tabsPanel = new PanelContainer();
            tabsPanel.CustomMinimumSize = new Vector2(0, 44);
            tabsPanel.AddThemeStyleboxOverride("panel", CreateTabsBarStyle());

            var tabsMargin = new MarginContainer();
            tabsMargin.AddThemeConstantOverride("margin_left", 6);
            tabsMargin.AddThemeConstantOverride("margin_top", 4);
            tabsMargin.AddThemeConstantOverride("margin_right", 6);
            tabsMargin.AddThemeConstantOverride("margin_bottom", 4);
            tabsPanel.AddChild(tabsMargin);

            // Tab dùng độ rộng theo nội dung thay vì kéo giãn toàn hàng như navbar web.
            var tabs = new HBoxContainer();
            tabs.AddThemeConstantOverride("separation", 4);
            tabs.Alignment = BoxContainer.AlignmentMode.Begin;
            tabsMargin.AddChild(tabs);

            _allButton = CreateCategoryButton("All", InventoryCategory.All);
            _consumablesButton = CreateCategoryButton("Consumables", InventoryCategory.Consumables);
            _materialsButton = CreateCategoryButton("Materials", InventoryCategory.Materials);
            _equipmentButton = CreateCategoryButton("Equipment", InventoryCategory.Equipment);
            _questButton = CreateCategoryButton("Quest", InventoryCategory.Quest);
            _othersButton = CreateCategoryButton("More", InventoryCategory.Others);

            tabs.AddChild(_allButton);
            tabs.AddChild(_consumablesButton);
            tabs.AddChild(_materialsButton);
            tabs.AddChild(_equipmentButton);
            tabs.AddChild(_questButton);
            tabs.AddChild(_othersButton);

            return tabsPanel;
        }

        private Control BuildBody()
        {
            var body = new HBoxContainer();
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddThemeConstantOverride("separation", 8);

            var inventoryColumn = BuildInventoryColumn();
            inventoryColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inventoryColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddChild(inventoryColumn);

            var detailPanel = BuildDetailPanel();
            detailPanel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddChild(detailPanel);

            return body;
        }

        private PanelContainer BuildInventoryColumn()
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", CreateSectionStyle());

            var column = new VBoxContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 0);
            panel.AddChild(column);

            // Grid bám góc trên-trái. Bản trước dùng CenterContainer nên toàn bộ 40 ô
            // trôi giữa một vùng trống lớn, nhìn giống bảng demo hơn là kho đồ thật.
            var gridMargin = new MarginContainer();
            gridMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            gridMargin.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            gridMargin.AddThemeConstantOverride("margin_left", 20);
            gridMargin.AddThemeConstantOverride("margin_top", 18);
            gridMargin.AddThemeConstantOverride("margin_right", 20);
            gridMargin.AddThemeConstantOverride("margin_bottom", 14);
            column.AddChild(gridMargin);

            var gridRow = new HBoxContainer();
            gridRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            gridRow.Alignment = BoxContainer.AlignmentMode.Begin;
            gridMargin.AddChild(gridRow);

            _grid = new GridContainer();
            _grid.Columns = GridColumns;
            _grid.AddThemeConstantOverride("h_separation", 8);
            _grid.AddThemeConstantOverride("v_separation", 8);
            _grid.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            _grid.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            gridRow.AddChild(_grid);

            int slotCount = GetSlotCount();
            for (int i = 0; i < slotCount; i++)
            {
                CreateInventorySlot(i);
            }

            // Spacer chỉ chiếm phần còn dư phía dưới, không chen khoảng trống lên trên grid.
            var lowerSpacer = new Control();
            lowerSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(lowerSpacer);

            column.AddChild(CreateDivider());
            column.AddChild(BuildFooter());
            return panel;
        }

        private Control BuildFooter()
        {
            var footerMargin = new MarginContainer();
            footerMargin.CustomMinimumSize = new Vector2(0, 52);
            footerMargin.AddThemeConstantOverride("margin_left", 16);
            footerMargin.AddThemeConstantOverride("margin_top", 8);
            footerMargin.AddThemeConstantOverride("margin_right", 16);
            footerMargin.AddThemeConstantOverride("margin_bottom", 8);

            var footer = new HBoxContainer();
            footer.AddThemeConstantOverride("separation", 12);
            footerMargin.AddChild(footer);

            _capacityLabel = CreateLabel("0 / 40", 14, _mainTextColor);
            _capacityLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _capacityLabel.VerticalAlignment = VerticalAlignment.Center;
            footer.AddChild(_capacityLabel);

            _sortButton = new Button();
            _sortButton.Text = "Sort: ID";
            _sortButton.CustomMinimumSize = new Vector2(138, 36);
            _sortButton.FocusMode = FocusModeEnum.None;
            _sortButton.MouseDefaultCursorShape = CursorShape.PointingHand;
            _sortButton.Pressed += CycleSortMode;
            ApplySecondaryButtonStyle(_sortButton);
            footer.AddChild(_sortButton);

            return footerMargin;
        }

        private PanelContainer BuildDetailPanel()
        {
            var detailPanel = new PanelContainer();
            detailPanel.CustomMinimumSize = new Vector2(DetailPanelWidth, 0);
            detailPanel.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());

            var detailMargin = new MarginContainer();
            detailMargin.AddThemeConstantOverride("margin_left", 16);
            detailMargin.AddThemeConstantOverride("margin_top", 16);
            detailMargin.AddThemeConstantOverride("margin_right", 16);
            detailMargin.AddThemeConstantOverride("margin_bottom", 14);
            detailPanel.AddChild(detailMargin);

            var detail = new VBoxContainer();
            detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detail.SizeFlagsVertical = SizeFlags.ExpandFill;
            detail.AddThemeConstantOverride("separation", 9);
            detailMargin.AddChild(detail);

            var previewFrame = new PanelContainer();
            previewFrame.CustomMinimumSize = new Vector2(0, 150);
            previewFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
            detail.AddChild(previewFrame);

            var previewCenter = new CenterContainer();
            previewFrame.AddChild(previewCenter);

            _detailIcon = new TextureRect();
            _detailIcon.CustomMinimumSize = new Vector2(108, 108);
            _detailIcon.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
            _detailIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            previewCenter.AddChild(_detailIcon);

            _detailNameLabel = CreateLabel("Select Item", 21, _mainTextColor);
            _detailNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            detail.AddChild(_detailNameLabel);

            var metaRow = new HBoxContainer();
            metaRow.CustomMinimumSize = new Vector2(0, 24);
            metaRow.AddThemeConstantOverride("separation", 6);
            detail.AddChild(metaRow);

            _detailCategoryLabel = CreateLabel("Category", 14, _categoryColor);
            _detailCategoryLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            _detailCategoryLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            metaRow.AddChild(_detailCategoryLabel);

            var metaDot = CreateLabel("·", 14, _mutedTextColor);
            metaDot.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            metaRow.AddChild(metaDot);

            // Không cho "Main hand" wrap từng ký tự. Đây là bug layout rõ nhất của bản trước.
            _detailSlotLabel = CreateLabel("Slot", 14, _mutedTextColor);
            _detailSlotLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _detailSlotLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            _detailSlotLabel.ClipText = true;
            metaRow.AddChild(_detailSlotLabel);

            detail.AddChild(CreateDivider());

            var damageRow = CreateStatRow("Damage", out _detailDamageValueLabel);
            detail.AddChild(damageRow);
            detail.AddChild(CreateThinDivider());

            var valueRow = CreateStatRow("Value", out _detailValueValueLabel);
            detail.AddChild(valueRow);
            detail.AddChild(CreateThinDivider());

            _detailDescriptionLabel = CreateLabel("Choose an item to view its details.", 14, _mutedTextColor);
            _detailDescriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _detailDescriptionLabel.VerticalAlignment = VerticalAlignment.Top;
            _detailDescriptionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
            detail.AddChild(_detailDescriptionLabel);

            // Nút hành động luôn nằm sát đáy panel.
            var actionRow = new HBoxContainer();
            actionRow.CustomMinimumSize = new Vector2(0, 46);
            actionRow.AddThemeConstantOverride("separation", 10);
            detail.AddChild(actionRow);

            _primaryActionButton = CreateActionButton("Equip", _equipColor);
            _primaryActionButton.Pressed += OnPrimaryActionPressed;
            actionRow.AddChild(_primaryActionButton);

            _dropButton = CreateDangerActionButton("Drop");
            _dropButton.Pressed += OnDropPressed;
            actionRow.AddChild(_dropButton);

            return detailPanel;
        }

        private Control CreateStatRow(string name, out Label valueLabel)
        {
            var row = new HBoxContainer();
            row.CustomMinimumSize = new Vector2(0, 34);

            var nameLabel = CreateLabel(name, 14, _mainTextColor);
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(nameLabel);

            valueLabel = CreateLabel("-", 14, _mainTextColor);
            valueLabel.CustomMinimumSize = new Vector2(54, 0);
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            valueLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(valueLabel);

            return row;
        }

        private Button CreateCategoryButton(string text, InventoryCategory category)
        {
            var button = new Button();
            button.Text = text;
            button.Icon = GetCategoryIcon(category);
            button.ExpandIcon = false;
            button.IconAlignment = HorizontalAlignment.Left;

            // Không ExpandFill: mỗi tab chỉ rộng theo nội dung, tránh cảm giác thanh navbar web.
            button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            button.CustomMinimumSize = new Vector2(GetCategoryButtonWidth(category), 36);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.Pressed += () => ShowCategory(category);
            return button;
        }

        private float GetCategoryButtonWidth(InventoryCategory category)
        {
            return category switch
            {
                InventoryCategory.All => 84f,
                InventoryCategory.Consumables => 146f,
                InventoryCategory.Materials => 116f,
                InventoryCategory.Equipment => 126f,
                InventoryCategory.Quest => 92f,
                _ => 94f
            };
        }

        private Button CreateCloseButton()
        {
            var button = new Button();
            button.Text = "X";
            button.CustomMinimumSize = new Vector2(38, 36);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.Pressed += Hide;

            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(_dangerColor, _strongBorderColor, 2));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(_dangerColor.Lightened(0.08f), _accentColor, 2));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(_dangerColor.Darkened(0.08f), _strongBorderColor, 2));
            button.AddThemeColorOverride("font_color", _mainTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 16);
            return button;
        }

        private void CreateInventorySlot(int slotIndex)
        {
            var slot = new PanelContainer();
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.AddThemeStyleboxOverride("panel", CreateSlotStyle(false));

            var inner = new Control();
            inner.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.AddChild(inner);

            var icon = new TextureRect();
            icon.Position = new Vector2(7, 7);
            icon.Size = new Vector2(54, 54);
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.Visible = false;
            inner.AddChild(icon);

            // Fallback chỉ xuất hiện khi item chưa có icon.
            var fallbackLabel = CreateLabel(string.Empty, 12, _mainTextColor);
            fallbackLabel.Position = new Vector2(6, 23);
            fallbackLabel.Size = new Vector2(56, 22);
            fallbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
            fallbackLabel.VerticalAlignment = VerticalAlignment.Center;
            inner.AddChild(fallbackLabel);

            var countLabel = CreateLabel(string.Empty, 12, Colors.White);
            countLabel.Position = new Vector2(34, 50);
            countLabel.Size = new Vector2(27, 15);
            countLabel.HorizontalAlignment = HorizontalAlignment.Right;
            countLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
            countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            inner.AddChild(countLabel);

            var clickArea = new Button();
            clickArea.Text = string.Empty;
            clickArea.SetAnchorsPreset(LayoutPreset.FullRect);
            clickArea.FocusMode = FocusModeEnum.None;
            clickArea.MouseDefaultCursorShape = CursorShape.PointingHand;
            clickArea.AddThemeStyleboxOverride("normal", CreateTransparentButtonStyle());
            clickArea.AddThemeStyleboxOverride("hover", CreateSlotHoverStyle());
            clickArea.AddThemeStyleboxOverride("pressed", CreateSlotPressedStyle());
            clickArea.Pressed += () => OnSlotPressed(slotIndex);
            slot.AddChild(clickArea);

            _slotFrames.Add(slot);
            _slotIcons.Add(icon);
            _slotFallbackLabels.Add(fallbackLabel);
            _slotCounts.Add(countLabel);
            _slotButtons.Add(clickArea);
            _grid.AddChild(slot);
        }

        // ---------------------------------------------------------------------
        // Data binding và hành vi inventory
        // ---------------------------------------------------------------------

        private void FindInventoryManager()
        {
            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged -= RefreshInventoryView;
            }

            _inventoryManager = ResolveInventoryManager();
            _player = ResolvePlayer();

            if (_inventoryManager != null)
            {
                _inventoryManager.InventoryChanged += RefreshInventoryView;
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

        private Player ResolvePlayer()
        {
            var sceneManager = GetTree()?.Root?.GetNodeOrNull<SceneManager>("SceneManager");
            if (sceneManager?.Player != null)
            {
                return sceneManager.Player;
            }

            var playerNodes = GetTree()?.GetNodesInGroup("Player");
            if (playerNodes != null)
            {
                foreach (var node in playerNodes)
                {
                    if (node is Player player)
                    {
                        return player;
                    }
                }
            }

            return null;
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
            UpdateCategoryButtonStyles();
            RefreshInventoryView();
        }

        private void UpdateCategoryButtonStyles()
        {
            SetCategoryButtonSelected(_allButton, _currentCategory == InventoryCategory.All);
            SetCategoryButtonSelected(_consumablesButton, _currentCategory == InventoryCategory.Consumables);
            SetCategoryButtonSelected(_materialsButton, _currentCategory == InventoryCategory.Materials);
            SetCategoryButtonSelected(_equipmentButton, _currentCategory == InventoryCategory.Equipment);
            SetCategoryButtonSelected(_questButton, _currentCategory == InventoryCategory.Quest);
            SetCategoryButtonSelected(_othersButton, _currentCategory == InventoryCategory.Others);
        }

        private void SetCategoryButtonSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            // Active tab dùng nền ấm nhẹ + gạch chân vàng, không đóng nguyên khung như nút form.
            button.AddThemeStyleboxOverride("normal", CreateTabStyle(
                selected ? _raisedSurfaceColor : new Color(0f, 0f, 0f, 0f),
                selected ? _accentColor : new Color(0f, 0f, 0f, 0f),
                selected ? 2 : 0));
            button.AddThemeStyleboxOverride("hover", CreateTabStyle(
                _raisedSurfaceColor.Lightened(0.03f),
                _strongBorderColor,
                1));
            button.AddThemeStyleboxOverride("pressed", CreateTabStyle(
                _deepSurfaceColor,
                _accentColor,
                2));
            button.AddThemeColorOverride("font_color", selected ? _accentColor : _mutedTextColor);
            button.AddThemeColorOverride("font_hover_color", _mainTextColor);
            button.AddThemeColorOverride("font_pressed_color", _accentColor);
            button.AddThemeFontSizeOverride("font_size", 14);
        }

        private void RefreshInventoryView()
        {
            ClearSlots();
            BuildVisibleEntries();
            EnsureValidSelection();
            FillItemSlots();
            RefreshDetailPanel();

            if (_capacityLabel != null)
            {
                int usedSlots = _inventoryManager?.Items?.Count ?? 0;
                int maxSlots = Mathf.Max(DefaultSlotCount, _inventoryManager?.MaxSlots ?? DefaultSlotCount);
                _capacityLabel.Text = $"{usedSlots} / {maxSlots}";
            }
        }

        private void BuildVisibleEntries()
        {
            if (_inventoryManager == null)
            {
                FindInventoryManager();
            }

            _visibleEntries.Clear();
            IReadOnlyList<EquipmentItemData> items = _inventoryManager?.Items;
            if (items == null)
            {
                return;
            }

            var grouped = new Dictionary<string, InventoryEntry>();
            for (int i = 0; i < items.Count; i++)
            {
                EquipmentItemData item = items[i];
                if (item == null || !MatchesCurrentCategory(item))
                {
                    continue;
                }

                // Item thiếu ID vẫn chọn được nhờ key theo index, tránh selection bị null.
                string key = string.IsNullOrWhiteSpace(item.ID) ? $"__index_{i}" : item.ID;
                if (grouped.TryGetValue(key, out InventoryEntry existing))
                {
                    existing.Count++;
                    continue;
                }

                grouped[key] = new InventoryEntry
                {
                    Key = key,
                    Item = item,
                    Count = 1,
                    FirstIndex = i
                };
            }

            IEnumerable<InventoryEntry> sortedEntries = _sortMode switch
            {
                InventorySortMode.Name => grouped.Values
                    .OrderBy(entry => entry.Item?.ItemName ?? string.Empty)
                    .ThenBy(entry => entry.FirstIndex),
                InventorySortMode.Category => grouped.Values
                    .OrderBy(entry => entry.Item?.InventoryCategory)
                    .ThenBy(entry => entry.Item?.ItemName ?? string.Empty),
                _ => grouped.Values
                    .OrderBy(entry => entry.Item?.ID ?? string.Empty)
                    .ThenBy(entry => entry.FirstIndex)
            };

            _visibleEntries.AddRange(sortedEntries);
        }

        private bool MatchesCurrentCategory(EquipmentItemData item)
        {
            if (item == null)
            {
                return false;
            }

            return _currentCategory switch
            {
                InventoryCategory.All => true,
                InventoryCategory.Consumables => item.InventoryCategory == InventoryItemCategory.Consumable,
                InventoryCategory.Materials => item.InventoryCategory == InventoryItemCategory.Material,
                InventoryCategory.Equipment => item.InventoryCategory == InventoryItemCategory.Equipment,
                InventoryCategory.Quest => item.InventoryCategory == InventoryItemCategory.Quest,
                InventoryCategory.Others => item.InventoryCategory == InventoryItemCategory.Other,
                _ => true
            };
        }

        private void EnsureValidSelection()
        {
            if (_visibleEntries.Count == 0)
            {
                _selectedEntryKey = null;
                return;
            }

            if (string.IsNullOrEmpty(_selectedEntryKey) || !_visibleEntries.Any(entry => entry.Key == _selectedEntryKey))
            {
                _selectedEntryKey = _visibleEntries[0].Key;
            }
        }

        private void FillItemSlots()
        {
            for (int i = 0; i < _visibleEntries.Count && i < _slotButtons.Count; i++)
            {
                InventoryEntry entry = _visibleEntries[i];
                EquipmentItemData item = entry.Item;
                if (item == null)
                {
                    continue;
                }

                _slotIcons[i].Texture = item.Icon;
                _slotIcons[i].Visible = item.Icon != null;
                _slotFallbackLabels[i].Text = item.Icon == null ? CompactItemName(item.ItemName) : string.Empty;
                _slotCounts[i].Text = entry.Count > 1 ? entry.Count.ToString() : string.Empty;
                _slotButtons[i].TooltipText = $"{item.ItemName} ×{entry.Count}";
                _slotFrames[i].AddThemeStyleboxOverride("panel", CreateSlotStyle(entry.Key == _selectedEntryKey));
            }
        }

        private void RefreshDetailPanel()
        {
            InventoryEntry entry = GetSelectedEntry();
            if (entry?.Item == null)
            {
                _detailIcon.Texture = null;
                _detailNameLabel.Text = "Select Item";
                _detailCategoryLabel.Text = "Category";
                _detailSlotLabel.Text = "Slot";
                _detailDamageValueLabel.Text = "-";
                _detailValueValueLabel.Text = "-";
                _detailDescriptionLabel.Text = "Choose an item to view its details.";
                _primaryActionButton.Text = "Use";
                _primaryActionButton.Disabled = true;
                _dropButton.Disabled = true;
                return;
            }

            EquipmentItemData item = entry.Item;
            _detailIcon.Texture = item.Icon;
            _detailNameLabel.Text = string.IsNullOrWhiteSpace(item.ItemName) ? "Unnamed Item" : item.ItemName;
            _detailCategoryLabel.Text = GetCategoryDisplayName(item.InventoryCategory);
            _detailSlotLabel.Text = item.InventoryCategory == InventoryItemCategory.Equipment
                ? GetSlotDisplayName(item.SlotType)
                : $"Owned: {entry.Count}";
            _detailDamageValueLabel.Text = item.InventoryCategory == InventoryItemCategory.Equipment
                ? FormatNumber(item.BaseValue)
                : "-";
            _detailValueValueLabel.Text = FormatNumber(item.BaseValue);
            _detailDescriptionLabel.Text = GetDescription(item);

            bool isEquipment = item.InventoryCategory == InventoryItemCategory.Equipment;
            _primaryActionButton.Text = isEquipment ? "Equip" : "Use";

            // Hiện tại project mới có logic EquipFromInventory. Consumable để disabled thay vì giả vờ bấm được.
            _primaryActionButton.Disabled = !isEquipment;

            // Quest item thường không được thả. Sau này có thể thay bằng field CanDrop trong ItemData.
            _dropButton.Disabled = item.InventoryCategory == InventoryItemCategory.Quest;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                _slotIcons[i].Texture = null;
                _slotIcons[i].Visible = false;
                _slotFallbackLabels[i].Text = string.Empty;
                _slotCounts[i].Text = string.Empty;
                _slotButtons[i].TooltipText = string.Empty;
                _slotFrames[i].AddThemeStyleboxOverride("panel", CreateSlotStyle(false));
            }
        }

        private void OnSlotPressed(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _visibleEntries.Count)
            {
                return;
            }

            _selectedEntryKey = _visibleEntries[slotIndex].Key;
            RefreshInventoryView();
        }

        private void CycleSortMode()
        {
            _sortMode = _sortMode switch
            {
                InventorySortMode.Id => InventorySortMode.Name,
                InventorySortMode.Name => InventorySortMode.Category,
                _ => InventorySortMode.Id
            };

            _sortButton.Text = _sortMode switch
            {
                InventorySortMode.Name => "Sort: Name",
                InventorySortMode.Category => "Sort: Type",
                _ => "Sort: ID"
            };

            RefreshInventoryView();
        }

        private void OnPrimaryActionPressed()
        {
            InventoryEntry entry = GetSelectedEntry();
            if (entry?.Item == null)
            {
                return;
            }

            if (_player == null)
            {
                _player = ResolvePlayer();
            }

            if (entry.Item.InventoryCategory == InventoryItemCategory.Equipment && _player != null)
            {
                _player.EquipFromInventory(entry.Item.ID);
                RefreshInventoryView();
            }
        }

        private void OnDropPressed()
        {
            InventoryEntry entry = GetSelectedEntry();
            if (entry?.Item == null || _inventoryManager == null)
            {
                return;
            }

            if (entry.Item.InventoryCategory == InventoryItemCategory.Quest)
            {
                return;
            }

            _inventoryManager.RemoveItem(entry.Item);
            RefreshInventoryView();
        }

        private InventoryEntry GetSelectedEntry()
        {
            return _visibleEntries.FirstOrDefault(entry => entry.Key == _selectedEntryKey);
        }

        private string CompactItemName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return "?";
            }

            string[] words = itemName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            }

            return itemName.Length <= 2
                ? itemName.ToUpperInvariant()
                : itemName.Substring(0, 2).ToUpperInvariant();
        }

        private string GetDescription(EquipmentItemData item)
        {
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                return item.Description.Trim();
            }

            return item.InventoryCategory switch
            {
                InventoryItemCategory.Consumable => "A consumable item. Its effect will appear here when the use system is connected.",
                InventoryItemCategory.Material => "A crafting material collected during the journey.",
                InventoryItemCategory.Equipment => "A basic piece of equipment.",
                InventoryItemCategory.Quest => "A quest item. It cannot be dropped.",
                _ => "An item carried in your inventory."
            };
        }

        private string GetCategoryDisplayName(InventoryItemCategory category)
        {
            return category switch
            {
                InventoryItemCategory.Consumable => "Consumable",
                InventoryItemCategory.Material => "Material",
                InventoryItemCategory.Equipment => "Equipment",
                InventoryItemCategory.Quest => "Quest",
                _ => "Other"
            };
        }

        private string GetSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => "Main hand",
                EquipmentSlot.OffHand => "Off hand",
                EquipmentSlot.Accessory1 => "Accessory",
                EquipmentSlot.Accessory2 => "Accessory",
                _ => slot.ToString()
            };
        }

        private string FormatNumber(float value)
        {
            return value.ToString("0.##");
        }

        // ---------------------------------------------------------------------
        // Factory UI và StyleBox
        // ---------------------------------------------------------------------

        private Label CreateLabel(string text, int fontSize, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        private Button CreateActionButton(string text, Color color)
        {
            var button = new Button();
            button.Text = text;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 44);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(color, color.Lightened(0.12f), 1));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(color.Lightened(0.08f), color.Lightened(0.24f), 1));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(color.Darkened(0.08f), color, 1));
            button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(color.Darkened(0.38f), _borderColor, 1));
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.38f));
            button.AddThemeFontSizeOverride("font_size", 15);
            return button;
        }

        /// <summary>
        /// Drop là hành động nguy hiểm nhưng không cần đỏ rực khi đứng yên.
        /// Bình thường chỉ dùng viền đỏ; hover mới tô nền để giảm cảm giác "hai nút app nội bộ".
        /// </summary>
        private Button CreateDangerActionButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 44);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;

            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(_raisedSurfaceColor, _dropColor.Darkened(0.08f), 1));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(_dropColor, _dropColor.Lightened(0.18f), 1));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(_dropColor.Darkened(0.1f), _dropColor, 1));
            button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(_deepSurfaceColor, _borderColor, 1));
            button.AddThemeColorOverride("font_color", new Color("#efc0bb"));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.32f));
            button.AddThemeFontSizeOverride("font_size", 15);
            return button;
        }

        private void ApplySecondaryButtonStyle(Button button)
        {
            button.AddThemeStyleboxOverride("normal", CreateButtonStyle(_raisedSurfaceColor, _borderColor, 1));
            button.AddThemeStyleboxOverride("hover", CreateButtonStyle(_raisedSurfaceColor.Lightened(0.06f), _strongBorderColor, 1));
            button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(_deepSurfaceColor, _accentColor, 1));
            button.AddThemeColorOverride("font_color", _mainTextColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeFontSizeOverride("font_size", 14);
        }

        private ColorRect CreateDivider()
        {
            var divider = new ColorRect();
            divider.Color = _borderColor;
            divider.CustomMinimumSize = new Vector2(0, 1);
            divider.MouseFilter = MouseFilterEnum.Ignore;
            return divider;
        }

        private ColorRect CreateThinDivider()
        {
            var divider = new ColorRect();
            divider.Color = new Color(_borderColor.R, _borderColor.G, _borderColor.B, 0.72f);
            divider.CustomMinimumSize = new Vector2(0, 1);
            divider.MouseFilter = MouseFilterEnum.Ignore;
            return divider;
        }

        private StyleBoxFlat CreateWindowStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _windowColor;
            style.BorderColor = _strongBorderColor;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(4);
            style.ShadowColor = new Color(0f, 0f, 0f, 0.46f);
            style.ShadowSize = 7;
            return style;
        }

        private StyleBoxFlat CreateHeaderStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _deepSurfaceColor;
            style.BorderColor = _borderColor;
            style.BorderWidthBottom = 1;
            style.SetCornerRadiusAll(2);
            return style;
        }

        private StyleBoxFlat CreateTabsBarStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _surfaceColor.Darkened(0.035f);
            style.BorderColor = _borderColor;
            style.BorderWidthBottom = 1;
            style.SetCornerRadiusAll(2);
            return style;
        }

        private StyleBoxFlat CreateSectionStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _surfaceColor;
            style.BorderColor = _borderColor;
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        private StyleBoxFlat CreateDetailSectionStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _raisedSurfaceColor.Darkened(0.055f);
            style.BorderColor = _strongBorderColor.Darkened(0.1f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        private StyleBoxFlat CreatePreviewStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _deepSurfaceColor;
            style.BorderColor = _strongBorderColor.Darkened(0.18f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        private StyleBoxFlat CreateSlotStyle(bool selected)
        {
            var style = new StyleBoxFlat();
            style.BgColor = selected ? _raisedSurfaceColor.Lightened(0.035f) : _slotSurfaceColor;
            style.BorderColor = selected ? _accentColor : _borderColor.Darkened(0.08f);
            style.SetBorderWidthAll(selected ? 2 : 1);
            style.SetCornerRadiusAll(3);

            // Một chút "độ nổi" bằng content margin, không cần texture hay bevel giả.
            style.ContentMarginLeft = 2;
            style.ContentMarginTop = 2;
            style.ContentMarginRight = 2;
            style.ContentMarginBottom = 2;
            return style;
        }

        private StyleBoxFlat CreateTabStyle(Color background, Color bottomBorder, int bottomBorderWidth)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = bottomBorder;
            style.BorderWidthBottom = bottomBorderWidth;
            style.SetCornerRadiusAll(2);
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            return style;
        }

        private StyleBoxFlat CreateButtonStyle(Color background, Color border, int borderWidth)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = border;
            style.SetBorderWidthAll(borderWidth);
            style.SetCornerRadiusAll(3);
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            return style;
        }

        private StyleBoxFlat CreateTransparentButtonStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0f, 0f, 0f, 0f);
            return style;
        }

        private StyleBoxFlat CreateSlotHoverStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(_raisedSurfaceColor.R, _raisedSurfaceColor.G, _raisedSurfaceColor.B, 0.72f);
            style.BorderColor = _strongBorderColor;
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(3);
            return style;
        }

        private StyleBoxFlat CreateSlotPressedStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = _deepSurfaceColor;
            style.BorderColor = _accentColor;
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(3);
            return style;
        }

        // ---------------------------------------------------------------------
        // Icon procedural tối giản. Đây là placeholder code-first, có thể thay bằng PNG 16x16 sau.
        // ---------------------------------------------------------------------

        private Texture2D GetCategoryIcon(InventoryCategory category)
        {
            if (_categoryIcons.TryGetValue(category, out Texture2D icon))
            {
                return icon;
            }

            icon = category switch
            {
                InventoryCategory.All => CreateAllIcon(),
                InventoryCategory.Consumables => CreateConsumablesIcon(),
                InventoryCategory.Materials => CreateMaterialsIcon(),
                InventoryCategory.Equipment => CreateEquipmentIcon(),
                InventoryCategory.Quest => CreateQuestIcon(),
                _ => CreateOthersIcon()
            };

            _categoryIcons[category] = icon;
            return icon;
        }

        private Texture2D CreateAllIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillRect(image, 3, 3, 4, 4, _accentColor);
                FillRect(image, 9, 3, 4, 4, _accentColor);
                FillRect(image, 3, 9, 4, 4, _accentColor);
                FillRect(image, 9, 9, 4, 4, _accentColor);
            });
        }

        private Texture2D CreateConsumablesIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillRect(image, 6, 2, 4, 3, new Color("#d7cbb7"));
                FillRect(image, 5, 5, 6, 2, new Color("#704331"));
                FillRect(image, 4, 7, 8, 7, new Color("#a83f43"));
                FillRect(image, 6, 8, 3, 3, new Color("#e77872"));
            });
        }

        private Texture2D CreateMaterialsIcon()
        {
            return CreatePixelIcon(image =>
            {
                DrawLine(image, 4, 13, 12, 5, new Color("#3d7735"));
                FillCircle(image, 9, 8, 5, new Color("#5c9a48"));
                FillCircle(image, 11, 6, 3, new Color("#78b65b"));
            });
        }

        private Texture2D CreateEquipmentIcon()
        {
            return CreatePixelIcon(image =>
            {
                DrawLine(image, 4, 13, 12, 5, new Color("#c8c9c4"));
                DrawLine(image, 5, 13, 13, 5, new Color("#8c918e"));
                FillRect(image, 3, 12, 5, 2, new Color("#8a5632"));
                FillRect(image, 5, 10, 2, 6, new Color("#bc8246"));
            });
        }

        private Texture2D CreateQuestIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillRect(image, 4, 3, 9, 11, new Color("#c39a61"));
                FillRect(image, 5, 5, 7, 7, new Color("#e0c28e"));
                DrawLine(image, 6, 8, 10, 8, new Color("#72543a"));
                DrawLine(image, 6, 10, 9, 10, new Color("#72543a"));
            });
        }

        private Texture2D CreateOthersIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillCircle(image, 4, 8, 2, _mutedTextColor);
                FillCircle(image, 8, 8, 2, _mutedTextColor);
                FillCircle(image, 12, 8, 2, _mutedTextColor);
            });
        }

        private Texture2D CreateBagIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillRect(image, 5, 7, 10, 9, new Color("#765034"));
                FillRect(image, 6, 8, 8, 7, new Color("#9a7045"));
                FillRect(image, 7, 3, 6, 3, new Color("#715035"));
                FillRect(image, 6, 5, 2, 3, new Color("#3b2c21"));
                FillRect(image, 12, 5, 2, 3, new Color("#3b2c21"));
                FillRect(image, 8, 10, 4, 2, new Color("#d0aa61"));
            }, 20, 18);
        }

        private Texture2D CreateCoinIcon()
        {
            return CreatePixelIcon(image =>
            {
                FillCircle(image, 8, 8, 7, new Color("#9b651f"));
                FillCircle(image, 8, 8, 5, new Color("#e9bb3f"));
                FillCircle(image, 7, 7, 2, new Color("#f8d975"));
            }, 16, 16);
        }

        private Texture2D CreatePixelIcon(System.Action<Image> draw, int width = 16, int height = 16)
        {
            var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            image.Fill(new Color(0f, 0f, 0f, 0f));
            draw(image);
            return ImageTexture.CreateFromImage(image);
        }

        private void FillRect(Image image, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixelSafe(image, px, py, color);
                }
            }
        }

        private void FillCircle(Image image, int centerX, int centerY, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetPixelSafe(image, x, y, color);
                    }
                }
            }
        }

        private void DrawLine(Image image, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                SetPixelSafe(image, x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private void SetPixelSafe(Image image, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
            {
                return;
            }

            image.SetPixel(x, y, color);
        }
    }
}
