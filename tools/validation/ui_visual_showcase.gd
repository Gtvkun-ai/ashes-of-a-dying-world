extends Control

const PARTY_HUD_SCENE := preload("res://scenes/ui/hud/party_hud.tscn")
const PLAYER_STATS_SCRIPT := preload("res://scripts/Characters/Stats/PlayerStats.cs")
const COMPONENTS_SCRIPT := preload("res://tools/validation/UiVisualShowcaseComponents.cs")
const DIALOG_BASE_SCENE := preload("res://addons/dialogic/Modules/DefaultLayoutParts/Base_Default/default_layout_base.tscn")
const DIALOG_TEXTBOX_SCENE := preload("res://scenes/ui/dialog/jrpg_textbox.tscn")
const DIALOG_CHOICE_SCENE := preload("res://scenes/ui/dialog/jrpg_choice_layer.tscn")
const PANEL_SHOWCASE_SCENE := preload("res://tools/validation/ui_screen_showcase.tscn")
const MAIN_SCREEN_SCENE := preload("res://scenes/app/screen_main.tscn")
const GAME_MENU_SCENE := preload("res://scenes/ui/menus/game_menu_button.tscn")

# Components exercised by the C# runtime harness:
# EnemyHealthBarService, UiGlyphResolver and PixelButtonSkin.
const SUPPORTED_VIEWPORTS := [Vector2i(1600, 900), Vector2i(1280, 720)]
const SUPPORTED_LIGHTING := ["day", "night"]
const SUPPORTED_MODES := ["overview", "panels", "main", "menu"]
const CONFIG_PATHS := [
	"res://data/characters/main.tres",
	"res://data/characters/hyou.tres",
	"res://data/characters/main.tres",
]
const DISPLAY_NAMES := ["Nguyễn Ánh Dương", "Hyou", "Mai An · Người giữ mầm"]
const RESOURCE_VALUES := [
	Vector3(86.0, 44.0, 72.0),
	Vector3(24.0, 68.0, 41.0),
	Vector3(61.0, 18.0, 93.0),
]

var _stats_nodes: Array[Node] = []
var _party_hud: CanvasLayer
var _dialog_layout: Node
var _panel_layer: CanvasLayer
var _panel_showcase: Control
var _screen_showcase: Node
var _showcase_mode := "overview"
var _lighting := "day"


func _ready() -> void:
	var viewport_size := _requested_viewport_size()
	_showcase_mode = _requested_showcase_mode()
	_lighting = _requested_lighting()
	get_window().size = viewport_size
	custom_minimum_size = viewport_size
	_apply_lighting()
	var showcase_title := get_node_or_null("ShowcaseTitle") as Label
	if showcase_title != null:
		showcase_title.visible = _showcase_mode == "overview"
	await get_tree().process_frame

	if _showcase_mode == "panels":
		_panel_layer = CanvasLayer.new()
		_panel_layer.name = "PanelShowcaseLayer"
		add_child(_panel_layer)
		_panel_showcase = PANEL_SHOWCASE_SCENE.instantiate()
		_panel_showcase.name = "PanelShowcase"
		_panel_showcase.set_meta("showcase_viewport_size", viewport_size)
		_panel_layer.add_child(_panel_showcase)
	elif _showcase_mode == "main" or _showcase_mode == "menu":
		if not await _build_main_menu_sample(viewport_size):
			get_tree().quit(1)
			return
	else:
		var components: Node = COMPONENTS_SCRIPT.new()
		components.name = "RuntimeComponents"
		add_child(components)

		if not await _build_party_hud():
			get_tree().quit(1)
			return
		if not await _build_dialog_sample():
			get_tree().quit(1)
			return

	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame
	if _showcase_mode == "panels":
		for _frame in range(12):
			if _panel_showcase.get_meta("validation_passed", false):
				break
			await get_tree().process_frame

	if not _validate_showcase(viewport_size):
		get_tree().quit(1)
		return

	var capture_path := _capture_path()
	if not capture_path.is_empty():
		await _capture_viewport(capture_path)

	print("UI visual showcase passed at %dx%d · %s · %s" % [viewport_size.x, viewport_size.y, _lighting, _showcase_mode])
	if _should_exit_after_validation() or not capture_path.is_empty():
		await _cleanup()
		get_tree().quit(0)


func _build_main_menu_sample(viewport_size: Vector2i) -> bool:
	var scene := MAIN_SCREEN_SCENE if _showcase_mode == "main" else GAME_MENU_SCENE
	_screen_showcase = scene.instantiate()
	_screen_showcase.name = "MainScreenSample" if _showcase_mode == "main" else "GameMenuSample"
	add_child(_screen_showcase)

	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame
	if get_window().size != viewport_size:
		get_window().size = viewport_size
		custom_minimum_size = viewport_size
		set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		await get_tree().process_frame
		await get_tree().process_frame

	if _showcase_mode == "main":
		await get_tree().process_frame
	else:
		var grid_panel := _screen_showcase.get_node_or_null("Control/MenuGridPanel") as Control
		var character_button := _screen_showcase.get_node_or_null("Control/MenuGridPanel/CenterContainer/MenuSurface/Margin/Layout/MenuGrid/CharacterButton") as Button
		if not _require(grid_panel != null and character_button != null, "Game menu grid nodes are missing"):
			return false
		grid_panel.show()
		character_button.grab_focus()
		await get_tree().process_frame
	return true

func _build_party_hud() -> bool:
	var manager := get_node_or_null("/root/PlayerManager")
	if not _require(manager != null, "PlayerManager autoload is missing"):
		return false
	manager.call("ResetParty")

	for index in DISPLAY_NAMES.size():
		var source_config = load(CONFIG_PATHS[index])
		if not _require(source_config != null, "Could not load %s" % CONFIG_PATHS[index]):
			return false
		var config = source_config.duplicate(true)
		config.set("Name", DISPLAY_NAMES[index])

		var stats: Node = PLAYER_STATS_SCRIPT.new()
		stats.name = "VisualShowcaseStats%d" % (index + 1)
		stats.set("ConfigData", config)
		stats.set("InitialLevel", 12 - index * 2)
		stats.set("UseManualProfile", true)
		stats.set("ManualMaxHP", 100.0)
		stats.set("ManualMaxMP", 100.0)
		stats.set("ManualMaxStamina", 100.0)
		add_child(stats)
		_stats_nodes.append(stats)

	await get_tree().process_frame
	for index in _stats_nodes.size():
		var values: Vector3 = RESOURCE_VALUES[index]
		_stats_nodes[index].call("RestoreResourceValues", values.x, values.y, values.z)

	_party_hud = PARTY_HUD_SCENE.instantiate()
	add_child(_party_hud)
	await get_tree().process_frame
	await get_tree().process_frame

	var units := _party_units()
	if not _require(units.size() == 3, "Party HUD did not render three units"):
		return false
	for index in units.size():
		var strip: HBoxContainer = units[index].get_node("Content/Columns/PortraitColumn/StatusFrame/StatusStrip")
		for badge_index in strip.get_child_count():
			strip.get_child(badge_index).visible = badge_index == index % 3
	return true


func _build_dialog_sample() -> bool:
	_dialog_layout = DIALOG_BASE_SCENE.instantiate()
	add_child(_dialog_layout)

	var textbox: Node = DIALOG_TEXTBOX_SCENE.instantiate()
	_dialog_layout.add_layer(textbox)
	DialogicUtil.apply_scene_export_overrides(textbox, {})

	var name_label := textbox.get_node_or_null("Anchor/AnimationParent/NamePlate/DialogicNode_NameLabel") as Label
	var dialog_text := textbox.get_node_or_null("Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox/DialogicNode_DialogText")
	if not _require(name_label != null and dialog_text != null, "Dialog textbox nodes are missing"):
		return false
	name_label.text = "NGƯỜI GIỮ MẦM"
	dialog_text.text = "Sự sống vẫn xanh. Điều đang tàn đi nằm sâu hơn những gì mắt ta nhìn thấy."

	var choices: Node = DIALOG_CHOICE_SCENE.instantiate()
	_dialog_layout.add_layer(choices)
	DialogicUtil.apply_scene_export_overrides(choices, {})
	await get_tree().process_frame

	var choice_box := choices.get_node_or_null("ChoiceScroll/Choices") as VBoxContainer
	if not _require(choice_box != null, "Dialog choice container is missing"):
		return false
	var buttons: Array[Button] = []
	for child in choice_box.get_children():
		if child is Button:
			buttons.append(child)
	if not _require(buttons.size() >= 2, "Dialogic did not create showcase choices"):
		return false
	for button in buttons:
		button.visible = false
	buttons[0].text = "Ở lại và lắng nghe ký ức còn sót lại."
	buttons[1].text = "Trở về cánh đồng đang hồi sinh."
	buttons[0].visible = true
	buttons[1].visible = true
	choices.call("_style_buttons")
	choices.call("_apply_responsive_layout")
	buttons[0].grab_focus()
	return true


func _validate_showcase(expected_size: Vector2i) -> bool:
	if not _require(SUPPORTED_VIEWPORTS.has(expected_size), "Unsupported showcase viewport"):
		return false
	if not _require(SUPPORTED_LIGHTING.has(_lighting), "Unsupported showcase lighting"):
		return false
	if not _require(SUPPORTED_MODES.has(_showcase_mode), "Unsupported showcase mode"):
		return false
	var night_grade := get_node_or_null("WorldNightGrade") as ColorRect
	if not _require(night_grade != null and night_grade.visible == (_lighting == "night"), "World lighting fixture is inconsistent"):
		return false
	if _showcase_mode == "panels":
		if not _require(is_instance_valid(_panel_showcase), "Runtime panel showcase is missing"):
			return false
		if not _require(_panel_showcase.get_meta("validation_passed", false), "Runtime panel validation did not complete"):
			return false
		var panel_name := str(_panel_showcase.get_meta("active_panel_name", ""))
		var supported_panels := ["InventoryPanel", "CharacterDetailUI", "PartyPanel", "QuestJournalPanel", "SkillTreePanel", "SettingsPanel"]
		if not _require(supported_panels.has(panel_name), "Panel showcase did not report a supported runtime panel"):
			return false
		if not _require(_panel_showcase.get_node_or_null("PanelHost/%s" % panel_name) != null, "%s is missing from panel showcase" % panel_name):
			return false
		return true
	if _showcase_mode == "main" or _showcase_mode == "menu":
		return _validate_main_menu_sample(expected_size)
	if not _require(get_node_or_null("ComponentPanel") != null, "PixelButtonSkin component panel is missing"):
		return false
	if not _require(get_node_or_null("/root/EnemyHealthBarService") != null, "EnemyHealthBarService is missing"):
		return false
	if not _require(_party_units().size() == 3, "Party HUD validation failed"):
		return false
	if not _require(_dialog_layout != null and is_instance_valid(_dialog_layout), "Dialog sample is missing"):
		return false
	var choice_scroll := _dialog_layout.find_child("ChoiceScroll", true, false) as Control
	var party_stack := _party_hud.get_node("VBoxContainer") as Control
	if not _require(choice_scroll != null and party_stack != null, "Overlay bounds are missing"):
		return false
	if not _require(
		not choice_scroll.get_global_rect().intersects(party_stack.get_global_rect()),
		"Dialog choices overlap the right-anchored party HUD"
	):
		return false
	return true


func _validate_main_menu_sample(expected_size: Vector2i) -> bool:
	if not _require(is_instance_valid(_screen_showcase), "Main/menu runtime scene is missing"):
		return false

	if _showcase_mode == "main":
		var main_ui := _screen_showcase.get_node_or_null("MainUi") as Control
		var primary := _screen_showcase.get_node_or_null("MainUi/SafeMargin/Row/MenuSurface/RailMargin/Rail/login") as Button
		var settings := _screen_showcase.get_node_or_null("MainUi/SafeMargin/Row/MenuSurface/RailMargin/Rail/settings") as Button
		var exits := _screen_showcase.get_node_or_null("MainUi/SafeMargin/Row/MenuSurface/RailMargin/Rail/exits") as Button
		if not _require(main_ui != null and primary != null and settings != null and exits != null, "Main screen controls are missing"):
			return false
		if not _require(primary.focus_mode == Control.FOCUS_ALL and settings.focus_mode == Control.FOCUS_ALL and exits.focus_mode == Control.FOCUS_ALL, "Main screen actions are not keyboard focusable"):
			return false
		return _require(_control_fits_viewport(main_ui, expected_size), "Main screen escapes the viewport")

	var grid_panel := _screen_showcase.get_node_or_null("Control/MenuGridPanel") as Control
	if not _require(grid_panel != null and grid_panel.visible, "Game menu grid is not visible"):
		return false
	var grid_path := "Control/MenuGridPanel/CenterContainer/MenuSurface/Margin/Layout/MenuGrid"
	for node_name in ["CharacterButton", "InventoryButton", "SkillsButton", "QuestsButton", "PartyButton", "SettingsButton"]:
		var button := _screen_showcase.get_node_or_null("%s/%s" % [grid_path, node_name]) as Button
		if not _require(button != null and button.focus_mode == Control.FOCUS_ALL, "%s is missing or not focusable" % node_name):
			return false
	return _require(_control_fits_viewport(grid_panel, expected_size), "Game menu grid escapes the viewport")


func _control_fits_viewport(control: Control, expected_size: Vector2i) -> bool:
	var bounds := control.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, control.size)
	var viewport_bounds := Rect2(Vector2.ZERO, Vector2(expected_size))
	return (
		bounds.position.x >= viewport_bounds.position.x - 1.0
		and bounds.position.y >= viewport_bounds.position.y - 1.0
		and bounds.end.x <= viewport_bounds.end.x + 1.0
		and bounds.end.y <= viewport_bounds.end.y + 1.0
	)


func _party_units() -> Array[Node]:
	var result: Array[Node] = []
	if not is_instance_valid(_party_hud):
		return result
	for child in _party_hud.get_node("VBoxContainer").get_children():
		if child.visible:
			result.append(child)
	return result


func _capture_viewport(path: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	if not _require(image != null and not image.is_empty(), "Viewport capture is empty"):
		return
	var output_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
	var result := image.save_png(output_path)
	_require(result == OK, "Could not save showcase capture: %s" % output_path)


func _requested_viewport_size() -> Vector2i:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--showcase-size="):
			var parts := argument.trim_prefix("--showcase-size=").split("x")
			if parts.size() == 2:
				var parsed := Vector2i(parts[0].to_int(), parts[1].to_int())
				if SUPPORTED_VIEWPORTS.has(parsed):
					return parsed
	return Vector2i(1600, 900)


func _capture_path() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--showcase-capture="):
			return argument.trim_prefix("--showcase-capture=")
	return ""


func _requested_lighting() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--showcase-lighting="):
			var requested := argument.trim_prefix("--showcase-lighting=").to_lower()
			if SUPPORTED_LIGHTING.has(requested):
				return requested
	return "day"


func _requested_showcase_mode() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--showcase-mode="):
			var requested := argument.trim_prefix("--showcase-mode=").to_lower()
			if SUPPORTED_MODES.has(requested):
				return requested
	return "overview"


func _apply_lighting() -> void:
	var night_grade := get_node_or_null("WorldNightGrade") as ColorRect
	if night_grade != null:
		night_grade.visible = _lighting == "night"


func _should_exit_after_validation() -> bool:
	return OS.get_cmdline_user_args().has("--showcase-validate")


func _cleanup() -> void:
	var manager := get_node_or_null("/root/PlayerManager")
	if manager != null:
		manager.call("ResetParty")
	if is_instance_valid(_party_hud):
		_party_hud.queue_free()
	if is_instance_valid(_dialog_layout):
		_dialog_layout.queue_free()
	if is_instance_valid(_panel_layer):
		_panel_layer.queue_free()
	elif is_instance_valid(_panel_showcase):
		_panel_showcase.queue_free()
	if is_instance_valid(_screen_showcase):
		_screen_showcase.queue_free()
	for stats in _stats_nodes:
		if is_instance_valid(stats):
			stats.queue_free()
	_stats_nodes.clear()
	await get_tree().process_frame
	await get_tree().process_frame


func _require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error("UI visual showcase failed: %s" % message)
	return false
