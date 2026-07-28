using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;
using AshesofaDyingWorld.UI.Shared;

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
		private VBoxContainer _statsTextContainer;

		// bar gốc (Hp, Mana, Stamina)
		private VBoxContainer _resourceBarsContainer;
		private ProgressBar _hpBar;
		private ProgressBar _mpBar;
		private ProgressBar _staminaBar;
		private Label _hpValueLabel;
		private Label _mpValueLabel;
		private Label _staminaValueLabel;
		private VBoxContainer _skillsListContainer;
		
		// Chart
		private StatHexagonChart _overviewStatsChart; 
		
		// Layout cố định, đồng bộ với InventoryPanel cho viewport mục tiêu 1600 x 900.
		private const float DetailPanelWidth = InventoryPanelChrome.DetailPanelWidth;
		private const string CharacterIconPath = "res://assets/sprites/button/characterbutton.png";

		// Design tokens dùng chung tinh thần với InventoryPanel.
		private Color _deepSurfaceColor => InventoryPanelChrome.DeepSurfaceColor;
		private Color _borderColor => InventoryPanelChrome.BorderColor;
		private Color _accentColor => InventoryPanelChrome.AccentColor;
		private Color _mainTextColor => InventoryPanelChrome.MainTextColor;
		private Color _subTextColor => InventoryPanelChrome.MutedTextColor;
		private string _currentTab = "overview";

		private Color _currentThemeColor;
		private Texture2D _characterIconTexture;
		private TextureRect _headerIcon;
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
			// Đây không còn là bản "lấy cảm hứng" nữa. Character và Inventory dùng
			// chung đúng một shell: nền, grain, margin và frame 9-slice.
			var root = InventoryPanelChrome.BuildWindowShell(this);
			root.AddChild(BuildCharacterHeader());
			root.AddChild(BuildCharacterTabs());
			root.AddChild(BuildCharacterBody());
		}

		private Control BuildCharacterHeader()
		{
			var headerPanel = InventoryPanelChrome.CreateHeader(out var row);

			_headerIcon = new TextureRect();
			_characterIconTexture ??= TryLoadTexture(CharacterIconPath);
			_headerIcon.Texture = _characterIconTexture;
			_headerIcon.CustomMinimumSize = new Vector2(24, 24);
			_headerIcon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			_headerIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_headerIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			row.AddChild(_headerIcon);

			var title = CreateLabel("CHARACTER", 22, _mainTextColor);
			title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			title.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(title);

			var identity = new VBoxContainer();
			identity.Alignment = BoxContainer.AlignmentMode.Center;
			identity.AddThemeConstantOverride("separation", 0);
			row.AddChild(identity);

			_nameLabel = CreateLabel("NHÂN VẬT", 18, _mainTextColor);
			_nameLabel.HorizontalAlignment = HorizontalAlignment.Right;
			identity.AddChild(_nameLabel);

			var meta = new HBoxContainer();
			meta.Alignment = BoxContainer.AlignmentMode.End;
			meta.AddThemeConstantOverride("separation", 8);
			identity.AddChild(meta);

			_levelLabel = CreateLabel("LV. 00", 12, _accentColor);
			meta.AddChild(_levelLabel);
			meta.AddChild(CreateLabel("·", 12, _subTextColor));
			_raceLabel = CreateLabel("UNKNOWN", 12, _subTextColor);
			meta.AddChild(_raceLabel);

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
			// Body bám đúng tỉ lệ InventoryPanel: vùng nội dung lớn bên trái,
			// detail panel cố định 330 px bên phải. Không còn party rail riêng.
			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 8);

			var mainPanel = new PanelContainer();
			mainPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			mainPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			mainPanel.AddThemeStyleboxOverride("panel", CreateSectionStyle());
			body.AddChild(mainPanel);

			var mainMargin = new MarginContainer();
			mainMargin.AddThemeConstantOverride("margin_left", 14);
			mainMargin.AddThemeConstantOverride("margin_top", 14);
			mainMargin.AddThemeConstantOverride("margin_right", 14);
			mainMargin.AddThemeConstantOverride("margin_bottom", 14);
			mainPanel.AddChild(mainMargin);

			var content = new Control();
			content.SetAnchorsPreset(LayoutPreset.FullRect);
			content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			content.SizeFlagsVertical = SizeFlags.ExpandFill;
			mainMargin.AddChild(content);

			_overviewPanel = CreateOverviewPanel();
			content.AddChild(_overviewPanel);
			_equipmentPanel = CreateEquipmentPanel();
			content.AddChild(_equipmentPanel);
			_skillsPanel = CreateSkillsPanelLayout();
			content.AddChild(_skillsPanel);

			var sidebar = BuildCharacterSidebar();
			sidebar.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			sidebar.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddChild(sidebar);
			return body;
		}

		private PanelContainer BuildCharacterSidebar()
		{
			var panel = new PanelContainer();
			panel.CustomMinimumSize = new Vector2(DetailPanelWidth, 0);
			panel.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 16);
			margin.AddThemeConstantOverride("margin_top", 16);
			margin.AddThemeConstantOverride("margin_right", 16);
			margin.AddThemeConstantOverride("margin_bottom", 14);
			panel.AddChild(margin);

			var column = new VBoxContainer();
			column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			column.SizeFlagsVertical = SizeFlags.ExpandFill;
			column.AddThemeConstantOverride("separation", 9);
			margin.AddChild(column);

			var previewFrame = new PanelContainer();
			previewFrame.CustomMinimumSize = new Vector2(0, 150);
			previewFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			column.AddChild(previewFrame);

			_backgroundDisplay = new TextureRect();
			_backgroundDisplay.SetAnchorsPreset(LayoutPreset.FullRect);
			_backgroundDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_backgroundDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			_backgroundDisplay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			_backgroundDisplay.Modulate = new Color(0.92f, 0.88f, 0.8f, 0.9f);
			_backgroundDisplay.MouseFilter = MouseFilterEnum.Ignore;
			previewFrame.AddChild(_backgroundDisplay);

			column.AddChild(CreateLabel("PARTY", 14, _mainTextColor));
			column.AddChild(CreateDivider());

			_characterListContainer = new HBoxContainer();
			_characterListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_characterListContainer.Alignment = BoxContainer.AlignmentMode.Begin;
			_characterListContainer.AddThemeConstantOverride("separation", 8);
			column.AddChild(_characterListContainer);

			column.AddChild(CreateLabel("ATTRIBUTES", 14, _mainTextColor));
			column.AddChild(CreateDivider());

			var chartFrame = new PanelContainer();
			chartFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
			chartFrame.CustomMinimumSize = new Vector2(0, 215);
			chartFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			column.AddChild(chartFrame);

			var chartMargin = new MarginContainer();
			chartMargin.AddThemeConstantOverride("margin_left", 6);
			chartMargin.AddThemeConstantOverride("margin_top", 6);
			chartMargin.AddThemeConstantOverride("margin_right", 6);
			chartMargin.AddThemeConstantOverride("margin_bottom", 6);
			chartFrame.AddChild(chartMargin);

			_overviewStatsChart = new StatHexagonChart();
			_overviewStatsChart.MainColor = _accentColor;
			_overviewStatsChart.ChartRadiusOffset = 30f;
			_overviewStatsChart.FontSize = 9;
			_overviewStatsChart.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_overviewStatsChart.SizeFlagsVertical = SizeFlags.ExpandFill;
			chartMargin.AddChild(_overviewStatsChart);
			return panel;
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

		// Nội dung chính bên dưới tab bar.
		private Control CreateOverviewPanel()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 14);
			panel.AddChild(body);

			var statsColumn = new VBoxContainer();
			statsColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			statsColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
			statsColumn.AddThemeConstantOverride("separation", 9);
			body.AddChild(statsColumn);
			statsColumn.AddChild(CreateLabel("THUỘC TÍNH", 15, _mainTextColor));
			statsColumn.AddChild(CreateDivider());

			_statsTextContainer = new VBoxContainer();
			_statsTextContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_statsTextContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			_statsTextContainer.AddThemeConstantOverride("separation", 0);
			statsColumn.AddChild(_statsTextContainer);

			var resourcesPanel = new PanelContainer();
			resourcesPanel.CustomMinimumSize = new Vector2(300, 0);
			resourcesPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			resourcesPanel.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());
			body.AddChild(resourcesPanel);

			var resourceMargin = new MarginContainer();
			resourceMargin.AddThemeConstantOverride("margin_left", 16);
			resourceMargin.AddThemeConstantOverride("margin_top", 16);
			resourceMargin.AddThemeConstantOverride("margin_right", 16);
			resourceMargin.AddThemeConstantOverride("margin_bottom", 16);
			resourcesPanel.AddChild(resourceMargin);

			var resourceColumn = new VBoxContainer();
			resourceColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			resourceColumn.AddThemeConstantOverride("separation", 12);
			resourceMargin.AddChild(resourceColumn);
			resourceColumn.AddChild(CreateLabel("TÀI NGUYÊN", 15, _mainTextColor));
			resourceColumn.AddChild(CreateDivider());

			_resourceBarsContainer = new VBoxContainer();
			_resourceBarsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_resourceBarsContainer.AddThemeConstantOverride("separation", 12);
			resourceColumn.AddChild(_resourceBarsContainer);
			_resourceBarsContainer.AddChild(CreateResourceBarRow("HP", new Color("#b85348"), out _hpBar, out _hpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow("MP", new Color("#4f7896"), out _mpBar, out _mpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow("STA", new Color("#657d4d"), out _staminaBar, out _staminaValueLabel));

			var resourceSpacer = new Control();
			resourceSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
			resourceColumn.AddChild(resourceSpacer);
			var hint = CreateLabel("Chọn thành viên ở panel bên phải để xem chỉ số.", 12, _subTextColor);
			hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			resourceColumn.AddChild(hint);
			return panel;
		}

		private Control CreateEquipmentPanel()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.AddThemeConstantOverride("separation", 12);
			panel.AddChild(body);

			var loadoutColumn = new VBoxContainer();
			loadoutColumn.CustomMinimumSize = new Vector2(335, 0);
			loadoutColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
			loadoutColumn.AddThemeConstantOverride("separation", 9);
			body.AddChild(loadoutColumn);
			loadoutColumn.AddChild(CreateLabel("TRANG BỊ ĐANG MẶC", 15, _mainTextColor));
			loadoutColumn.AddChild(CreateDivider());

			var loadoutPanel = new PanelContainer();
			loadoutPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			loadoutPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			loadoutPanel.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());
			loadoutColumn.AddChild(loadoutPanel);

			var loadoutMargin = new MarginContainer();
			loadoutMargin.AddThemeConstantOverride("margin_left", 12);
			loadoutMargin.AddThemeConstantOverride("margin_top", 12);
			loadoutMargin.AddThemeConstantOverride("margin_right", 12);
			loadoutMargin.AddThemeConstantOverride("margin_bottom", 12);
			loadoutPanel.AddChild(loadoutMargin);

			var loadout = new HBoxContainer();
			loadout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			loadout.SizeFlagsVertical = SizeFlags.ExpandFill;
			loadout.Alignment = BoxContainer.AlignmentMode.Center;
			loadout.AddThemeConstantOverride("separation", 8);
			loadoutMargin.AddChild(loadout);

			var leftSlots = new VBoxContainer();
			leftSlots.Alignment = BoxContainer.AlignmentMode.Center;
			leftSlots.AddThemeConstantOverride("separation", 10);
			loadout.AddChild(leftSlots);
			CreateEquipmentSlot(leftSlots, "ĐẦU", null, EquipmentSlot.Head);
			CreateEquipmentSlot(leftSlots, "ÁO", null, EquipmentSlot.Body);
			CreateEquipmentSlot(leftSlots, "QUẦN", null, EquipmentSlot.Legs);

			var bodyFrame = new PanelContainer();
			bodyFrame.CustomMinimumSize = new Vector2(125, 315);
			bodyFrame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			loadout.AddChild(bodyFrame);

			_equipmentBodyContainer = new Control();
			_equipmentBodyContainer.CustomMinimumSize = new Vector2(125, 315);
			_equipmentBodyContainer.ClipContents = true;
			bodyFrame.AddChild(_equipmentBodyContainer);

			var rightSlots = new VBoxContainer();
			rightSlots.Alignment = BoxContainer.AlignmentMode.Center;
			rightSlots.AddThemeConstantOverride("separation", 10);
			loadout.AddChild(rightSlots);
			CreateEquipmentSlot(rightSlots, "GIÀY", null, EquipmentSlot.Accessory2);
			CreateEquipmentSlot(rightSlots, "VŨ KHÍ", null, EquipmentSlot.MainHand);
			CreateEquipmentSlot(rightSlots, "PHỤ", null, EquipmentSlot.OffHand);

			var inventoryColumn = new VBoxContainer();
			inventoryColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			inventoryColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
			inventoryColumn.AddThemeConstantOverride("separation", 9);
			body.AddChild(inventoryColumn);
			inventoryColumn.AddChild(CreateLabel("TÚI ĐỒ", 15, _mainTextColor));
			inventoryColumn.AddChild(CreateDivider());

			var scroll = new ScrollContainer();
			scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			inventoryColumn.AddChild(scroll);

			_inventoryGrid = new GridContainer();
			_inventoryGrid.Columns = 4;
			_inventoryGrid.AddThemeConstantOverride("h_separation", 8);
			_inventoryGrid.AddThemeConstantOverride("v_separation", 8);
			_inventoryGrid.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			_inventoryGrid.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			scroll.AddChild(_inventoryGrid);

			var inventory = ResolveInventoryManager();
			int slotCount = inventory != null ? inventory.MaxSlots : 40;
			if (slotCount < 1) slotCount = 40;
			for (int i = 0; i < slotCount; i++) CreateInventorySlot(_inventoryGrid);
			return panel;
		}

		// Tạo 1 ô inventory nhỏ (nền đen, viền theo màu nhân vật)
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
			iconRect.Size = new Vector2(48, 44);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			iconRect.Visible = false;
			inner.AddChild(iconRect);

			var label = CreateLabel("", 9, _subTextColor);
			label.Position = new Vector2(4, 48);
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

			_inventorySlotIcons.Add(iconRect);
			_inventorySlotLabels.Add(label);
			_inventorySlotButtons.Add(button);
			_inventorySlotItemIds.Add(string.Empty);
			parent.AddChild(slot);
		}

		// Cập nhật hiển thị cho grid inventory dựa trên InventoryManager hiện tại
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
				if (item == null) // Nếu không có item nào ở slot này, reset về trạng thái trống
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
		}

		private void OnInventorySlotPressed(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= _inventorySlotItemIds.Count) return;

			string itemId = _inventorySlotItemIds[slotIndex];
			if (string.IsNullOrEmpty(itemId)) return;

			var player = ResolvePlayer();
			if (player == null)
			{
				GD.PrintErr("[CharacterDetailUI] Không tìm thấy Player để equip item.");
				return;
			}

			player.EquipFromInventory(itemId);
			RefreshInventoryGrid();
			RefreshEquipmentSlots();
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

		private InventoryManager ResolveInventoryManager()
		{
			var sceneManager = GetTree()?.Root?.GetNodeOrNull<SceneManager>("SceneManager");
			var playerFromSceneManager = sceneManager?.Player;
			if (playerFromSceneManager != null)
			{
				var inventoryFromPlayer = playerFromSceneManager.GetNodeOrNull<InventoryManager>("InventoryManager");
				if (inventoryFromPlayer != null)
					return inventoryFromPlayer;
			}

			var playerNodes = GetTree()?.GetNodesInGroup("Player");
			if (playerNodes != null)
			{
				// Nếu có nhiều node Player, ưu tiên node nào có InventoryManager
				foreach (var node in playerNodes)
				{
					if (node is Node playerNode)
					{
						var inventory = playerNode.GetNodeOrNull<InventoryManager>("InventoryManager");
						if (inventory != null)
							return inventory;
					}
				}
			}

			return null;
		}

		private void RebindInventory(InventoryManager inventory)
		{
			if (_boundInventory == inventory) return;

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

		private void OnInventoryChanged()
		{
			RefreshInventoryGrid(); // Cập nhật lại grid khi có thay đổi trong inventory
		}

		private string CompactItemName(string itemName)
		{
			if (string.IsNullOrEmpty(itemName)) return "";
			return itemName.Length <= 2 ? itemName.ToUpper() : itemName.Substring(0, 2).ToUpper();
		}

		public void OpenEquipmentTab()
		{
			SwitchTab("equipment");
			RefreshInventoryGrid();
			RefreshEquipmentSlots();
		}

		// Tạo một slot trang bị (đang mặc) - nhỏ gọn
		private void CreateEquipmentSlot(Container parent, string slotName, string iconPath = null, EquipmentSlot? slotType = null)
		{
			var slotColumn = new VBoxContainer();
			slotColumn.AddThemeConstantOverride("separation", 4);
			slotColumn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

			var slotPanel = new PanelContainer();
			slotPanel.CustomMinimumSize = new Vector2(62, 62);
			slotPanel.AddThemeStyleboxOverride("panel", CreateSlotStyle());
			slotColumn.AddChild(slotPanel);

			Texture2D fallback = !string.IsNullOrEmpty(iconPath) ? TryLoadTexture(iconPath) : null;
			var icon = new TextureRect();
			icon.Texture = fallback;
			icon.CustomMinimumSize = new Vector2(48, 48);
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			slotPanel.AddChild(icon);

			var button = new Button();
			button.Text = "";
			button.SetAnchorsPreset(LayoutPreset.FullRect);
			button.FocusMode = FocusModeEnum.None;
			button.MouseDefaultCursorShape = CursorShape.PointingHand;
			button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
			button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateSlotHoverStyle());
			button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateSlotPressedStyle());
			if (slotType.HasValue)
			{
				EquipmentSlot captured = slotType.Value;
				button.Pressed += () => OnEquipmentSlotPressed(captured);
				_equipmentSlotIcons[captured] = icon;
				_equipmentSlotDefaultIcons[captured] = fallback;
				_equipmentSlotButtons[captured] = button;
			}
			slotPanel.AddChild(button);

			var label = CreateLabel(slotName, 9, _subTextColor);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			slotColumn.AddChild(label);
			parent.AddChild(slotColumn);
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

				var equipped = equipmentManager?.GetEquippedItem(slotType);
				var fallback = _equipmentSlotDefaultIcons[slotType];

				iconRect.Texture = equipped?.Icon ?? fallback;
				button.TooltipText = equipped != null
					? $"{equipped.ItemName} (Chuot trai de thao)"
					: "Trong (Chuot trai de thao neu co do)";
			}
		}

		private void OnEquipmentSlotPressed(EquipmentSlot slotType)
		{
			var player = ResolvePlayer();
			if (player == null) return;

			player.UnequipToInventory(slotType);
			RefreshEquipmentSlots();
			RefreshInventoryGrid();
		}


		// Nội dung tab dùng nền trong suốt vì shell và section ngoài đã đảm nhiệm phần khung.
		private Control CreateSkillsPanelLayout()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			var column = new VBoxContainer();
			column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			column.SizeFlagsVertical = SizeFlags.ExpandFill;
			column.AddThemeConstantOverride("separation", 10);
			panel.AddChild(column);
			column.AddChild(CreateLabel("KỸ NĂNG", 15, _mainTextColor));
			column.AddChild(CreateDivider());

			var scroll = new ScrollContainer();
			scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
			column.AddChild(scroll);

			_skillsListContainer = new VBoxContainer();
			_skillsListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_skillsListContainer.AddThemeConstantOverride("separation", 8);
			scroll.AddChild(_skillsListContainer);
			return panel;
		}

		private void UpdateSkillsPanel(CharacterConfig config)
		{
			if (_skillsListContainer == null)
			{
				return;
			}

			foreach (var child in _skillsListContainer.GetChildren())
			{
				child.QueueFree();
			}

			var skills = new List<SkillData>();
			AddSkillsFromCollection(config?.ActiveSkills, skills);
			AddSkillsFromCollection(config?.ComboSequence, skills);

			if (skills.Count == 0)
			{
				_skillsListContainer.AddChild(CreateSkillEmptyState());
				return;
			}

			foreach (var skill in skills)
			{
				_skillsListContainer.AddChild(CreateSkillEntry(skill));
			}
		}

		private void AddSkillsFromCollection(Godot.Collections.Array<SkillData> source, List<SkillData> target)
		{
			if (source == null)
			{
				return;
			}

			foreach (var skill in source)
			{
				if (skill != null && !target.Contains(skill))
				{
					target.Add(skill);
				}
			}
		}

		private Control CreateSkillEntry(SkillData skill)
		{
			var card = new PanelContainer();
			card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			card.CustomMinimumSize = new Vector2(0, 92);
			card.AddThemeStyleboxOverride("panel", CreateDetailSectionStyle());

			var margin = new MarginContainer();
			margin.AddThemeConstantOverride("margin_left", 12);
			margin.AddThemeConstantOverride("margin_top", 10);
			margin.AddThemeConstantOverride("margin_right", 12);
			margin.AddThemeConstantOverride("margin_bottom", 10);
			card.AddChild(margin);

			var row = new HBoxContainer();
			row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddThemeConstantOverride("separation", 12);
			margin.AddChild(row);
			row.AddChild(CreateSkillIconFrame(skill));

			var textColumn = new VBoxContainer();
			textColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			textColumn.AddThemeConstantOverride("separation", 5);
			row.AddChild(textColumn);

			var title = CreateLabel(string.IsNullOrWhiteSpace(skill?.SkillName) ? "Kỹ năng chưa đặt tên" : skill.SkillName, 17, _mainTextColor);
			textColumn.AddChild(title);
			var description = CreateLabel(string.IsNullOrWhiteSpace(skill?.Description) ? "Kỹ năng này chưa có mô tả." : skill.Description, 13, _subTextColor);
			description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			textColumn.AddChild(description);
			return card;
		}

		private Control CreateSkillIconFrame(SkillData skill)
		{
			var frame = new PanelContainer();
			frame.CustomMinimumSize = new Vector2(68, 68);
			frame.AddThemeStyleboxOverride("panel", CreatePreviewStyle());
			var center = new CenterContainer();
			frame.AddChild(center);

			if (skill?.Icon != null)
			{
				var icon = new TextureRect();
				icon.Texture = skill.Icon;
				icon.CustomMinimumSize = new Vector2(48, 48);
				icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				center.AddChild(icon);
			}
			else
			{
				var fallback = CreateLabel("—", 20, _subTextColor);
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
			margin.AddThemeConstantOverride("margin_top", 18);
			margin.AddThemeConstantOverride("margin_right", 16);
			margin.AddThemeConstantOverride("margin_bottom", 18);
			panel.AddChild(margin);
			var label = CreateLabel("Nhân vật này chưa được gán dữ liệu kỹ năng.", 13, _subTextColor);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			margin.AddChild(label);
			return panel;
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
			InventoryPanelChrome.ApplyTabStyle(button, active);
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
					break;
				default:
					_currentTab = "overview";
					ShowPanel(_overviewPanel);
					break;
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
			Color characterAccent = _currentThemeColor != default ? _currentThemeColor : _accentColor;
			_levelLabel?.AddThemeColorOverride("font_color", characterAccent);
			if (_overviewStatsChart != null)
			{
				_overviewStatsChart.MainColor = characterAccent;
				_overviewStatsChart.QueueRedraw();
			}
			ResetTabButtonColors();
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
			_levelLabel.Text = $"LV. {currentStats.CurrentLevel:00}";
			_raceLabel.Text = config.CharacterRace?.RaceName?.ToUpper() ?? "UNKNOWN";
			
			// Cập nhật theme color từ character config
			_currentThemeColor = config.ThemeColor;
			
			// Cập nhật background từ character config
			if (_headerIcon != null)
			{
				_headerIcon.Texture = config.Icon ?? _characterIconTexture;
			}

			if (_backgroundDisplay != null)
			{
				_backgroundDisplay.Texture = config.BackgroundImage ?? config.Icon;
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

			if (_overviewStatsChart != null)
			{
				_overviewStatsChart.ClearStats();
				if (stats.FinalAttributes != null)
				{
					foreach (var attr in stats.FinalAttributes)
						_overviewStatsChart.SetStat(FormatStatName(attr.Key.ToString()), attr.Value);
				}
				_overviewStatsChart.UpdateAllStats();
			}
			UpdateResourceBars(stats);
		}

		private void UpdateResourceBars(PlayerStats stats)
		{
			SetBarValue(_hpBar, _hpValueLabel, (int)stats.CurrentHP, (int)stats.MaxHP);
			SetBarValue(_mpBar, _mpValueLabel, (int)stats.CurrentMP, (int)stats.MaxMP);
			SetBarValue(_staminaBar, _staminaValueLabel, (int)stats.CurrentStamina, (int)stats.MaxStamina);

		}
		private void SetBarValue(ProgressBar bar, Label valueLabel, int current, int max)
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

		private HBoxContainer CreateResourceBarRow(string labelText, Color fillColor, out ProgressBar bar, out Label valueLabel)
		{
			var row = new HBoxContainer();
			row.CustomMinimumSize = new Vector2(0, 28);
			row.AddThemeConstantOverride("separation", 8);

			var name = CreateLabel(labelText, 12, _subTextColor);
			name.CustomMinimumSize = new Vector2(38, 0);
			name.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(name);

			bar = new ProgressBar();
			bar.CustomMinimumSize = new Vector2(220, 12);
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.ShowPercentage = false;

			var background = new StyleBoxFlat();
			background.BgColor = _deepSurfaceColor;
			background.BorderColor = _borderColor.Darkened(0.1f);
			background.SetBorderWidthAll(1);
			background.SetCornerRadiusAll(2);
			var fill = new StyleBoxFlat();
			fill.BgColor = fillColor;
			fill.SetCornerRadiusAll(2);
			bar.AddThemeStyleboxOverride("background", background);
			bar.AddThemeStyleboxOverride("fill", fill);
			row.AddChild(bar);

			valueLabel = CreateLabel("0/0", 11, _mainTextColor);
			valueLabel.CustomMinimumSize = new Vector2(72, 0);
			valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
			valueLabel.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(valueLabel);
			return row;
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
