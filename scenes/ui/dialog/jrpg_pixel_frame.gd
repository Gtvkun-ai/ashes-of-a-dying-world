@tool
extends Control
## Draw the original nine-slice in a larger pixel grid without scaling text.

var frame_style: StyleBoxTexture
var pixel_scale: float = 2.0
var button_styles: Dictionary = {}


static func apply(control: Control, slot: StringName, style: StyleBoxTexture, scale_factor: float) -> void:
	var frame := control.get_node_or_null("PixelFrame")
	if frame == null:
		frame = load("res://scenes/ui/dialog/jrpg_pixel_frame.gd").new()
		frame.name = "PixelFrame"
		frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		frame.show_behind_parent = true
		control.add_child(frame)
		control.resized.connect(frame.queue_redraw)
	frame.frame_style = style
	# Chỉ phóng to khung bằng bội số NGUYÊN của pixel gốc.
	# Ví dụ: màn 900p trước đây cho scale 2.5, mỗi pixel bị 2px/3px
	# xen kẽ; đường chéo và các nét trang trí trông như bị vỡ.
	# Snap về 2x ở 900p, 3x ở 1080p; font chữ vẫn theo UI scale riêng.
	var clean_pixel_scale: float = maxf(1.0, roundf(scale_factor))
	frame.pixel_scale = clean_pixel_scale
	var empty := StyleBoxEmpty.new()
	for side in [SIDE_LEFT, SIDE_TOP, SIDE_RIGHT, SIDE_BOTTOM]:
		empty.set_content_margin(side, style.get_content_margin(side) * clean_pixel_scale)
	control.add_theme_stylebox_override(slot, empty)
	frame.queue_redraw()


static func apply_button(button: Button, styles: Dictionary, scale_factor: float) -> void:
	apply(button, &"normal", styles[&"normal"], scale_factor)
	var frame: Control = button.get_node("PixelFrame")
	frame.button_styles = styles
	for slot in [&"hover", &"focus", &"pressed", &"disabled", &"hover_pressed"]:
		button.add_theme_stylebox_override(slot, button.get_theme_stylebox(&"normal"))
	for signal_name in [&"mouse_entered", &"mouse_exited", &"focus_entered", &"focus_exited", &"button_down", &"button_up"]:
		if not button.is_connected(signal_name, frame.queue_redraw):
			button.connect(signal_name, frame.queue_redraw)


func _draw() -> void:
	if frame_style == null:
		return
	var control := get_parent() as Control
	var style := frame_style
	if control is Button and not button_styles.is_empty():
		if control.button_pressed:
			style = button_styles[&"pressed"]
		elif control.has_focus():
			style = button_styles[&"focus"]
		elif control.is_hovered():
			style = button_styles[&"hover"]
	# PanelContainer may place this child inside its content margins.
	# Offset the drawing back to the parent's outer edge.
	draw_set_transform(-position, 0.0, Vector2.ONE * pixel_scale)
	draw_style_box(style, Rect2(Vector2.ZERO, control.size / pixel_scale))
