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
@export var side_layout_min_width: float = 1500.0
@export var side_layout_min_aspect: float = 1.55

## Choice width limits. On narrow screens the width becomes a fraction of viewport.
@export var centered_width_ratio: float = 0.66
@export var centered_min_width: float = 360.0
@export var centered_max_width: float = 680.0
@export var side_width: float = 380.0

## Spacing from the dialogue panel / screen edge.
@export var textbox_height_ratio: float = 0.215
@export var textbox_min_height: float = 150.0
@export var textbox_max_height: float = 195.0
@export var textbox_bottom_distance: float = 22.0
@export var gap_above_textbox: float = 18.0
@export var screen_side_margin: float = 32.0

## Maximum vertical area reserved for the choice stack.
@export var choices_height_ratio: float = 0.36
@export var choices_min_height: float = 150.0
@export var choices_max_height: float = 330.0

var _viewport_connected := false


func _apply_export_overrides() -> void:
	# Keep every standard Dialogic choice feature/theme setting.
	super._apply_export_overrides()
	_connect_viewport_resize()
	call_deferred("_apply_responsive_layout")


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

	# Work in one predictable coordinate system: bottom-center anchored.
	choices.anchor_left = 0.5
	choices.anchor_right = 0.5
	choices.anchor_top = 1.0
	choices.anchor_bottom = 1.0
	choices.grow_horizontal = Control.GROW_DIRECTION_BOTH
	choices.grow_vertical = Control.GROW_DIRECTION_BEGIN
	choices.alignment = BoxContainer.ALIGNMENT_END

	# Match the textbox sizing formula from jrpg_textbox_layer.gd so the choice
	# stack always clears the top edge of the textbox at every resolution.
	var textbox_height := clampf(
		viewport_size.y * textbox_height_ratio,
		textbox_min_height,
		textbox_max_height
	)
	var choices_bottom := -(textbox_height + textbox_bottom_distance + gap_above_textbox)
	var choices_height := clampf(
		viewport_size.y * choices_height_ratio,
		choices_min_height,
		choices_max_height
	)

	var aspect := viewport_size.x / viewport_size.y
	var can_use_side_layout := (
		viewport_size.x >= side_layout_min_width
		and aspect >= side_layout_min_aspect
	)

	if can_use_side_layout:
		# Wide screens: place choices in the free space at the far right.
		var width := minf(side_width, viewport_size.x * 0.28)
		var desired_right := viewport_size.x - screen_side_margin
		var desired_left := desired_right - width
		var center_x := viewport_size.x * 0.5

		choices.offset_left = desired_left - center_x
		choices.offset_right = desired_right - center_x
		choices.offset_bottom = choices_bottom
		choices.offset_top = choices_bottom - choices_height
	else:
		# Narrow/tall screens: center the choices above the textbox instead of
		# forcing them into the portrait. Width remains responsive and clamped.
		var safe_max_width := maxf(260.0, viewport_size.x - screen_side_margin * 2.0)
		var width := clampf(
			viewport_size.x * centered_width_ratio,
			centered_min_width,
			centered_max_width
		)
		width = minf(width, safe_max_width)

		choices.offset_left = -width * 0.5
		choices.offset_right = width * 0.5
		choices.offset_bottom = choices_bottom
		choices.offset_top = choices_bottom - choices_height
