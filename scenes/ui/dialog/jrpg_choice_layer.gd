@tool
extends "res://addons/dialogic/Modules/DefaultLayoutParts/Layer_VN_Choices/vn_choice_layer.gd"
## Responsive choice layout for the centered-Hyou dialogue UI.
##
## The old scene used fixed offsets tuned for one resolution, so on narrow or
## tall viewports the choice stack collided with the portrait/textbox.
## This layer keeps Dialogic's normal choice behaviour, but repositions and
## resizes the VBox from the current viewport size.

@export_group("Responsive Layout")
## Only use the right-side layout when there is genuinely enough horizontal room.
@export var side_layout_min_width: float = 0.0
@export var side_layout_min_aspect: float = 1.35

## Choice width limits. On narrow screens the width becomes a fraction of viewport.
@export var centered_width_ratio: float = 0.22
@export var centered_min_width: float = 240.0
@export var centered_max_width: float = 280.0
@export var side_width: float = 320.0
@export var centered_x_offset: float = 125.0
@export var side_x_offset: float = 0.0
@export var right_reserved_ratio: float = 0.18
@export var right_reserved_min: float = 170.0
@export var right_reserved_max: float = 270.0

## Spacing from the dialogue panel / screen edge.
@export var textbox_height_ratio: float = 0.168
@export var textbox_min_height: float = 114.0
@export var textbox_max_height: float = 130.0
@export var textbox_bottom_distance: float = 22.0
@export var gap_above_textbox: float = 10.0
@export var screen_side_margin: float = 24.0
@export var textbox_width_ratio: float = 0.64
@export var textbox_min_width: float = 760.0
@export var textbox_max_width: float = 920.0

## Maximum vertical area reserved for the choice stack.
@export var choices_height_ratio: float = 0.12
@export var choices_min_height: float = 80.0
@export var choices_max_height: float = 96.0

var _viewport_connected := false


func _apply_export_overrides() -> void:
	# Keep every standard Dialogic choice feature/theme setting.
	super._apply_export_overrides()
	_connect_viewport_resize()
	call_deferred("_apply_responsive_layout")
	call_deferred("_style_buttons")


func _connect_viewport_resize() -> void:
	if _viewport_connected:
		return
	var viewport := get_viewport()
	if viewport == null:
		return
	if not viewport.size_changed.is_connected(_on_viewport_size_changed):
		viewport.size_changed.connect(_on_viewport_size_changed)
	_viewport_connected = true


func _on_viewport_size_changed() -> void:
	call_deferred("_apply_responsive_layout")
	call_deferred("_style_buttons")


func _apply_responsive_layout() -> void:
	if not is_inside_tree() or not has_node("Choices"):
		return

	var viewport := get_viewport()
	if viewport == null:
		return
	var viewport_size := viewport.get_visible_rect().size
	if viewport_size.x <= 0.0 or viewport_size.y <= 0.0:
		return

	var choices: VBoxContainer = $Choices
	choices.anchor_left = 0.5
	choices.anchor_right = 0.5
	choices.anchor_top = 1.0
	choices.anchor_bottom = 1.0
	choices.grow_horizontal = Control.GROW_DIRECTION_BOTH
	choices.grow_vertical = Control.GROW_DIRECTION_BEGIN
	choices.alignment = BoxContainer.ALIGNMENT_BEGIN

	var textbox_width := clampf(viewport_size.x * textbox_width_ratio, textbox_min_width, textbox_max_width)
	var textbox_height := clampf(viewport_size.y * textbox_height_ratio, textbox_min_height, textbox_max_height)
	var center_x := viewport_size.x * 0.5
	var textbox_left := center_x - textbox_width * 0.5
	var textbox_right := center_x + textbox_width * 0.5

	var width := clampf(viewport_size.x * 0.18, 240.0, 280.0)
	var choices_height := clampf(viewport_size.y * choices_height_ratio, choices_min_height, choices_max_height)
	var right_edge := textbox_right - 10.0
	var left_edge := right_edge - width
	if left_edge < screen_side_margin:
		left_edge = screen_side_margin
		right_edge = left_edge + width
	if right_edge > viewport_size.x - screen_side_margin:
		right_edge = viewport_size.x - screen_side_margin
		left_edge = right_edge - width

	var choices_bottom := -(textbox_height + textbox_bottom_distance + gap_above_textbox)
	choices.offset_left = left_edge - center_x
	choices.offset_right = right_edge - center_x
	choices.offset_bottom = choices_bottom
	choices.offset_top = choices_bottom - choices_height


func _style_buttons() -> void:
	if not has_node("Choices"):
		return
	for child in $Choices.get_children():
		if child is BaseButton:
			child.alignment = HORIZONTAL_ALIGNMENT_LEFT
			child.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
			child.clip_text = true
			child.focus_mode = Control.FOCUS_ALL
			child.text = child.text.strip_edges()
