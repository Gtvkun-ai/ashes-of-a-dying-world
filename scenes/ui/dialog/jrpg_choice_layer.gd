@tool
extends "res://addons/dialogic/Modules/DefaultLayoutParts/Layer_VN_Choices/vn_choice_layer.gd"
## Lựa chọn Story: menu matte liền khối; từng dòng KHÔNG phải một nút dạng thanh.
## Giữ cơ chế wrap/scroll/focus (chuột, bàn phím, gamepad) của Dialogic.

@export_group("Responsive Layout")
@export var side_layout_min_width: float = 0.0
@export var side_layout_min_aspect: float = 1.35

@export var centered_width_ratio: float = 0.285
@export var centered_min_width: float = 340.0
@export var centered_max_width: float = 456.0
@export var side_width: float = 460.0
@export var centered_x_offset: float = 0.0
@export var side_x_offset: float = 0.0
@export var right_reserved_ratio: float = 0.0
@export var right_reserved_min: float = 0.0
@export var right_reserved_max: float = 0.0

@export var textbox_height_ratio: float = 0.154
@export var textbox_min_height: float = 124.0
@export var textbox_max_height: float = 152.0
@export var textbox_bottom_distance: float = 0.0
@export var gap_above_textbox: float = 27.0
@export var screen_side_margin: float = 24.0
@export var textbox_width_ratio: float = 1.0
@export var textbox_min_width: float = 0.0
@export var textbox_max_width: float = 4096.0

@export var choices_height_ratio: float = 0.42
@export var choices_min_height: float = 120.0
@export var choices_max_height: float = 330.0

var _viewport_connected := false
var _marker_idle: Texture2D = null
var _marker_active: Texture2D = null
var _shell_padding := 8.0


func _ready() -> void:
	super._ready()
	# Godot có thể instanciate choice layer sau khi style đã áp overrides.
	# Đặt lại ở frame đầu để tránh hiện tọa độ mặc định tại giữa màn hình.
	_connect_viewport_resize()
	call_deferred("_apply_responsive_layout")
	call_deferred("_style_buttons")
	if not Engine.is_editor_hint() and Dialogic.has_subsystem("Choices"):
		if not Dialogic.Choices.question_shown.is_connected(_on_question_shown):
			Dialogic.Choices.question_shown.connect(_on_question_shown)


func _on_question_shown(_question: Dictionary) -> void:
	# Cập nhật khi các Button mới được hiển thị, không chỉ khi đổi viewport.
	call_deferred("_apply_responsive_layout")
	call_deferred("_style_buttons")


func get_choices() -> VBoxContainer:
	return %Choices


func _apply_export_overrides() -> void:
	# Không tạo lại cơ chế lựa chọn; giữ xử lý focus / âm thanh từ Dialogic.
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
	var textbox_left := center_x - textbox_width * 0.5

	var available_width := maxf(240.0, viewport_size.x - screen_side_margin * 2.0)
	var width := clampf(viewport_size.x * centered_width_ratio, centered_min_width, centered_max_width)
	width = minf(width, available_width)
	# Chiều cao dựa vào số lựa chọn HIỆN HỮU, không chừa một cột trống 240px.
	# Với nhiều đáp án: giới hạn chiều cao và giữ ScrollContainer của Dialogic.
	var visible_count := 0
	for node in choices.get_children():
		if node is Button and node.visible:
			visible_count += 1
	var row_height := 44.0
	var row_gap := 4.0
	var desired_height := float(visible_count) * row_height + maxf(0.0, float(visible_count - 1)) * row_gap
	var cap_height := minf(choices_max_height, viewport_size.y * choices_height_ratio)
	var choices_height := minf(maxf(44.0, desired_height), cap_height)

	# CÙNG một trục trái với nameplate; lựa chọn là một cụm rõ ràng phía trên thoại.
	var left_edge := maxf(screen_side_margin, textbox_left + maxf(54.0, viewport_size.x * 0.091) + centered_x_offset)
	var right_edge := minf(left_edge + width, viewport_size.x - screen_side_margin)
	left_edge = right_edge - width

	# Cách nameplate một khoảng nhỏ, không đè đường kẻ hay chân dung.
	var choices_bottom := -(textbox_height + textbox_bottom_distance + gap_above_textbox)
	choice_scroll.offset_left = roundf(left_edge - center_x + _shell_padding)
	choice_scroll.offset_right = roundf(right_edge - center_x - _shell_padding)
	choice_scroll.offset_bottom = roundf(choices_bottom - _shell_padding)
	choice_scroll.offset_top = roundf(choices_bottom - _shell_padding - choices_height)
	choice_scroll.custom_minimum_size = Vector2(width - _shell_padding * 2.0, choices_height)

	# Bề mặt thống nhất đặt SAU danh sách; không gây mờ vào map hay portrait.
	var shell: Panel = %ChoiceShell
	shell.visible = visible_count > 0
	shell.position = Vector2(roundf(left_edge), roundf(viewport_size.y + choices_bottom - choices_height - _shell_padding * 2.0))
	shell.size = Vector2(roundf(width), roundf(choices_height + _shell_padding * 2.0))

	choices.custom_minimum_size.x = 0.0
	choices.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# Fill the viewport when content is short so one or two choices stay next
	# to the textbox. Content taller than the viewport keeps its natural size
	# and remains scrollable.
	choices.size_flags_vertical = Control.SIZE_EXPAND_FILL
	choices.alignment = BoxContainer.ALIGNMENT_BEGIN


func _style_buttons() -> void:
	if not has_node("ChoiceScroll/Choices"):
		return
	_ensure_choice_markers()
	for child in %Choices.get_children():
		if child is Button:
			child.alignment = HORIZONTAL_ALIGNMENT_LEFT
			child.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			child.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
			child.clip_text = false
			child.focus_mode = Control.FOCUS_ALL
			child.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			child.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
			child.custom_minimum_size = Vector2(0.0, 44.0)
			child.text = child.text.strip_edges()
			# Đặt icon trong cả hai trạng thái để chữ không bị giật khi đổi focus.
			child.icon_alignment = HORIZONTAL_ALIGNMENT_LEFT
			child.expand_icon = false
			child.icon = _marker_active if child.has_focus() else _marker_idle
			var focus_callback := _scroll_choice_into_view.bind(child)
			if not child.focus_entered.is_connected(focus_callback):
				child.focus_entered.connect(focus_callback)
			var on_focus := _show_choice_marker.bind(child)
			var on_blur := _hide_choice_marker.bind(child)
			if not child.focus_entered.is_connected(on_focus):
				child.focus_entered.connect(on_focus)
			if not child.focus_exited.is_connected(on_blur):
				child.focus_exited.connect(on_blur)
			if not child.mouse_entered.is_connected(on_focus):
				child.mouse_entered.connect(on_focus)
			if not child.mouse_exited.is_connected(on_blur):
				child.mouse_exited.connect(on_blur)
			if not child.visibility_changed.is_connected(_schedule_choice_layout):
				child.visibility_changed.connect(_schedule_choice_layout)


func _schedule_choice_layout() -> void:
	# Kích thước thay đổi khi Dialogic ẩn/hiện các nút; không phá focus/selection.
	call_deferred("_apply_responsive_layout")


func _scroll_choice_into_view(button: Control) -> void:
	if is_instance_valid(button):
		%ChoiceScroll.ensure_control_visible(button)


## Hai texture pixel 12x12 tạo tại runtime, không cần ảnh tải ngoài.
## Marker chỉ sáng khi hover/focus; phím/gamepad và chuột cùng một tín hiệu.
func _ensure_choice_markers() -> void:
	if _marker_idle != null and _marker_active != null:
		return
	var idle := Image.create(12, 12, false, Image.FORMAT_RGBA8)
	idle.fill(Color.TRANSPARENT)
	var active := Image.create(12, 12, false, Image.FORMAT_RGBA8)
	active.fill(Color.TRANSPARENT)
	for y in range(12):
		for x in range(12):
			if absi(x - 5) + absi(y - 5) <= 3:
				active.set_pixel(x, y, Color(0.62, 0.86, 1.0))
	_marker_idle = ImageTexture.create_from_image(idle)
	_marker_active = ImageTexture.create_from_image(active)


func _show_choice_marker(button: Button) -> void:
	if is_instance_valid(button) and not button.disabled:
		button.icon = _marker_active


func _hide_choice_marker(button: Button) -> void:
	if is_instance_valid(button) and not button.has_focus():
		button.icon = _marker_idle
