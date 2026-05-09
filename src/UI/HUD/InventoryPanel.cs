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
		private const float RightPanelHeight = 360f;

		private enum InventoryCategory
		{
			Items,
			Tools,
			Quests
		}

		private sealed class InventoryEntry
		{
			public EquipmentItemData Item { get; init; }
			public int Count { get; set; }
			public int FirstIndex { get; init; }
		}

		[Export] public NodePath InventoryManagerPath { get; set; }

		private readonly Color _accentColor = new Color("#38bdf8");
		private readonly Color _subTextColor = new Color("#94a3b8");
		private readonly Color _themeBorderColor = new Color("#38bdf8");
		private readonly Color _btnNormalColor = new Color("#1e293b");
		private readonly Color _btnHoverColor = new Color("#334155");
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
		private NinePatchRect _panelGlow;
		private NinePatchRect _panelFrame;
		private Button _itemsButton;
		private Button _toolsButton;
		private Button _questsButton;
		private Label _titleLabel;
		private GridContainer _grid;
		private Label _detailNameLabel;
		private Label _detailCategoryLabel;
		private Label _detailOwnedLabel;
		private RichTextLabel _detailDescriptionLabel;
		private TextureRect _detailIcon;
		private Label _quantityLabel;
		private Button _useButton;
		private Button _dropButton;

		private InventoryCategory _currentCategory = InventoryCategory.Items;
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

			SetupPanelFrame();
			BuildUI();
			FindInventoryManager();
			ShowCategory(InventoryCategory.Items);
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
			root.AddThemeConstantOverride("margin_left", 34);
			root.AddThemeConstantOverride("margin_top", 34);
			root.AddThemeConstantOverride("margin_right", 56);
			root.AddThemeConstantOverride("margin_bottom", 42);
			AddChild(root);

			var main = new HBoxContainer();
			main.AddThemeConstantOverride("separation", 12);
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

			var contentWrap = new VBoxContainer();
			contentWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			contentWrap.SizeFlagsVertical = SizeFlags.ExpandFill;
			contentWrap.AddThemeConstantOverride("separation", 10);
			main.AddChild(contentWrap);

			_titleLabel = new Label();
			_titleLabel.Text = "KHO ĐỒ";
			_titleLabel.AddThemeFontSizeOverride("font_size", 30);
			_titleLabel.AddThemeColorOverride("font_color", Colors.White);
			contentWrap.AddChild(_titleLabel);

			var rightAreaWrap = new MarginContainer();
			rightAreaWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rightAreaWrap.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			contentWrap.AddChild(rightAreaWrap);

			var rightArea = new HBoxContainer();
			rightArea.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			rightArea.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			rightArea.CustomMinimumSize = new Vector2(0, RightPanelHeight);
			rightArea.AddThemeConstantOverride("separation", 10);
			rightAreaWrap.AddChild(rightArea);

			var gridPanel = new PanelContainer();
			gridPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			gridPanel.CustomMinimumSize = new Vector2(0, RightPanelHeight);
			gridPanel.AddThemeStyleboxOverride("panel", CreateInventoryAreaStyle());
			rightArea.AddChild(gridPanel);

			var gridWrap = new MarginContainer();
			gridWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridWrap.SizeFlagsVertical = SizeFlags.ExpandFill;
			gridWrap.AddThemeConstantOverride("margin_left", 12);
			gridWrap.AddThemeConstantOverride("margin_top", 12);
			gridWrap.AddThemeConstantOverride("margin_right", 12);
			gridWrap.AddThemeConstantOverride("margin_bottom", 12);
			gridPanel.AddChild(gridWrap);

			var gridCenter = new CenterContainer();
			gridCenter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			gridCenter.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			gridWrap.AddChild(gridCenter);

			_grid = new GridContainer();
			_grid.Columns = GridColumns;
			_grid.AddThemeConstantOverride("h_separation", 6);
			_grid.AddThemeConstantOverride("v_separation", 6);
			_grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			_grid.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			gridCenter.AddChild(_grid);

			int slotCount = GetSlotCount();
			for (int i = 0; i < slotCount; i++)
			{
				CreateInventorySlot(i);
			}

			rightArea.AddChild(BuildDetailPanel());

			AddExitButton();
		}

		private Control BuildDetailPanel()
		{
			var detailPanel = new PanelContainer();
			detailPanel.CustomMinimumSize = new Vector2(170, RightPanelHeight);
			detailPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
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
			button.CustomMinimumSize = new Vector2(128, 38);
			button.FocusMode = FocusModeEnum.None;
			button.Pressed += () => ShowCategory(category);
			return button;
		}

		private void CreateInventorySlot(int slotIndex)
		{
			var slot = new PanelContainer();
			slot.CustomMinimumSize = new Vector2(50, 50);
			slot.AddThemeStyleboxOverride("panel", CreateSlotStyle(false));

			var inner = new Control();
			inner.CustomMinimumSize = new Vector2(50, 50);
			slot.AddChild(inner);

			var icon = new TextureRect();
			icon.CustomMinimumSize = new Vector2(24, 24);
			icon.Position = new Vector2(13, 7);
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
			count.Position = new Vector2(26, 34);
			count.Size = new Vector2(18, 12);
			count.HorizontalAlignment = HorizontalAlignment.Right;
			count.AddThemeFontSizeOverride("font_size", 10);
			count.AddThemeColorOverride("font_color", Colors.White);
			inner.AddChild(count);

			var hint = new Label();
			hint.Text = "";
			hint.Visible = false;
			hint.Position = new Vector2(1, 17);
			hint.Size = new Vector2(48, 14);
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

		private void RefreshInventoryView()
		{
			ClearSlots();
			BuildVisibleEntries();
			EnsureValidSelection();
			FillItemSlots();
			RefreshDetailPanel();
			_titleLabel.Text = _currentCategory switch
			{
				InventoryCategory.Items => "KHO ĐỒ",
				InventoryCategory.Tools => "CÔNG CỤ",
				InventoryCategory.Quests => "NHIỆM VỤ",
				_ => "KHO ĐỒ"
			};
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
				InventoryCategory.Items => item.InventoryCategory == InventoryItemCategory.Equipment || item.InventoryCategory == InventoryItemCategory.Consumable || item.InventoryCategory == InventoryItemCategory.Material || item.InventoryCategory == InventoryItemCategory.Other,
				InventoryCategory.Tools => item.InventoryCategory == InventoryItemCategory.Material || item.InventoryCategory == InventoryItemCategory.Other,
				InventoryCategory.Quests => item.InventoryCategory == InventoryItemCategory.Quest,
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
			style.SetCornerRadiusAll(8);
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
			style.SetCornerRadiusAll(8);
			return style;
		}

		private StyleBoxFlat CreateDetailInnerStyle()
		{
			var style = new StyleBoxFlat();
			style.BgColor = _detailInnerColor;
			style.BorderColor = _slotBorderColor;
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(6);
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
