@tool
extends DialogicLayoutLayer
## Cinematic JRPG / visual-novel dialogue layer.
##
## Bản UI này cố ý đặt portrait ở GIỮA màn hình thay vì góc trái:
## - gameplay phía sau được dim nhẹ để Hyou thành trọng tâm;
## - portrait lớn, neo giữa và chìm một phần sau hộp thoại;
## - textbox rộng, tối xanh-đen, viền vàng đồng;
## - nameplate nổi trên mép hộp thoại;
## - có fade/slide ngắn khi mở hội thoại và khi portrait xuất hiện.

# ── Box ────────────────────────────────────────────────────────────────
@export_group("Box")
@export_file("*.tres") var box_panel: String = ""
@export var box_width_ratio: float = 0.82
@export var box_min_width: float = 760.0
@export var box_max_width: float = 1500.0
@export var box_height_ratio: float = 0.215
@export var box_min_height: float = 150.0
@export var box_max_height: float = 195.0
@export var box_distance: int = 22

# ── Text ───────────────────────────────────────────────────────────────
@export_group("Text")
@export var text_use_global_size: bool = false
@export var text_custom_size: int = 18
@export var text_use_global_color: bool = false
@export var text_custom_color: Color = Color(0.96, 0.93, 0.86, 1.0)
@export var content_left_margin: int = 36
@export var content_top_margin: int = 34
@export var content_right_margin: int = 36
@export var content_bottom_margin: int = 18

# ── Name Label ─────────────────────────────────────────────────────────
@export_group("Name Label")
@export var name_label_color_mode: int = 1  # 0=GLOBAL, 1=CUSTOM, 2=CHARACTER
@export var name_label_custom_color: Color = Color(0.95, 0.79, 0.48, 1.0)
@export var name_label_use_global_size: bool = false
@export var name_label_custom_size: int = 20
## Offset tính từ góc trên-trái của textbox.
@export var name_plate_offset: Vector2 = Vector2(38, -24)
@export var name_plate_size: Vector2 = Vector2(170, 44)

# ── Portrait ───────────────────────────────────────────────────────────
@export_group("Portrait")
## Kích thước chuẩn cho viewport 1280x720. Script tự scale theo chiều cao màn hình.
@export var portrait_size: Vector2 = Vector2(700, 620)
@export var portrait_show: bool = true
@export var portrait_center_x_offset: float = 0.0
## Số pixel portrait chui xuống phía sau textbox (ở 720p).
@export var portrait_panel_overlap: float = 105.0
@export var portrait_draw_behind_panel: bool = true

# ── Backdrop & Motion ──────────────────────────────────────────────────
@export_group("Backdrop & Motion")
@export var dim_color: Color = Color(0.015, 0.025, 0.04, 0.46)
@export var enable_entry_animation: bool = true
@export var entry_duration: float = 0.28
@export var portrait_entry_offset: float = 22.0

const VIETNAMESE_FONT_PATH := "res://assets/fonts/BeVietnamPro-Regular.ttf"

var _portrait_target_position := Vector2.ZERO
var _layout_intro_played := false
var _last_portrait_texture: Texture2D = null
var _viewport_connected := false


## Dialogic gọi hàm này sau khi áp style overrides.
func _apply_export_overrides() -> void:
	if !is_inside_tree():
		await ready

	if not Engine.is_editor_hint():
		_ensure_dch_directory()

	_connect_viewport_resize()

	var viewport_size := Vector2(1280, 720)
	var _vp := get_viewport()
	if _vp != null:
		var vp_rect := _vp.get_visible_rect()
		if vp_rect.size.x > 0.0 and vp_rect.size.y > 0.0:
			viewport_size = vp_rect.size

	# ── Backdrop ───────────────────────────────────────────────────────
	var dim: ColorRect = %DimBackground
	dim.color = dim_color
	dim.visible = true

	# ── Textbox responsive ─────────────────────────────────────────────
	var max_available_width: float = maxf(520.0, viewport_size.x - 72.0)
	var panel_width: float = clampf(viewport_size.x * box_width_ratio, box_min_width, box_max_width)
	panel_width = minf(panel_width, max_available_width)
	var panel_height: float = clampf(viewport_size.y * box_height_ratio, box_min_height, box_max_height)
	var effective_box_size: Vector2 = Vector2(panel_width, panel_height)

	var panel: PanelContainer = %DialogTextPanel
	panel.custom_minimum_size = effective_box_size
	panel.position = Vector2(-effective_box_size.x * 0.5, -effective_box_size.y - box_distance)
	panel.size = effective_box_size
	panel.z_index = 4

	if not box_panel.is_empty() and ResourceLoader.exists(box_panel):
		panel.add_theme_stylebox_override(&"panel", load(box_panel))

	# ── Portrait giữa màn hình ─────────────────────────────────────────
	# Scale theo độ cao để vẫn giữ cảm giác "nhân vật lớn" ở 1080p,
	# đồng thời không tràn khỏi màn hình nhỏ.
	var portrait_scale: float = clamp(viewport_size.y / 720.0, 0.78, 1.34)
	var effective_portrait_size: Vector2 = portrait_size * portrait_scale
	var effective_overlap: float = portrait_panel_overlap * portrait_scale

	var portrait: TextureRect = %SpeakerPortrait
	portrait.visible = portrait_show and portrait.texture != null
	portrait.custom_minimum_size = effective_portrait_size
	portrait.size = effective_portrait_size
	_portrait_target_position = Vector2(
		-effective_portrait_size.x * 0.5 + portrait_center_x_offset,
		panel.position.y - effective_portrait_size.y + effective_overlap
	)
	portrait.position = _portrait_target_position
	portrait.z_index = 2 if portrait_draw_behind_panel else 5

	# ── Margins / typography ───────────────────────────────────────────
	var content_margin: MarginContainer = panel.get_node("ContentMargin")
	content_margin.add_theme_constant_override(&"margin_left", content_left_margin)
	content_margin.add_theme_constant_override(&"margin_top", content_top_margin)
	content_margin.add_theme_constant_override(&"margin_right", content_right_margin)
	content_margin.add_theme_constant_override(&"margin_bottom", content_bottom_margin)

	var name_plate: PanelContainer = %NamePlate
	name_plate.position = panel.position + name_plate_offset
	name_plate.size = name_plate_size
	name_plate.z_index = 6

	var dialog_text: DialogicNode_DialogText = %DialogicNode_DialogText
	var vietnamese_font: Font = load(VIETNAMESE_FONT_PATH)
	if vietnamese_font:
		dialog_text.add_theme_font_override(&"normal_font", vietnamese_font)
		dialog_text.add_theme_font_override(&"bold_font", vietnamese_font)
		dialog_text.add_theme_font_override(&"italics_font", vietnamese_font)
		dialog_text.add_theme_font_override(&"bold_italics_font", vietnamese_font)
	dialog_text.add_theme_font_size_override(&"normal_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"bold_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"italics_font_size", text_custom_size)
	dialog_text.add_theme_color_override(&"default_color", text_custom_color)

	var name_label: DialogicNode_NameLabel = %DialogicNode_NameLabel
	if vietnamese_font:
		name_label.add_theme_font_override(&"font", vietnamese_font)
	name_label.add_theme_font_size_override(&"font_size", name_label_custom_size)
	match name_label_color_mode:
		0:
			name_label.add_theme_color_override(&"font_color",
				get_global_setting(&'font_color', name_label_custom_color) as Color)
		1:
			name_label.use_character_color = false
			name_label.add_theme_color_override(&"font_color", name_label_custom_color)
		2:
			name_label.use_character_color = true

	if not Engine.is_editor_hint():
		_connect_speaker_signal()
		if enable_entry_animation and not _layout_intro_played:
			_layout_intro_played = true
			call_deferred("_play_layout_intro")


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
	call_deferred("_apply_export_overrides")


## Fade nền + hộp thoại rất ngắn để cảnh hội thoại có cảm giác cinematic nhưng không chậm game.
func _play_layout_intro() -> void:
	if not is_inside_tree():
		return

	var dim: ColorRect = %DimBackground
	var panel: PanelContainer = %DialogTextPanel
	var name_plate: PanelContainer = %NamePlate
	var panel_target := panel.position
	var name_target := name_plate.position

	dim.modulate = Color(1, 1, 1, 0)
	panel.modulate = Color(1, 1, 1, 0)
	name_plate.modulate = Color(1, 1, 1, 0)
	panel.position = panel_target + Vector2(0, 12)
	name_plate.position = name_target + Vector2(0, 12)

	var tween := create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(dim, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(panel, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(panel, "position", panel_target, entry_duration)
	tween.tween_property(name_plate, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(name_plate, "position", name_target, entry_duration)


func _animate_portrait_in(portrait: TextureRect) -> void:
	portrait.position = _portrait_target_position + Vector2(0, portrait_entry_offset)
	portrait.modulate = Color(1, 1, 1, 0)
	var tween := create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(portrait, "position", _portrait_target_position, entry_duration + 0.08)
	tween.tween_property(portrait, "modulate", Color.WHITE, entry_duration + 0.04)


## Đảm bảo Dialogic biết character bằng cả identifier file lẫn display_name.
func _ensure_dch_directory() -> void:
	var dir: Dictionary = DialogicResourceUtil.get_directory("dch")
	var keys_to_add := {}

	for key in dir.keys():
		var value = dir[key]
		var display_key := ""

		if typeof(value) == TYPE_STRING:
			var path: String = value
			if not ResourceLoader.exists(path):
				continue
			display_key = _read_display_name_from_file(path)
		elif value is DialogicCharacter:
			display_key = value.display_name

		if not display_key.is_empty() and not display_key in dir and not display_key in keys_to_add:
			keys_to_add[display_key] = value

	if not keys_to_add.is_empty():
		dir.merge(keys_to_add)
		Engine.set_meta("dch_directory", dir)


func _read_display_name_from_file(path: String) -> String:
	var file := FileAccess.open(path, FileAccess.READ)
	if not file:
		return ""
	var content := file.get_as_text()
	file.close()

	var dictionary_regex := RegEx.new()
	dictionary_regex.compile(r'&?"display_name"\s*:\s*"([^"]*)"')
	var dictionary_match := dictionary_regex.search(content)
	if dictionary_match:
		return dictionary_match.get_string(1)

	var tres_regex := RegEx.new()
	tres_regex.compile(r'display_name\s*=\s*"([^"]*)"')
	var tres_match := tres_regex.search(content)
	if tres_match:
		return tres_match.get_string(1)

	return ""


func _connect_speaker_signal() -> void:
	if not Dialogic.has_subsystem("Text"):
		return
	if not Dialogic.Text.speaker_updated.is_connected(_on_speaker_changed):
		Dialogic.Text.speaker_updated.connect(_on_speaker_changed)

	var current_speaker: String = Dialogic.current_state_info.get("speaker", "")
	if not current_speaker.is_empty():
		var char_res: DialogicCharacter = DialogicResourceUtil.get_character_resource(current_speaker)
		if char_res:
			_on_speaker_changed(char_res)


## Speaker đổi -> portrait đổi và chạy một slide/fade ngắn.
func _on_speaker_changed(character: DialogicCharacter) -> void:
	var portrait_rect: TextureRect = %SpeakerPortrait
	var name_plate: Control = %NamePlate

	if character == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		name_plate.visible = false
		_last_portrait_texture = null
		return

	name_plate.visible = true
	var tex: Texture2D = _get_character_portrait_texture(character)
	if tex == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		_last_portrait_texture = null
		return

	var is_new_portrait: bool = tex != _last_portrait_texture
	portrait_rect.texture = tex
	portrait_rect.visible = portrait_show
	portrait_rect.modulate = Color.WHITE

	# Tính lại size theo aspect ratio thực của ảnh để không bị vạch đen letterbox.
	var img_size: Vector2 = tex.get_size()
	var current_h: float = portrait_rect.size.y
	if current_h <= 0.0:
		current_h = portrait_size.y
	if img_size.y > 0.0:
		var aspect: float = img_size.x / img_size.y
		var fitted_w: float = current_h * aspect
		portrait_rect.custom_minimum_size = Vector2(fitted_w, current_h)
		portrait_rect.size = Vector2(fitted_w, current_h)
		_portrait_target_position.x = -fitted_w * 0.5 + portrait_center_x_offset
		portrait_rect.stretch_mode = TextureRect.STRETCH_SCALE

	portrait_rect.position = _portrait_target_position

	if enable_entry_animation and is_new_portrait and not Engine.is_editor_hint():
		_animate_portrait_in(portrait_rect)

	_last_portrait_texture = tex


func _get_character_portrait_texture(character: DialogicCharacter) -> Texture2D:
	var portraits: Dictionary = character.portraits
	if not portraits.is_empty():
		var portrait_key: String = character.default_portrait
		if portrait_key.is_empty() or not portrait_key in portraits:
			portrait_key = str(portraits.keys()[0])

		var portrait_data = portraits[portrait_key]
		if portrait_data is Dictionary:
			var image_path: String = str(portrait_data.get("image", ""))
			if image_path.is_empty():
				var overrides = portrait_data.get("export_overrides", {})
				if overrides is Dictionary:
					image_path = str(overrides.get("image", ""))
			if not image_path.is_empty() and ResourceLoader.exists(image_path):
				return load(image_path) as Texture2D
		elif portrait_data is Texture2D:
			return portrait_data

	var fallback_path := _find_image_in_dch(character.display_name)
	if not fallback_path.is_empty() and ResourceLoader.exists(fallback_path):
		return load(fallback_path) as Texture2D
	return null


func _find_image_in_dch(display_name_to_find: String) -> String:
	if display_name_to_find.is_empty():
		return ""

	for raw_path in DialogicResourceUtil.list_resources_of_type("dch"):
		var path: String = raw_path
		if _read_display_name_from_file(path) != display_name_to_find:
			continue

		var file := FileAccess.open(path, FileAccess.READ)
		if not file:
			continue
		var content := file.get_as_text()
		file.close()

		var image_regex := RegEx.new()
		image_regex.compile(r'"image"\s*:\s*"([^"]+)"')
		var image_match := image_regex.search(content)
		if image_match:
			return image_match.get_string(1)

		var ext_regex := RegEx.new()
		ext_regex.compile(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]*)"')
		var ext_match := ext_regex.search(content)
		if ext_match:
			return ext_match.get_string(1)

	return ""
