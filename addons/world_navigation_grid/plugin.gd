@tool
extends EditorPlugin

enum PaintMode {
	VIEW,
	WALKABLE,
	BLOCKED,
	ERASE,
}

const GRID_SCRIPT_PATH := "res://scripts/World/Navigation/WorldNavigationGrid2D.cs"

var _toolbar: HBoxContainer
var _overlay_button: Button
var _bake_button: Button
var _mode_buttons: Array[Button] = []
var _brush_spin: SpinBox
var _clear_button: Button
var _status_label: Label
var _edited_grid: Node2D
var _paint_mode := PaintMode.VIEW
var _painting := false
var _stroke_changed := false
var _stroke_before := PackedByteArray()
var _waiting_for_bake := false
var _last_status := ""
var _grid_changed_signal := StringName()


func _enter_tree() -> void:
	_build_toolbar()
	add_control_to_container(EditorPlugin.CONTAINER_CANVAS_EDITOR_MENU, _toolbar)
	_toolbar.hide()
	set_process(true)


func _exit_tree() -> void:
	_set_edited_grid(null)
	if is_instance_valid(_toolbar):
		remove_control_from_container(EditorPlugin.CONTAINER_CANVAS_EDITOR_MENU, _toolbar)
		_toolbar.queue_free()


func _handles(object: Object) -> bool:
	return _is_navigation_grid(object)


func _edit(object: Object) -> void:
	_set_edited_grid(object as Node2D if _is_navigation_grid(object) else null)


func _clear() -> void:
	_set_edited_grid(null)


func _apply_changes() -> void:
	_save_current_grid_data()


func _save_external_data() -> void:
	_save_current_grid_data()


func _make_visible(visible: bool) -> void:
	if not is_instance_valid(_toolbar):
		return
	if not visible and _painting:
		_finish_paint_stroke()
	_toolbar.visible = visible and is_instance_valid(_edited_grid)
	_sync_preview_visibility()


func _process(_delta: float) -> void:
	if not is_instance_valid(_edited_grid):
		return

	var status := str(_edited_grid.call("GetEditorStatusText"))
	if status != _last_status:
		_last_status = status
		_status_label.text = status
		update_overlays()

	var baking := bool(_edited_grid.call("IsEditorBakeRunning"))
	_bake_button.disabled = baking
	var has_data := bool(_edited_grid.call("HasBakedGridData"))
	_set_paint_controls_enabled(has_data and not baking)
	if _waiting_for_bake and not baking:
		_waiting_for_bake = false
		EditorInterface.mark_scene_as_unsaved()


func _forward_canvas_draw_over_viewport(viewport_control: Control) -> void:
	if not is_instance_valid(_edited_grid) \
		or not _toolbar.visible \
		or not _overlay_button.button_pressed:
		return
	var canvas_transform := EditorInterface.get_editor_viewport_2d().global_canvas_transform
	_edited_grid.call("DrawEditorOverlay", viewport_control, canvas_transform)


func _forward_canvas_gui_input(event: InputEvent) -> bool:
	if not _can_paint():
		return false

	if event is InputEventMouseMotion:
		_update_brush_preview(event.position)
		if _painting:
			if event.button_mask & MOUSE_BUTTON_MASK_LEFT:
				_paint_at_screen_position(event.position)
				return true
			_finish_paint_stroke()
		return false

	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed and (event.alt_pressed \
			or event.ctrl_pressed \
			or event.meta_pressed \
			or Input.is_key_pressed(KEY_SPACE)):
			return false
		if event.pressed:
			_painting = true
			_stroke_changed = false
			_stroke_before = _get_override_snapshot()
			_paint_at_screen_position(event.position)
		else:
			_finish_paint_stroke()
		return true

	return false


func _build_toolbar() -> void:
	_toolbar = HBoxContainer.new()
	_toolbar.name = "WorldNavigationGridToolbar"

	_overlay_button = Button.new()
	_overlay_button.toggle_mode = true
	_overlay_button.button_pressed = true
	_overlay_button.icon = _editor_icon(&"GuiVisibilityVisible")
	_overlay_button.tooltip_text = "Show baked walkability overlay"
	_overlay_button.custom_minimum_size = Vector2(34, 0)
	_overlay_button.toggled.connect(_on_overlay_toggled)
	_toolbar.add_child(_overlay_button)

	_bake_button = Button.new()
	_bake_button.text = "Bake"
	_bake_button.icon = _editor_icon(&"Bake")
	_bake_button.tooltip_text = "Rebuild walkability from physics collision"
	_bake_button.pressed.connect(_on_bake_pressed)
	_toolbar.add_child(_bake_button)

	_toolbar.add_child(VSeparator.new())

	var mode_group := ButtonGroup.new()
	_add_mode_button("View", &"Search", "Inspect without painting", PaintMode.VIEW, mode_group)
	_add_mode_button("Walk", &"EditAddRemove", "Force cells to be walkable", PaintMode.WALKABLE, mode_group)
	_add_mode_button("Block", &"Lock", "Force cells to be blocked", PaintMode.BLOCKED, mode_group)
	_add_mode_button("Erase", &"Eraser", "Remove manual overrides", PaintMode.ERASE, mode_group)
	_mode_buttons[PaintMode.VIEW].button_pressed = true

	var brush_label := Label.new()
	brush_label.text = "Brush"
	_toolbar.add_child(brush_label)

	_brush_spin = SpinBox.new()
	_brush_spin.min_value = 1
	_brush_spin.max_value = 8
	_brush_spin.step = 1
	_brush_spin.value = 1
	_brush_spin.custom_minimum_size = Vector2(64, 0)
	_brush_spin.tooltip_text = "Brush radius in grid cells"
	_brush_spin.value_changed.connect(_on_brush_changed)
	_toolbar.add_child(_brush_spin)

	_clear_button = Button.new()
	_clear_button.icon = _editor_icon(&"Clear")
	_clear_button.tooltip_text = "Clear all manual walkability overrides"
	_clear_button.custom_minimum_size = Vector2(34, 0)
	_clear_button.pressed.connect(_on_clear_pressed)
	_toolbar.add_child(_clear_button)

	_toolbar.add_child(VSeparator.new())
	_status_label = Label.new()
	_status_label.text = "No grid selected"
	_status_label.custom_minimum_size = Vector2(230, 0)
	_status_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_toolbar.add_child(_status_label)


func _add_mode_button(
	label: String,
	icon_name: StringName,
	tooltip: String,
	mode: PaintMode,
	group: ButtonGroup
) -> void:
	var button := Button.new()
	button.text = label
	button.icon = _editor_icon(icon_name)
	button.tooltip_text = tooltip
	button.toggle_mode = true
	button.button_group = group
	button.pressed.connect(_on_mode_pressed.bind(mode))
	_toolbar.add_child(button)
	_mode_buttons.append(button)


func _set_edited_grid(grid: Node2D) -> void:
	if _painting:
		_finish_paint_stroke()
	if is_instance_valid(_edited_grid):
		if not _grid_changed_signal.is_empty() \
			and _edited_grid.is_connected(_grid_changed_signal, _on_grid_data_changed):
			_edited_grid.disconnect(_grid_changed_signal, _on_grid_data_changed)
		_edited_grid.call("SetEditorPreviewVisible", false)
		_edited_grid.call("ClearEditorBrushPreview")

	_edited_grid = grid
	_grid_changed_signal = StringName()
	_painting = false
	_stroke_changed = false
	_waiting_for_bake = false
	_last_status = ""
	if is_instance_valid(_toolbar):
		_toolbar.visible = is_instance_valid(_edited_grid)
	if is_instance_valid(_edited_grid):
		for signal_name: StringName in [&"EditorDataChanged", &"editor_data_changed"]:
			if _edited_grid.has_signal(signal_name):
				_grid_changed_signal = signal_name
				_edited_grid.connect(signal_name, _on_grid_data_changed)
				break
	_sync_preview_visibility()


func _on_grid_data_changed() -> void:
	_last_status = ""
	update_overlays()


func _sync_preview_visibility() -> void:
	if not is_instance_valid(_edited_grid):
		return
	var visible := is_instance_valid(_toolbar) and _toolbar.visible and _overlay_button.button_pressed
	_edited_grid.call("SetEditorPreviewVisible", visible)
	if not visible:
		_edited_grid.call("ClearEditorBrushPreview")
	update_overlays()


func _on_overlay_toggled(_pressed: bool) -> void:
	_sync_preview_visibility()


func _on_bake_pressed() -> void:
	if not is_instance_valid(_edited_grid):
		return
	_waiting_for_bake = bool(_edited_grid.call("RequestEditorBake"))
	if _waiting_for_bake:
		_overlay_button.button_pressed = true
		_sync_preview_visibility()


func _on_mode_pressed(mode: PaintMode) -> void:
	_paint_mode = mode
	if mode != PaintMode.VIEW:
		_overlay_button.button_pressed = true
	_sync_preview_visibility()
	if mode == PaintMode.VIEW and is_instance_valid(_edited_grid):
		_edited_grid.call("ClearEditorBrushPreview")


func _on_brush_changed(_value: float) -> void:
	if is_instance_valid(_edited_grid):
		_edited_grid.call("ClearEditorBrushPreview")
	update_overlays()


func _on_clear_pressed() -> void:
	if not is_instance_valid(_edited_grid):
		return
	var before := _get_override_snapshot()
	if not bool(_edited_grid.call("ClearManualOverrides")):
		return
	var after := _get_override_snapshot()
	_commit_override_action("Clear navigation overrides", before, after)


func _can_paint() -> bool:
	return is_instance_valid(_edited_grid) \
		and _toolbar.visible \
		and _overlay_button.button_pressed \
		and _paint_mode != PaintMode.VIEW \
		and bool(_edited_grid.call("HasBakedGridData")) \
		and not bool(_edited_grid.call("IsEditorBakeRunning"))


func _paint_at_screen_position(screen_position: Vector2) -> void:
	if not is_instance_valid(_edited_grid):
		return
	var world_position := _screen_to_world(screen_position)
	var changed := bool(_edited_grid.call(
		"PaintEditorCells",
		world_position,
		_paint_mode,
		int(_brush_spin.value)
	))
	_stroke_changed = _stroke_changed or changed
	if changed:
		update_overlays()


func _update_brush_preview(screen_position: Vector2) -> void:
	if not is_instance_valid(_edited_grid):
		return
	_edited_grid.call(
		"SetEditorBrushPreview",
		_screen_to_world(screen_position),
		int(_brush_spin.value),
		_paint_mode
	)
	update_overlays()


func _screen_to_world(screen_position: Vector2) -> Vector2:
	var viewport := EditorInterface.get_editor_viewport_2d()
	return viewport.global_canvas_transform.affine_inverse() * screen_position


func _finish_paint_stroke() -> void:
	if not _painting:
		return
	_painting = false
	if not _stroke_changed or not is_instance_valid(_edited_grid):
		return
	var after := _get_override_snapshot()
	if after == _stroke_before:
		_stroke_changed = false
		return
	_commit_override_action("Paint navigation grid", _stroke_before, after)
	_stroke_changed = false


func _commit_override_action(action_name: String, before: PackedByteArray, after: PackedByteArray) -> void:
	var undo_redo := get_undo_redo()
	undo_redo.create_action(action_name, UndoRedo.MERGE_DISABLE, _edited_grid)
	undo_redo.add_do_method(_edited_grid, "ApplyManualOverrideSnapshot", after)
	undo_redo.add_undo_method(_edited_grid, "ApplyManualOverrideSnapshot", before)
	undo_redo.commit_action(false)
	_save_current_grid_data()
	EditorInterface.mark_scene_as_unsaved()


func _save_current_grid_data() -> void:
	if is_instance_valid(_edited_grid) and _edited_grid.has_method("SaveEditorData"):
		_edited_grid.call("SaveEditorData")


func _get_override_snapshot() -> PackedByteArray:
	if not is_instance_valid(_edited_grid):
		return PackedByteArray()
	return _edited_grid.call("GetManualOverrideSnapshot") as PackedByteArray


func _set_paint_controls_enabled(enabled: bool) -> void:
	for button in _mode_buttons:
		button.disabled = not enabled
	_brush_spin.editable = enabled
	_clear_button.disabled = not enabled


func _is_navigation_grid(object: Object) -> bool:
	if not object is Node2D:
		return false
	if not object.has_method("RequestEditorBake"):
		return false
	var script := object.get_script() as Script
	return script != null and script.resource_path == GRID_SCRIPT_PATH


func _editor_icon(icon_name: StringName) -> Texture2D:
	var base := EditorInterface.get_base_control()
	if base.has_theme_icon(icon_name, &"EditorIcons"):
		return base.get_theme_icon(icon_name, &"EditorIcons")
	return null
