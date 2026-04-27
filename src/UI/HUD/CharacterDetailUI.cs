using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
	public partial class CharacterDetailUI : Control
	{
		// UI Elements
		private VBoxContainer _characterListContainer;
		private VideoStreamPlayer _avatarDisplay;
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
		
		// Cấu hình màu sắc
		private readonly Color _accentColor = new Color("#38bdf8"); 
		private readonly Color _subTextColor = new Color("#94a3b8"); 
		private readonly Color _tabActiveColor = new Color("#38bdf8");
		private readonly Color _tabInactiveColor = new Color("#64748b");
		private string _currentTab = "overview";
		private Color _themeBorderColor = new Color("#38bdf8");   
		private Color _btnNormalColor = new Color("#1e293b");     
		private Color _btnHoverColor = new Color("#334155");       

		private TextureRect _avatarDisplayRect;     // Cái này để hiện lên UI (có thể resize)
		private SubViewport _videoViewport;         // Cái này để chứa video gốc
		private VideoStreamPlayer _hiddenPlayer;    // Cái này là trình phát video thật (nằm ẩn)
		private Color _currentThemeColor;           // Màu theme của nhân vật hiện tại
		private NinePatchRect _panelFrame;          // Khung bọc ngoài toàn bộ UI
		private NinePatchRect _panelGlow;           // Lớp glow phía sau khung panel
		private Control _equipmentBodyContainer;    // Container chứa AnimatedSprite2D body trong tab Equipment
		private GridContainer _inventoryGrid;       // Grid inventory trong tab Equipment (số ô dựa theo InventoryManager.MaxSlots)
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
			// Đặt UI ở giữa màn hình với 80% kích thước
			// Set anchors để UI chiếm 80% màn hình (10% margin mỗi bên)
			AnchorLeft = 0.1f;
			AnchorTop = 0.1f;
			AnchorRight = 0.9f;
			AnchorBottom = 0.9f;
			
			// Reset tất cả offset về 0 để UI theo đúng anchor
			OffsetLeft = 0;
			OffsetTop = 0;
			OffsetRight = 0;
			OffsetBottom = 0;
			
			SetupBackground();
			SetupPanelFrame();

			var mainHBox = new HBoxContainer();
			mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
			mainHBox.AddThemeConstantOverride("separation", 0);
			AddChild(mainHBox);

			SetupCharacterListColumn(mainHBox);
			SetupMainContentColumn(mainHBox);
			SetupAvatarColumn(mainHBox);

			VisibilityChanged += OnVisibilityChanged;
			if (PlayerManager.Instance != null)
			{
				PlayerManager.Instance.ActiveCharacterChanged += OnActiveCharacterChanged;
			}
			
			ApplyIceTheme();
			
			// Add avatar overlay SAU CÙNG để nó render trên tất cả
			AddAvatarOverlay();
			
			// Thêm nút Exit
			AddExitButton();
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

		private void SetupBackground()
		{
			// Tạo TextureRect rỗng, sẽ cập nhật texture khi load character
			_backgroundDisplay = new TextureRect();
			_backgroundDisplay.SetAnchorsPreset(LayoutPreset.FullRect);
			
			// Thêm margin nhỏ để không bị lồi ra ngoài khung bo góc
			_backgroundDisplay.OffsetLeft = 5;
			_backgroundDisplay.OffsetTop = 5;
			_backgroundDisplay.OffsetRight = -5;
			_backgroundDisplay.OffsetBottom = -5;
			
			_backgroundDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_backgroundDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			_backgroundDisplay.ZIndex = -100;  // Đảm bảo luôn nằm dưới cùng
			
			// Bo góc cho background để khớp với khung panel
			ApplyRoundedCorners(_backgroundDisplay, 35f);
			
			AddChild(_backgroundDisplay);
		}

		private void SetupPanelFrame()
		{
			// Load texture khung panel
			var frameTexture = GD.Load<Texture2D>("res://assets/sprites/button/khungPanel.png");
			
			if (frameTexture != null)
			{
				// 1. TẠO LỚP GLOW PHÍA SAU (render trước để nằm dưới)
				_panelGlow = new NinePatchRect();
				_panelGlow.Texture = frameTexture;
				_panelGlow.SetAnchorsPreset(LayoutPreset.FullRect);
				_panelGlow.ZIndex = -1;  // Nằm dưới panel chính
				
				// Cấu hình NinePatch giống panel chính
				_panelGlow.PatchMarginLeft = 40;
				_panelGlow.PatchMarginTop = 40;
				_panelGlow.PatchMarginRight = 40;
				_panelGlow.PatchMarginBottom = 40;
				
				// Tắt vẽ phần giữa cho glow - chỉ vẽ viền
				_panelGlow.DrawCenter = false;
				
				// Tạo shader glow additive
				var glowShaderCode = @"
shader_type canvas_item;
render_mode blend_add, unshaded;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    COLOR = tex;
}
";
				var glowShader = new Shader();
				glowShader.Code = glowShaderCode;
				var glowMaterial = new ShaderMaterial();
				glowMaterial.Shader = glowShader;
				_panelGlow.Material = glowMaterial;
				
				// Set màu mặc định, sẽ update khi có theme color
				_panelGlow.Modulate = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.8f);
				
				// Scale nhỏ hơn để glow mỏng và gần sát viền hơn
				_panelGlow.Scale = new Vector2(1.005f, 1.005f);
				_panelGlow.Position = new Vector2(-2, -2);  // Offset nhỏ để glow đều các cạnh
				
				AddChild(_panelGlow);
				
				// 2. TẠO PANEL CHÍNH (render sau để nằm trên)
				_panelFrame = new NinePatchRect();
				_panelFrame.Texture = frameTexture;
				_panelFrame.SetAnchorsPreset(LayoutPreset.FullRect);
				_panelFrame.ZIndex = 0;
				
				// Cấu hình NinePatch để kéo dãn đúng cách
				_panelFrame.PatchMarginLeft = 40;
				_panelFrame.PatchMarginTop = 40;
				_panelFrame.PatchMarginRight = 40;
				_panelFrame.PatchMarginBottom = 40;
				
				// QUAN TRỌNG: Tắt vẽ phần giữa - chỉ vẽ viền khung, tránh lớp phủ đen
				_panelFrame.DrawCenter = false;
				
				AddChild(_panelFrame);
			}
		}
		
		// Shader bo góc cho NinePatchRect - dựa trên FRAGCOORD thay vì UV
		private void ApplyRoundedCornersForNinePatch(Control node, float radius, bool additive)
		{
			string blendMode = additive ? "render_mode blend_add, unshaded;" : "";
			
			var shaderCode = $@"
shader_type canvas_item;
{blendMode}

uniform float corner_radius = 25.0;

void fragment() {{
    // Lấy kích thước thực của node (pixel)
    vec2 size = 1.0 / TEXTURE_PIXEL_SIZE;
    
    // Vị trí pixel hiện tại trong node
    vec2 pixel_pos = UV * size;
    
    // Tính khoảng cách từ pixel đến 4 góc
    float dist = 0.0;
    
    // Góc trên trái
    if (pixel_pos.x < corner_radius && pixel_pos.y < corner_radius) {{
        dist = length(pixel_pos - vec2(corner_radius));
    }}
    // Góc trên phải  
    else if (pixel_pos.x > size.x - corner_radius && pixel_pos.y < corner_radius) {{
        dist = length(pixel_pos - vec2(size.x - corner_radius, corner_radius));
    }}
    // Góc dưới trái
    else if (pixel_pos.x < corner_radius && pixel_pos.y > size.y - corner_radius) {{
        dist = length(pixel_pos - vec2(corner_radius, size.y - corner_radius));
    }}
    // Góc dưới phải
    else if (pixel_pos.x > size.x - corner_radius && pixel_pos.y > size.y - corner_radius) {{
        dist = length(pixel_pos - vec2(size.x - corner_radius, size.y - corner_radius));
    }}
    
    // Nếu nằm ngoài vùng bo góc thì discard
    if (dist > corner_radius) {{
        discard;
    }}
    
    // Anti-aliasing cho viền bo góc
    float alpha = 1.0 - smoothstep(corner_radius - 1.5, corner_radius, dist);
    
    vec4 tex = texture(TEXTURE, UV);
    
    if (alpha < 0.01) {{
        discard;
    }}
    
    COLOR = vec4(tex.rgb, tex.a * alpha);
}}
";
			
			var shader = new Shader();
			shader.Code = shaderCode;
			
			var material = new ShaderMaterial();
			material.Shader = shader;
			material.SetShaderParameter("corner_radius", radius);
			
			node.Material = material;
		}
		
		// Method để apply shader làm tròn góc
		private void ApplyRoundedCorners(CanvasItem node, float radius)
		{
			var shaderCode = @"
shader_type canvas_item;

uniform float corner_radius = 20.0;

void fragment() {
    vec2 uv = UV;
    vec2 size = 1.0 / TEXTURE_PIXEL_SIZE;
    vec2 pixel_pos = uv * size;
    
    // Tính khoảng cách từ pixel đến góc gần nhất
    vec2 dist_to_corner = max(vec2(0.0), 
        max(vec2(corner_radius) - pixel_pos, 
            pixel_pos - (size - vec2(corner_radius))));
    
    float corner_dist = length(dist_to_corner);
    
    // Cải thiện anti-aliasing với smoothstep rộng hơn
    float alpha = 1.0 - smoothstep(corner_radius - 2.0, corner_radius + 1.0, corner_dist);
    
    vec4 tex = texture(TEXTURE, UV);
    
    // Chỉ áp dụng bo góc nếu alpha > 0, tránh pixel đen
    if (alpha < 0.01) {
        discard; // Loại bỏ hoàn toàn pixel thay vì làm đen
    }
    
    COLOR = vec4(tex.rgb, tex.a * alpha);
}
";
			
			var shader = new Shader();
			shader.Code = shaderCode;
			
			var material = new ShaderMaterial();
			material.Shader = shader;
			material.SetShaderParameter("corner_radius", radius);
			
			node.Material = material;
		}
		
		// Method để apply shader làm tròn góc + blend additive (cho glow)
		private void ApplyRoundedCornersWithBlend(CanvasItem node, float radius)
		{
			var shaderCode = @"
shader_type canvas_item;
render_mode blend_add, unshaded;

uniform float corner_radius = 20.0;

void fragment() {
    vec2 uv = UV;
    vec2 size = 1.0 / TEXTURE_PIXEL_SIZE;
    vec2 pixel_pos = uv * size;
    
    // Tính khoảng cách từ pixel đến góc gần nhất
    vec2 dist_to_corner = max(vec2(0.0), 
        max(vec2(corner_radius) - pixel_pos, 
            pixel_pos - (size - vec2(corner_radius))));
    
    float corner_dist = length(dist_to_corner);
    
    // Anti-aliasing mượt hơn
    float alpha = 1.0 - smoothstep(corner_radius - 3.0, corner_radius + 2.0, corner_dist);
    
    vec4 tex = texture(TEXTURE, UV);
    
    // Loại bỏ pixel ở góc hoàn toàn
    if (alpha < 0.01) {
        discard;
    }
    
    COLOR = vec4(tex.rgb, tex.a * alpha);
}
";
			
			var shader = new Shader();
			shader.Code = shaderCode;
			
			var material = new ShaderMaterial();
			material.Shader = shader;
			material.SetShaderParameter("corner_radius", radius);
			
			node.Material = material;
		}

		private void ApplyIceTheme()
		{
			var btnNormal = new StyleBoxFlat
			{
				BgColor = _btnNormalColor,
				CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
				ContentMarginLeft = 15, ContentMarginRight = 15,
				ContentMarginTop = 8, ContentMarginBottom = 8
			};

			var btnHover = (StyleBoxFlat)btnNormal.Duplicate();
			btnHover.BgColor = _btnHoverColor;
			btnHover.BorderColor = _themeBorderColor;
			btnHover.BorderWidthBottom = 2;

			var btnPressed = (StyleBoxFlat)btnNormal.Duplicate();
			btnPressed.BgColor = new Color("#0f172a");
			btnPressed.BorderColor = _themeBorderColor;
			btnPressed.BorderWidthTop = 2;

			// ĐÃ XÓA: _btnTalents khỏi danh sách này
			Button[] tabButtons = { _btnOverview, _btnEquipment, _btnSkills };

			foreach (var btn in tabButtons)
			{
				if (btn != null)
				{
					btn.AddThemeStyleboxOverride("normal", btnNormal);
					btn.AddThemeStyleboxOverride("hover", btnHover);
					btn.AddThemeStyleboxOverride("pressed", btnPressed);
					btn.AddThemeColorOverride("font_color", _tabInactiveColor);
					btn.AddThemeColorOverride("font_hover_color", Colors.White);
					btn.AddThemeColorOverride("font_pressed_color", _accentColor);
					btn.AddThemeColorOverride("font_focus_color", _accentColor);
				}
			}
		}

		private void SetupCharacterListColumn(HBoxContainer parent)
		{
			// Dùng MarginContainer thay vì PanelContainer để không có nền đen
			var listMargin = new MarginContainer();
			listMargin.CustomMinimumSize = new Vector2(50, 0); 
			listMargin.AddThemeConstantOverride("margin_left", 30);
			listMargin.AddThemeConstantOverride("margin_top", 10);
			parent.AddChild(listMargin);

			var listVBox = new VBoxContainer();
			listMargin.AddChild(listVBox);

			var titleLabel = new Label();
			titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
			listVBox.AddChild(titleLabel);

			_characterListContainer = new VBoxContainer();
			_characterListContainer.AddThemeConstantOverride("separation", 10);
			_characterListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_characterListContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			listVBox.AddChild(_characterListContainer);
		}

		private void SetupMainContentColumn(HBoxContainer parent)
		{
			var contentVBox = new VBoxContainer();
			contentVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			parent.AddChild(contentVBox);

			SetupContentHeader(contentVBox);
			SetupTabBar(contentVBox);
			SetupContentArea(contentVBox);
		}

		private void SetupContentHeader(VBoxContainer parent)
		{
			var headerMargin = new MarginContainer();
			headerMargin.AddThemeConstantOverride("margin_left", 20);
			headerMargin.AddThemeConstantOverride("margin_top", 20);
			parent.AddChild(headerMargin);

			var headerVBox = new VBoxContainer();
			headerMargin.AddChild(headerVBox);

			_nameLabel = new Label();
			_nameLabel.AddThemeFontSizeOverride("font_size", 36);
			_nameLabel.Uppercase = true;
			headerVBox.AddChild(_nameLabel);

			var subInfoHBox = new HBoxContainer();
			subInfoHBox.AddThemeConstantOverride("separation", 15);
			headerVBox.AddChild(subInfoHBox);

			_levelLabel = CreateStyledLabel(18, _accentColor);
			subInfoHBox.AddChild(_levelLabel);

			var separator = new Label();
			separator.Text = "|";
			subInfoHBox.AddChild(separator);

			_raceLabel = CreateStyledLabel(18, _subTextColor);
			subInfoHBox.AddChild(_raceLabel);
		}

		private void SetupTabBar(VBoxContainer parent)
		{
			var tabMargin = new MarginContainer();
			tabMargin.AddThemeConstantOverride("margin_left", 20);
			tabMargin.AddThemeConstantOverride("margin_top", 10);
			parent.AddChild(tabMargin);

			var tabHBox = new HBoxContainer();
			tabHBox.AddThemeConstantOverride("separation", 5);
			tabMargin.AddChild(tabHBox);

			_btnOverview = CreateTabButton("TỔNG QUAN");
			_btnOverview.Pressed += () => SwitchTab("overview");
			tabHBox.AddChild(_btnOverview);

			_btnEquipment = CreateTabButton("TRANG BỊ");
			_btnEquipment.Pressed += () => SwitchTab("equipment");
			tabHBox.AddChild(_btnEquipment);

			_btnSkills = CreateTabButton("KỸ NĂNG");
			_btnSkills.Pressed += () => SwitchTab("skills");
			tabHBox.AddChild(_btnSkills);

		}

		// Nội dung chính bên dưới tab bar
		private void SetupContentArea(VBoxContainer parent)
		{
			var contentMargin = new MarginContainer();
			contentMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
			contentMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			contentMargin.AddThemeConstantOverride("margin_left", 20);
			contentMargin.AddThemeConstantOverride("margin_right", 10);
			contentMargin.AddThemeConstantOverride("margin_top", 10);
			contentMargin.AddThemeConstantOverride("margin_bottom", 50);
			parent.AddChild(contentMargin);

			// Dùng Control với FullRect anchors để các panel con có thể fill đầy
			var contentContainer = new Control();
			contentContainer.SetAnchorsPreset(LayoutPreset.FullRect);
			contentContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			contentContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			contentMargin.AddChild(contentContainer);

			_overviewPanel = CreateOverviewPanel();
			contentContainer.AddChild(_overviewPanel);

			_equipmentPanel = CreateEquipmentPanel();
			contentContainer.AddChild(_equipmentPanel);

			_skillsPanel = CreateSkillsPanelLayout();
			contentContainer.AddChild(_skillsPanel);

		}

		private Control CreateOverviewPanel()
		{
			var panel = new PanelContainer();
			//FullRect dùng để hiển thị panel đúng kích thước cha
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			// Thêm ScrollContainer để tránh bị tràn xuống dưới
			var scrollContainer = new ScrollContainer();
			scrollContainer.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.AddChild(scrollContainer);

			var mainVBox = new VBoxContainer();
			mainVBox.AddThemeConstantOverride("separation", 10);  // Giảm separation từ 15 xuống 10
			mainVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scrollContainer.AddChild(mainVBox);

			

			// 2. Tạo khu vực chứa 2 cột (Stats bên trái, Hexagon bên phải)
			var statsAreaHBox = new HBoxContainer();
			statsAreaHBox.AddThemeConstantOverride("separation", 15);  // Giảm từ 20 xuống 15
			statsAreaHBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;  // Không expand vertical
			mainVBox.AddChild(statsAreaHBox);

			// Cột Trái: Chỉ chứa các dòng STR, DEX... 
			_statsTextContainer = new VBoxContainer();
			_statsTextContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_statsTextContainer.SizeFlagsVertical = SizeFlags.ShrinkCenter;  // Không expand vertical
			_statsTextContainer.AddThemeConstantOverride("separation", 5);  // Thêm separation nhỏ
			statsAreaHBox.AddChild(_statsTextContainer);

			// Cột Phải: Chứa Hexagon Chart
			_overviewStatsChart = new StatHexagonChart();
			_overviewStatsChart.MainColor = _accentColor;
			_overviewStatsChart.ChartRadiusOffset = 30f;  // Giảm từ 35 xuống 30
			_overviewStatsChart.FontSize = 9;  // Giảm từ 10 xuống 9
			_overviewStatsChart.CustomMinimumSize = new Vector2(180, 180);  // Giảm từ 220x220 xuống 180x180
			_overviewStatsChart.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
			_overviewStatsChart.SizeFlagsVertical = SizeFlags.ShrinkCenter;  // Không expand vertical
			statsAreaHBox.AddChild(_overviewStatsChart);

			// --- Phần thanh HP, MP, Stamina ở dưới
			_resourceBarsContainer = new VBoxContainer();
			_resourceBarsContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			_resourceBarsContainer.SizeFlagsVertical = SizeFlags.ShrinkCenter;  // Không expand vertical
			_resourceBarsContainer.AddThemeConstantOverride("separation", 1);  // Giảm từ 8 xuống 5
			mainVBox.AddChild(_resourceBarsContainer);

			// Tạo sẵn các thanh Bar
			_resourceBarsContainer.AddChild(CreateResourceBarRow("HP", new Color("#ef4444"), out _hpBar, out _hpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow("MP", new Color("#3b82f6"), out _mpBar, out _mpValueLabel));
			_resourceBarsContainer.AddChild(CreateResourceBarRow("STA", new Color("#22c55e"), out _staminaBar, out _staminaValueLabel));

			return panel;
		}

		private Control CreateEquipmentPanel()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect); 
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			// HBox chính chia 2 phần: [Bên trái: Slots + Body] | [Bên phải: Inventory Grid]
			var mainHBox = new HBoxContainer();
			mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
			mainHBox.AddThemeConstantOverride("separation", 15);
			mainHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			mainHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
			panel.AddChild(mainHBox);

			// ========== NỬA TRÁI: Character + Equipment Slots ==========
			var leftHBox = new HBoxContainer();
			leftHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			leftHBox.SizeFlagsVertical = SizeFlags.ExpandFill;
			leftHBox.AddThemeConstantOverride("separation", 2);
			leftHBox.Alignment = BoxContainer.AlignmentMode.Center;
			mainHBox.AddChild(leftHBox);

			// --- Cột slot trái (3 slot) ---
			var leftSlotsVBox = new VBoxContainer();
			leftSlotsVBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			leftSlotsVBox.AddThemeConstantOverride("separation", 4);
			leftSlotsVBox.Alignment = BoxContainer.AlignmentMode.Center;
			leftHBox.AddChild(leftSlotsVBox);

			CreateEquipmentSlot(leftSlotsVBox, "Đầu", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.Head);
			CreateEquipmentSlot(leftSlotsVBox, "Áo", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.Body);
			CreateEquipmentSlot(leftSlotsVBox, "Quần", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.Legs);

			// --- Body Idle ở giữa ---
			_equipmentBodyContainer = new Control();
			_equipmentBodyContainer.CustomMinimumSize = new Vector2(80, 160);
			_equipmentBodyContainer.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			leftHBox.AddChild(_equipmentBodyContainer);

			// --- Cột slot phải (3 slot) ---
			var rightSlotsVBox = new VBoxContainer();
			rightSlotsVBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			rightSlotsVBox.AddThemeConstantOverride("separation", 4);
			rightSlotsVBox.Alignment = BoxContainer.AlignmentMode.Center;
			leftHBox.AddChild(rightSlotsVBox);

			CreateEquipmentSlot(rightSlotsVBox, "Giày", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.Accessory2);
			CreateEquipmentSlot(rightSlotsVBox, "Vũ khí", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.MainHand);
			CreateEquipmentSlot(rightSlotsVBox, "Phụ", "res://assets/resources/data/icon/Exit.tres", EquipmentSlot.OffHand);

			// ========== NỬA PHẢI: Inventory Grid (theo số ô của túi đồ) ==========
			var inventoryVBox = new VBoxContainer();
			inventoryVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			inventoryVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
			inventoryVBox.AddThemeConstantOverride("separation", 8);
			mainHBox.AddChild(inventoryVBox);
			// Grid 4 cột, số hàng tùy theo MaxSlots của InventoryManager (mặc định 20 ô => 5 hàng)
			_inventoryGrid = new GridContainer();
			_inventoryGrid.Columns = 4;
			_inventoryGrid.AddThemeConstantOverride("h_separation", 6);
			_inventoryGrid.AddThemeConstantOverride("v_separation", 6);
			_inventoryGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_inventoryGrid.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			inventoryVBox.AddChild(_inventoryGrid);

			// Tạo số ô inventory dựa trên MaxSlots của InventoryManager (nếu tìm được),
			// nếu không thì mặc định 20 ô để khớp với cấu hình túi đồ.
			var inventory = ResolveInventoryManager();
			int slotCount = inventory != null ? inventory.MaxSlots : 20;
			if (slotCount < 1)
			{
				slotCount = 20;
			}

			for (int i = 0; i < slotCount; i++)
			{
				CreateInventorySlot(_inventoryGrid);
			}

			return panel;
		}

		// Tạo 1 ô inventory nhỏ (nền đen, viền theo màu nhân vật)
		private void CreateInventorySlot(GridContainer parent)
		{
			var slot = new PanelContainer();
			slot.CustomMinimumSize = new Vector2(48, 48);
			int slotIndex = _inventorySlotButtons.Count;

			var style = new StyleBoxFlat();
			style.BgColor = new Color(0, 0, 0, 0.85f); // Nền đen
			Color borderCol = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor; // Màu viền theo theme nhân vật hoặc mặc định
			style.BorderColor = new Color(borderCol.R, borderCol.G, borderCol.B, 0.6f);
			style.SetBorderWidthAll(2); // Viền 2px
			style.SetCornerRadiusAll(4); // Bo góc nhẹ
			slot.AddThemeStyleboxOverride("panel", style);

			// Icon/placeholder
			var center = new CenterContainer();
			slot.AddChild(center);

			var content = new VBoxContainer();
			content.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			content.SizeFlagsVertical = SizeFlags.ShrinkCenter; // Không expand, giữ kích thước vừa đủ cho icon và label
			content.Alignment = BoxContainer.AlignmentMode.Center; // Căn giữa cả icon và label
			content.AddThemeConstantOverride("separation", 1);
			center.AddChild(content);

			var iconRect = new TextureRect();
			iconRect.CustomMinimumSize = new Vector2(24, 24);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.Visible = false;
			content.AddChild(iconRect);

			var lbl = new Label();
			lbl.Text = "";
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			lbl.AddThemeFontSizeOverride("font_size", 8);
			lbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f, 0.5f));
			content.AddChild(lbl);

			// Hover effect
			var hoverBtn = new Button();
			hoverBtn.Text = "";
			hoverBtn.SetAnchorsPreset(LayoutPreset.FullRect); // Đảm bảo button phủ toàn bộ slot để bắt hover
			hoverBtn.MouseDefaultCursorShape = CursorShape.PointingHand; // Đổi con trỏ khi hover
			var normalStyle = new StyleBoxFlat();
			normalStyle.BgColor = new Color(0, 0, 0, 0);
			hoverBtn.AddThemeStyleboxOverride("normal", normalStyle);
			var hoverStyle = new StyleBoxFlat();
			hoverStyle.BgColor = new Color(1f, 1f, 1f, 0.08f);
			hoverStyle.SetCornerRadiusAll(4);
			hoverBtn.AddThemeStyleboxOverride("hover", hoverStyle);
			hoverBtn.Pressed += () => OnInventorySlotPressed(slotIndex);
			slot.AddChild(hoverBtn);

			_inventorySlotIcons.Add(iconRect);
			_inventorySlotLabels.Add(lbl);
			_inventorySlotButtons.Add(hoverBtn);
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
			var slotVBox = new VBoxContainer();
			slotVBox.AddThemeConstantOverride("separation", 2);
			slotVBox.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

			// Kích thước nhỏ
			float slotSize = 36f;
			float panelSize = slotSize + 10f;

			var slotPanel = new Panel();
			slotPanel.CustomMinimumSize = new Vector2(panelSize, panelSize);
			slotPanel.Size = new Vector2(panelSize, panelSize);
			slotPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			slotPanel.SizeFlagsVertical = SizeFlags.ShrinkCenter;

			Color borderCol = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor;
			var slotStyle = new StyleBoxFlat();
			slotStyle.BgColor = new Color(0, 0, 0, 0.8f); // Nền đen
			slotStyle.BorderColor = new Color(borderCol.R, borderCol.G, borderCol.B, 0.6f);
			slotStyle.SetBorderWidthAll(2);
			slotStyle.SetCornerRadiusAll(5);
			slotPanel.AddThemeStyleboxOverride("panel", slotStyle);

			Texture2D defaultTexture = null;
			if (!string.IsNullOrEmpty(iconPath))
			{
				defaultTexture = GD.Load<Texture2D>(iconPath);
			}

			var iconRect = new TextureRect();
			iconRect.Texture = defaultTexture;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SetAnchorsPreset(LayoutPreset.Center);
			iconRect.Size = new Vector2(slotSize, slotSize);
			iconRect.Position = new Vector2(-slotSize / 2f, -slotSize / 2f);
			slotPanel.AddChild(iconRect);

			// Button trong suốt để bắt click
			var clickButton = new Button();
			clickButton.Text = "";
			clickButton.SetAnchorsPreset(LayoutPreset.FullRect);
			clickButton.MouseDefaultCursorShape = CursorShape.PointingHand;
			var transparentStyle = new StyleBoxFlat();
			transparentStyle.BgColor = new Color(0, 0, 0, 0);
			clickButton.AddThemeStyleboxOverride("normal", transparentStyle);
			var hoverStyle = new StyleBoxFlat();
			hoverStyle.BgColor = new Color(1f, 1f, 1f, 0.12f);
			hoverStyle.SetCornerRadiusAll(5);
			clickButton.AddThemeStyleboxOverride("hover", hoverStyle);
			if (slotType.HasValue)
			{
				var capturedSlot = slotType.Value;
				clickButton.Pressed += () => OnEquipmentSlotPressed(capturedSlot);
			}
			slotPanel.AddChild(clickButton);

			slotVBox.AddChild(slotPanel);

			// Label tên slot nhỏ
			var nameLabel = new Label();
			nameLabel.Text = slotName;
			nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			nameLabel.AddThemeFontSizeOverride("font_size", 10);
			nameLabel.AddThemeColorOverride("font_color", _subTextColor);
			slotVBox.AddChild(nameLabel);

			if (slotType.HasValue)
			{
				_equipmentSlotIcons[slotType.Value] = iconRect;
				_equipmentSlotDefaultIcons[slotType.Value] = defaultTexture;
				_equipmentSlotButtons[slotType.Value] = clickButton;
			}

			parent.AddChild(slotVBox);
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

		private Control CreateSkillsPanelPlaceholder()
		{
			var panel = new PanelContainer();
			// QUAN TRỌNG: Set FullRect
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;

			// Thêm style nền
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			var label = new Label();
			label.Text = "KỸ NĂNG (Đang cập nhật)";
			label.HorizontalAlignment = HorizontalAlignment.Center;
			panel.AddChild(label);

			return panel;
		}

		// Tạo style chung cho các panel - trong suốt với viền phát sáng
		private Control CreateSkillsPanelLayout()
		{
			var panel = new PanelContainer();
			panel.SetAnchorsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

			var scrollContainer = new ScrollContainer();
			scrollContainer.SetAnchorsPreset(LayoutPreset.FullRect);
			scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			panel.AddChild(scrollContainer);

			var mainVBox = new VBoxContainer();
			mainVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			mainVBox.AddThemeConstantOverride("separation", 12);
			scrollContainer.AddChild(mainVBox);

			_skillsListContainer = new VBoxContainer();
			_skillsListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_skillsListContainer.AddThemeConstantOverride("separation", 10);
			mainVBox.AddChild(_skillsListContainer);

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
			card.CustomMinimumSize = new Vector2(0, 104);

			Color borderColor = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor;
			var cardStyle = new StyleBoxFlat();
			cardStyle.BgColor = new Color(0.02f, 0.04f, 0.08f, 0.72f);
			cardStyle.BorderColor = new Color(borderColor.R, borderColor.G, borderColor.B, 0.6f);
			cardStyle.SetBorderWidthAll(2);
			cardStyle.SetCornerRadiusAll(10);
			cardStyle.ContentMarginLeft = 12;
			cardStyle.ContentMarginRight = 12;
			cardStyle.ContentMarginTop = 12;
			cardStyle.ContentMarginBottom = 12;
			card.AddThemeStyleboxOverride("panel", cardStyle);

			var contentHBox = new HBoxContainer();
			contentHBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			contentHBox.AddThemeConstantOverride("separation", 12);
			card.AddChild(contentHBox);

			contentHBox.AddChild(CreateSkillIconFrame(skill));

			var textVBox = new VBoxContainer();
			textVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			textVBox.AddThemeConstantOverride("separation", 6);
			contentHBox.AddChild(textVBox);

			var titleLabel = new Label();
			titleLabel.Text = string.IsNullOrWhiteSpace(skill?.SkillName) ? "Ky nang chua dat ten" : skill.SkillName;
			titleLabel.AddThemeFontSizeOverride("font_size", 18);
			titleLabel.AddThemeColorOverride("font_color", Colors.White);
			textVBox.AddChild(titleLabel);

			var descriptionLabel = new Label();
			descriptionLabel.Text = string.IsNullOrWhiteSpace(skill?.Description)
				? "Ky nang nay chua co mo ta."
				: skill.Description;
			descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			descriptionLabel.AddThemeFontSizeOverride("font_size", 13);
			descriptionLabel.AddThemeColorOverride("font_color", _subTextColor);
			textVBox.AddChild(descriptionLabel);

			return card;
		}

		private Control CreateSkillIconFrame(SkillData skill)
		{
			var frame = new PanelContainer();
			frame.CustomMinimumSize = new Vector2(84, 84);

			Color borderColor = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor;
			var frameStyle = new StyleBoxFlat();
			frameStyle.BgColor = new Color(0f, 0f, 0f, 0.55f);
			frameStyle.BorderColor = new Color(borderColor.R, borderColor.G, borderColor.B, 0.85f);
			frameStyle.SetBorderWidthAll(2);
			frameStyle.SetCornerRadiusAll(8);
			frameStyle.ContentMarginLeft = 8;
			frameStyle.ContentMarginRight = 8;
			frameStyle.ContentMarginTop = 8;
			frameStyle.ContentMarginBottom = 8;
			frame.AddThemeStyleboxOverride("panel", frameStyle);

			var center = new CenterContainer();
			frame.AddChild(center);

			if (skill?.Icon != null)
			{
				var iconRect = new TextureRect();
				iconRect.Texture = skill.Icon;
				iconRect.CustomMinimumSize = new Vector2(56, 56);
				iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional;
				iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				center.AddChild(iconRect);
			}
			else
			{
				var fallbackLabel = new Label();
				fallbackLabel.Text = "NO ICON";
				fallbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
				fallbackLabel.VerticalAlignment = VerticalAlignment.Center;
				fallbackLabel.AddThemeFontSizeOverride("font_size", 11);
				fallbackLabel.AddThemeColorOverride("font_color", _subTextColor);
				center.AddChild(fallbackLabel);
			}

			return frame;
		}

		private Control CreateSkillEmptyState()
		{
			var emptyPanel = new PanelContainer();
			emptyPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

			Color borderColor = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor;
			var style = new StyleBoxFlat();
			style.BgColor = new Color(0.02f, 0.04f, 0.08f, 0.55f);
			style.BorderColor = new Color(borderColor.R, borderColor.G, borderColor.B, 0.45f);
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(10);
			style.ContentMarginLeft = 16;
			style.ContentMarginRight = 16;
			style.ContentMarginTop = 16;
			style.ContentMarginBottom = 16;
			emptyPanel.AddThemeStyleboxOverride("panel", style);

			var label = new Label();
			label.Text = "Nhan vat nay chua duoc gan du lieu ky nang.";
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", _subTextColor);
			emptyPanel.AddChild(label);

			return emptyPanel;
		}

		private StyleBoxFlat GetCommonPanelStyle()
		{
			var style = new StyleBoxFlat();
			
			Color themeColor = _currentThemeColor != default ? _currentThemeColor : _themeBorderColor;
			
			// 1. QUAN TRỌNG: Phải set DrawCenter = true thì mới có màu nền để chỉnh Alpha
			style.DrawCenter = true;

			// 2. Nền: Màu theme với độ Alpha thấp (0.1f = 10% đậm)
			// Nếu bạn muốn trong hơn nữa, thử 0.05f. Đừng dùng 0.01f vì sẽ không thấy gì cả.
			style.BgColor = new Color(themeColor.R, themeColor.G, themeColor.B, 0.01f);
			
			// Viền
			style.BorderColor = new Color(themeColor.R, themeColor.G, themeColor.B, 0.9f);
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(8);
			
			// 3. SỬA SHADOW: Giảm Alpha của shadow xuống 0.1f hoặc tắt luôn nếu muốn kính trong vắt
			// Shadow quá đậm sẽ làm nền bị tối khi nhìn xuyên qua
			style.ShadowColor = new Color(themeColor.R, themeColor.G, themeColor.B, 0.1f); 
			style.ShadowSize = 8;
			
			style.ContentMarginLeft = 15;
			style.ContentMarginRight = 15;
			style.ContentMarginTop = 10;
			style.ContentMarginBottom = 10;
			
			return style;
		}
		private void SetupAvatarColumn(HBoxContainer parent)
		{
			// Dùng MarginContainer thay vì PanelContainer để không có nền đen
			var avatarMargin = new MarginContainer();
			avatarMargin.CustomMinimumSize = new Vector2(250, 0);  // Giảm để cột content rộng hơn
			avatarMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
			avatarMargin.AddThemeConstantOverride("margin_left", 0);
			avatarMargin.AddThemeConstantOverride("margin_right", 0);
			avatarMargin.AddThemeConstantOverride("margin_top", 0);    // Không margin trên
			avatarMargin.AddThemeConstantOverride("margin_bottom", 0); // Không margin dưới
			parent.AddChild(avatarMargin);

			// 1. TẠO VIEWPORT & PLAYER ẨN (Nơi render video gốc)
			// Lưu ý: Viewport cần kích thước cố định bằng đúng độ phân giải video của bạn
			_videoViewport = new SubViewport();
			_videoViewport.Size = new Vector2I(980, 1420);
			_videoViewport.TransparentBg = true; // Để nền trong suốt cho Shader hoạt động tốt
			_videoViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible; // Tối ưu hiệu năng
			AddChild(_videoViewport); // Add vào cây nhưng nó sẽ không hiện ra màn hình

			_hiddenPlayer = new VideoStreamPlayer();
			_hiddenPlayer.Loop = false;  // Tắt loop, sẽ dùng signal Finished để restart
			_hiddenPlayer.Autoplay = false;  // Tắt autoplay, sẽ play thủ công
			_hiddenPlayer.VolumeDb = -80;
			_hiddenPlayer.BufferingMsec = 0;
			_hiddenPlayer.Finished += OnVideoFinished;  // Khi video kết thúc sẽ restart
			_videoViewport.AddChild(_hiddenPlayer); // Nhét Player vào trong Viewport

			// TẠO TEXTURE RECT (Nơi hiển thị trên UI)
			_avatarDisplayRect = new TextureRect();
			_avatarDisplayRect.SetAnchorsPreset(LayoutPreset.FullRect);
			_avatarDisplayRect.ZIndex = 100;  // Z-index rất cao để luôn hiển thị trên cùng
			
			// ĐÂY LÀ CHÌA KHÓA: TextureRect hỗ trợ Expand!
			_avatarDisplayRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;  // Giữ nguyên chiều cao
			_avatarDisplayRect.StretchMode = TextureRect.StretchModeEnum.KeepAspect; // Không crop, giữ tỉ lệ
			
			// Lấy texture từ Viewport gán vào Rect
			_avatarDisplayRect.Texture = _videoViewport.GetTexture();

			// --- SETUP SHADER ---
			// Bây giờ bạn gắn Shader vào TextureRect chứ không phải VideoStreamPlayer
			var chromaShader = GD.Load<Shader>("res://assets/shader/chroma_key.gdshader");
			if (chromaShader != null)
			{
				var shaderMaterial = new ShaderMaterial();
				shaderMaterial.Shader = chromaShader;
				// Cấu hình tham số cho Shader 
				shaderMaterial.SetShaderParameter("chroma_key", new Vector3(0f, 1f, 0f));
				// Tham số điều chỉnh hiệu ứng - đã tối ưu để giảm vỡ pixel
				shaderMaterial.SetShaderParameter("similarity", 0.35f);   // Giảm xuống để bớt ăn vào subject
				shaderMaterial.SetShaderParameter("smoothness", 0.4f);   // Tăng lên để edge mượt hơn
				shaderMaterial.SetShaderParameter("spill", 0.6f);         // Giảm xuống để giữ màu gốc tốt hơn
				
				_avatarDisplayRect.Material = shaderMaterial; // Gán vào Rect
			}

		}
		
		// Method riêng để add avatar overlay - gọi sau cùng trong _Ready()
		private void AddAvatarOverlay()
		{
			// Không dùng TopLevel nữa - avatar sẽ nằm trong bounds của UI 80%
			_avatarDisplayRect.SetAnchorsPreset(LayoutPreset.RightWide);
			_avatarDisplayRect.OffsetLeft = -320;  // Chiều rộng cột avatar
			_avatarDisplayRect.ZIndex = 100;  // Đảm bảo hiển thị trên các panel
			AddChild(_avatarDisplayRect);
		}
		
		private void AddExitButton()
		{
			var exitTexture = GD.Load<Texture2D>("res://assets/resources/data/icon/Exit.tres");
			
			var exitBtn = new TextureButton();
			exitBtn.TextureNormal = exitTexture;
			exitBtn.CustomMinimumSize = new Vector2(50, 50);
			exitBtn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
			exitBtn.IgnoreTextureSize = true;
			
			// Đặt nút ở góc trên phải
			exitBtn.SetAnchorsPreset(LayoutPreset.TopRight);
			exitBtn.Position = new Vector2(-80, 30);
			
			// Hiệu ứng hover - làm sáng hơn khi di chuột qua
			var hoverStyle = new StyleBoxFlat();
			hoverStyle.BgColor = new Color(1f, 1f, 1f, 0.2f);
			hoverStyle.SetCornerRadiusAll(25);
			exitBtn.AddThemeStyleboxOverride("hover", hoverStyle);
			
			exitBtn.Pressed += OnExitPressed;
			
			AddChild(exitBtn);
		}
		
		private void OnExitPressed()
		{
			Visible = false;
		}
		
		private Label CreateStyledLabel(int size, Color color)
		{
			var lbl = new Label();
			lbl.AddThemeFontSizeOverride("font_size", size);
			lbl.AddThemeColorOverride("font_color", color);
			return lbl;
		}

		private Button CreateTabButton(string text)
		{
			var btn = new Button();
			btn.Text = text;
			btn.CustomMinimumSize = new Vector2(120, 35);
			return btn;
		}

		private void SwitchTab(string tabName)
		{
			HidePanel(_overviewPanel);
			HidePanel(_equipmentPanel);
			HidePanel(_skillsPanel);
			// ĐÃ XÓA: HidePanel(_talentsPanel);
			
			ResetTabButtonColors();

			switch (tabName)
			{
				case "overview":
					ShowPanel(_overviewPanel);
					_btnOverview.AddThemeColorOverride("font_color", _tabActiveColor);
					break;
				case "equipment":
					ShowPanel(_equipmentPanel);
					_btnEquipment.AddThemeColorOverride("font_color", _tabActiveColor);
					break;
				case "skills":
					ShowPanel(_skillsPanel);
					_btnSkills.AddThemeColorOverride("font_color", _tabActiveColor);
					break;
				// ĐÃ XÓA: Case talents
			}
			_currentTab = tabName; 
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
			_btnOverview.AddThemeColorOverride("font_color", _tabInactiveColor);
			_btnEquipment.AddThemeColorOverride("font_color", _tabInactiveColor);
			_btnSkills.AddThemeColorOverride("font_color", _tabInactiveColor);
			// ĐÃ XÓA: Talents button reset
		}

		// Cập nhật style của các panel với màu theme mới
		private void UpdatePanelStyles()
		{
			var newStyle = GetCommonPanelStyle();
			
			if (_overviewPanel is PanelContainer overviewPanelContainer)
				overviewPanelContainer.AddThemeStyleboxOverride("panel", newStyle);
			
			if (_equipmentPanel is PanelContainer equipmentPanelContainer)
				equipmentPanelContainer.AddThemeStyleboxOverride("panel", newStyle);
			
			if (_skillsPanel is PanelContainer skillsPanelContainer)
				skillsPanelContainer.AddThemeStyleboxOverride("panel", newStyle);
				
			// Cập nhật màu accent cho các label
			Color themeColor = _currentThemeColor != default ? _currentThemeColor : _accentColor;
			_levelLabel?.AddThemeColorOverride("font_color", themeColor);
			
			// Cập nhật màu glow của khung panel
			if (_panelGlow != null)
			{
				// Giữ nguyên shader, chỉ update màu modulate
				var currentMaterial = _panelGlow.Material;
				_panelGlow.Modulate = new Color(themeColor.R, themeColor.G, themeColor.B, 0.8f);
				
				// Tạo lại shader material với màu mới nếu cần
				if (currentMaterial is ShaderMaterial)
				{
					_panelGlow.Material = currentMaterial; // Giữ nguyên shader
				}
			}
		}

		private void LoadCharacterList()
		{
			var children = _characterListContainer.GetChildren();
			foreach (var child in children) child.QueueFree();

			for (int i = 0; i < PlayerManager.Instance.PartyMembers.Count; i++)
			{
				int index = i; 
				var character = PlayerManager.Instance.PartyMembers[i];
				
				if (character?.ConfigData?.Icon != null)
				{
					var btn = new TextureButton();
					btn.TextureNormal = character.ConfigData.Icon;
					btn.CustomMinimumSize = new Vector2(70, 70);
					btn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
					btn.IgnoreTextureSize = true;
					btn.Pressed += () => OnCharacterSelected(index);
					
					if (index == PlayerManager.Instance.ActiveCharacterIndex)
					{
						var style = new StyleBoxFlat();
						style.BgColor = new Color(1, 1, 0, 0.3f);
						style.SetBorderWidthAll(3);
						style.BorderColor = _accentColor;
						style.SetCornerRadiusAll(5);
						btn.AddThemeStyleboxOverride("normal", style);
					}
					_characterListContainer.AddChild(btn);
				}
			}
		}

		private void OnCharacterSelected(int index)
		{
			PlayerManager.Instance.SetActiveCharacter(index);
			UpdateCharacterInfo();
		}

		private void OnVisibilityChanged()
		{
			if (Visible) UpdateCharacterInfo();
		}

		public void UpdateCharacterInfo()
		{
			var activeIndex = PlayerManager.Instance.ActiveCharacterIndex;
			if (activeIndex >= PlayerManager.Instance.PartyMembers.Count) return;

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
			if (config.BackgroundImage != null && _backgroundDisplay != null)
			{
				_backgroundDisplay.Texture = config.BackgroundImage;
			}
			
			// Cập nhật style panel với màu theme mới
			UpdatePanelStyles();
			
			if (config.Avatar is VideoStream videoStream)
				{
					// Dừng video cũ nếu có
					if (_hiddenPlayer.IsPlaying())
					{
						_hiddenPlayer.Stop();
					}
					
					// Gán stream mới
					_hiddenPlayer.Stream = videoStream;
					_hiddenPlayer.Loop = true;  // Đảm bảo loop được set
					
					// Chờ 1 frame rồi mới play để đảm bảo stream đã load
					CallDeferred(MethodName.PlayVideoDeferred);
				}
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
					var hbox = new HBoxContainer();
					hbox.AddThemeConstantOverride("separation", 10);
					
					// Thêm icon cho stat
					string statShortName = FormatStatName(attr.Key.ToString());
					var iconTexture = LoadStatIcon(statShortName);
					if (iconTexture != null)
					{
						var iconRect = new TextureRect();
						iconRect.Texture = iconTexture;
						iconRect.CustomMinimumSize = new Vector2(24, 24);
						iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitHeight;
						iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
						hbox.AddChild(iconRect);
					}
					
					var nameLabel = new Label();
					nameLabel.Text = statShortName;
					nameLabel.CustomMinimumSize = new Vector2(70, 0);
					nameLabel.AddThemeFontSizeOverride("font_size", 16);
					nameLabel.AddThemeColorOverride("font_color", _subTextColor);
					hbox.AddChild(nameLabel);

					var valueLabel = new Label();
					valueLabel.Text = attr.Value.ToString();
					valueLabel.AddThemeFontSizeOverride("font_size", 16);
					valueLabel.AddThemeColorOverride("font_color", Colors.White);
					hbox.AddChild(valueLabel);

					_statsTextContainer.AddChild(hbox);
				}
			}

			if (_overviewStatsChart != null)
			{
				_overviewStatsChart.ClearStats();
				if (stats.FinalAttributes != null)
				{
					foreach (var attr in stats.FinalAttributes)
					{
						_overviewStatsChart.SetStat(FormatStatName(attr.Key.ToString()), attr.Value);
					}
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
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = labelText;
			nameLabel.CustomMinimumSize = new Vector2(35, 0);
			nameLabel.AddThemeFontSizeOverride("font_size", 12);
			nameLabel.AddThemeColorOverride("font_color", _subTextColor);
			row.AddChild(nameLabel);

			bar = new ProgressBar();
			bar.CustomMinimumSize = new Vector2(200, 8);
			bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			bar.ShowPercentage = false;

			var bg = new StyleBoxFlat();
			bg.BgColor = new Color(0.1f, 0.12f, 0.2f, 1f);
			bg.CornerRadiusTopLeft = 1;
			bg.CornerRadiusTopRight = 1;
			bg.CornerRadiusBottomLeft = 1;
			bg.CornerRadiusBottomRight = 1;
			bg.ContentMarginTop = -5;
			bg.ContentMarginBottom = -5;
			bg.ContentMarginLeft = 0;
			bg.ContentMarginRight = 0;

			var fill = new StyleBoxFlat();
			fill.BgColor = fillColor;
			fill.CornerRadiusTopLeft = 1;
			fill.CornerRadiusTopRight = 1;
			fill.CornerRadiusBottomLeft = 1;
			fill.CornerRadiusBottomRight = 1;
			fill.ContentMarginTop = -5;
			fill.ContentMarginBottom = -5;
			fill.ContentMarginLeft = 0;
			fill.ContentMarginRight = 0;

			bar.AddThemeStyleboxOverride("background", bg);
			bar.AddThemeStyleboxOverride("fill", fill);

			row.AddChild(bar);

			valueLabel = new Label();
			valueLabel.Text = "0/0";
			valueLabel.CustomMinimumSize = new Vector2(50, 0);
			valueLabel.AddThemeFontSizeOverride("font_size", 8);
			valueLabel.AddThemeColorOverride("font_color", Colors.White);
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

		// Kiểm tra mỗi frame xem video đã kết thúc chưa để restart
		public override void _Process(double delta)
		{
			// Chỉ xử lý khi UI đang hiển thị và có video stream
			if (!Visible || _hiddenPlayer == null || _hiddenPlayer.Stream == null)
				return;

			// Lấy độ dài video (nếu có)
			double streamLength = _hiddenPlayer.GetStreamLength();
			
			// Kiểm tra nếu video đã chạy đến cuối hoặc đã dừng
			// StreamPosition >= streamLength - 0.1 nghĩa là gần hết video
			// Hoặc IsPlaying() = false nghĩa là đã dừng
			bool isAtEnd = streamLength > 0 && _hiddenPlayer.StreamPosition >= streamLength - 0.1;
			bool isStopped = !_hiddenPlayer.IsPlaying();
			
			if (isAtEnd || isStopped)
			{
				// Reset về đầu và play lại
				_hiddenPlayer.Stop();
				_hiddenPlayer.Play();
			}
		}

		// Mẹo nhỏ: Dùng CallDeferred để play video tránh lỗi trạng thái
		// Call Deferred đảm bảo hàm được gọi sau khi frame hiện tại kết thúc
		private void PlayVideoDeferred()
		{
			if (_hiddenPlayer != null && _hiddenPlayer.Stream != null)
			{
				// Mẹo: Stop trước khi Play để reset con trỏ về 0 chắc chắn
				_hiddenPlayer.Stop(); 
				_hiddenPlayer.Play();
			}
		}

		// Signal handler khi video kết thúc - restart video
		private void OnVideoFinished()
		{
			// Dùng CallDeferred để đợi hết frame hiện tại rồi mới Play lại
			// Tránh xung đột trạng thái
			CallDeferred(MethodName.PlayVideoDeferred);
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
