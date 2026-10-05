extends Control

const VIEWPORT_SIZE := Vector2(1280, 720)
const SURFACE := Color("#29221B")
const BORDER := Color("#5C4B38")
const TEXT_PRIMARY := Color("#F0E5D2")
const TEXT_SECONDARY := Color("#BFAF98")
const ACCENT := Color("#C39A57")
const INVENTORY_PANEL_SCRIPT := "res://scripts/UI/HUD/InventoryPanel.cs"
const CHARACTER_PANEL_SCRIPT := "res://scripts/UI/HUD/CharacterDetailUI.cs"

func _ready() -> void:
	custom_minimum_size = VIEWPORT_SIZE
	_build_showcase()

func _build_showcase() -> void:
	var background := ColorRect.new()
	background.color = Color("#120F0C")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var title := _label("UI screen showcase · 1280 × 720", 22, TEXT_PRIMARY)
	title.position = Vector2(40, 28)
	add_child(title)

	var inventory_panel := _surface_panel(Vector2(40, 88), Vector2(760, 540), "TÚI ĐỒ")
	add_child(inventory_panel)
	var category := _label("Vật phẩm tiêu hao", 16, ACCENT)
	category.position = Vector2(24, 58)
	category.tooltip_text = "Vật phẩm tiêu hao"
	inventory_panel.add_child(category)

	var item_name := _label("Kiếm trường kiếm cổ đại có chuôi bọc da và các ký tự khắc dọc thân kiếm", 19, TEXT_PRIMARY)
	item_name.position = Vector2(24, 104)
	item_name.size = Vector2(680, 70)
	item_name.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	item_name.tooltip_text = "Kiếm trường kiếm cổ đại có chuôi bọc da và các ký tự khắc dọc thân kiếm"
	inventory_panel.add_child(item_name)

	var empty_state := _label("Không có vật phẩm trong mục này.", 14, TEXT_SECONDARY)
	empty_state.position = Vector2(24, 230)
	empty_state.size = Vector2(680, 42)
	empty_state.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	empty_state.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	inventory_panel.add_child(empty_state)

	var character_panel := _surface_panel(Vector2(824, 88), Vector2(416, 540), "NHÂN VẬT")
	add_child(character_panel)
	var character_name := _label("Nhân vật đồng hành có tên dài để kiểm tra", 18, TEXT_PRIMARY)
	character_name.position = Vector2(24, 62)
	character_name.size = Vector2(360, 68)
	character_name.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	character_name.tooltip_text = "Nhân vật đồng hành có tên dài để kiểm tra"
	character_panel.add_child(character_name)

	var portrait_empty := _label("Chưa có ảnh chân dung", 14, TEXT_SECONDARY)
	portrait_empty.position = Vector2(24, 178)
	portrait_empty.size = Vector2(360, 48)
	portrait_empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	portrait_empty.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	character_panel.add_child(portrait_empty)

	var body_empty := _label("Chưa có dữ liệu nhân vật", 14, TEXT_SECONDARY)
	body_empty.position = Vector2(24, 250)
	body_empty.size = Vector2(360, 48)
	body_empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	body_empty.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	character_panel.add_child(body_empty)

	# Mount real runtime panels so this scene exercises the same controls players use.
	var inventory_runtime := _mount_runtime_panel(load("res://scripts/UI/HUD/InventoryPanel.cs").new(), Vector2(40, 88), Vector2(0.58, 0.58))
	var character_runtime := _mount_runtime_panel(load("res://scripts/UI/HUD/CharacterDetailUI.cs").new(), Vector2(820, 88), Vector2(0.38, 0.38))
	_validate_focus_modes(inventory_runtime)
	_validate_focus_modes(character_runtime)
	_exercise_inventory_filter(inventory_runtime)

	var party_runtime := _mount_runtime_panel(load("res://scripts/UI/Party/PartyPanel.cs").new(), Vector2(40, 510), Vector2(0.27, 0.27))
	var quest_runtime := _mount_runtime_panel(load("res://scripts/UI/Quests/QuestJournalPanel.cs").new(), Vector2(350, 510), Vector2(0.27, 0.27))
	var skill_runtime := _mount_runtime_panel(load("res://scripts/UI/Skills/SkillTreePanel.cs").new(), Vector2(660, 510), Vector2(0.27, 0.27))
	var settings_runtime := _mount_runtime_panel(load("res://scripts/UI/HUD/SettingsPanel.cs").new(), Vector2(970, 510), Vector2(0.27, 0.27))
	for panel in [party_runtime, quest_runtime, skill_runtime, settings_runtime]:
		_validate_secondary_focus(panel)
		panel.set_meta("ReducedMotion", true)

func _mount_runtime_panel(panel: Control, origin: Vector2, panel_scale: Vector2) -> Control:
	add_child(panel)
	panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	panel.position = origin
	panel.scale = panel_scale
	return panel

func _validate_focus_modes(root: Node) -> void:
	var focused_controls := 0
	for node in root.find_children("*", "Control", true, false):
		if node is Control and node.focus_mode == Control.FOCUS_ALL:
			focused_controls += 1
	assert(focused_controls > 0)

func _exercise_inventory_filter(root: Node) -> void:
	var options := root.find_children("*", "OptionButton", true, false)
	if options.is_empty():
		return
	var filter: OptionButton = options[0]
	filter.grab_focus()
	filter.select(0)

func _validate_secondary_focus(root: Node) -> void:
	var focused_controls := 0
	for node in root.find_children("*", "Control", true, false):
		if node is Control and node.focus_mode == Control.FOCUS_ALL:
			focused_controls += 1
	assert(focused_controls > 0)

func _surface_panel(origin: Vector2, size: Vector2, title_text: String) -> Panel:
	var panel := Panel.new()
	panel.position = origin
	panel.size = size
	var style := StyleBoxFlat.new()
	style.bg_color = SURFACE
	style.border_color = BORDER
	style.set_border_width_all(1)
	panel.add_theme_stylebox_override("panel", style)
	var heading := _label(title_text, 18, TEXT_PRIMARY)
	heading.position = Vector2(24, 18)
	panel.add_child(heading)
	return panel

func _label(value: String, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = value
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	return label

