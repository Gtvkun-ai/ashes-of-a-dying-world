using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.Core.Skills;
using AshesofaDyingWorld.UI.HUD.Skills;

namespace AshesofaDyingWorld.UI.HUD
{
	public partial class CharacterDetailUI : Panel
	{
		// UI Elements
		private HBoxContainer _characterListContainer;
		private TextureRect _backgroundDisplay;
		
		// Tab system
		private Button _btnOverview;
		private Button _btnEquipment;
		private Button _btnSkills;
		
		// Content panels
		private Control _overviewPanel;
		private Control _equipmentPanel;
		private Control _skillsPanel;
		
		// Overview panel elements
		private Label _nameLabel;
		private Label _levelLabel;
		private Label _raceLabel;
		private Label _sidebarNameLabel;
		private Label _sidebarLevelLabel;
		private Label _portraitPlaceholderLabel;
		private Label _attackValueLabel;
		private Label _speedValueLabel;
		private Label _armorValueLabel;
		private Label _unspentAttributePointsLabel;
		private Control _overviewFooter;
		private VBoxContainer _statsTextContainer;

		// Ba thanh tài nguyên dùng ảnh riêng trong thư mục "3 main stat".
		// TextureProgressBar sẽ cắt ảnh từ trái sang phải theo giá trị hiện tại.
		private const string MainStatTextureRoot = "res://assets/sprites/UI_HUD/Status_bar/3 main stat";
		private VBoxContainer _resourceBarsContainer;
		private TextureProgressBar _hpBar;
		private TextureProgressBar _mpBar;
		private TextureProgressBar _staminaBar;
		private Label _hpValueLabel;
		private Label _mpValueLabel;
		private Label _staminaValueLabel;
		private VBoxContainer _skillsListContainer;
		private readonly List<PanelContainer> _inventorySlotPanels = new();
		private readonly Dictionary<EquipmentSlot, Label> _equipmentSlotTextLabels = new();
		private readonly Dictionary<EquipmentSlot, PanelContainer> _equipmentSlotPanels = new();
		private readonly Dictionary<EquipmentSlot, string> _equipmentSlotEmptyCaptions = new();
		private Label _equipmentDetailNameLabel;
		private Label _equipmentDetailTypeLabel;
		private Label _equipmentDetailStat1Label;
		private Label _equipmentDetailStat2Label;
		private Label _equipmentDetailDescriptionLabel;
		private Label _inventoryCountLabel;
		private Button _equipmentPrimaryActionButton;
		private Label _equipmentActionHintLabel;
		private EquipmentItemData _selectedEquipmentItem;
		private EquipmentSlot? _selectedEquipmentSlot;
		private int _selectedInventorySlotIndex = -1;
		private string _selectedEquipmentSource = "";
		private VBoxContainer _skillEntriesContainer;
		private readonly Dictionary<string, Button> _skillCategoryButtons = new();
		private readonly Dictionary<SkillData, PanelContainer> _skillEntryCards = new();
		private readonly List<SkillData> _allSkills = new();
		private string _currentSkillFilter = "all";
		private SkillData _selectedSkill;

		// Các control của tab Kỹ năng. Dữ liệu hiển thị được gom qua SkillViewModel.
		private Label _skillPointsLabel;
		private Label _skillDetailTitleLabel;
		private Label _skillDetailMetaLabel;
		private Label _skillDetailDescriptionLabel;
		private TextureRect _skillDetailIconRect;
		private Label _skillDetailIconFallbackLabel;
		private HFlowContainer _skillDetailBadgeContainer;
		private Label _skillDamageValueLabel;
		private Label _skillCooldownValueLabel;
		private Label _skillCostValueLabel;
		private Label _skillCastTimeValueLabel;
		private Label _skillDetailActionHintLabel;
		private Button _skillEquipButton;
		private Button _skillUpgradeButton;
		

		// Design tokens dùng chung tinh thần với InventoryPanel.
		private Color _deepSurfaceColor => InventoryPanelChrome.DeepSurfaceColor;
		private Color _borderColor => InventoryPanelChrome.BorderColor;
		private Color _accentColor => InventoryPanelChrome.AccentColor;
		private Color _mainTextColor => InventoryPanelChrome.MainTextColor;
		private Color _subTextColor => InventoryPanelChrome.MutedTextColor;
		private string _currentTab = "overview";

		private Color _currentThemeColor;
		private Control _equipmentBodyContainer;
		private GridContainer _inventoryGrid;
		private readonly List<TextureRect> _inventorySlotIcons = new();
		private readonly List<Label> _inventorySlotLabels = new();
		private readonly List<Button> _inventorySlotButtons = new();
		private readonly List<string> _inventorySlotItemIds = new();
		private InventoryManager _boundInventory;
		private readonly Dictionary<EquipmentSlot, TextureRect> _equipmentSlotIcons = new();
		private readonly Dictionary<EquipmentSlot, Texture2D> _equipmentSlotDefaultIcons = new();
		private readonly Dictionary<EquipmentSlot, Button> _equipmentSlotButtons = new();
		private EquipmentManager _boundEquipmentManager;
		private PlayerStats _observedStats;
		
		public override void _Ready()
		{
			ApplyPanelSize();
			AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
			BuildInventoryInspiredUI();

			VisibilityChanged += OnVisibilityChanged;
			if (PlayerManager.Instance != null)
			{
				PlayerManager.Instance.ActiveCharacterChanged += OnActiveCharacterChanged;
			}

			SwitchTab("overview");
		}

		public override void _ExitTree()
		{
			if (_boundInventory != null)
			{
				_boundInventory.InventoryChanged -= OnInventoryChanged;
			}

			if (_boundEquipmentManager != null)
			{
				_boundEquipmentManager.EquipmentChanged -= OnEquipmentChanged;
			}

			if (_observedStats != null)
			{
				_observedStats.StatsChanged -= OnObservedStatsChanged;
			}

			if (PlayerManager.Instance != null)
			{
				PlayerManager.Instance.ActiveCharacterChanged -= OnActiveCharacterChanged;
			}
		}


		private void ApplyPanelSize()
		{
			InventoryPanelChrome.ApplyPanelSize(this);
		}

		private void BuildInventoryInspiredUI()
		{
			// Khung ngoài vẫn dùng chung với Inventory để giao diện đồng bộ.
			// Phần bố cục bên trong được dựng riêng theo bản wireframe 3 cột.
			var root = InventoryPanelChrome.BuildWindowShell(this);
			root.AddChild(BuildCharacterHeader());
			root.AddChild(BuildCharacterTabs());
			root.AddChild(BuildCharacterBody());
			_overviewFooter = BuildOverviewFooter();
			root.AddChild(_overviewFooter);
		}

		private Control BuildCharacterHeader()
		{
			var headerPanel = InventoryPanelChrome.CreateHeader(out var row);

			// Tiêu đề bên trái. Không bắt buộc ảnh, người dùng có thể thêm icon sau.
			var title = CreateLabel("NHÂN VẬT", 18, _mainTextColor);
			title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			title.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(title);

			// Thông tin ngắn gọn bên phải theo wireframe: Hikaru | Cấp 01 | [x].
			_nameLabel = CreateLabel("NHÂN VẬT", 15, _mainTextColor);
			_nameLabel.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(_nameLabel);

			row.AddChild(CreateLabel("·", 13, _subTextColor));

			_levelLabel = CreateLabel("Cấp 00", 13, _mainTextColor);
			_levelLabel.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(_levelLabel);

			row.AddChild(CreateCloseButton());
			return headerPanel;
		}

		private Control BuildCharacterTabs()
		{
			var tabsPanel = InventoryPanelChrome.CreateTabBar(out var tabs);

			_btnOverview = CreateTabButton("TỔNG QUAN");
			_btnOverview.Pressed += () => SwitchTab("overview");
			tabs.AddChild(_btnOverview);

			_btnEquipment = CreateTabButton("TRANG BỊ");
			_btnEquipment.Pressed += () => SwitchTab("equipment");
			tabs.AddChild(_btnEquipment);

			_btnSkills = CreateTabButton("KỸ NĂNG");
			_btnSkills.Pressed += () => SwitchTab("skills");
			tabs.AddChild(_btnSkills);

			return tabsPanel;
		}

		private Control BuildCharacterBody()
		{
			// Ba tab dùng chung một vùng nội dung. Mỗi panel được chồng lên nhau
			// và SwitchTab chỉ bật panel cần thiết.
			var content = new Control();
			content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.SizeFlagsVertical = SizeFlags.ExpandFill;

			_overviewPanel = CreateOverviewPanel();
			content.AddChild(_overviewPanel);
			_equipmentPanel = CreateEquipmentPanel();
			content.AddChild(_equipmentPanel);
			_skillsPanel = CreateSkillsPanelLayout();
			content.AddChild(_skillsPanel);

			return content;
		}

		private Texture2D TryLoadTexture(string path)
		{
			return InventoryPanelChrome.TryLoadTexture(path);
		}

		private Label CreateLabel(string text, int fontSize, Color color)
		{
			return InventoryPanelChrome.CreateLabel(text, fontSize, color);
		}

		private ColorRect CreateDivider()
		{
			return InventoryPanelChrome.CreateDivider();
		}

		private ColorRect CreateThinDivider()
		{
			return InventoryPanelChrome.CreateDivider(true);
		}

		private Button CreateCloseButton()
		{
			return InventoryPanelChrome.CreateCloseButton(Hide);
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

		private StyleBoxFlat CreateSlotStyle(bool selected = false)
		{
			return InventoryPanelChrome.CreateSlotStyle(selected);
		}

		// Nội dung tab Tổng quan: đúng bố cục 3 cột của wireframe.
		private Control CreateOverviewPanel()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.AddThemeStyleboxOverride("panel", CreateOverviewSurfaceStyle());

			var outerMargin = new MarginContainer();
			outerMargin.AddThemeConstantOverride("margin_left", 12);
			outerMargin.AddThemeConstantOverride("margin_top", 12);
			outerMargin.AddThemeConstantOverride("margin_right", 12);
			outerMargin.AddThemeConstantOverride("margin_bottom", 12);
			panel.AddChild(outerMargin);

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 0);
			outerMargin.AddChild(body);

			// Cột 1: thuộc tính cơ bản.
			var statsFrame = CreateOverviewColumn(300, out var statsColumn);
			body.AddChild(statsFrame);
			statsColumn.AddChild(CreateSectionTitle("THUỘC TÍNH"));
			statsColumn.AddChild(CreateDivider());

			_statsTextContainer = new VBoxContainer();
			_statsTextContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_statsTextContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			_statsTextContainer.AddThemeConstantOverride("separation", 0);
			statsColumn.AddChild(_statsTextContainer);

			body.AddChild(CreateVerticalDivider());

			// Cột 2: tài nguyên và chỉ số chiến đấu.
			var centerFrame = CreateOverviewColumn(390, out var centerColumn);
			centerFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.AddChild(centerFrame);
			centerColumn.AddChild(CreateSectionTitle("TÀI NGUYÊN"));
			centerColumn.AddChild(CreateDivider());

			_resourceBarsContainer = new VBoxContainer();
			_resourceBarsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_resourceBarsContainer.AddThemeConstantOverride("separation", 6);
			centerColumn.AddChild(_resourceBarsContainer);

			// Tất cả PNG nằm trực tiếp trong thư mục "3 main stat", không có thư mục con.
			// File "... ic.png" là phần khung/icon; file hp.png, mp.png, sta.png là phần màu chạy.
			_resourceBarsContainer.AddChild(CreateResourceBarRow(
				"HP", "hp ic.png", "hp.png", "Sinh lực", out _hpBar, out _hpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow(
				"MP", "mp ic.png", "mp.png", "Năng lượng phép", out _mpBar, out _mpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow(
				"STA", "sta ic.png", "sta.png", "Thể lực", out _staminaBar, out _staminaValueLabel));

			centerColumn.AddChild(CreateSectionSpacer(18));
			centerColumn.AddChild(CreateSectionTitle("CHỈ SỐ CHIẾN ĐẤU", HorizontalAlignment.Left));
			centerColumn.AddChild(CreateDivider());
			centerColumn.AddChild(CreateCombatStatRow("Công vật lý", out _attackValueLabel));
			centerColumn.AddChild(CreateCombatStatRow("Tốc độ", out _speedValueLabel));
			centerColumn.AddChild(CreateCombatStatRow("Kháng phép", out _armorValueLabel));
			var centerSpacer = new Control();
			centerSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
			centerColumn.AddChild(centerSpacer);

			body.AddChild(CreateVerticalDivider());

			// Cột 3: chân dung, danh tính và tổ đội.
			var identityFrame = CreateOverviewColumn(250, out var identityColumn);
			body.AddChild(identityFrame);
			identityColumn.AddChild(CreateSectionTitle("NHÂN VẬT"));
			identityColumn.AddChild(CreateDivider());

			var previewFrame = new PanelContainer();
			previewFrame.CustomMinimumSize = new Vector2(0, 145);
			previewFrame.AddThemeStyleboxOverride("panel", CreatePortraitStyle());
			identityColumn.AddChild(previewFrame);

			_portraitPlaceholderLabel = CreateLabel("[ ẢNH CHÂN DUNG ]", 12, _subTextColor);
			_portraitPlaceholderLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_portraitPlaceholderLabel.VerticalAlignment = VerticalAlignment.Center;
			_portraitPlaceholderLabel.SetAnchorsPreset(LayoutPreset.FullRect);
			previewFrame.AddChild(_portraitPlaceholderLabel);

			_backgroundDisplay = new TextureRect();
			_backgroundDisplay.SetAnchorsPreset(LayoutPreset.FullRect);
			_backgroundDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_backgroundDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			_backgroundDisplay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			_backgroundDisplay.MouseFilter = MouseFilterEnum.Ignore;
			previewFrame.AddChild(_backgroundDisplay);

			identityColumn.AddChild(CreateSectionSpacer(8));
			_sidebarNameLabel = CreateLabel("Hikaru", 15, _mainTextColor);
			_sidebarNameLabel.HorizontalAlignment = HorizontalAlignment.Left;
			identityColumn.AddChild(_sidebarNameLabel);

			_raceLabel = CreateLabel("Con người", 12, _subTextColor);
			identityColumn.AddChild(_raceLabel);
			_sidebarLevelLabel = CreateLabel("Cấp 01", 12, _subTextColor);
			identityColumn.AddChild(_sidebarLevelLabel);

			identityColumn.AddChild(CreateSectionSpacer(12));
			identityColumn.AddChild(CreateSectionTitle("TỔ ĐỘI", HorizontalAlignment.Left));
			identityColumn.AddChild(CreateDivider());

			_characterListContainer = new HBoxContainer();
			_characterListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_characterListContainer.Alignment = BoxContainer.AlignmentMode.Begin;
			_characterListContainer.AddThemeConstantOverride("separation", 8);
			identityColumn.AddChild(_characterListContainer);

			var identitySpacer = new Control();
			identitySpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
			identityColumn.AddChild(identitySpacer);
			return panel;
		}

		/// <summary>
		/// Tạo một cột của màn Tổng quan. Toàn bộ khoảng cách nằm ở đây để
		/// sau này chỉnh layout không phải săn từng con số rải rác.
		/// </summary>
		private MarginContainer CreateOverviewColumn(float minimumWidth, out VBoxContainer column)
		{
			// MarginContainer tạo khoảng thở thật giữa nội dung và đường chia cột.
			var frame = new MarginContainer();
			frame.CustomMinimumSize = new Vector2(minimumWidth, 0);
			frame.SizeFlagsVertical = SizeFlags.ExpandFill;
			frame.AddThemeConstantOverride("margin_left", 16);
			frame.AddThemeConstantOverride("margin_top", 4);
			frame.AddThemeConstantOverride("margin_right", 16);
			frame.AddThemeConstantOverride("margin_bottom", 4);

			column = new VBoxContainer();
			column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			column.SizeFlagsVertical = SizeFlags.ExpandFill;
			column.AddThemeConstantOverride("separation", 8);
			frame.AddChild(column);
			return frame;
		}

		private Label CreateSectionTitle(string text, HorizontalAlignment alignment = HorizontalAlignment.Center)
		{
			var label = CreateLabel(text, 14, _mainTextColor);
			label.CustomMinimumSize = new Vector2(0, 28);
			label.HorizontalAlignment = alignment;
			label.VerticalAlignment = VerticalAlignment.Center;
			return label;
		}

		private ColorRect CreateVerticalDivider()
		{
			var divider = new ColorRect();
			divider.Color = new Color(_borderColor.R, _borderColor.G, _borderColor.B, 0.72f);
			divider.CustomMinimumSize = new Vector2(1, 0);
			divider.MouseFilter = MouseFilterEnum.Ignore;
			return divider;
		}

		private Control CreateSectionSpacer(float height)
		{
			var spacer = new Control();
			spacer.CustomMinimumSize = new Vector2(0, height);
			return spacer;
		}

		private HBoxContainer CreateCombatStatRow(string title, out Label valueLabel)
		{
			var row = new HBoxContainer();
			row.CustomMinimumSize = new Vector2(0, 30);
			var name = CreateLabel(title, 13, _mainTextColor);
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			name.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(name);
			valueLabel = CreateLabel("0", 13, _mainTextColor);
			valueLabel.CustomMinimumSize = new Vector2(72, 0);
			valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
			valueLabel.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(valueLabel);
			return row;
		}

		private Control BuildOverviewFooter()
		{
			var panel = new PanelContainer();
			panel.CustomMinimumSize = new Vector2(0, 42);
			panel.AddThemeStyleboxOverride("panel", CreateFooterStyle());
			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 14);
			margin.AddThemeConstantOverride("margin_right", 14);
			panel.AddChild(margin);
			_unspentAttributePointsLabel = CreateLabel("0 điểm thuộc tính chưa sử dụng", 12, _mainTextColor);
			_unspentAttributePointsLabel.VerticalAlignment = VerticalAlignment.Center;
			margin.AddChild(_unspentAttributePointsLabel);
			return panel;
		}

		private StyleBoxFlat CreateOverviewSurfaceStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color(_deepSurfaceColor.R, _deepSurfaceColor.G, _deepSurfaceColor.B, 0.58f);
			style.BorderColor = new Color(_borderColor.R, _borderColor.G, _borderColor.B, 0.84f);
			style.SetBorderWidthAll(1);
			return style;
		}

		private StyleBoxFlat CreatePortraitStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = _deepSurfaceColor;
			style.BorderColor = _borderColor;
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(2);
			return style;
		}

		private StyleBoxFlat CreateFooterStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color(_deepSurfaceColor.R, _deepSurfaceColor.G, _deepSurfaceColor.B, 0.72f);
			style.BorderColor = _borderColor;
			style.BorderWidthTop = 1;
			return style;
		}

		private Control CreateEquipmentPanel()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", CreateOverviewSurfaceStyle());

			var outerMargin = new MarginContainer();
			outerMargin.AddThemeConstantOverride("margin_left", 12);
			outerMargin.AddThemeConstantOverride("margin_top", 12);
			outerMargin.AddThemeConstantOverride("margin_right", 12);
			outerMargin.AddThemeConstantOverride("margin_bottom", 12);
			panel.AddChild(outerMargin);

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 0);
			outerMargin.AddChild(body);

			var loadoutFrame = CreateOverviewColumn(330, out var loadoutColumn);
			body.AddChild(loadoutFrame);
			loadoutColumn.AddChild(CreateSectionTitle("TRANG BỊ ĐANG MẶC"));
			loadoutColumn.AddChild(CreateDivider());
			loadoutColumn.AddChild(CreateEquipmentLoadoutLayout());

			body.AddChild(CreateVerticalDivider());

			var inventoryFrame = CreateOverviewColumn(360, out var inventoryColumn);
			inventoryFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.AddChild(inventoryFrame);
			inventoryColumn.AddChild(CreateSectionTitle("TÚI ĐỒ"));
			inventoryColumn.AddChild(CreateDivider());

			var inventoryScroll = new ScrollContainer();
			inventoryScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			inventoryScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			inventoryColumn.AddChild(inventoryScroll);

			_inventoryGrid = new GridContainer();
			_inventoryGrid.Columns = 5;
			_inventoryGrid.AddThemeConstantOverride("h_separation", 8);
			_inventoryGrid.AddThemeConstantOverride("v_separation", 8);
			_inventoryGrid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			_inventoryGrid.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			inventoryScroll.AddChild(_inventoryGrid);

			var inventory = ResolveInventoryManager();
			int slotCount = inventory != null ? inventory.MaxSlots : 40;
			if (slotCount < 1) slotCount = 40;
			for (int i = 0; i < slotCount; i++) CreateInventorySlot(_inventoryGrid);

			var inventoryFooter = new HBoxContainer();
			inventoryFooter.CustomMinimumSize = new Vector2(0, 28);
			inventoryFooter.AddThemeConstantOverride("separation", 8);
			inventoryColumn.AddChild(inventoryFooter);

			var filterLabel = CreateLabel("Bộ lọc: Tất cả ▼", 12, _mainTextColor);
			filterLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			inventoryFooter.AddChild(filterLabel);

			_inventoryCountLabel = CreateLabel("0 / 0", 12, _mainTextColor);
			_inventoryCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
			inventoryFooter.AddChild(_inventoryCountLabel);

			body.AddChild(CreateVerticalDivider());

			var detailFrame = CreateOverviewColumn(220, out var detailColumn);
			body.AddChild(detailFrame);
			detailColumn.AddChild(CreateSectionTitle("CHI TIẾT"));
			detailColumn.AddChild(CreateDivider());

			_equipmentDetailNameLabel = CreateLabel("Chưa chọn vật phẩm", 15, _mainTextColor);
			_equipmentDetailNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_equipmentDetailNameLabel);

			_equipmentDetailTypeLabel = CreateLabel("", 13, _subTextColor);
			_equipmentDetailTypeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_equipmentDetailTypeLabel);

			detailColumn.AddChild(CreateSectionSpacer(14));
			_equipmentDetailStat1Label = CreateLabel("", 13, _mainTextColor);
			detailColumn.AddChild(_equipmentDetailStat1Label);
			_equipmentDetailStat2Label = CreateLabel("", 13, _mainTextColor);
			detailColumn.AddChild(_equipmentDetailStat2Label);

			detailColumn.AddChild(CreateSectionSpacer(14));
			_equipmentDetailDescriptionLabel = CreateLabel("Chọn một ô trong túi đồ hoặc trang bị đang mặc để xem chi tiết.", 12, _subTextColor);
			_equipmentDetailDescriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_equipmentDetailDescriptionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
			detailColumn.AddChild(_equipmentDetailDescriptionLabel);

			_equipmentPrimaryActionButton = CreateActionButton("Trang bị", OnEquipmentPrimaryActionPressed);
			_equipmentPrimaryActionButton.Disabled = true;
			detailColumn.AddChild(_equipmentPrimaryActionButton);

			_equipmentActionHintLabel = CreateLabel("", 11, _subTextColor);
			_equipmentActionHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_equipmentActionHintLabel);

			UpdateEquipmentDetailPanel();
			return panel;
		}

		private Control CreateEquipmentLoadoutLayout()
		{
			var wrapper = new VBoxContainer();
			wrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			wrapper.SizeFlagsVertical = SizeFlags.ExpandFill;
			wrapper.Alignment = BoxContainer.AlignmentMode.Center;
			wrapper.AddThemeConstantOverride("separation", 12);

			var topRow = new HBoxContainer();
			topRow.Alignment = BoxContainer.AlignmentMode.Center;
			wrapper.AddChild(topRow);
			CreateEquipmentSlotWidget(topRow, "Đầu", EquipmentSlot.Head);

			var middleRow = new HBoxContainer();
			middleRow.Alignment = BoxContainer.AlignmentMode.Center;
			middleRow.AddThemeConstantOverride("separation", 10);
			wrapper.AddChild(middleRow);
			CreateEquipmentSlotWidget(middleRow, "Áo", EquipmentSlot.Body);

			var bodyFrame = new PanelContainer();
			bodyFrame.CustomMinimumSize = new Vector2(116, 116);
			bodyFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			middleRow.AddChild(bodyFrame);

			var bodyCenter = new CenterContainer();
			bodyFrame.AddChild(bodyCenter);

			var bodyInner = new VBoxContainer();
			bodyInner.Alignment = BoxContainer.AlignmentMode.Center;
			bodyInner.AddThemeConstantOverride("separation", 6);
			bodyCenter.AddChild(bodyInner);

			var bodyLabel = CreateLabel("[ Nhân vật ]", 12, _subTextColor);
			bodyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			bodyInner.AddChild(bodyLabel);

			_equipmentBodyContainer = new Control();
			_equipmentBodyContainer.CustomMinimumSize = new Vector2(84, 84);
			_equipmentBodyContainer.ClipContents = true;
			bodyInner.AddChild(_equipmentBodyContainer);

			CreateEquipmentSlotWidget(middleRow, "Vũ khí", EquipmentSlot.MainHand);

			var bottomRow1 = new HBoxContainer();
			bottomRow1.Alignment = BoxContainer.AlignmentMode.Center;
			wrapper.AddChild(bottomRow1);
			CreateEquipmentSlotWidget(bottomRow1, "Quần", EquipmentSlot.Legs);

			var bottomRow2 = new HBoxContainer();
			bottomRow2.Alignment = BoxContainer.AlignmentMode.Center;
			wrapper.AddChild(bottomRow2);
			CreateEquipmentSlotWidget(bottomRow2, "Giày", EquipmentSlot.Accessory2);

			var spacer = new Control();
			spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
			wrapper.AddChild(spacer);
			return wrapper;
		}

		private void CreateEquipmentSlotWidget(Container parent, string displayText, EquipmentSlot slotType)
		{
			var slotPanel = new PanelContainer();
			slotPanel.CustomMinimumSize = new Vector2(108, 46);
			slotPanel.AddThemeStyleboxOverride("panel", CreateSlotStyle());
			parent.AddChild(slotPanel);

			var inner = new Control();
			inner.CustomMinimumSize = new Vector2(108, 46);
			slotPanel.AddChild(inner);

			var icon = new TextureRect();
			icon.SetAnchorsPreset(LayoutPreset.FullRect);
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			icon.Visible = false;
			inner.AddChild(icon);

			var label = CreateLabel($"[ {displayText} ]", 12, _mainTextColor);
			label.SetAnchorsPreset(LayoutPreset.FullRect);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			inner.AddChild(label);

			var button = new Button();
			button.SetAnchorsPreset(LayoutPreset.FullRect);
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateSlotHoverStyle());
			button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateSlotPressedStyle());
			button.Pressed += () => OnEquipmentSlotPressed(slotType);
			inner.AddChild(button);

			_equipmentSlotIcons[slotType] = icon;
			_equipmentSlotButtons[slotType] = button;
			_equipmentSlotTextLabels[slotType] = label;
			_equipmentSlotPanels[slotType] = slotPanel;
			_equipmentSlotDefaultIcons[slotType] = null;
			_equipmentSlotEmptyCaptions[slotType] = displayText;
		}

		// Tạo 1 ô inventory nhỏ.
		private void CreateInventorySlot(GridContainer parent)
		{
			int slotIndex = _inventorySlotButtons.Count;
			var slot = new PanelContainer();
			slot.CustomMinimumSize = new Vector2(InventoryPanelChrome.SlotSize, InventoryPanelChrome.SlotSize);
			slot.AddThemeStyleboxOverride("panel", CreateSlotStyle());

			var inner = new Control();
			inner.CustomMinimumSize = new Vector2(InventoryPanelChrome.SlotSize, InventoryPanelChrome.SlotSize);
			slot.AddChild(inner);

			var iconRect = new TextureRect();
			iconRect.Position = new Vector2(8, 7);
			iconRect.Size = new Vector2(48, 40);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			iconRect.Visible = false;
			inner.AddChild(iconRect);

			var label = CreateLabel("", 9, _subTextColor);
			label.Position = new Vector2(4, 46);
			label.Size = new Vector2(56, 13);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			inner.AddChild(label);

			var button = new Button();
			button.SetAnchorsPreset(LayoutPreset.FullRect);
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateSlotHoverStyle());
			button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateSlotPressedStyle());
			button.Pressed += () => OnInventorySlotPressed(slotIndex);
			slot.AddChild(button);

			_inventorySlotPanels.Add(slot);
			_inventorySlotIcons.Add(iconRect);
			_inventorySlotLabels.Add(label);
			_inventorySlotButtons.Add(button);
			_inventorySlotItemIds.Add(string.Empty);
			parent.AddChild(slot);
		}

		private void RefreshInventoryGrid()
		{
			if (_inventorySlotIcons.Count == 0) return;

			var inventory = ResolveInventoryManager();
			RebindInventory(inventory);

			for (int i = 0; i < _inventorySlotIcons.Count; i++)
			{
				var iconRect = _inventorySlotIcons[i];
				var lbl = _inventorySlotLabels[i];
				var button = _inventorySlotButtons[i];

				var item = (inventory != null && i < inventory.Items.Count) ? inventory.Items[i] : null;
				if (item == null)
				{
					iconRect.Texture = null;
					iconRect.Visible = false;
					lbl.Text = "";
					button.TooltipText = "";
					_inventorySlotItemIds[i] = string.Empty;
					continue;
				}

				iconRect.Texture = item.Icon;
				iconRect.Visible = item.Icon != null;
				lbl.Text = item.Icon == null ? CompactItemName(item.ItemName) : "";
				button.TooltipText = item.ItemName;
				_inventorySlotItemIds[i] = item.ID;
			}

			int usedSlots = inventory?.Items.Count ?? 0;
			int maxSlots = inventory?.MaxSlots ?? _inventorySlotIcons.Count;
			if (_inventoryCountLabel != null)
			{
				_inventoryCountLabel.Text = $"{usedSlots} / {maxSlots}";
			}

			if (_selectedInventorySlotIndex >= usedSlots)
			{
				_selectedInventorySlotIndex = -1;
				if (_selectedEquipmentSource == "inventory")
				{
					_selectedEquipmentItem = null;
					_selectedEquipmentSource = "";
				}
			}

			RefreshInventorySelectionVisuals();
			UpdateEquipmentDetailPanel();
		}

		private void OnInventorySlotPressed(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= _inventorySlotItemIds.Count) return;

			string itemId = _inventorySlotItemIds[slotIndex];
			_selectedInventorySlotIndex = slotIndex;
			_selectedEquipmentSlot = null;
			_selectedEquipmentSource = "inventory";
			_selectedEquipmentItem = string.IsNullOrEmpty(itemId) ? null : ResolveInventoryManager()?.GetItem(itemId);

			RefreshInventorySelectionVisuals();
			RefreshEquipmentSlotSelectionVisuals();
			UpdateEquipmentDetailPanel();
		}

		private void OnEquipmentPrimaryActionPressed()
		{
			var player = ResolvePlayer();
			if (player == null || _selectedEquipmentItem == null)
			{
				return;
			}

			if (_selectedEquipmentSource == "inventory")
			{
				player.EquipFromInventory(_selectedEquipmentItem.ID);
			}
			else if (_selectedEquipmentSource == "equipped" && _selectedEquipmentSlot.HasValue)
			{
				player.UnequipToInventory(_selectedEquipmentSlot.Value);
			}

			_selectedEquipmentItem = null;
			_selectedEquipmentSlot = null;
			_selectedInventorySlotIndex = -1;
			_selectedEquipmentSource = "";
			RefreshInventoryGrid();
			RefreshEquipmentSlots();
			UpdateCharacterInfo();
		}

		private void RefreshInventorySelectionVisuals()
		{
			for (int i = 0; i < _inventorySlotPanels.Count; i++)
			{
				_inventorySlotPanels[i].AddThemeStyleboxOverride("panel", CreateSlotStyle(i == _selectedInventorySlotIndex));
			}
		}

		/// <summary>
		/// Tìm Player hiện tại. Ưu tiên Player do SceneManager quản lý,
		/// sau đó mới dò node thuộc group "Player" để tránh phụ thuộc cứng vào scene.
		/// </summary>
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

		/// <summary>
		/// Lấy InventoryManager của Player hiện tại.
		/// Hàm này không giữ tham chiếu vĩnh viễn vì Player có thể đổi khi chuyển scene.
		/// </summary>
		private InventoryManager ResolveInventoryManager()
		{
			var player = ResolvePlayer();
			return player?.GetNodeOrNull<InventoryManager>("InventoryManager");
		}

		/// <summary>
		/// Gỡ signal khỏi inventory cũ và nối signal vào inventory mới.
		/// Làm vậy để tránh callback bị gọi nhiều lần sau khi đổi nhân vật hoặc scene.
		/// </summary>
		private void RebindInventory(InventoryManager inventory)
		{
			if (_boundInventory == inventory)
			{
				return;
			}

			if (_boundInventory != null)
			{
				_boundInventory.InventoryChanged -= OnInventoryChanged;
			}

			_boundInventory = inventory;

			if (_boundInventory != null)
			{
				_boundInventory.InventoryChanged += OnInventoryChanged;
			}
		}

		/// <summary>
		/// Làm mới tab Trang bị khi nội dung túi đồ thay đổi.
		/// </summary>
		private void OnInventoryChanged()
		{
			RefreshInventoryGrid();
		}

		/// <summary>
		/// Tạo chữ viết tắt dùng khi vật phẩm chưa có icon.
		/// Ví dụ "Kiếm gỗ" thành "KI".
		/// </summary>
		private string CompactItemName(string itemName)
		{
			if (string.IsNullOrWhiteSpace(itemName))
			{
				return "?";
			}

			string normalized = itemName.Trim();
			return normalized.Length <= 2
				? normalized.ToUpperInvariant()
				: normalized.Substring(0, 2).ToUpperInvariant();
		}

		private EquipmentManager ResolveEquipmentManager()
		{
			var player = ResolvePlayer();
			if (player != null)
			{
				return player.GetNodeOrNull<EquipmentManager>("EquipmentManager");
			}

			return null;
		}

		private void RebindEquipmentManager(EquipmentManager equipmentManager)
		{
			if (_boundEquipmentManager == equipmentManager) return;

			if (_boundEquipmentManager != null)
			{
				_boundEquipmentManager.EquipmentChanged -= OnEquipmentChanged;
			}

			_boundEquipmentManager = equipmentManager;

			if (_boundEquipmentManager != null)
			{
				_boundEquipmentManager.EquipmentChanged += OnEquipmentChanged;
			}
		}

		private void OnEquipmentChanged(int slot, EquipmentItemData item)
		{
			RefreshEquipmentSlots();
		}

		private void RefreshEquipmentSlots()
		{
			var equipmentManager = ResolveEquipmentManager();
			RebindEquipmentManager(equipmentManager);

			foreach (var pair in _equipmentSlotIcons)
			{
				var slotType = pair.Key;
				var iconRect = pair.Value;
				var button = _equipmentSlotButtons[slotType];
				var label = _equipmentSlotTextLabels[slotType];

				var equipped = equipmentManager?.GetEquippedItem(slotType);
				iconRect.Texture = equipped?.Icon;
				iconRect.Visible = equipped?.Icon != null;
				label.Text = equipped == null
					? $"[ {_equipmentSlotEmptyCaptions[slotType]} ]"
					: (equipped.Icon == null ? $"[ {CompactItemName(equipped.ItemName)} ]" : "");
				button.TooltipText = equipped != null ? equipped.ItemName : $"Ô {_equipmentSlotEmptyCaptions[slotType]}";
			}

			RefreshEquipmentSlotSelectionVisuals();
			UpdateEquipmentDetailPanel();
		}

		private void RefreshEquipmentSlotSelectionVisuals()
		{
			foreach (var pair in _equipmentSlotPanels)
			{
				pair.Value.AddThemeStyleboxOverride("panel", CreateSlotStyle(_selectedEquipmentSlot.HasValue && _selectedEquipmentSlot.Value == pair.Key));
			}
		}

		private void OnEquipmentSlotPressed(EquipmentSlot slotType)
		{
			var equipmentManager = ResolveEquipmentManager();
			_selectedInventorySlotIndex = -1;
			_selectedEquipmentSlot = slotType;
			_selectedEquipmentItem = equipmentManager?.GetEquippedItem(slotType);
			_selectedEquipmentSource = _selectedEquipmentItem != null ? "equipped" : "";
			RefreshInventorySelectionVisuals();
			RefreshEquipmentSlotSelectionVisuals();
			UpdateEquipmentDetailPanel();
		}

		private void UpdateEquipmentDetailPanel()
		{
			if (_equipmentDetailNameLabel == null)
			{
				return;
			}

			if (_selectedEquipmentItem == null)
			{
				_equipmentDetailNameLabel.Text = "Chưa chọn vật phẩm";
				_equipmentDetailTypeLabel.Text = "";
				_equipmentDetailStat1Label.Text = "";
				_equipmentDetailStat2Label.Text = "";
				_equipmentDetailDescriptionLabel.Text = "Chọn một ô trong túi đồ hoặc trang bị đang mặc để xem chi tiết.";
				if (_equipmentPrimaryActionButton != null)
				{
					_equipmentPrimaryActionButton.Text = "Trang bị";
					_equipmentPrimaryActionButton.Disabled = true;
				}
				if (_equipmentActionHintLabel != null)
				{
					_equipmentActionHintLabel.Text = "";
				}
				return;
			}

			_equipmentDetailNameLabel.Text = _selectedEquipmentItem.ItemName;
			_equipmentDetailTypeLabel.Text = GetEquipmentSlotDisplayName(_selectedEquipmentItem.SlotType);
			_equipmentDetailStat1Label.Text = BuildPrimaryItemStatText(_selectedEquipmentItem);
			_equipmentDetailStat2Label.Text = BuildSecondaryItemStatText(_selectedEquipmentItem);
			_equipmentDetailDescriptionLabel.Text = string.IsNullOrWhiteSpace(_selectedEquipmentItem.Description)
				? "Vật phẩm này chưa có mô tả."
				: _selectedEquipmentItem.Description;

			if (_equipmentPrimaryActionButton != null)
			{
				bool fromInventory = _selectedEquipmentSource == "inventory";
				_equipmentPrimaryActionButton.Text = fromInventory ? "Trang bị" : "Tháo";
				_equipmentPrimaryActionButton.Disabled = false;
			}

			if (_equipmentActionHintLabel != null)
			{
				_equipmentActionHintLabel.Text = _selectedEquipmentSource == "inventory"
					? "Nhấn để chuyển vật phẩm từ túi đồ sang ô trang bị phù hợp."
					: "Nhấn để tháo vật phẩm đang mặc và trả về túi đồ.";
			}
		}

		private string BuildPrimaryItemStatText(EquipmentItemData item)
		{
			if (item == null) return "";
			int amount = Mathf.RoundToInt(item.BaseValue);
			if (item.SlotType == EquipmentSlot.MainHand)
			{
				return $"Công {(amount >= 0 ? "+" : "")}{amount}";
			}
			return $"Phòng thủ {(amount >= 0 ? "+" : "")}{amount}";
		}

		private string BuildSecondaryItemStatText(EquipmentItemData item)
		{
			if (item == null) return "";

			if (item.AttributeBonuses != null)
			{
				foreach (var bonus in item.AttributeBonuses)
				{
					if (bonus.Value != 0)
					{
						return $"{GetAttributeDisplayName(bonus.Key)} {(bonus.Value >= 0 ? "+" : "")}{bonus.Value}";
					}
				}
			}

			if (item.SlotType == EquipmentSlot.MainHand)
			{
				int speedBonus = Mathf.RoundToInt((1.15f - item.WeaponWeight) * 10f);
				return $"Tốc độ {(speedBonus >= 0 ? "+" : "")}{speedBonus}";
			}

			return "Không có cộng thêm";
		}

		private string GetAttributeDisplayName(AttributeType attributeType)
		{
			return attributeType switch
			{
				AttributeType.Strength => "STR",
				AttributeType.Dexterity => "DEX",
				AttributeType.Intelligence => "INT",
				AttributeType.Vitality => "VIT",
				AttributeType.Spirit => "SPI",
				AttributeType.Defense => "DEF",
				_ => attributeType.ToString().ToUpper()
			};
		}

		private string GetEquipmentSlotDisplayName(EquipmentSlot slotType)
		{
			return slotType switch
			{
				EquipmentSlot.MainHand => "Vũ khí",
				EquipmentSlot.OffHand => "Phụ",
				EquipmentSlot.Head => "Đầu",
				EquipmentSlot.Body => "Áo",
				EquipmentSlot.Legs => "Quần",
				EquipmentSlot.Accessory1 => "Phụ kiện 1",
				EquipmentSlot.Accessory2 => "Giày",
				_ => "Trang bị"
			};
		}

		/// <summary>
		/// Dựng tab Kỹ năng theo bố cục toolbar ngang + danh sách/chi tiết 60/40.
		/// Layout chỉ chịu trách nhiệm trình bày; state và loadout được quản lý trong PlayerSkillCollection.
		/// </summary>
		private Control CreateSkillsPanelLayout()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", CreateOverviewSurfaceStyle());

			var outerMargin = new MarginContainer();
			outerMargin.AddThemeConstantOverride("margin_left", 12);
			outerMargin.AddThemeConstantOverride("margin_top", 12);
			outerMargin.AddThemeConstantOverride("margin_right", 12);
			outerMargin.AddThemeConstantOverride("margin_bottom", 12);
			panel.AddChild(outerMargin);

			var layout = new VBoxContainer();
			layout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			layout.SizeFlagsVertical = SizeFlags.ExpandFill;
			layout.AddThemeConstantOverride("separation", 10);
			outerMargin.AddChild(layout);

			// Thanh lọc ngang giúp dành toàn bộ chiều cao cho nội dung kỹ năng.
			var toolbar = new PanelContainer();
			toolbar.CustomMinimumSize = new Vector2(0, 44);
			toolbar.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());
			layout.AddChild(toolbar);

			var toolbarMargin = new MarginContainer();
			toolbarMargin.AddThemeConstantOverride("margin_left", 12);
			toolbarMargin.AddThemeConstantOverride("margin_top", 6);
			toolbarMargin.AddThemeConstantOverride("margin_right", 12);
			toolbarMargin.AddThemeConstantOverride("margin_bottom", 6);
			toolbar.AddChild(toolbarMargin);

			var toolbarRow = new HBoxContainer();
			toolbarRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			toolbarRow.AddThemeConstantOverride("separation", 8);
			toolbarMargin.AddChild(toolbarRow);

			var filtersRow = new HBoxContainer();
			filtersRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			filtersRow.AddThemeConstantOverride("separation", 8);
			toolbarRow.AddChild(filtersRow);
			filtersRow.AddChild(CreateSkillCategoryButton("all", "Tất cả"));
			filtersRow.AddChild(CreateSkillCategoryButton("active", "Chủ động"));
			filtersRow.AddChild(CreateSkillCategoryButton("passive", "Bị động"));
			filtersRow.AddChild(CreateSkillCategoryButton("innate", "Nội tại"));

			_skillPointsLabel = CreateLabel("Điểm KN: 0", 13, _mainTextColor);
			_skillPointsLabel.VerticalAlignment = VerticalAlignment.Center;
			toolbarRow.AddChild(_skillPointsLabel);

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 0);
			layout.AddChild(body);

			var listFrame = CreateOverviewColumn(620, out var listColumn);
			listFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.AddChild(listFrame);
			listColumn.AddChild(CreateSectionTitle("DANH SÁCH KỸ NĂNG", HorizontalAlignment.Left));
			listColumn.AddChild(CreateDivider());

			var scroll = new ScrollContainer();
			scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			listColumn.AddChild(scroll);

			_skillEntriesContainer = new VBoxContainer();
			_skillEntriesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_skillEntriesContainer.AddThemeConstantOverride("separation", 8);
			scroll.AddChild(_skillEntriesContainer);

			body.AddChild(CreateVerticalDivider());

			var detailFrame = CreateOverviewColumn(400, out var detailColumn);
			detailFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.AddChild(detailFrame);
			detailColumn.AddChild(CreateSectionTitle("CHI TIẾT", HorizontalAlignment.Left));
			detailColumn.AddChild(CreateDivider());

			var iconHolder = new CenterContainer();
			iconHolder.CustomMinimumSize = new Vector2(0, 104);
			detailColumn.AddChild(iconHolder);

			var iconFrame = new PanelContainer();
			iconFrame.CustomMinimumSize = new Vector2(88, 88);
			iconFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			iconHolder.AddChild(iconFrame);

			var iconCenter = new CenterContainer();
			iconFrame.AddChild(iconCenter);

			_skillDetailIconRect = new TextureRect();
			_skillDetailIconRect.CustomMinimumSize = new Vector2(70, 70);
			_skillDetailIconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_skillDetailIconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			_skillDetailIconRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			iconCenter.AddChild(_skillDetailIconRect);

			_skillDetailIconFallbackLabel = CreateLabel("?", 22, _subTextColor);
			_skillDetailIconFallbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
			iconCenter.AddChild(_skillDetailIconFallbackLabel);

			_skillDetailTitleLabel = CreateLabel("Chưa chọn kỹ năng", 18, _mainTextColor);
			_skillDetailTitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_skillDetailTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
			detailColumn.AddChild(_skillDetailTitleLabel);

			_skillDetailBadgeContainer = new HFlowContainer();
			_skillDetailBadgeContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_skillDetailBadgeContainer.AddThemeConstantOverride("h_separation", 6);
			_skillDetailBadgeContainer.AddThemeConstantOverride("v_separation", 6);
			detailColumn.AddChild(_skillDetailBadgeContainer);

			_skillDetailMetaLabel = CreateLabel("", 12, _subTextColor);
			_skillDetailMetaLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_skillDetailMetaLabel);

			_skillDetailDescriptionLabel = CreateLabel(
				"Chọn một kỹ năng ở danh sách bên trái để xem mô tả chi tiết.",
				12,
				_subTextColor);
			_skillDetailDescriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_skillDetailDescriptionLabel);

			detailColumn.AddChild(CreateSectionSpacer(4));
			detailColumn.AddChild(CreateDivider());

			var stats = new VBoxContainer();
			stats.AddThemeConstantOverride("separation", 2);
			detailColumn.AddChild(stats);
			stats.AddChild(CreateCombatStatRow("Sát thương", out _skillDamageValueLabel));
			stats.AddChild(CreateThinDivider());
			stats.AddChild(CreateCombatStatRow("Hồi chiêu", out _skillCooldownValueLabel));
			stats.AddChild(CreateThinDivider());
			stats.AddChild(CreateCombatStatRow("Tiêu hao", out _skillCostValueLabel));
			stats.AddChild(CreateThinDivider());
			stats.AddChild(CreateCombatStatRow("Thi triển", out _skillCastTimeValueLabel));

			var actionSpacer = new Control();
			actionSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
			detailColumn.AddChild(actionSpacer);

			var actions = new HBoxContainer();
			actions.AddThemeConstantOverride("separation", 8);
			detailColumn.AddChild(actions);

			_skillEquipButton = CreateActionButton("TRANG BỊ VÀO SLOT 1", EquipSelectedSkillToSlotOne);
			actions.AddChild(_skillEquipButton);

			_skillUpgradeButton = CreateActionButton("NÂNG CẤP", UpgradeSelectedSkill);
			actions.AddChild(_skillUpgradeButton);

			_skillDetailActionHintLabel = CreateLabel("", 11, _subTextColor);
			_skillDetailActionHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			detailColumn.AddChild(_skillDetailActionHintLabel);

			RefreshSkillCategoryStyles();
			UpdateSkillDetailPanel();
			return panel;
		}

		private Button CreateSkillCategoryButton(string filterId, string text)
		{
			var button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(0, 32);
			button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeFontSizeOverride("font_size", 13);
			button.Pressed += () => SelectSkillFilter(filterId);
			_skillCategoryButtons[filterId] = button;
			return button;
		}

		private void SelectSkillFilter(string filterId)
		{
			_currentSkillFilter = filterId;
			RefreshSkillCategoryStyles();
			RefreshSkillsList();
		}

		private void RefreshSkillCategoryStyles()
		{
			foreach (var pair in _skillCategoryButtons)
			{
				bool active = pair.Key == _currentSkillFilter;
				pair.Value.Text = GetSkillFilterCaption(pair.Key);
				pair.Value.AddThemeStyleboxOverride("normal", CreateSkillFilterChipStyle(active));
				pair.Value.AddThemeStyleboxOverride("hover", CreateSkillFilterChipHoverStyle(active));
				pair.Value.AddThemeStyleboxOverride("pressed", CreateSkillFilterChipHoverStyle(true));
				pair.Value.AddThemeColorOverride("font_color", active ? _mainTextColor : _subTextColor);
				pair.Value.AddThemeColorOverride("font_hover_color", _mainTextColor);
				pair.Value.AddThemeColorOverride("font_pressed_color", _mainTextColor);
			}

			if (_skillPointsLabel != null)
			{
				_skillPointsLabel.Text = $"Điểm KN: {ResolvePlayer()?.GetUnspentSkillPoints() ?? 0}";
			}
		}

		private string GetSkillFilterCaption(string filterId)
		{
			string label = filterId switch
			{
				"active" => "Chủ động",
				"passive" => "Bị động",
				"innate" => "Nội tại",
				_ => "Tất cả"
			};
			return $"{label} {GetSkillCountForFilter(filterId)}";
		}

		private int GetSkillCountForFilter(string filterId)
		{
			int count = 0;
			foreach (SkillData skill in _allSkills)
			{
				if (skill != null && (filterId == "all" || SkillMatchesFilter(skill, filterId)))
				{
					count++;
				}
			}
			return count;
		}

		private void UpdateSkillsPanel(CharacterConfig config)
		{
			_allSkills.Clear();
			AddSkillsFromCollection(config?.ActiveSkills, _allSkills);
			AddSkillsFromCollection(config?.ComboSequence, _allSkills);

			if (_selectedSkill != null && !_allSkills.Contains(_selectedSkill))
			{
				_selectedSkill = null;
			}

			RefreshSkillCategoryStyles();
			RefreshSkillsList();
		}

		private void AddSkillsFromCollection(Godot.Collections.Array<SkillData> source, List<SkillData> target)
		{
			if (source == null)
			{
				return;
			}

			foreach (SkillData skill in source)
			{
				if (skill != null && !target.Contains(skill))
				{
					target.Add(skill);
				}
			}
		}

		private void RefreshSkillsList()
		{
			if (_skillEntriesContainer == null)
			{
				return;
			}

			foreach (Node child in _skillEntriesContainer.GetChildren())
			{
				_skillEntriesContainer.RemoveChild(child);
				child.QueueFree();
			}
			_skillEntryCards.Clear();

			var filteredSkills = new List<SkillData>();
			foreach (SkillData skill in _allSkills)
			{
				if (skill != null && (_currentSkillFilter == "all" || SkillMatchesFilter(skill, _currentSkillFilter)))
				{
					filteredSkills.Add(skill);
				}
			}

			if (filteredSkills.Count == 0)
			{
				_skillEntriesContainer.AddChild(CreateSkillEmptyState());
				_selectedSkill = null;
				UpdateSkillDetailPanel();
				return;
			}

			if (_selectedSkill == null || !filteredSkills.Contains(_selectedSkill))
			{
				_selectedSkill = filteredSkills[0];
			}

			foreach (SkillData skill in filteredSkills)
			{
				_skillEntriesContainer.AddChild(CreateSkillEntry(skill));
			}

			UpdateSkillEntrySelectionVisuals();
			UpdateSkillDetailPanel();
		}

		private bool SkillMatchesFilter(SkillData skill, string filterId)
		{
			if (skill == null)
			{
				return false;
			}

			return filterId switch
			{
				"active" => skill.Category == SkillCategory.Active,
				"passive" => skill.Category == SkillCategory.Passive,
				"innate" => skill.Category == SkillCategory.Innate,
				_ => true
			};
		}

		private Control CreateSkillEntry(SkillData skill)
		{
			SkillViewModel view = BuildSkillViewModel(skill);

			var card = new PanelContainer();
			card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			card.CustomMinimumSize = new Vector2(0, 76);
			card.AddThemeStyleboxOverride("panel", CreateSkillCardStyle(skill == _selectedSkill));

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 12);
			margin.AddThemeConstantOverride("margin_top", 9);
			margin.AddThemeConstantOverride("margin_right", 12);
			margin.AddThemeConstantOverride("margin_bottom", 9);
			card.AddChild(margin);

			var row = new HBoxContainer();
			row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddThemeConstantOverride("separation", 12);
			margin.AddChild(row);
			row.AddChild(CreateSkillIconFrame(view.Icon, 48f, 34f));

			var textColumn = new VBoxContainer();
			textColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			textColumn.AddThemeConstantOverride("separation", 2);
			row.AddChild(textColumn);

			var titleRow = new HBoxContainer();
			titleRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			textColumn.AddChild(titleRow);

			var title = CreateLabel(view.Name.ToUpper(), 14, _mainTextColor);
			title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			titleRow.AddChild(title);

			string stateText = view.IsEquipped
				? $"SLOT {view.EquippedSlot + 1}"
				: view.LevelText;
			var stateLabel = CreateLabel(stateText, 11, view.IsEquipped ? GetCharacterAccentColor() : _subTextColor);
			stateLabel.HorizontalAlignment = HorizontalAlignment.Right;
			titleRow.AddChild(stateLabel);

			var subtitle = CreateLabel(view.SubtitleText, 12, _subTextColor);
			subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			textColumn.AddChild(subtitle);

			var quickStats = CreateLabel(view.QuickStatsText, 12, _mainTextColor);
			textColumn.AddChild(quickStats);

			var button = new Button();
			button.SetAnchorsPreset(LayoutPreset.FullRect);
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.Pressed += () => OnSkillEntryPressed(skill);
			card.AddChild(button);

			_skillEntryCards[skill] = card;
			return card;
		}

		private void OnSkillEntryPressed(SkillData skill)
		{
			_selectedSkill = skill;
			UpdateSkillEntrySelectionVisuals();
			UpdateSkillDetailPanel();
		}

		private void UpdateSkillEntrySelectionVisuals()
		{
			foreach (var pair in _skillEntryCards)
			{
				pair.Value.AddThemeStyleboxOverride("panel", CreateSkillCardStyle(pair.Key == _selectedSkill));
			}
		}

		private Control CreateSkillIconFrame(Texture2D texture, float frameSize, float iconSize)
		{
			var frame = new PanelContainer();
			frame.CustomMinimumSize = new Vector2(frameSize, frameSize);
			frame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			var center = new CenterContainer();
			frame.AddChild(center);

			if (texture != null)
			{
				var icon = new TextureRect();
				icon.Texture = texture;
				icon.CustomMinimumSize = new Vector2(iconSize, iconSize);
				icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
				center.AddChild(icon);
			}
			else
			{
				var fallback = CreateLabel("?", 14, _subTextColor);
				fallback.HorizontalAlignment = HorizontalAlignment.Center;
				center.AddChild(fallback);
			}
			return frame;
		}

		private Control CreateSkillEmptyState()
		{
			var panel = new PanelContainer();
			panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			panel.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());
			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 16);
			margin.AddThemeConstantOverride("margin_top", 20);
			margin.AddThemeConstantOverride("margin_right", 16);
			margin.AddThemeConstantOverride("margin_bottom", 20);
			panel.AddChild(margin);
			var label = CreateLabel("Không có kỹ năng phù hợp với bộ lọc hiện tại.", 13, _subTextColor);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			margin.AddChild(label);
			return panel;
		}

		private void UpdateSkillDetailPanel()
		{
			if (_skillDetailTitleLabel == null)
			{
				return;
			}

			RefreshSkillCategoryStyles();

			if (_selectedSkill == null)
			{
				_skillDetailTitleLabel.Text = "Chưa chọn kỹ năng";
				_skillDetailMetaLabel.Text = "";
				_skillDetailDescriptionLabel.Text = "Chọn một kỹ năng ở danh sách bên trái để xem mô tả chi tiết.";
				_skillDamageValueLabel.Text = "-";
				_skillCooldownValueLabel.Text = "-";
				_skillCostValueLabel.Text = "-";
				_skillCastTimeValueLabel.Text = "-";
				_skillDetailIconRect.Texture = null;
				_skillDetailIconRect.Visible = false;
				_skillDetailIconFallbackLabel.Visible = true;
				ClearSkillDetailBadges();
				_skillEquipButton.Disabled = true;
				_skillUpgradeButton.Visible = false;
				_skillDetailActionHintLabel.Text = "";
				return;
			}

			SkillViewModel view = BuildSkillViewModel(_selectedSkill);
			_skillDetailTitleLabel.Text = view.MaxLevel > 1
				? $"{view.Name.ToUpper()} · LV.{view.Level}/{view.MaxLevel}"
				: view.Name.ToUpper();
			_skillDetailMetaLabel.Text = view.BonusSummary;
			_skillDetailDescriptionLabel.Text = string.IsNullOrWhiteSpace(_selectedSkill.Description)
				? "Kỹ năng này chưa có mô tả."
				: _selectedSkill.Description;
			_skillDamageValueLabel.Text = view.DamageText;
			_skillCooldownValueLabel.Text = view.CooldownText;
			_skillCostValueLabel.Text = view.CostText;
			_skillCastTimeValueLabel.Text = view.CastTimeText;

			_skillDetailIconRect.Texture = view.Icon;
			_skillDetailIconRect.Visible = view.Icon != null;
			_skillDetailIconFallbackLabel.Visible = view.Icon == null;
			PopulateSkillDetailBadges(view);
			UpdateSkillActionButtons(view);
		}

		private SkillViewModel BuildSkillViewModel(SkillData skill)
		{
			Player player = ResolvePlayer();
			PlayerSkillState state = player?.GetSkillState(skill);
			return new SkillViewModel(skill, state);
		}

		private void UpdateSkillActionButtons(SkillViewModel view)
		{
			Player player = ResolvePlayer();
			bool canManage = player != null && player.OwnsSkill(view.Definition);

			_skillEquipButton.Visible = view.Definition.Category == SkillCategory.Active;
			_skillEquipButton.Disabled = !canManage || !view.CanEquip || view.EquippedSlot == 0;
			_skillEquipButton.Text = view.EquippedSlot switch
			{
				0 => "ĐÃ TRANG BỊ SLOT 1",
				> 0 => "CHUYỂN SANG SLOT 1",
				_ => "TRANG BỊ VÀO SLOT 1"
			};

			_skillUpgradeButton.Visible = view.MaxLevel > 1;
			_skillUpgradeButton.Disabled = !canManage
				|| !view.IsUnlocked
				|| view.Level >= view.MaxLevel
				|| (player?.GetUnspentSkillPoints() ?? 0) <= 0;
			_skillUpgradeButton.Text = view.Level >= view.MaxLevel
				? "ĐÃ ĐẠT CẤP TỐI ĐA"
				: "NÂNG CẤP · 1 ĐIỂM";

			if (!canManage)
			{
				_skillDetailActionHintLabel.Text = "Kỹ năng này không thuộc loadout của Player hiện tại.";
			}
			else if (!view.IsUnlocked)
			{
				_skillDetailActionHintLabel.Text = "Kỹ năng chưa được mở khóa.";
			}
			else if (view.Definition.Category != SkillCategory.Active)
			{
				_skillDetailActionHintLabel.Text = "Kỹ năng bị động và nội tại không cần gắn vào slot chủ động.";
			}
			else if (view.IsEquipped)
			{
				_skillDetailActionHintLabel.Text = $"Đang trang bị ở slot {view.EquippedSlot + 1}.";
			}
			else
			{
				_skillDetailActionHintLabel.Text = "Trang bị vào slot 1 sẽ thay kỹ năng đang chiếm slot đó, nếu có.";
			}
		}

		private void EquipSelectedSkillToSlotOne()
		{
			Player player = ResolvePlayer();
			if (_selectedSkill == null || player == null || !player.TryEquipSkill(_selectedSkill, 0))
			{
				return;
			}

			RefreshSkillsList();
		}

		private void UpgradeSelectedSkill()
		{
			Player player = ResolvePlayer();
			if (_selectedSkill == null || player == null || !player.TryUpgradeSkill(_selectedSkill))
			{
				return;
			}

			RefreshSkillsList();
		}

		private void ClearSkillDetailBadges()
		{
			if (_skillDetailBadgeContainer == null)
			{
				return;
			}

			foreach (Node child in _skillDetailBadgeContainer.GetChildren())
			{
				_skillDetailBadgeContainer.RemoveChild(child);
				child.QueueFree();
			}
		}

		private void PopulateSkillDetailBadges(SkillViewModel view)
		{
			ClearSkillDetailBadges();
			foreach (string badgeText in view.GetBadges())
			{
				_skillDetailBadgeContainer.AddChild(CreateSkillBadge(badgeText));
			}
		}

		private Control CreateSkillBadge(string text)
		{
			var badge = new PanelContainer();
			badge.AddThemeStyleboxOverride("panel", CreateSkillBadgeStyle());
			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 8);
			margin.AddThemeConstantOverride("margin_top", 3);
			margin.AddThemeConstantOverride("margin_right", 8);
			margin.AddThemeConstantOverride("margin_bottom", 3);
			badge.AddChild(margin);
			margin.AddChild(CreateLabel(text, 11, _mainTextColor));
			return badge;
		}

		private StyleBoxFlat CreateSkillFilterChipStyle(bool active)
		{
			Color accent = GetCharacterAccentColor();
			return InventoryPanelChrome.CreateButtonStyle(
				active ? new Color(accent.R, accent.G, accent.B, 0.18f) : InventoryPanelChrome.ButtonNormalColor,
				active ? accent : _borderColor,
				active ? 2 : 1);
		}

		private StyleBoxFlat CreateSkillFilterChipHoverStyle(bool active)
		{
			Color accent = GetCharacterAccentColor();
			return InventoryPanelChrome.CreateButtonStyle(
				InventoryPanelChrome.ButtonHoverColor,
				active ? accent : InventoryPanelChrome.StrongBorderColor,
				active ? 2 : 1);
		}

		private StyleBoxFlat CreateSkillCardStyle(bool selected)
		{
			Color accent = GetCharacterAccentColor();
			var style = new StyleBoxFlat();
			style.BgColor = selected
				? new Color(accent.R, accent.G, accent.B, 0.12f)
				: InventoryPanelChrome.SlotSurfaceColor;
			style.BorderColor = selected ? accent : _borderColor.Darkened(0.05f);
			style.SetBorderWidthAll(selected ? 2 : 1);
			style.SetCornerRadiusAll(3);
			if (selected)
			{
				style.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.20f);
				style.ShadowSize = 4;
			}
			return style;
		}

		private StyleBoxFlat CreateSkillBadgeStyle()
		{
			Color accent = GetCharacterAccentColor();
			var style = new StyleBoxFlat();
			style.BgColor = new Color(accent.R, accent.G, accent.B, 0.14f);
			style.BorderColor = new Color(accent.R, accent.G, accent.B, 0.55f);
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(99);
			return style;
		}

		private Color GetCharacterAccentColor()
		{
			bool themeIsReadable = _currentThemeColor.A > 0.1f
				&& Mathf.Max(_currentThemeColor.R, Mathf.Max(_currentThemeColor.G, _currentThemeColor.B)) > 0.22f;
			return themeIsReadable ? _currentThemeColor : _accentColor;
		}

		private Button CreateActionButton(string text, System.Action onPressed)
		{
			var button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(0, 34);
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateButtonStyle(InventoryPanelChrome.ButtonNormalColor, _borderColor, 1));
			button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateButtonStyle(InventoryPanelChrome.ButtonHoverColor, _accentColor, 1));
			button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateButtonStyle(_deepSurfaceColor, _accentColor, 1));
			button.AddThemeColorOverride("font_color", _mainTextColor);
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			if (onPressed != null)
			{
				button.Pressed += onPressed;
			}
			return button;
		}

		private StyleBoxFlat GetCommonPanelStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color(0f, 0f, 0f, 0f);
			return style;
		}


		private Button CreateTabButton(string text)
		{
			var button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(126, 36);
			button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			ApplyTabStyle(button, false);
			return button;
		}

		private void ApplyTabStyle(Button button, bool active)
		{
			if (button == null) return;

			// Tab active chỉ dùng một gạch chân sáng, tránh đóng hộp mọi thứ.
			button.AddThemeStyleboxOverride("normal", CreateCharacterTabStyle(active, false));
			button.AddThemeStyleboxOverride("hover", CreateCharacterTabStyle(active, true));
			button.AddThemeStyleboxOverride("pressed", CreateCharacterTabStyle(true, true));
			button.AddThemeColorOverride("font_color", active ? _mainTextColor : _subTextColor);
			button.AddThemeColorOverride("font_hover_color", _mainTextColor);
			button.AddThemeColorOverride("font_pressed_color", _accentColor);
			button.AddThemeFontSizeOverride("font_size", 14);
		}

		private StyleBoxFlat CreateCharacterTabStyle(bool active, bool hovered)
		{
			var style = new StyleBoxFlat();
			style.BgColor = hovered
				? new Color(_deepSurfaceColor.R, _deepSurfaceColor.G, _deepSurfaceColor.B, 0.44f)
				: new Color(0f, 0f, 0f, 0f);
			style.BorderColor = active ? _accentColor : new Color(0f, 0f, 0f, 0f);
			style.BorderWidthBottom = active ? 2 : 0;
			style.ContentMarginLeft = 14;
			style.ContentMarginRight = 14;
			style.ContentMarginTop = 7;
			style.ContentMarginBottom = 7;
			return style;
		}

		private void SwitchTab(string tabName)
		{
			HidePanel(_overviewPanel);
			HidePanel(_equipmentPanel);
			HidePanel(_skillsPanel);
			_currentTab = tabName;

			switch (tabName)
			{
				case "equipment":
					ShowPanel(_equipmentPanel);
					RefreshInventoryGrid();
					RefreshEquipmentSlots();
					break;
				case "skills":
					ShowPanel(_skillsPanel);
					RefreshSkillsList();
					UpdateSkillDetailPanel();
					break;
				default:
					_currentTab = "overview";
					ShowPanel(_overviewPanel);
					break;
			}
			if (_overviewFooter != null)
			{
				_overviewFooter.Visible = _currentTab == "overview";
			}
			ResetTabButtonColors();
		}

		private void HidePanel(Control panel)
		{
			if (panel != null)
			{
				panel.Visible = false;
				panel.ProcessMode = ProcessModeEnum.Disabled;
				panel.MouseFilter = MouseFilterEnum.Ignore;
			}
		}

		private void ShowPanel(Control panel)
		{
			if (panel != null)
			{
				panel.Visible = true;
				panel.ProcessMode = ProcessModeEnum.Inherit;
				panel.MouseFilter = MouseFilterEnum.Pass;
			}
		}
		private void ResetTabButtonColors()
		{
			ApplyTabStyle(_btnOverview, _currentTab == "overview");
			ApplyTabStyle(_btnEquipment, _currentTab == "equipment");
			ApplyTabStyle(_btnSkills, _currentTab == "skills");
		}

		// Cập nhật style của các panel với màu theme mới
		private void UpdatePanelStyles()
		{
			Color characterAccent = GetCharacterAccentColor();
			_levelLabel?.AddThemeColorOverride("font_color", characterAccent);
			ResetTabButtonColors();

			// Theme nhân vật cũng được dùng cho chip, badge và card đang chọn.
			RefreshSkillCategoryStyles();
			UpdateSkillEntrySelectionVisuals();
			UpdateSkillDetailPanel();
		}

		private void LoadCharacterList()
		{
			if (_characterListContainer == null || PlayerManager.Instance == null) return;
			foreach (var child in _characterListContainer.GetChildren()) child.QueueFree();

			for (int i = 0; i < PlayerManager.Instance.PartyMembers.Count; i++)
			{
				int index = i;
				var character = PlayerManager.Instance.PartyMembers[i];
				bool active = index == PlayerManager.Instance.ActiveCharacterIndex;

				var button = new Button();
				button.CustomMinimumSize = new Vector2(58, 58);
				button.FocusMode = FocusModeEnum.None;
				button.MouseDefaultCursorShape = CursorShape.PointingHand;
				button.Icon = character?.ConfigData?.Icon;
				button.ExpandIcon = true;
				button.IconAlignment = HorizontalAlignment.Center;
				button.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
				button.AddThemeConstantOverride("icon_max_width", 46);
				string characterName = character?.ConfigData?.Name;
				button.Text = button.Icon == null ? (!string.IsNullOrEmpty(characterName) ? characterName.Substring(0, 1).ToUpper() : "?") : "";
				button.TooltipText = character?.ConfigData?.Name ?? "Nhân vật";
				button.AddThemeStyleboxOverride("normal", CreateSlotStyle(active));
				button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateSlotHoverStyle());
				button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateSlotPressedStyle());
				button.AddThemeColorOverride("font_color", _mainTextColor);
				button.Pressed += () => OnCharacterSelected(index);
				_characterListContainer.AddChild(button);
			}

			// Ô "+" chỉ là placeholder cho chức năng thêm thành viên sau này.
			var addMember = new Button();
			addMember.Text = "+";
			addMember.CustomMinimumSize = new Vector2(48, 48);
			addMember.FocusMode = FocusModeEnum.None;
			addMember.Disabled = true;
			addMember.TooltipText = "Chưa có chức năng thêm thành viên";
			addMember.AddThemeStyleboxOverride("normal", CreateSlotStyle());
			addMember.AddThemeColorOverride("font_color", _subTextColor);
			addMember.AddThemeFontSizeOverride("font_size", 18);
			_characterListContainer.AddChild(addMember);
		}

		private void OnCharacterSelected(int index)
		{
			if (PlayerManager.Instance == null) return;
			PlayerManager.Instance.SetActiveCharacter(index);
			UpdateCharacterInfo();
		}

		private void OnVisibilityChanged()
		{
			if (Visible) UpdateCharacterInfo();
		}

		public void UpdateCharacterInfo()
		{
			if (PlayerManager.Instance == null) return;
			var activeIndex = PlayerManager.Instance.ActiveCharacterIndex;
			if (activeIndex < 0 || activeIndex >= PlayerManager.Instance.PartyMembers.Count) return;

			PlayerStats currentStats = PlayerManager.Instance.PartyMembers[activeIndex];
			if (currentStats == null || currentStats.ConfigData == null) return;
			BindObservedStats(currentStats);

			var config = currentStats.ConfigData;

			_nameLabel.Text = config.Name;
			_levelLabel.Text = $"Cấp {currentStats.CurrentLevel:00}";
			_sidebarNameLabel.Text = config.Name;
			_sidebarLevelLabel.Text = $"Cấp {currentStats.CurrentLevel:00}";
			_raceLabel.Text = config.CharacterRace?.RaceName ?? "Không rõ";
			
			// Cập nhật theme color từ character config
			_currentThemeColor = config.ThemeColor;
			
			// Cập nhật background từ character config
			if (_backgroundDisplay != null)
			{
				_backgroundDisplay.Texture = config.BackgroundImage ?? config.Icon;
				_backgroundDisplay.Visible = _backgroundDisplay.Texture != null;
			}
			if (_portraitPlaceholderLabel != null)
			{
				_portraitPlaceholderLabel.Visible = _backgroundDisplay?.Texture == null;
			}
			
			// Cập nhật style panel với màu theme mới
			UpdatePanelStyles();
			UpdateOverviewPanel(currentStats);
			UpdateSkillsPanel(config);
			UpdateEquipmentBody(config);
			RefreshInventoryGrid();
			RefreshEquipmentSlots();
			LoadCharacterList();
			SwitchTab(_currentTab);
		}

		/// <summary>
		/// Cập nhật body nhân vật (AnimatedSprite2D) trong tab Equipment
		/// Load BodyScene từ CharacterConfig, instantiate và play animation "Idle"
		/// </summary>
		private void UpdateEquipmentBody(AshesofaDyingWorld.Core.Data.CharacterConfig config)
		{
			if (_equipmentBodyContainer == null) return;

			// Xoá body cũ
			foreach (var child in _equipmentBodyContainer.GetChildren())
				child.QueueFree();

			if (config?.BodyScene == null) return;

			// Instantiate body scene (AnimatedSprite2D)
			var bodyNode = config.BodyScene.Instantiate();

			// Tìm AnimatedSprite2D trong scene (có thể là root hoặc con)
			AnimatedSprite2D bodySprite = bodyNode as AnimatedSprite2D;
			if (bodySprite == null)
				bodySprite = bodyNode.GetNodeOrNull<AnimatedSprite2D>(".");

			if (bodySprite == null)
			{
				// Tìm AnimatedSprite2D ở bất kỳ đâu trong scene tree con
				foreach (var child in bodyNode.GetChildren())
				{
					if (child is AnimatedSprite2D sprite)
					{
						bodySprite = sprite;
						break;
					}
				}
			}

			// Đặt vị trí ở giữa container nhỏ, scale vừa phải
			if (bodyNode is Node2D body2D)
			{
				body2D.Position = new Vector2(
					_equipmentBodyContainer.CustomMinimumSize.X / 2f,
					_equipmentBodyContainer.CustomMinimumSize.Y * 0.7f
				);
				body2D.Scale = new Vector2(3f, 3f); // Scale nhỏ hơn cho vừa container
			}

			_equipmentBodyContainer.AddChild(bodyNode);

			// Play animation Idle
			if (bodySprite != null && bodySprite.SpriteFrames != null)
			{
				if (bodySprite.SpriteFrames.HasAnimation("Idle"))
					bodySprite.Play("Idle");
				else if (bodySprite.SpriteFrames.HasAnimation("go_down"))
					bodySprite.Play("go_down"); // Fallback nếu không có Idle
			}
		}

		private void UpdateOverviewPanel(PlayerStats stats)
		{
			foreach (var child in _statsTextContainer.GetChildren()) child.QueueFree();

			if (stats.FinalAttributes != null)
			{
				foreach (var attr in stats.FinalAttributes)
				{
					var row = new HBoxContainer();
					row.CustomMinimumSize = new Vector2(0, 38);
					row.AddThemeConstantOverride("separation", 9);

					string shortName = FormatStatName(attr.Key.ToString());
					var iconTexture = LoadStatIcon(shortName);
					if (iconTexture != null)
					{
						var icon = new TextureRect();
						icon.Texture = iconTexture;
						icon.CustomMinimumSize = new Vector2(22, 22);
						icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
						icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
						row.AddChild(icon);
					}

					var name = CreateLabel(shortName, 14, _subTextColor);
					name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
					name.VerticalAlignment = VerticalAlignment.Center;
					row.AddChild(name);
					var value = CreateLabel(attr.Value.ToString(), 15, _mainTextColor);
					value.CustomMinimumSize = new Vector2(54, 0);
					value.HorizontalAlignment = HorizontalAlignment.Right;
					value.VerticalAlignment = VerticalAlignment.Center;
					row.AddChild(value);
					_statsTextContainer.AddChild(row);
					_statsTextContainer.AddChild(CreateThinDivider());
				}
			}

			// Công vật lý dùng giá trị đã tính của PlayerStats.
			if (_attackValueLabel != null)
				_attackValueLabel.Text = Mathf.RoundToInt(stats.AttackDamage).ToString();

			// Wireframe hiển thị số nguyên, vì vậy Tốc độ dùng DEX và Kháng phép dùng SPI.
			// Sau này nếu có stat riêng, chỉ cần đổi hai dòng lấy dữ liệu bên dưới.
			int dexterity = 0;
			int spirit = 0;
			stats.FinalAttributes?.TryGetValue(AttributeType.Dexterity, out dexterity);
			stats.FinalAttributes?.TryGetValue(AttributeType.Spirit, out spirit);
			if (_speedValueLabel != null)
				_speedValueLabel.Text = dexterity.ToString();
			if (_armorValueLabel != null)
				_armorValueLabel.Text = spirit.ToString();

			// Project hiện chưa có trường điểm thuộc tính chưa dùng, nên tạm hiển thị 0.
			// Khi thêm hệ thống tăng điểm, chỉ cần gán giá trị thật cho label này.
			if (_unspentAttributePointsLabel != null)
				_unspentAttributePointsLabel.Text = "0 điểm thuộc tính chưa sử dụng";

			UpdateResourceBars(stats);
		}

		private void UpdateResourceBars(PlayerStats stats)
		{
			SetBarValue(_hpBar, _hpValueLabel, (int)stats.CurrentHP, (int)stats.MaxHP);
			SetBarValue(_mpBar, _mpValueLabel, (int)stats.CurrentMP, (int)stats.MaxMP);
			SetBarValue(_staminaBar, _staminaValueLabel, (int)stats.CurrentStamina, (int)stats.MaxStamina);

		}
		/// <summary>
		/// Cập nhật giá trị cho thanh ảnh HP/MP/STA. TextureProgressBar tự cắt ảnh
		/// từ trái sang phải theo tỉ lệ Value / MaxValue.
		/// </summary>
		private void SetBarValue(TextureProgressBar bar, Label valueLabel, int current, int max)
		{
			max = Mathf.Max(1, max);
			current = Mathf.Clamp(current, 0, max);

			if (bar != null)
			{
				bar.MinValue = 0;
				bar.MaxValue = max;
				bar.Value = current;
			}

			if (valueLabel != null)
			{
				valueLabel.Text = $"{current}/{max}";
			}
		}

		/// <summary>
		/// Tạo một hàng stat từ hai PNG: ảnh khung/icon luôn hiển thị và
		/// ảnh màu được TextureProgressBar cắt theo phần trăm hiện tại.
		/// </summary>
		private Control CreateResourceBarRow(
			string statName,
			string frameFile,
			string progressFile,
			string vietnameseDescription,
			out TextureProgressBar bar,
			out Label valueLabel)
		{
			var row = new Control();
			row.CustomMinimumSize = new Vector2(-25, 10);
			row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.MouseFilter = MouseFilterEnum.Ignore;

			// Nạp đúng tên file chữ thường như trong thư mục thật:
			// hp ic.png + hp.png, mp ic.png + mp.png, sta ic.png + sta.png.
			Texture2D frameTexture = LoadMainStatTexture(statName, frameFile);
			Texture2D progressTexture = LoadMainStatTexture(statName, progressFile);

			bar = new TextureProgressBar();
			bar.SetAnchorsPreset(LayoutPreset.FullRect);
			bar.TextureUnder = frameTexture;
			bar.TextureProgress = progressTexture;
			bar.NinePatchStretch = true;
			bar.MinValue = 0;
			bar.MaxValue = 100;
			bar.Value = 100;
			bar.TooltipText = $"{statName} - {vietnameseDescription}";
			bar.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			bar.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(bar);

			// Số hiện tại/tối đa được đặt trên cùng, không phụ thuộc texture có tải được hay không.
			valueLabel = CreateLabel("0/0", 12, Colors.White);
			valueLabel.SetAnchorsPreset(LayoutPreset.FullRect);
			valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
			valueLabel.VerticalAlignment = VerticalAlignment.Center;
			valueLabel.MouseFilter = MouseFilterEnum.Ignore;
			valueLabel.AddThemeConstantOverride("outline_size", 4);
			valueLabel.AddThemeColorOverride(
				"font_outline_color", new Color(0.02f, 0.02f, 0.03f, 0.95f));
			row.AddChild(valueLabel);

			return row;
		}

		/// <summary>
		/// Nạp texture trong thư mục 3 main stat. Ưu tiên đường dẫn chính xác,
		/// sau đó quét tên file không phân biệt hoa/thường để tránh asset cũ như HP.png.
		/// </summary>
		private Texture2D LoadMainStatTexture(string statName, string fileName)
		{
			string exactPath = $"{MainStatTextureRoot}/{fileName}";
			Texture2D exactTexture = TryLoadTexture(exactPath);
			if (exactTexture != null)
			{
				GD.Print($"[CharacterDetailUI] Đã nạp ảnh {statName}: {exactPath}");
				return exactTexture;
			}

			// Fallback cho asset cũ có chữ hoa như HP.png hoặc tên lệch hoa/thường.
			string discoveredPath = FindTexturePathRecursive(MainStatTextureRoot, fileName, 0);
			if (!string.IsNullOrEmpty(discoveredPath))
			{
				Texture2D discoveredTexture = TryLoadTexture(discoveredPath);
				if (discoveredTexture != null)
				{
					GD.Print($"[CharacterDetailUI] Đã tự tìm ảnh {statName}: {discoveredPath}");
					return discoveredTexture;
				}
			}

			GD.PrintErr(
				$"[CharacterDetailUI] Không tìm thấy {fileName} trong {MainStatTextureRoot}. " +
				"Hãy kiểm tra tên file và chờ Godot import xong.");
			return null;
		}

		/// <summary>
		/// Tìm file theo tên không phân biệt chữ hoa/thường.
		/// </summary>
		private string FindTexturePathRecursive(
			string directoryPath,
			string targetFileName,
			int depth)
		{
			if (depth > 2)
			{
				return string.Empty;
			}

			DirAccess directory = DirAccess.Open(directoryPath);
			if (directory == null)
			{
				return string.Empty;
			}

			directory.ListDirBegin();
			string entryName = directory.GetNext();
			while (!string.IsNullOrEmpty(entryName))
			{
				if (entryName != "." && entryName != "..")
				{
					string entryPath = $"{directoryPath}/{entryName}";
					if (directory.CurrentIsDir())
					{
						string nestedResult = FindTexturePathRecursive(
							entryPath, targetFileName, depth + 1);
						if (!string.IsNullOrEmpty(nestedResult))
						{
							directory.ListDirEnd();
							return nestedResult;
						}
					}
					else if (string.Equals(
						entryName,
						targetFileName,
						System.StringComparison.OrdinalIgnoreCase))
					{
						directory.ListDirEnd();
						return entryPath;
					}
				}

				entryName = directory.GetNext();
			}

			directory.ListDirEnd();
			return string.Empty;
		}

		private string FormatStatName(string original)
		{
			return original switch
			{
				"Strength" => "STR",
				"Intelligence" => "INT",
				"Dexterity" => "DEX",
				"Vitality" => "VIT",
				"Spirit" => "SPI",
				_ => original.Substring(0, Mathf.Min(3, original.Length)).ToUpper()
			};
		}

		// Load icon cho stat dựa trên tên viết tắt
		private Texture2D LoadStatIcon(string statShortName)
		{
			string iconPath = statShortName switch
			{
				"STR" => "res://assets/resources/data/icon/STR .tres",
				"INT" => "res://assets/resources/data/icon/INT.tres",
				"DEX" => "res://assets/resources/data/icon/DEX.tres",
				"VIT" => "res://assets/resources/data/icon/VIT.tres",
				"DEF" => "res://assets/resources/data/icon/DEF.tres",
				"SPI" => "res://assets/resources/data/icon/SPI.tres",
				_ => null
			};

			if (!string.IsNullOrEmpty(iconPath))
			{
				var resource = GD.Load(iconPath);
				// .tres có thể là Texture2D hoặc AtlasTexture
				if (resource is Texture2D texture)
				{
					return texture;
				}
				else if (resource is AtlasTexture atlasTexture)
				{
					return atlasTexture;
				}
			}
			
			return null;
		}

		private void BindObservedStats(PlayerStats stats)
		{
			if (_observedStats == stats)
			{
				return;
			}

			if (_observedStats != null)
			{
				_observedStats.StatsChanged -= OnObservedStatsChanged;
			}

			_observedStats = stats;

			if (_observedStats != null)
			{
				_observedStats.StatsChanged += OnObservedStatsChanged;
			}
		}

		private void OnObservedStatsChanged()
		{
			if (!Visible)
			{
				return;
			}

			UpdateCharacterInfo();
		}

		private void OnActiveCharacterChanged(int index)
		{
			if (!Visible)
			{
				return;
			}

			UpdateCharacterInfo();
		}
	}
}