using Godot;
using System.Collections.Generic;
using System.Linq;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.UI.Shared;

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
        // Bản V3 thu gọn lại gần tỉ lệ mockup người dùng chọn: đủ lớn để đọc,
        // nhưng vẫn thấy được thế giới game quanh cửa sổ kho đồ.
        private const float PanelWidth = InventoryPanelChrome.PanelWidth;
        private const float PanelHeight = InventoryPanelChrome.PanelHeight;
        private const float SlotSize = InventoryPanelChrome.SlotSize;
        private const float DetailPanelWidth = InventoryPanelChrome.DetailPanelWidth;

        // ---------------------------------------------------------------------
        // Asset hook: người dùng chỉ cần đặt PNG đúng tên vào thư mục này.
        // Nếu file chưa tồn tại, UI vẫn chạy bằng icon/style fallback trong code.
        // ---------------------------------------------------------------------
        private const string InventoryAssetRoot = InventoryPanelChrome.AssetRoot;
        private const string BagIconPath = InventoryAssetRoot + "/icon_bag.png";
        private const string CoinIconPath = InventoryAssetRoot + "/icon_coin.png";

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
        // Palette nâu ấm, hơi cũ. Có phân tầng rõ nhưng không fantasy dát vàng.
        private Color _windowColor => InventoryPanelChrome.WindowColor;
        private Color _headerColor => InventoryPanelChrome.HeaderColor;
        private Color _surfaceColor => InventoryPanelChrome.SurfaceColor;
        private Color _raisedSurfaceColor => InventoryPanelChrome.RaisedSurfaceColor;
        private Color _deepSurfaceColor => InventoryPanelChrome.DeepSurfaceColor;
        private Color _slotSurfaceColor => InventoryPanelChrome.SlotSurfaceColor;
        private Color _borderColor => InventoryPanelChrome.BorderColor;
        private Color _strongBorderColor => InventoryPanelChrome.StrongBorderColor;
        private Color _accentColor => InventoryPanelChrome.AccentColor;
        private Color _mainTextColor => InventoryPanelChrome.MainTextColor;
        private Color _mutedTextColor => InventoryPanelChrome.MutedTextColor;
        private readonly Color _categoryColor = new("#79a85c");
        private readonly Color _dangerColor = new("#6b342d");

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
            InventoryPanelChrome.ApplyPanelSize(this);
        }

        private void BuildUI()
        {
            var root = InventoryPanelChrome.BuildWindowShell(this);
            root.AddChild(BuildHeader());
            root.AddChild(BuildCategoryTabs());
            root.AddChild(BuildBody());
        }

        private Control BuildHeader()
        {
            var headerPanel = InventoryPanelChrome.CreateHeader(out var header);

            // Icon nhỏ, không thêm badge lồng quanh icon để tránh cảm giác "box trong box".
            var bagIcon = new TextureRect();
            bagIcon.Texture = _bagIcon ??= TryLoadTexture(BagIconPath) ?? CreateBagIcon();
            bagIcon.CustomMinimumSize = new Vector2(24, 24);
            bagIcon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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
            coinIcon.Texture = _coinIcon ??= TryLoadTexture(CoinIconPath) ?? CreateCoinIcon();
            coinIcon.CustomMinimumSize = new Vector2(16, 16);
            coinIcon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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
            // Dùng đúng tab bar từ chrome chung để CharacterPanel và InventoryPanel không lệch style.
            var tabsPanel = InventoryPanelChrome.CreateTabBar(out var tabs);

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
            _detailIcon.CustomMinimumSize = new Vector2(104, 104);
            _detailIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _detailIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _detailIcon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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

            _primaryActionButton = CreateActionButton("Equip");
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
            button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Left;
            button.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            button.AddThemeConstantOverride("icon_max_width", 20);
            button.AddThemeConstantOverride("icon_spacing", 6);

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
            return InventoryPanelChrome.CreateCloseButton(Hide);
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
            icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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
            InventoryPanelChrome.ApplyTabStyle(button, selected);
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
            return InventoryPanelChrome.CreateLabel(text, fontSize, color);
        }

        private Button CreateActionButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 44);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            PixelButtonSkin.ApplyPrimary(button, PixelButtonSkin.LargeActionHeight);
            button.AddThemeFontSizeOverride("font_size", 15);
            return button;
        }

        /// <summary>
        /// Drop dùng danger skin riêng để hành động phá huỷ luôn khác primary/secondary.
        /// </summary>
        private Button CreateDangerActionButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 44);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            PixelButtonSkin.ApplyDanger(button, PixelButtonSkin.LargeActionHeight);
            button.AddThemeFontSizeOverride("font_size", 15);
            return button;
        }

        private void ApplySecondaryButtonStyle(Button button)
        {
            PixelButtonSkin.ApplySecondary(button, PixelButtonSkin.RegularHeight);
            button.AddThemeFontSizeOverride("font_size", 14);
        }

        private ColorRect CreateDivider()
        {
            return InventoryPanelChrome.CreateDivider();
        }

        private ColorRect CreateThinDivider()
        {
            return InventoryPanelChrome.CreateDivider(true);
        }

        private StyleBoxFlat CreateWindowStyle()
        {
            return InventoryPanelChrome.CreateWindowStyle();
        }

        private StyleBoxFlat CreateHeaderStyle()
        {
            return InventoryPanelChrome.CreateHeaderStyle();
        }

        private StyleBoxFlat CreateTabsBarStyle()
        {
            return InventoryPanelChrome.CreateTabsBarStyle();
        }

        private StyleBoxFlat CreateSectionStyle()
        {
            return InventoryPanelChrome.CreateSectionStyle();
        }

        private StyleBoxFlat CreateDetailSectionStyle()
        {
            return InventoryPanelChrome.CreateDetailSectionStyle();
        }

        private StyleBoxFlat CreatePreviewStyle()
        {
            return InventoryPanelChrome.CreatePreviewStyle();
        }

        private Color WithAlpha(Color color, float alpha)
        {
            return InventoryPanelChrome.WithAlpha(color, alpha);
        }

        private StyleBoxFlat CreateSlotStyle(bool selected)
        {
            return InventoryPanelChrome.CreateSlotStyle(selected);
        }

        private StyleBoxFlat CreateTabStyle(Color background, Color border, int borderWidth)
        {
            return InventoryPanelChrome.CreateTabStyle(background, border, borderWidth);
        }

        private StyleBoxFlat CreateButtonStyle(Color background, Color border, int borderWidth)
        {
            return InventoryPanelChrome.CreateButtonStyle(background, border, borderWidth);
        }

        private StyleBoxFlat CreateTransparentButtonStyle()
        {
            return InventoryPanelChrome.CreateTransparentButtonStyle();
        }

        private StyleBoxFlat CreateSlotHoverStyle()
        {
            return InventoryPanelChrome.CreateSlotHoverStyle();
        }

        private StyleBoxFlat CreateSlotPressedStyle()
        {
            return InventoryPanelChrome.CreateSlotPressedStyle();
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

            string assetName = category switch
            {
                InventoryCategory.All => "category_all.png",
                InventoryCategory.Consumables => "category_consumables.png",
                InventoryCategory.Materials => "category_materials.png",
                InventoryCategory.Equipment => "category_equipment.png",
                InventoryCategory.Quest => "category_quest.png",
                _ => "category_more.png"
            };

            // PNG thật được ưu tiên. Procedural icon chỉ là fallback để project không vỡ
            // trong lúc người dùng chưa bổ sung asset cuối cùng.
            icon = TryLoadTexture($"{InventoryAssetRoot}/{assetName}") ?? category switch
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

        /// <summary>
        /// Load asset tùy chọn. Không log lỗi nếu file chưa có vì người dùng sẽ tự bổ sung PNG sau.
        /// </summary>
        private Texture2D TryLoadTexture(string path)
        {
            return InventoryPanelChrome.TryLoadTexture(path);
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
