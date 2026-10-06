extends Control

const PANEL_SPECS := {
	"inventory": ["res://scripts/UI/HUD/InventoryPanel.cs", "InventoryPanel"],
	"character": ["res://scripts/UI/HUD/CharacterDetailUI.cs", "CharacterDetailUI"],
	"party": ["res://scripts/UI/Party/PartyPanel.cs", "PartyPanel"],
	"quest": ["res://scripts/UI/Quests/QuestJournalPanel.cs", "QuestJournalPanel"],
	"skills": ["res://scripts/UI/Skills/SkillTreePanel.cs", "SkillTreePanel"],
	"settings": ["res://scripts/UI/HUD/SettingsPanel.cs", "SettingsPanel"],
}

var _active_panel: Control
var _active_panel_key := "inventory"


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_active_panel_key = _requested_panel()
	_build_background()

	var spec: Array = PANEL_SPECS[_active_panel_key]
	var panel_script = load(spec[0])
	if not _require(panel_script != null, "Could not load %s" % spec[0]):
		get_tree().quit(1)
		return

	var panel_host := Control.new()
	panel_host.name = "PanelHost"
	panel_host.size = get_viewport_rect().size
	_active_panel = panel_script.new()
	_active_panel.name = spec[1]
	panel_host.add_child(_active_panel)
	_active_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(panel_host)
	_active_panel.visible = true
	set_meta("active_panel_name", spec[1])

	await get_tree().process_frame
	await get_tree().process_frame
	var expected_size := _expected_viewport_size()
	if get_window().size != Vector2i(expected_size):
		get_window().size = Vector2i(expected_size)
		panel_host.size = expected_size
		await get_tree().process_frame
		await get_tree().process_frame
	if not _validate_panel():
		get_tree().quit(1)
		return
	set_meta("validation_passed", true)

	var capture_path := _capture_path()
	if not capture_path.is_empty():
		await _capture_viewport(capture_path)

	print("UI screen showcase passed: %s" % _active_panel_key)
	if OS.get_cmdline_user_args().has("--screen-showcase-validate") or not capture_path.is_empty():
		get_tree().quit(0)


func _build_background() -> void:
	var background := ColorRect.new()
	background.name = "WorldScrim"
	background.color = Color("#120F0C")
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(background)


func _requested_panel() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--showcase-panel="):
			var requested := argument.trim_prefix("--showcase-panel=").to_lower()
			if PANEL_SPECS.has(requested):
				return requested
	return "inventory"


func _expected_viewport_size() -> Vector2:
	var configured = get_meta("showcase_viewport_size", null)
	if configured != null:
		return Vector2(configured)
	return get_viewport_rect().size


func _validate_panel() -> bool:
	if not _require(is_instance_valid(_active_panel), "Runtime panel is missing"):
		return false
	if not _require(_active_panel.visible, "Runtime panel is hidden"):
		return false
	var viewport_bounds := Rect2(Vector2.ZERO, _expected_viewport_size())
	var panel_bounds := _active_panel.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, _active_panel.size)
	var bounds_fit := (
		panel_bounds.position.x >= viewport_bounds.position.x - 1.0
		and panel_bounds.position.y >= viewport_bounds.position.y - 1.0
		and panel_bounds.end.x <= viewport_bounds.end.x + 1.0
		and panel_bounds.end.y <= viewport_bounds.end.y + 1.0
	)
	if not _require(bounds_fit, "Panel bounds escape the viewport: %s" % panel_bounds):
		return false

	var focusable_count := 0
	for node in _active_panel.find_children("*", "Control", true, false):
		if node is Control and node.focus_mode == Control.FOCUS_ALL and node.visible:
			focusable_count += 1
	return _require(focusable_count > 0, "%s has no visible focusable control" % _active_panel.name)


func _capture_path() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--screen-showcase-capture="):
			return argument.trim_prefix("--screen-showcase-capture=")
	return ""


func _capture_viewport(path: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	if not _require(image != null and not image.is_empty(), "Viewport capture is empty"):
		return
	var output_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
	_require(image.save_png(output_path) == OK, "Could not save screen capture: %s" % output_path)


func _require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error("UI screen showcase failed: %s" % message)
	return false
