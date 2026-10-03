@tool
extends "res://addons/dialogic/Modules/DefaultLayoutParts/Layer_VN_Choices/vn_choice_layer.gd"
## Responsive, wrap-capable choice layout for the centered dialogue UI.

@export_group("Responsive Layout")
@export var side_layout_min_width: float = 0.0
@export var side_layout_min_aspect: float = 1.35

@export var centered_width_ratio: float = 0.42
@export var centered_min_width: float = 360.0
@export var centered_max_width: float = 520.0
@export var side_width: float = 460.0
@export var centered_x_offset: float = 0.0
@export var side_x_offset: float = 0.0
@export var right_reserved_ratio: float = 0.18
@export var right_reserved_min: float = 170.0
@export var right_reserved_max: float = 270.0

@export var textbox_height_ratio: float = 0.168
@export var textbox_min_height: float = 114.0
@export var textbox_max_height: float = 130.0
@export var textbox_bottom_distance: float = 22.0
@export var gap_above_textbox: float = 10.0
@export var screen_side_margin: float = 24.0
@export var textbox_width_ratio: float = 0.64
@export var textbox_min_width: float = 760.0
@export var textbox_max_width: float = 920.0

@export var choices_height_ratio: float = 0.42
@export var choices_min_height: float = 160.0
@export var choices_max_height: float = 360.0

var _viewport_connected := false


func get_choices() -> VBoxContainer:
	return %Choices


func _apply_export_overrides() -> void:
	# Retain Dialogic's normal button creation, sounds and theme resources.
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
	if not is_inside_tree() or not has_node("ChoiceScroll"):
		return

	var viewport := get_viewport()
	if viewport == null:
		return
	var viewport_size := viewport.get_visible_rect().size
	if viewport_size.x <= 0.0 or viewport_size.y <= 0.0:
		return

	var choice_scroll: ScrollContainer = %ChoiceScroll
	var choices: VBoxContainer = %Choices
	choice_scroll.anchor_left = 0.5
	choice_scroll.anchor_right = 0.5
	choice_scroll.anchor_top = 1.0
	choice_scroll.anchor_bottom = 1.0
	choice_scroll.grow_horizontal = Control.GROW_DIRECTION_BOTH
	choice_scroll.grow_vertical = Control.GROW_DIRECTION_BEGIN

	var textbox_width := clampf(viewport_size.x * textbox_width_ratio, textbox_min_width, textbox_max_width)
	textbox_width = minf(textbox_width, viewport_size.x - screen_side_margin * 2.0)
	var textbox_height := clampf(viewport_size.y * textbox_height_ratio, textbox_min_height, textbox_max_height)
	var center_x := viewport_size.x * 0.5
	var textbox_right := center_x + textbox_width * 0.5

	var available_width := maxf(240.0, viewport_size.x - screen_side_margin * 2.0)
	var width := clampf(viewport_size.x * centered_width_ratio, centered_min_width, centered_max_width)
	width = minf(width, available_width)
	var choices_height := clampf(
		viewport_size.y * choices_height_ratio,
		choices_min_height,
		choices_max_height
	)

	var right_edge := textbox_right - 10.0 + centered_x_offset
	var left_edge := right_edge - width
	if left_edge < screen_side_margin:
		left_edge = screen_side_margin
		right_edge = left_edge + width
	if right_edge > viewport_size.x - screen_side_margin:
		right_edge = viewport_size.x - screen_side_margin
		left_edge = right_edge - width

	var choices_bottom := -(textbox_height + textbox_bottom_distance + gap_above_textbox)
	choice_scroll.offset_left = left_edge - center_x
	choice_scroll.offset_right = right_edge - center_x
	choice_scroll.offset_bottom = choices_bottom
	choice_scroll.offset_top = choices_bottom - choices_height
	choice_scroll.custom_minimum_size = Vector2(width, choices_height)

	choices.custom_minimum_size.x = 0.0
	choices.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	choices.alignment = BoxContainer.ALIGNMENT_BEGIN


func _style_buttons() -> void:
	if not has_node("ChoiceScroll/Choices"):
		return
	for child in %Choices.get_children():
		if child is Button:
			child.alignment = HORIZONTAL_ALIGNMENT_LEFT
			child.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			child.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
			child.clip_text = false
			child.focus_mode = Control.FOCUS_ALL
			child.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			child.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
			child.custom_minimum_size = Vector2(0.0, 40.0)
			child.text = child.text.strip_edges()
			var focus_callback := _scroll_choice_into_view.bind(child)
			if not child.focus_entered.is_connected(focus_callback):
				child.focus_entered.connect(focus_callback)


func _scroll_choice_into_view(button: Control) -> void:
	if is_instance_valid(button):
		%ChoiceScroll.ensure_control_visible(button)
