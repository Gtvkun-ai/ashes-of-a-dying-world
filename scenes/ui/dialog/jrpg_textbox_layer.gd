@tool
extends DialogicLayoutLayer
## Bản thử Dialogic điện ảnh — ưu tiên artwork và khả năng đọc lời thoại.
## - Bust nhân vật lớn đứng SAU dải thoại trong suốt, không bó vào một ô portrait.
## - Ảnh cận mặt nhỏ nằm trong dải thoại để về sau đổi biểu cảm độc lập.
## - Tên đặt giữa chân dải thoại, như giao diện visual novel tham khảo.
## - Giữ nguyên các node DialogicNode_* để timeline, typewriter, âm thanh hoạt động.
## - Dùng đúng asset nhân vật đã có; không sinh thêm portrait bằng AI.

# ── Box ────────────────────────────────────────────────────────────────
@export_group("Box")
@export_file("*.tres") var box_panel: String = ""
@export var box_width_ratio: float = 1.0
@export var box_min_width: float = 0.0
@export var box_max_width: float = 4096.0
@export var box_height_ratio: float = 0.267
@export var box_min_height: float = 150.0
@export var box_max_height: float = 280.0
@export var box_distance: int = 0

# ── Text ───────────────────────────────────────────────────────────────
@export_group("Text")
@export var text_use_global_size: bool = false
@export var text_custom_size: int = 20
@export var text_use_global_color: bool = false
@export var text_custom_color: Color = Color(0.955, 0.965, 0.99, 1.0)
@export var content_left_margin: int = 24  # Tối thiểu; script cộng khoảng dành cho mặt nhỏ.
@export var content_top_margin: int = 25
@export var content_right_margin: int = 54
@export var content_bottom_margin: int = 47  # Nhường chỗ cho tên ở chân dải thoại.

# ── Name Label ─────────────────────────────────────────────────────────
@export_group("Name Label")
@export var name_label_color_mode: int = 2  # 0=GLOBAL, 1=CUSTOM, 2=CHARACTER
@export var name_label_custom_color: Color = Color(0.69, 0.83, 1.0, 1.0)
@export var name_label_use_global_size: bool = false
@export var name_label_custom_size: int = 17
## Offset tính từ góc trên-trái của textbox.
@export var name_plate_offset: Vector2 = Vector2(0, -39)  # Căn giữa cạnh dưới, không gắn vào góc trái.
@export var name_plate_size: Vector2 = Vector2(210, 30)

# ── Portrait ───────────────────────────────────────────────────────────
@export_group("Portrait")
## Kích thước chuẩn cho viewport 1280x720. Script tự scale theo chiều cao màn hình.
@export var portrait_size: Vector2 = Vector2(790, 650)
@export var portrait_show: bool = true
@export var portrait_center_x_offset: float = 20.0
@export var portrait_vertical_offset: float = 0.0
## Số pixel portrait chui xuống phía sau textbox (ở 720p).
@export var portrait_panel_overlap: float = 160.0
@export var portrait_draw_behind_panel: bool = true

# ── Chân dung nhỏ ─────────────────────────────────────────────────────
@export_group("Mini portrait")
@export var show_mini_portrait: bool = true
## Tỷ lệ theo màn 720p. Màn rất hẹp tự ẩn để không bóp nội dung thoại.
@export var mini_portrait_size: float = 144.0
@export var mini_portrait_left_ratio: float = 0.125
@export var mini_portrait_gap: float = 29.0

# ── Backdrop & Motion ──────────────────────────────────────────────────
@export_group("Backdrop & Motion")
@export var dim_color: Color = Color(0.015, 0.025, 0.045, 0.075)
@export var enable_entry_animation: bool = true
@export var entry_duration: float = 0.18
@export var portrait_entry_offset: float = 12.0

const REGULAR_FONT_PATH := "res://assets/fonts/BeVietnamPro-Regular.ttf"
const SEMIBOLD_FONT_PATH := "res://assets/fonts/BeVietnamPro-SemiBold.ttf"
const ITALIC_FONT_PATH := "res://assets/fonts/BeVietnamPro-Italic.ttf"
const SEMIBOLD_ITALIC_FONT_PATH := "res://assets/fonts/BeVietnamPro-SemiBoldItalic.ttf"

var _portrait_target_position := Vector2.ZERO
var _layout_intro_played := false
var _last_portrait_texture: Texture2D = null
var _viewport_connected := false
var _current_portrait_key: String = ""
var _current_character: DialogicCharacter = null
var _layout_size: Vector2 = Vector2(1280, 720)


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
	# Dải lời thoại phủ hết bề ngang, sát đáy màn hình.
	# Cố ý không giới hạn 920px: hình lớn được nhìn xuyên qua nền.
	_layout_size = viewport_size
	var panel_width: float = minf(clampf(viewport_size.x * box_width_ratio, box_min_width, box_max_width), viewport_size.x)
	var panel_height: float = clampf(viewport_size.y * box_height_ratio, box_min_height, box_max_height)
	var effective_box_size := Vector2(panel_width, panel_height)

	var panel: PanelContainer = %DialogTextPanel
	panel.custom_minimum_size = effective_box_size
	panel.position = Vector2(-panel_width * 0.5, -panel_height - box_distance)
	panel.size = effective_box_size
	panel.z_index = 4
	if not box_panel.is_empty() and ResourceLoader.exists(box_panel):
		panel.add_theme_stylebox_override(&"panel", load(box_panel))

	# Bust luôn giữ đúng aspect của ảnh nguồn, kể cả khi người chơi đổi resolution.
	var portrait_scale: float = clampf(viewport_size.y / 720.0, 0.62, 1.6)
	var effective_portrait_size: Vector2 = portrait_size * portrait_scale
	var portrait: TextureRect = %SpeakerPortrait
	portrait.visible = portrait_show and portrait.texture != null
	portrait.custom_minimum_size = effective_portrait_size
	portrait.size = effective_portrait_size
	var effective_vertical_offset: float = portrait_vertical_offset * portrait_scale
	_portrait_target_position = Vector2(
		-effective_portrait_size.x * 0.5 + portrait_center_x_offset * portrait_scale,
		panel.position.y - effective_portrait_size.y + portrait_panel_overlap * portrait_scale + effective_vertical_offset
	)
	portrait.position = _portrait_target_position
	portrait.z_index = 2 if portrait_draw_behind_panel else 5
	if portrait.texture != null:
		_fit_portrait_to_current_height(portrait)
		portrait.position = _portrait_target_position
	if _is_reduced_motion_enabled():
		portrait.modulate = Color.WHITE

	# Hai đường sáng rất mảnh là yếu tố nhận diện — không đặt khung vàng kép.
	var top_rule: ColorRect = %TopRule
	top_rule.position = panel.position
	top_rule.size = Vector2(panel_width, 1.0)
	var accent_rule: ColorRect = %AccentRule
	accent_rule.position = panel.position + Vector2(viewport_size.x * 0.125, 0)
	accent_rule.size = Vector2(minf(96.0 * portrait_scale, panel_width * 0.18), 2.0)

	# Tên nhân vật nằm gần giữa cạnh dưới, tách khỏi vùng văn bản.
	var name_plate: PanelContainer = %NamePlate
	name_plate.position = Vector2(-name_plate_size.x * 0.5, -39.0 * portrait_scale)
	name_plate.size = name_plate_size
	name_plate.z_index = 7

	# ── Margins / typography ───────────────────────────────────────────
	var content_margin: MarginContainer = panel.get_node("ContentMargin")
	content_margin.add_theme_constant_override(&"margin_left", content_left_margin)
	content_margin.add_theme_constant_override(&"margin_top", content_top_margin)
	content_margin.add_theme_constant_override(&"margin_right", content_right_margin)
	content_margin.add_theme_constant_override(&"margin_bottom", content_bottom_margin)
	# Phải áp margin trái SAU margin mặc định để vùng chữ tránh icon nhỏ.
	_update_mini_portrait_layout(panel, portrait_scale)

	# Next indicator is a sibling of the PanelContainer. Putting it inside a
	# Container made Godot re-layout it to the panel's top-left.
	var next_indicator: Control = %NextIndicator
	next_indicator.position = panel.position + panel.size - Vector2(35.0 * portrait_scale, 31.0 * portrait_scale)
	next_indicator.z_index = 7
	if _is_reduced_motion_enabled():
		next_indicator.set("animation", 2)

	var dialog_text: DialogicNode_DialogText = %DialogicNode_DialogText
	var regular_font := _load_font_or_fallback(REGULAR_FONT_PATH, null)
	var semibold_font := _load_font_or_fallback(SEMIBOLD_FONT_PATH, regular_font)
	var italic_font := _load_font_or_fallback(ITALIC_FONT_PATH, regular_font)
	var semibold_italic_font := _load_font_or_fallback(SEMIBOLD_ITALIC_FONT_PATH, semibold_font)
	if regular_font:
		dialog_text.add_theme_font_override(&"normal_font", regular_font)
		dialog_text.add_theme_font_override(&"bold_font", semibold_font)
		dialog_text.add_theme_font_override(&"italics_font", italic_font)
		dialog_text.add_theme_font_override(&"bold_italics_font", semibold_italic_font)
	dialog_text.add_theme_font_size_override(&"normal_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"bold_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"italics_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"bold_italics_font_size", text_custom_size)
	dialog_text.add_theme_color_override(&"default_color", text_custom_color)

	var name_label: DialogicNode_NameLabel = %DialogicNode_NameLabel
	if semibold_font:
		name_label.add_theme_font_override(&"font", semibold_font)
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
		if _is_reduced_motion_enabled():
			_snap_layout_intro_to_final_state()
		elif enable_entry_animation and not _layout_intro_played:
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
	var mini: TextureRect = %MiniPortrait
	if _is_reduced_motion_enabled():
		_snap_layout_intro_to_final_state()
		return

	dim.modulate = Color(1, 1, 1, 0)
	panel.modulate = Color(1, 1, 1, 0)
	name_plate.modulate = Color(1, 1, 1, 0)
	mini.modulate = Color(1, 1, 1, 0)
	panel.position = panel_target + Vector2(0, 12)
	name_plate.position = name_target + Vector2(0, 12)

	var tween := create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(dim, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(panel, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(panel, "position", panel_target, entry_duration)
	tween.tween_property(name_plate, "modulate", Color.WHITE, entry_duration)
	tween.tween_property(name_plate, "position", name_target, entry_duration)
	tween.tween_property(mini, "modulate", Color.WHITE, entry_duration + 0.08)


func _snap_layout_intro_to_final_state() -> void:
	%DimBackground.modulate = Color.WHITE
	%DialogTextPanel.modulate = Color.WHITE
	%NamePlate.modulate = Color.WHITE
	%MiniPortrait.modulate = Color.WHITE


func _animate_portrait_in(portrait: TextureRect) -> void:
	if _is_reduced_motion_enabled():
		portrait.position = _portrait_target_position
		portrait.modulate = Color.WHITE
		return

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
	# Trường hợp timeline chỉ đổi biểu cảm trong một câu thoại, hoặc nhân vật
	# chưa có PortraitContainer mặc định, tín hiệu này vẫn mang portrait key.
	if not Dialogic.Text.text_started.is_connected(_on_text_started):
		Dialogic.Text.text_started.connect(_on_text_started)
	# Khi timeline đổi biểu cảm, cập nhật cả bust và icon theo cùng portrait key.
	if Dialogic.has_subsystem("Portraits"):
		if not Dialogic.Portraits.character_portrait_changed.is_connected(_on_portrait_changed):
			Dialogic.Portraits.character_portrait_changed.connect(_on_portrait_changed)

	var current_speaker: String = Dialogic.current_state_info.get("speaker", "")
	if not current_speaker.is_empty():
		var char_res: DialogicCharacter = DialogicResourceUtil.get_character_resource(current_speaker)
		if char_res:
			_on_speaker_changed(char_res)


func _load_font_or_fallback(path: String, fallback: Font) -> Font:
	if ResourceLoader.exists(path):
		var loaded_font := load(path) as Font
		if loaded_font != null:
			return loaded_font
	return fallback


## Người nói đổi: bật/tắt cả hai lớp chân dung, không ảnh hưởng typewriter.
func _on_speaker_changed(character: DialogicCharacter) -> void:
	_current_character = character
	_current_portrait_key = ""
	if character == null:
		_hide_character_portraits()
		return
	var portrait_key := character.default_portrait
	var identifiers: Dictionary = Dialogic.current_state_info.get("portraits", {})
	var joined = identifiers.get(character.get_identifier(), {})
	if joined is Dictionary:
		portrait_key = str(joined.get("portrait", portrait_key))
	_show_character_portraits(character, portrait_key)


## Mỗi câu thoại tự cập nhật biểu cảm nếu nó có portrait key cụ thể.
## Ví dụ trong timeline: Hyou (happy): ... — không cần thêm code.
func _on_text_started(info: Dictionary) -> void:
	var character: DialogicCharacter = info.get("character") as DialogicCharacter
	if character == null or _current_character == null:
		return
	if character.get_identifier() != _current_character.get_identifier():
		return
	var portrait_key: String = str(info.get("portrait", ""))
	if not portrait_key.is_empty() and portrait_key != _current_portrait_key:
		_show_character_portraits(character, portrait_key)


## Tín hiệu này được Dialogic phát khi timeline chuyển "neutral" -> "happy" ...
## sau khi bổ sung key và ảnh trong .dch. Không cần sửa giao diện lần nữa.
func _on_portrait_changed(info: Dictionary) -> void:
	var character: DialogicCharacter = info.get("character") as DialogicCharacter
	if character == null or _current_character == null:
		return
	if character.get_identifier() != _current_character.get_identifier():
		return
	_show_character_portraits(character, str(info.get("portrait", character.default_portrait)))


func _hide_character_portraits() -> void:
	%SpeakerPortrait.texture = null
	%SpeakerPortrait.visible = false
	%MiniPortrait.visible = false
	%MiniPortraitHalo.visible = false
	%NamePlate.visible = false
	_last_portrait_texture = null
	_update_mini_portrait_layout(%DialogTextPanel, clampf(_layout_size.y / 720.0, 0.62, 1.6))


func _show_character_portraits(character: DialogicCharacter, portrait_key: String) -> void:
	_current_portrait_key = portrait_key
	%NamePlate.visible = true
	var portrait_rect: TextureRect = %SpeakerPortrait
	var texture: Texture2D = _get_character_portrait_texture(character, portrait_key)
	if texture == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		%MiniPortrait.visible = false
		%MiniPortraitHalo.visible = false
		_update_mini_portrait_layout(%DialogTextPanel, clampf(_layout_size.y / 720.0, 0.62, 1.6))
		return

	var is_new_portrait: bool = texture != _last_portrait_texture
	portrait_rect.texture = texture
	portrait_rect.visible = portrait_show
	portrait_rect.modulate = Color.WHITE
	_fit_portrait_to_current_height(portrait_rect)
	portrait_rect.position = _portrait_target_position

	var mini_texture: Texture2D = _get_mini_portrait_texture(character, portrait_key)
	%MiniPortrait.texture = mini_texture if mini_texture != null else texture
	_update_mini_portrait_layout(%DialogTextPanel, clampf(_layout_size.y / 720.0, 0.62, 1.6))

	if enable_entry_animation and is_new_portrait and not Engine.is_editor_hint() and not _is_reduced_motion_enabled():
		_animate_portrait_in(portrait_rect)
	_last_portrait_texture = texture


## Fit ngang theo tỷ lệ gốc để bust không bị méo, đồng thời giữ neo giữa.
func _fit_portrait_to_current_height(portrait_rect: TextureRect) -> void:
	var tex: Texture2D = portrait_rect.texture
	if tex == null or tex.get_size().y <= 0.0:
		return
	var height: float = portrait_size.y * clampf(_layout_size.y / 720.0, 0.62, 1.6)
	var fitted_width: float = height * tex.get_size().x / tex.get_size().y
	portrait_rect.custom_minimum_size = Vector2(fitted_width, height)
	portrait_rect.size = Vector2(fitted_width, height)
	_portrait_target_position.x = -fitted_width * 0.5 + portrait_center_x_offset * clampf(_layout_size.y / 720.0, 0.62, 1.6)
	portrait_rect.stretch_mode = TextureRect.STRETCH_SCALE


## Tự chọn icon cận mặt nếu tồn tại. Convention cho cảm xúc về sau:
##   assets/graphics/characters/hyou/icon_happy.png, icon_sad.png, ...
## Nếu chưa vẽ thì dùng icon.png, không cần bổ sung asset cho bản thử.
func _get_mini_portrait_texture(character: DialogicCharacter, portrait_key: String) -> Texture2D:
	var directory: String = "res://assets/graphics/characters/%s/" % character.get_identifier().to_lower()
	var mood_path: String = directory + "icon_" + portrait_key + ".png"
	if ResourceLoader.exists(mood_path):
		return load(mood_path) as Texture2D
	var default_path: String = directory + "icon.png"
	if ResourceLoader.exists(default_path):
		return load(default_path) as Texture2D
	return null


## Giữ ô text rộng và dễ đọc cả khi không có nhân vật hoặc màn hình nhỏ.
func _update_mini_portrait_layout(panel: PanelContainer, portrait_scale: float) -> void:
	var mini: TextureRect = %MiniPortrait
	var halo: Panel = %MiniPortraitHalo
	var left_padding: float = maxf(float(content_left_margin), panel.size.x * 0.035)
	var can_show: bool = show_mini_portrait and _current_character != null and mini.texture != null and panel.size.x >= 850.0
	mini.visible = can_show
	halo.visible = can_show
	if can_show:
		var image_size: float = mini_portrait_size * portrait_scale
		var left_edge: float = panel.position.x + panel.size.x * mini_portrait_left_ratio
		var top_edge: float = panel.position.y + (panel.size.y - image_size) * 0.42
		mini.position = Vector2(left_edge, top_edge)
		mini.size = Vector2(image_size, image_size)
		halo.position = mini.position - Vector2(4, 4) * portrait_scale
		halo.size = mini.size + Vector2(8, 8) * portrait_scale
		left_padding = left_edge - panel.position.x + image_size + mini_portrait_gap * portrait_scale

	var margin: MarginContainer = panel.get_node("ContentMargin")
	margin.add_theme_constant_override(&"margin_left", int(round(left_padding)))
	margin.add_theme_constant_override(&"margin_right", content_right_margin)



func _is_reduced_motion_enabled() -> bool:
	var settings_manager := get_node_or_null("/root/SettingsManager")
	return is_instance_valid(settings_manager) \
		and settings_manager.has_method(&"IsReducedMotionEnabled") \
		and bool(settings_manager.call(&"IsReducedMotionEnabled"))


func _get_character_portrait_texture(character: DialogicCharacter, portrait_override: String = "") -> Texture2D:
	var portraits: Dictionary = character.portraits
	if not portraits.is_empty():
		var portrait_key: String = portrait_override if not portrait_override.is_empty() else character.default_portrait
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
