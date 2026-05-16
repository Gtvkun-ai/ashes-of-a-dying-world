using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.Menus
{
	public partial class InventoryPanel : Panel
	{
		private const int DefaultSlotCount = 40;
		private const int GridColumns = 8;
		private const float InventoryPanelHeight = 345f;

		private enum InventoryCategory
		{
			All,
			Consumables,
			Materials,
			Equipment,
			Quest,
			Others
		}

		private sealed class InventoryEntry
		{
			public EquipmentItemData Item { get; init; }
			public int Count { get; set; }
			public int FirstIndex { get; init; }
		}

		[Export] public NodePath InventoryManagerPath { get; set; }

		private readonly Color _accentColor = new Color("#f0c75d");
		private readonly Color _subTextColor = new Color("#b7aa8e");
		private readonly Color _btnNormalColor = new Color("#221d16");
		private readonly Color _btnHoverColor = new Color("#342b20");
		private readonly Color _slotFillColor = new Color("#242019");
		private readonly Color _slotBorderColor = new Color("#4f4332");
		private readonly Color _slotSelectedColor = new Color("#c8a24a");
		private readonly Color _inventoryAreaFillColor = new Color("#252019");
		private readonly Color _inventoryAreaBorderColor = new Color("#5b4a33");
		private readonly Color _inventoryAreaInnerBorderColor = new Color("#342a1f");
		private readonly Color _slotHoverOverlayColor = new Color(0f, 0f, 0f, 0.58f);
		private readonly Color _slotHoverTextColor = new Color("#f1e3c2");
		private readonly Color _detailPanelColor = new Color("#211c16");
		private readonly Color _detailInnerColor = new Color("#1c1712");
		private readonly Color _detailGreenColor = new Color("#67b24e");
		private readonly Color _detailGoldColor = new Color("#d8b15a");
		private readonly Color _useButtonColor = new Color("#5f8e43");
		private readonly Color _dropButtonColor = new Color("#a24d48");
		private readonly Color _smallButtonColor = new Color("#4d4338");

		private InventoryManager _inventoryManager;
		private Player _player;
		private Button _allButton;
		private Button _consumablesButton;
		private Button _materialsButton;
		private Button _equipmentButton;
		private Button _questButton;
		private Button _othersButton;
		private Label _titleLabel;
		private Label _capacityLabel;
		private GridContainer _grid;
		private Label _detailNameLabel;
		private Label _detailCategoryLabel;
		private Label _detailOwnedLabel;
		private RichTextLabel _detailDescriptionLabel;
		private TextureRect _detailIcon;
		private Label _quantityLabel;
		private Button _useButton;
		private Button _dropButton;
		private Texture2D _bagIcon;
		private Texture2D _coinIcon;
		private readonly Dictionary<InventoryCategory, Texture2D> _categoryIcons = new();

		private InventoryCategory _currentCategory = InventoryCategory.All;
		private readonly List<InventoryEntry> _visibleEntries = new();
		private string _selectedItemId;

		private readonly List<PanelContainer> _slotFrames = new();
		private readonly List<TextureRect> _slotIcons = new();
		private readonly List<Label> _slotLabels = new();
		private readonly List<Label> _slotCounts = new();
		private readonly List<Label> _slotHints = new();
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

		private void ApplyPanelSize()
		{
			AnchorLeft = 0.13f;
			AnchorTop = 0.08f;
			AnchorRight = 0.87f;
			AnchorBottom = 0.92f;

			OffsetLeft = 0;
			OffsetTop = 0;
			OffsetRight = 0;
			OffsetBottom = 0;
		}

		private void BuildUI()
		{
			var window = new PanelContainer();
			window.SetAnchorsPreset(LayoutPreset.FullRect);
			window.AddThemeStyleboxOverride("panel", CreateWindowStyle());
			AddChild(window);

			var root = new MarginContainer();
			root.AddThemeConstantOverride("margin_left", 12);
			root.AddThemeConstantOverride("margin_top", 10);
			root.AddThemeConstantOverride("margin_right", 12);
			root.AddThemeConstantOverride("margin_bottom", 10);
			window.AddChild(root);

			var main = new VBoxContainer();
			main.AddThemeConstantOverride("separation", 7);
			main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			main.SizeFlagsVertical = SizeFlags.ExpandFill;
			root.AddChild(main);

			var headerPanel = new PanelContainer();
			headerPanel.CustomMinimumSize = new Vector2(0, 42);
			headerPanel.AddThemeStyleboxOverride("panel", CreateHeaderStyle());
			main.AddChild(headerPanel);

			var header = new HBoxContainer();
			header.AddThemeConstantOverride("separation", 10);
			headerPanel.AddChild(header);

			var chest = new PanelContainer();
			chest.CustomMinimumSize = new Vector2(30, 30);
			chest.AddThemeStyleboxOverride("panel", CreateIconBadgeStyle());
			header.AddChild(chest);

			var chestIcon = new TextureRect();
			chestIcon.Texture = _bagIcon ??= CreateBagIcon();
			chestIcon.CustomMinimumSize = new Vector2(20, 20);
			chestIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			chestIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			chest.AddChild(chestIcon);

			_titleLabel = new Label();
			_titleLabel.Text = "INVENTORY";
			_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_titleLabel.VerticalAlignment = VerticalAlignment.Center;
			_titleLabel.AddThemeFontSizeOverride("font_size", 19);
			_titleLabel.AddThemeColorOverride("font_color", new Color("#f4ead8"));
			header.AddChild(_titleLabel);

			var coinRow = new HBoxContainer();
			coinRow.AddThemeConstantOverride("separation", 4);
			header.AddChild(coinRow);

			var coinIcon = new TextureRect();
			coinIcon.Texture = _coinIcon ??= CreateCoinIcon();
			coinIcon.CustomMinimumSize = new Vector2(16, 16);
			coinIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			coinIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			coinRow.AddChild(coinIcon);

			var coinLabel = CreateDetailLabel(16, new Color("#f6d77b"));
			coinLabel.Text = "12,345";
			coinLabel.VerticalAlignment = VerticalAlignment.Center;
			coinRow.AddChild(coinLabel);

			header.AddChild(CreateCloseButton());

			var tabs = new HBoxContainer();
			tabs.CustomMinimumSize = new Vector2(0, 31);
			tabs.AddThemeConstantOverride("separation", 5);
			main.AddChild(tabs);

			_allButton = CreateCategoryButton("All", InventoryCategory.All);
			_consumablesButton = CreateCategoryButton("Consumables", InventoryCategory.Consumables);
			_materialsButton = CreateCategoryButton("Materials", InventoryCategory.Materials);
			_equipmentButton = CreateCategoryButton("Equipment", InventoryCategory.Equipment);
			_questButton = CreateCategoryButton("Quest", InventoryCategory.Quest);
			_othersButton = CreateCategoryButton("Others", InventoryCategory.Others);

			tabs.AddChild(_allButton);
			tabs.AddChild(_consumablesButton);
			tabs.AddChild(_materialsButton);
			tabs.AddChild(_equipmentButton);
			tabs.AddChild(_questButton);
			tabs.AddChild(_othersButton);

			var body = new HBoxContainer();
			body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			body.SizeFlagsVertical = SizeFlags.ExpandFill;
			body.CustomMinimumSize = new Vector2(0, InventoryPanelHeight);
			body.AddThemeConstantOverride("separation", 7);
			main.AddChild(body);

			var gridPanel = new PanelContainer();
			gridPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			gridPanel.CustomMinimumSize = new Vector2(485, InventoryPanelHeight);
			gridPanel.AddThemeStyleboxOverride("panel", CreateInventoryAreaStyle());
			body.AddChild(gridPanel);

			var gridColumn = new VBoxContainer();
			gridColumn.AddThemeConstantOverride("separation", 8);
			gridColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
			gridPanel.AddChild(gridColumn);

			var gridMargin = new MarginContainer();
			gridMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
			gridMargin.AddThemeConstantOverride("margin_left", 8);
			gridMargin.AddThemeConstantOverride("margin_top", 8);
			gridMargin.AddThemeConstantOverride("margin_right", 8);
			gridColumn.AddChild(gridMargin);

			var gridCenter = new CenterContainer();
			gridCenter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridCenter.SizeFlagsVertical = SizeFlags.ExpandFill;
			gridMargin.AddChild(gridCenter);

			_grid = new GridContainer();
			_grid.Columns = GridColumns;
			_grid.AddThemeConstantOverride("h_separation", 5);
			_grid.AddThemeConstantOverride("v_separation", 5);
			_grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			_grid.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			gridCenter.AddChild(_grid);

			int slotCount = GetSlotCount();
			for (int i = 0; i < slotCount; i++)
			{
				CreateInventorySlot(i);
			}

			var footer = new HBoxContainer();
			footer.CustomMinimumSize = new Vector2(0, 40);
			footer.AddThemeConstantOverride("separation", 8);
			gridColumn.AddChild(footer);

			_capacityLabel = CreateDetailLabel(14, new Color("#d7c7a4"));
			_capacityLabel.Text = "[] 0/60";
			_capacityLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_capacityLabel.VerticalAlignment = VerticalAlignment.Center;
			footer.AddChild(_capacityLabel);

			var addButton = CreateSmallActionButton("+");
			addButton.Disabled = true;
			footer.AddChild(addButton);

			footer.AddChild(CreateFooterButton("Sort  ="));

			body.AddChild(BuildDetailPanel());

		}

		private Control BuildDetailPanel()
		{
			var detailPanel = new PanelContainer();
			detailPanel.CustomMinimumSize = new Vector2(170, InventoryPanelHeight);
			detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			detailPanel.AddThemeStyleboxOverride("panel", CreateDetailPanelStyle());

			var detailMargin = new MarginContainer();
			detailMargin.AddThemeConstantOverride("margin_left", 10);
			detailMargin.AddThemeConstantOverride("margin_top", 10);
			detailMargin.AddThemeConstantOverride("margin_right", 10);
			detailMargin.AddThemeConstantOverride("margin_bottom", 10);
			detailPanel.AddChild(detailMargin);

			var detailContent = new VBoxContainer();
			detailContent.SizeFlagsVertical = SizeFlags.ExpandFill;
			detailContent.AddThemeConstantOverride("separation", 10);
			detailMargin.AddChild(detailContent);

			var iconFrame = new PanelContainer();
			iconFrame.CustomMinimumSize = new Vector2(0, 96);
			iconFrame.AddThemeStyleboxOverride("panel", CreateDetailInnerStyle());
			detailContent.AddChild(iconFrame);

			var iconCenter = new CenterContainer();
			iconFrame.AddChild(iconCenter);

			_detailIcon = new TextureRect();
			_detailIcon.CustomMinimumSize = new Vector2(52, 52);
			_detailIcon.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
			_detailIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconCenter.AddChild(_detailIcon);

			_detailNameLabel = CreateDetailLabel(16, Colors.White);
			_detailNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_detailNameLabel.Text = "Select Item";
			detailContent.AddChild(_detailNameLabel);

			_detailCategoryLabel = CreateDetailLabel(13, _detailGreenColor);
			_detailCategoryLabel.Text = "Category";
			detailContent.AddChild(_detailCategoryLabel);

			_detailOwnedLabel = CreateDetailLabel(13, _detailGoldColor);
			_detailOwnedLabel.Text = "Owned: 0";
			detailContent.AddChild(_detailOwnedLabel);

			var descPanel = new PanelContainer();
			descPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
			descPanel.AddThemeStyleboxOverride("panel", CreateDetailInnerStyle());
			detailContent.AddChild(descPanel);

			var descMargin = new MarginContainer();
			descMargin.AddThemeConstantOverride("margin_left", 10);
			descMargin.AddThemeConstantOverride("margin_top", 10);
			descMargin.AddThemeConstantOverride("margin_right", 10);
			descMargin.AddThemeConstantOverride("margin_bottom", 10);
			descPanel.AddChild(descMargin);

			_detailDescriptionLabel = new RichTextLabel();
			_detailDescriptionLabel.BbcodeEnabled = false;
			_detailDescriptionLabel.ScrollActive = false;
			_detailDescriptionLabel.FitContent = true;
			_detailDescriptionLabel.SelectionEnabled = false;
			_detailDescriptionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
			_detailDescriptionLabel.AddThemeFontSizeOverride("normal_font_size", 12);
			_detailDescriptionLabel.AddThemeColorOverride("default_color", _subTextColor);
			descMargin.AddChild(_detailDescriptionLabel);

			var quantityRow = new HBoxContainer();
			quantityRow.AddThemeConstantOverride("separation", 6);
			detailContent.AddChild(quantityRow);

			quantityRow.AddChild(CreateSmallActionButton("-"));

			var quantityPanel = new PanelContainer();
			quantityPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			quantityPanel.CustomMinimumSize = new Vector2(0, 44);
			quantityPanel.AddThemeStyleboxOverride("panel", CreateDetailInnerStyle());
			quantityRow.AddChild(quantityPanel);

			var quantityCenter = new CenterContainer();
			quantityPanel.AddChild(quantityCenter);

			_quantityLabel = CreateDetailLabel(18, Colors.White);
			_quantityLabel.Text = "0";
			quantityCenter.AddChild(_quantityLabel);

			quantityRow.AddChild(CreateSmallActionButton("+"));

			var actionRow = new HBoxContainer();
			actionRow.AddThemeConstantOverride("separation", 10);
			detailContent.AddChild(actionRow);

			_useButton = CreateActionButton("Use", _useButtonColor);
			_useButton.Pressed += OnUsePressed;
			actionRow.AddChild(_useButton);

			_dropButton = CreateActionButton("Drop", _dropButtonColor);
			_dropButton.Pressed += OnDropPressed;
			actionRow.AddChild(_dropButton);

			return detailPanel;
		}

		private Button CreateCategoryButton(string text, InventoryCategory category)
		{
			var button = new Button();
			button.Text = text;
			button.Icon = GetCategoryIcon(category);
			button.ExpandIcon = false;
			button.IconAlignment = HorizontalAlignment.Left;
			button.CustomMinimumSize = new Vector2(GetCategoryButtonWidth(category), 28);
			button.FocusMode = FocusModeEnum.None;
			button.Pressed += () => ShowCategory(category);
			return button;
		}

		private float GetCategoryButtonWidth(InventoryCategory category)
		{
			return category switch
			{
				InventoryCategory.All => 58f,
				InventoryCategory.Consumables => 112f,
				InventoryCategory.Equipment => 92f,
				_ => 78f
			};
		}

		private Button CreateCloseButton()
		{
			var button = new Button();
			button.Text = "X";
			button.CustomMinimumSize = new Vector2(30, 30);
			button.FocusMode = FocusModeEnum.None;
			button.Pressed += Hide;

			var normal = CreateActionStyle(new Color("#6e2d24"));
			normal.SetCornerRadiusAll(3);
			var hover = CreateActionStyle(new Color("#8a3b31"));
			hover.SetCornerRadiusAll(3);
			var pressed = CreateActionStyle(new Color("#4c1e19"));
			pressed.SetCornerRadiusAll(3);

			button.AddThemeStyleboxOverride("normal", normal);
			button.AddThemeStyleboxOverride("hover", hover);
			button.AddThemeStyleboxOverride("pressed", pressed);
			button.AddThemeColorOverride("font_color", new Color("#ffe6dc"));
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			return button;
		}

		private void CreateInventorySlot(int slotIndex)
		{
			var slot = new PanelContainer();
			slot.CustomMinimumSize = new Vector2(51, 51);
			slot.AddThemeStyleboxOverride("panel", CreateSlotStyle(false));

			var inner = new Control();
			inner.CustomMinimumSize = new Vector2(51, 51);
			slot.AddChild(inner);

			var icon = new TextureRect();
			icon.CustomMinimumSize = new Vector2(33, 33);
			icon.Position = new Vector2(9, 6);
			icon.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.Visible = false;
			inner.AddChild(icon);

			var label = new Label();
			label.Text = "";
			label.Position = new Vector2(4, 4);
			label.Size = new Vector2(42, 14);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.AddThemeFontSizeOverride("font_size", 8);
			label.AddThemeColorOverride("font_color", new Color(0.88f, 0.95f, 1f, 0.88f));
			inner.AddChild(label);

			var count = new Label();
			count.Text = "";
			count.Position = new Vector2(22, 35);
			count.Size = new Vector2(24, 12);
			count.HorizontalAlignment = HorizontalAlignment.Right;
			count.AddThemeFontSizeOverride("font_size", 10);
			count.AddThemeColorOverride("font_color", Colors.White);
			inner.AddChild(count);

			var hint = new Label();
			hint.Text = "";
			hint.Visible = false;
			hint.Position = new Vector2(1, 17);
			hint.Size = new Vector2(49, 14);
			hint.HorizontalAlignment = HorizontalAlignment.Center;
			hint.VerticalAlignment = VerticalAlignment.Center;
			hint.AddThemeFontSizeOverride("font_size", 7);
			hint.AddThemeColorOverride("font_color", _slotHoverTextColor);
			hint.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.95f));
			hint.AddThemeConstantOverride("shadow_offset_x", 1);
			hint.AddThemeConstantOverride("shadow_offset_y", 1);
			inner.AddChild(hint);

			var hoverButton = new Button();
			hoverButton.Text = "";
			hoverButton.SetAnchorsPreset(LayoutPreset.FullRect);
			hoverButton.FocusMode = FocusModeEnum.None;
			hoverButton.MouseDefaultCursorShape = CursorShape.PointingHand;
			hoverButton.AddThemeStyleboxOverride("normal", CreateTransparentButtonStyle());
			hoverButton.AddThemeStyleboxOverride("hover", CreateHoverButtonStyle());
			hoverButton.AddThemeStyleboxOverride("pressed", CreateHoverButtonStyle());
			hoverButton.Pressed += () => OnSlotPressed(slotIndex);
			hoverButton.MouseEntered += () => OnSlotHovered(slotIndex, true);
			hoverButton.MouseExited += () => OnSlotHovered(slotIndex, false);
			slot.AddChild(hoverButton);

			_slotFrames.Add(slot);
			_slotIcons.Add(icon);
			_slotLabels.Add(label);
			_slotCounts.Add(count);
			_slotHints.Add(hint);
			_slotButtons.Add(hoverButton);
			_grid.AddChild(slot);
		}

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
			UpdateCategoryButtonStyle();
			RefreshInventoryView();
		}

		private void UpdateCategoryButtonStyle()
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

			var normal = new StyleBoxFlat();
			normal.BgColor = selected ? new Color("#3a2f21") : _btnNormalColor;
			normal.BorderColor = selected ? _accentColor : new Color("#4b4030");
			normal.SetBorderWidthAll(selected ? 2 : 0);
			normal.SetCornerRadiusAll(4);
			normal.ContentMarginLeft = 10;
			normal.ContentMarginRight = 10;
			normal.ContentMarginTop = 7;
			normal.ContentMarginBottom = 7;

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

		private void RefreshInventoryView()
		{
			ClearSlots();
			BuildVisibleEntries();
			EnsureValidSelection();
			FillItemSlots();
			RefreshDetailPanel();
			_titleLabel.Text = "INVENTORY";
			if (_capacityLabel != null)
			{
				int usedSlots = _inventoryManager?.Items?.Count ?? 0;
				int maxSlots = _inventoryManager?.MaxSlots ?? DefaultSlotCount;
				_capacityLabel.Text = $"[] {usedSlots}/{Mathf.Max(DefaultSlotCount, maxSlots)}";
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

				string key = string.IsNullOrWhiteSpace(item.ID) ? $"__index_{i}" : item.ID;
				if (grouped.TryGetValue(key, out InventoryEntry existing))
				{
					existing.Count++;
					continue;
				}

				grouped[key] = new InventoryEntry
				{
					Item = item,
					Count = 1,
					FirstIndex = i
				};
			}

			foreach (InventoryEntry entry in grouped.Values.OrderBy(entry => entry.FirstIndex))
			{
				_visibleEntries.Add(entry);
			}
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
				_selectedItemId = null;
				return;
			}

			if (string.IsNullOrEmpty(_selectedItemId) || !_visibleEntries.Any(entry => entry.Item?.ID == _selectedItemId))
			{
				_selectedItemId = _visibleEntries[0].Item?.ID;
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
				_slotLabels[i].Text = item.Icon == null ? CompactItemName(item.ItemName) : "";
				_slotCounts[i].Text = entry.Count > 1 ? entry.Count.ToString() : "";
				_slotButtons[i].TooltipText = $"{item.ItemName} x{entry.Count}";
				_slotFrames[i].AddThemeStyleboxOverride("panel", CreateSlotStyle(item.ID == _selectedItemId));
			}
		}

		private void RefreshDetailPanel()
		{
			var entry = _visibleEntries.FirstOrDefault(item => item.Item?.ID == _selectedItemId);
			if (entry?.Item == null)
			{
				_detailIcon.Texture = null;
				_detailNameLabel.Text = "Select Item";
				_detailCategoryLabel.Text = "Category";
				_detailOwnedLabel.Text = "Owned: 0";
				_detailDescriptionLabel.Text = "Chon mot vat pham trong kho de xem thong tin.";
				_quantityLabel.Text = "0";
				_useButton.Disabled = true;
				_dropButton.Disabled = true;
				return;
			}

			var item = entry.Item;
			_detailIcon.Texture = item.Icon;
			_detailNameLabel.Text = item.ItemName;
			_detailCategoryLabel.Text = item.InventoryCategory.ToString();
			_detailOwnedLabel.Text = $"Owned: {entry.Count}";
			_detailDescriptionLabel.Text = BuildDescription(item);
			_quantityLabel.Text = entry.Count.ToString();
			_useButton.Disabled = false;
			_dropButton.Disabled = false;
		}

		private void ClearSlots()
		{
			for (int i = 0; i < _slotButtons.Count; i++)
			{
				_slotIcons[i].Texture = null;
				_slotIcons[i].Visible = false;
				_slotLabels[i].Text = "";
				_slotCounts[i].Text = "";
				_slotHints[i].Text = "";
				_slotHints[i].Visible = false;
				_slotButtons[i].TooltipText = "";
				_slotFrames[i].AddThemeStyleboxOverride("panel", CreateSlotStyle(false));
			}
		}

		private void OnSlotHovered(int slotIndex, bool hovered)
		{
			if (slotIndex < 0 || slotIndex >= _slotHints.Count || slotIndex >= _visibleEntries.Count)
			{
				return;
			}

			var entry = _visibleEntries[slotIndex];
			if (entry?.Item == null)
			{
				_slotHints[slotIndex].Visible = false;
				return;
			}

			_slotHints[slotIndex].Text = GetHoverHint(entry.Item);
			_slotHints[slotIndex].Visible = hovered;
			if (hovered)
			{
				_selectedItemId = entry.Item.ID;
				RefreshDetailPanel();
			}
		}

		private void OnSlotPressed(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= _visibleEntries.Count)
			{
				return;
			}

			_selectedItemId = _visibleEntries[slotIndex].Item?.ID;

			if (_player == null)
			{
				_player = ResolvePlayer();
			}

			RefreshInventoryView();
		}

		private string CompactItemName(string itemName)
		{
			if (string.IsNullOrEmpty(itemName))
			{
				return "";
			}

			return itemName.Length <= 2 ? itemName.ToUpperInvariant() : itemName.Substring(0, 2).ToUpperInvariant();
		}

		private string GetHoverHint(EquipmentItemData item)
		{
			if (item == null)
			{
				return "";
			}

			return item.InventoryCategory == InventoryItemCategory.Quest ? "DROP" : "USE | DROP";
		}

		private string BuildDescription(EquipmentItemData item)
		{
			if (item == null)
			{
				return "";
			}

			if (!string.IsNullOrWhiteSpace(item.Description))
			{
				return item.Description;
			}

			var builder = new StringBuilder();
			if (item.InventoryCategory == InventoryItemCategory.Consumable)
			{
				builder.AppendLine("Restores 50 HP.");
				builder.Append("A potion made by alchemists.");
				return builder.ToString();
			}

			builder.AppendLine($"Loai: {item.InventoryCategory}");
			if (item.InventoryCategory == InventoryItemCategory.Equipment)
			{
				builder.AppendLine($"Slot: {item.SlotType}");
				builder.AppendLine($"Base Value: {item.BaseValue:0.##}");
			}

			if (item.MinLevel > 1)
			{
				builder.AppendLine($"Min Level: {item.MinLevel}");
			}

			return builder.ToString().Trim();
		}

		private void OnUsePressed()
		{
			var entry = _visibleEntries.FirstOrDefault(item => item.Item?.ID == _selectedItemId);
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
			var entry = _visibleEntries.FirstOrDefault(item => item.Item?.ID == _selectedItemId);
			if (entry?.Item == null || _inventoryManager == null)
			{
				return;
			}

			_inventoryManager.RemoveItem(entry.Item);
			RefreshInventoryView();
		}

		private Label CreateDetailLabel(int fontSize, Color color)
		{
			var label = new Label();
			label.AddThemeFontSizeOverride("font_size", fontSize);
			label.AddThemeColorOverride("font_color", color);
			return label;
		}

		private Button CreateSmallActionButton(string text)
		{
			var button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(36, 40);
			button.FocusMode = FocusModeEnum.None;
			button.Disabled = true;
			button.AddThemeStyleboxOverride("normal", CreateActionStyle(_smallButtonColor));
			button.AddThemeStyleboxOverride("hover", CreateActionStyle(_smallButtonColor));
			button.AddThemeStyleboxOverride("pressed", CreateActionStyle(_smallButtonColor));
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.55f));
			return button;
		}

		private Button CreateActionButton(string text, Color color)
		{
			var button = new Button();
			button.Text = text;
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.CustomMinimumSize = new Vector2(0, 42);
			button.FocusMode = FocusModeEnum.None;
			button.AddThemeStyleboxOverride("normal", CreateActionStyle(color));
			button.AddThemeStyleboxOverride("hover", CreateActionStyle(color.Lightened(0.08f)));
			button.AddThemeStyleboxOverride("pressed", CreateActionStyle(color.Darkened(0.08f)));
			button.AddThemeColorOverride("font_color", Colors.White);
			button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.45f));
			return button;
		}

		private Button CreateFooterButton(string text)
		{
			var button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(76, 32);
			button.FocusMode = FocusModeEnum.None;
			button.AddThemeStyleboxOverride("normal", CreateActionStyle(_smallButtonColor));
			button.AddThemeStyleboxOverride("hover", CreateActionStyle(_smallButtonColor.Lightened(0.08f)));
			button.AddThemeStyleboxOverride("pressed", CreateActionStyle(_smallButtonColor.Darkened(0.08f)));
			button.AddThemeColorOverride("font_color", new Color("#ead9b8"));
			return button;
		}

		private StyleBoxFlat CreateIconBadgeStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color("#342719");
			style.BorderColor = new Color("#7c623e");
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(4);
			return style;
		}

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
				InventoryCategory.Others => CreateOthersIcon(),
				_ => CreateOthersIcon()
			};
			_categoryIcons[category] = icon;
			return icon;
		}

		private Texture2D CreateAllIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillRect(image, 3, 3, 4, 4, new Color("#f3d46a"));
				FillRect(image, 9, 3, 4, 4, new Color("#f3d46a"));
				FillRect(image, 3, 9, 4, 4, new Color("#f3d46a"));
				FillRect(image, 9, 9, 4, 4, new Color("#f3d46a"));
				FillRect(image, 4, 4, 2, 2, new Color("#fff1a3"));
				FillRect(image, 10, 4, 2, 2, new Color("#fff1a3"));
			});
		}

		private Texture2D CreateConsumablesIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillRect(image, 6, 2, 4, 3, new Color("#d8d1bd"));
				FillRect(image, 5, 5, 6, 2, new Color("#7c4b35"));
				FillRect(image, 4, 7, 8, 7, new Color("#9b252a"));
				FillRect(image, 5, 8, 6, 5, new Color("#e24b4c"));
				FillRect(image, 6, 9, 2, 2, new Color("#ffd1c1"));
				FillRect(image, 3, 12, 10, 2, new Color("#5b1c22"));
			});
		}

		private Texture2D CreateBagIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillRect(image, 5, 7, 10, 8, new Color("#6f4728"));
				FillRect(image, 6, 8, 8, 6, new Color("#9a6735"));
				FillRect(image, 7, 4, 6, 2, new Color("#80542f"));
				FillRect(image, 6, 5, 2, 3, new Color("#3d2b1c"));
				FillRect(image, 12, 5, 2, 3, new Color("#3d2b1c"));
				FillRect(image, 8, 10, 4, 2, new Color("#d5ad62"));
			}, 20, 18);
		}

		private Texture2D CreateCoinIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillCircle(image, 8, 8, 7, new Color("#9d6320"));
				FillCircle(image, 8, 8, 5, new Color("#f4c94f"));
				FillCircle(image, 7, 7, 2, new Color("#ffe98b"));
				SetPixelSafe(image, 10, 11, new Color("#c88324"));
			}, 16, 16);
		}

		private Texture2D CreateMaterialsIcon()
		{
			return CreatePixelIcon(image =>
			{
				DrawLine(image, 4, 13, 13, 4, new Color("#2d7d32"));
				FillCircle(image, 8, 8, 5, new Color("#2f9a3a"));
				FillCircle(image, 10, 6, 4, new Color("#5fca55"));
				SetPixelSafe(image, 5, 12, new Color("#8ee36e"));
				SetPixelSafe(image, 11, 5, new Color("#b7ef8c"));
			});
		}

		private Texture2D CreateEquipmentIcon()
		{
			return CreatePixelIcon(image =>
			{
				DrawLine(image, 4, 13, 12, 5, new Color("#d8dde0"));
				DrawLine(image, 5, 13, 13, 5, new Color("#8d9aa0"));
				FillRect(image, 11, 3, 3, 3, new Color("#eef4f2"));
				FillRect(image, 3, 12, 5, 2, new Color("#8a4d2a"));
				FillRect(image, 5, 10, 2, 6, new Color("#c98a42"));
			});
		}

		private Texture2D CreateQuestIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillRect(image, 4, 4, 9, 10, new Color("#b9834f"));
				FillRect(image, 5, 5, 7, 8, new Color("#e0bd82"));
				FillRect(image, 3, 4, 11, 2, new Color("#8b5130"));
				FillRect(image, 3, 12, 11, 2, new Color("#8b5130"));
				DrawLine(image, 6, 8, 11, 8, new Color("#6f5034"));
				DrawLine(image, 6, 10, 10, 10, new Color("#6f5034"));
			});
		}

		private Texture2D CreateOthersIcon()
		{
			return CreatePixelIcon(image =>
			{
				FillCircle(image, 5, 8, 2, new Color("#d8c39b"));
				FillCircle(image, 9, 8, 2, new Color("#d8c39b"));
				FillCircle(image, 13, 8, 2, new Color("#d8c39b"));
			});
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
			int err = dx + dy;

			while (true)
			{
				SetPixelSafe(image, x0, y0, color);
				if (x0 == x1 && y0 == y1)
				{
					break;
				}

				int e2 = 2 * err;
				if (e2 >= dy)
				{
					err += dy;
					x0 += sx;
				}
				if (e2 <= dx)
				{
					err += dx;
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

		private StyleBoxFlat CreateWindowStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color("#201b14");
			style.BorderColor = new Color("#6f5a3b");
			style.SetBorderWidthAll(3);
			style.SetCornerRadiusAll(4);
			style.ShadowColor = new Color(0f, 0f, 0f, 0.45f);
			style.ShadowSize = 6;
			style.ContentMarginLeft = 0;
			style.ContentMarginTop = 0;
			style.ContentMarginRight = 0;
			style.ContentMarginBottom = 0;
			return style;
		}

		private StyleBoxFlat CreateHeaderStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = new Color("#1c1711");
			style.BorderColor = new Color("#3e3324");
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(2);
			style.ContentMarginLeft = 7;
			style.ContentMarginTop = 5;
			style.ContentMarginRight = 7;
			style.ContentMarginBottom = 5;
			return style;
		}

		private StyleBoxFlat CreateSlotStyle(bool selected)
		{
			var style = new StyleBoxFlat();
			style.BgColor = _slotFillColor;
			style.BorderColor = selected ? _slotSelectedColor : _slotBorderColor;
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(4);
			return style;
		}

		private StyleBoxFlat CreateInventoryAreaStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = _inventoryAreaFillColor;
			style.BorderColor = _inventoryAreaBorderColor;
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(3);
			style.ShadowColor = new Color(0f, 0f, 0f, 0.28f);
			style.ShadowSize = 6;
			style.ContentMarginLeft = 10;
			style.ContentMarginTop = 10;
			style.ContentMarginRight = 10;
			style.ContentMarginBottom = 10;
			return style;
		}

		private StyleBoxFlat CreateDetailPanelStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = _detailPanelColor;
			style.BorderColor = _inventoryAreaBorderColor;
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(3);
			return style;
		}

		private StyleBoxFlat CreateDetailInnerStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = _detailInnerColor;
			style.BorderColor = _slotBorderColor;
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(3);
			return style;
		}

		private StyleBoxFlat CreateActionStyle(Color bgColor)
		{
			var style = new StyleBoxFlat();
			style.BgColor = bgColor;
			style.BorderColor = bgColor.Lightened(0.1f);
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(6);
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
			style.BgColor = _slotHoverOverlayColor;
			style.SetCornerRadiusAll(4);
			return style;
		}
	}
}
