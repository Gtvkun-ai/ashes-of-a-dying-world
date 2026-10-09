@tool
extends DialogicLayoutLayer
## Dialogic Story — bố cục retro JRPG cho hội thoại cốt truyện.
## - Chân dung bust neo PHẢI, chồng lên dải thoại nhưng KHÔNG đè chữ.
## - Tên đặt TRÊN câu thoại, có nameplate gọn; không dùng avatar trùng lặp.
## - Phần artwork giữ nguyên file PNG hiện có, chỉ cắt vùng hiển thị bằng AtlasTexture.
## - Không thay đổi node DialogicNode_* / timeline / hiệu ứng typewriter.
## - Tự co bố cục cho viewport hẹp; HUD được quản lý ở chính HUD CanvasLayer.

# ── Box ────────────────────────────────────────────────────────────────
@export_group("Box")
@export_file("*.tres") var box_panel: String = ""
@export var box_width_ratio: float = 1.0
@export var box_min_width: float = 0.0
@export var box_max_width: float = 4096.0
@export var box_height_ratio: float = 0.205
@export var box_min_height: float = 164.0
@export var box_max_height: float = 195.0
@export var box_distance: int = 0

# ── Text ───────────────────────────────────────────────────────────────
@export_group("Text")
@export var text_use_global_size: bool = false
@export var text_custom_size: int = 23
@export var text_use_global_color: bool = false
@export var text_custom_color: Color = Color(0.955, 0.965, 0.99, 1.0)
@export var content_left_margin: int = 96  # Lề đọc tối thiểu tại viewport 1280px.
@export var content_top_margin: int = 40  # Giảm khoảng trống giữa tên và câu thoại.
@export var content_right_margin: int = 42
@export var content_bottom_margin: int = 24

# ── Name Label ─────────────────────────────────────────────────────────
@export_group("Name Label")
@export var name_label_color_mode: int = 1  # 0=GLOBAL, 1=CUSTOM, 2=CHARACTER
@export var name_label_custom_color: Color = Color(0.69, 0.83, 1.0, 1.0)
@export var name_label_use_global_size: bool = false
@export var name_label_custom_size: int = 20
## Vị trí tên được tính từ lề trái và cạnh TRÊN textbox.
@export var name_plate_offset: Vector2 = Vector2(-8, -16)
@export var name_plate_size: Vector2 = Vector2(188, 34)

# ── Portrait ───────────────────────────────────────────────────────────
@export_group("Portrait")
## Kích thước chuẩn cho viewport 1280x720. Script tự scale theo chiều cao màn hình.
@export var portrait_size: Vector2 = Vector2(930, 825)
@export var portrait_show: bool = true
@export var portrait_center_x_offset: float = 8.0  # Khoảng cách tới mép PHẢI màn hình.
@export var portrait_vertical_offset: float = 86.0  # Hạ mặt xuống vùng đọc, chân biến mất sau dải thoại.
## Số pixel portrait chui xuống phía sau textbox (ở 720p).
@export var portrait_panel_overlap: float = 255.0
@export var portrait_draw_behind_panel: bool = true

# ── Framing ───────────────────────────────────────────────────────────
@export_group("Portrait framing")
@export var portrait_crop_to_bust: bool = true
@export_range(0.5, 1.0, 0.01) var portrait_crop_height_ratio: float = 0.79
## Màn hình hẹp sẽ ẩn bust, mở toàn bộ chiều rộng cho tiếng Việt.
@export var portrait_min_screen_width: float = 980.0
@export var portrait_text_gap: float = 30.0

# ── Backdrop & Motion ──────────────────────────────────────────────────
@export_group("Backdrop & Motion")
@export var dim_color: Color = Color(0.015, 0.025, 0.045, 0.04)
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
var _available_text_right: float = 0.0


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

	# Bust tính theo kích thước texture thật (đã crop); mọi trường hợp neo MÉP PHẢI.
	var portrait_scale: float = clampf(viewport_size.y / 720.0, 0.62, 1.6)
	_update_portrait_and_text_area(panel, portrait_scale)

	# Một thanh phân cách mảnh dẫn mắt từ tên đến mép vùng đọc.
	# Đường không chạy xuyên qua mặt/tóc của nhân vật.
	var top_rule: ColorRect = %TopRule
	top_rule.position = panel.position + Vector2(0.0, 1.0)
	top_rule.size = Vector2(maxf(0.0, _available_text_right - panel.position.x), 1.0)

	# Tên neo liền với mép trên dải thoại, không phải một "button" rời.
	var name_plate: PanelContainer = %NamePlate
	name_plate.position = panel.position + Vector2(
		float(_get_text_left_margin(panel)) + name_plate_offset.x,
		name_plate_offset.y * portrait_scale
	)
	name_plate.size = name_plate_size * portrait_scale
	name_plate.z_index = 7

	# Dấu băng bên trái tên: vừa mang nhận diện, vừa chỉ vị trí bắt đầu đọc.
	var name_gem: ColorRect = %NameGem
	name_gem.position = name_plate.position + Vector2(8.0 * portrait_scale, 12.0 * portrait_scale)
	name_gem.size = Vector2(8.0, 8.0) * portrait_scale
	name_gem.pivot_offset = name_gem.size * 0.5
	name_gem.rotation = PI / 4.0
	name_gem.visible = name_plate.visible

	var accent_rule: ColorRect = %AccentRule
	var line_start: float = name_plate.position.x + name_plate.size.x + 12.0 * portrait_scale
	accent_rule.position = Vector2(line_start, panel.position.y + 1.0)
	accent_rule.size = Vector2(maxf(0.0, _available_text_right - line_start - 8.0), 1.0)

	# Margin thực tế đã được đo theo vị trí chân dung; chữ không bao giờ bị tóc che.
	var content_margin: MarginContainer = panel.get_node("ContentMargin")
	content_margin.add_theme_constant_override(&"margin_top", int(round(content_top_margin * portrait_scale)))
	content_margin.add_theme_constant_override(&"margin_bottom", content_bottom_margin)

	# Next nằm ở mép cuối vùng đọc, KHÔNG trôi qua chân dung.
	var next_indicator: Control = %NextIndicator
	next_indicator.position = Vector2(
		_available_text_right - 24.0 * portrait_scale,
		panel.position.y + panel.size.y - 20.0 * portrait_scale
	)
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
	if _is_reduced_motion_enabled():
		_snap_layout_intro_to_final_state()
		return

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


func _snap_layout_intro_to_final_state() -> void:
	%DimBackground.modulate = Color.WHITE
	%DialogTextPanel.modulate = Color.WHITE
	%NamePlate.modulate = Color.WHITE


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
	# Khi timeline đổi biểu cảm, cập nhật bust theo portrait key hiện tại.
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


## Người nói đổi: cập nhật bust / nameplate mà không can thiệp typewriter.
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
	%MiniPortrait.visible = false  # Node cũ giữ lại để scene Dialogic không mất NodePath.
	%MiniPortraitHalo.visible = false
	%NamePlate.visible = false
	%NameGem.visible = false
	_last_portrait_texture = null
	var scale: float = clampf(_layout_size.y / 720.0, 0.62, 1.6)
	_update_portrait_and_text_area(%DialogTextPanel, scale)


func _show_character_portraits(character: DialogicCharacter, portrait_key: String) -> void:
	_current_portrait_key = portrait_key
	%NamePlate.visible = true
	%NameGem.visible = true
	var source: Texture2D = _get_character_portrait_texture(character, portrait_key)
	var portrait_rect: TextureRect = %SpeakerPortrait
	if source == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		%MiniPortrait.visible = false
		%MiniPortraitHalo.visible = false
		_update_portrait_and_text_area(%DialogTextPanel, clampf(_layout_size.y / 720.0, 0.62, 1.6))
		return

	var is_new_portrait: bool = source != _last_portrait_texture
	portrait_rect.texture = _make_bust_texture(source)
	portrait_rect.modulate = Color.WHITE
	portrait_rect.stretch_mode = TextureRect.STRETCH_SCALE
	# Không nhân bản chân dung nhỏ: cùng một khuôn mặt hai lần làm UI bị rối.
	%MiniPortrait.visible = false
	%MiniPortraitHalo.visible = false
	_update_portrait_and_text_area(%DialogTextPanel, clampf(_layout_size.y / 720.0, 0.62, 1.6))

	if enable_entry_animation and is_new_portrait and portrait_rect.visible \
		and not Engine.is_editor_hint() and not _is_reduced_motion_enabled():
		_animate_portrait_in(portrait_rect)
	_last_portrait_texture = source


## Không tạo ảnh mới: AtlasTexture chỉ quyết định phần PNG sẽ hiển thị.
## Ảnh full-body vẫn ở nguyên đường dẫn, các portrait nhân vật tỷ lệ ngang không bị cắt.
func _make_bust_texture(source: Texture2D) -> Texture2D:
	if not portrait_crop_to_bust or source.get_height() <= source.get_width() * 1.2:
		return source
	var cropped := AtlasTexture.new()
	cropped.atlas = source
	cropped.region = Rect2(0.0, 0.0, source.get_width(),
		float(roundi(source.get_height() * portrait_crop_height_ratio)))
	return cropped


## Layout từ hai biên: vùng chữ bên trái, chân dung bên phải.
## Nếu viewport hẹp, ẩn portrait để câu tiếng Việt dài không bị bóp.
func _update_portrait_and_text_area(panel: PanelContainer, scale: float) -> void:
	var portrait: TextureRect = %SpeakerPortrait
	var display_bust: bool = portrait_show and portrait.texture != null \
		and _layout_size.x >= portrait_min_screen_width \
		and _layout_size.x / maxf(_layout_size.y, 1.0) >= 1.35
	portrait.visible = display_bust
	portrait.z_index = 5 if not portrait_draw_behind_panel else 2

	var text_right: float = panel.position.x + panel.size.x - content_right_margin
	if portrait.texture != null:
		var texture_size: Vector2 = portrait.texture.get_size()
		if texture_size.y > 0.0:
			var display_height: float = portrait_size.y * scale
			var display_width: float = display_height * texture_size.x / texture_size.y
			portrait.custom_minimum_size = Vector2(display_width, display_height)
			portrait.size = Vector2(display_width, display_height)
			_portrait_target_position = Vector2(
				_layout_size.x * 0.5 - display_width - portrait_center_x_offset * scale,
				panel.position.y - display_height + portrait_panel_overlap * scale + portrait_vertical_offset * scale
			)
			portrait.position = _portrait_target_position
			if display_bust:
				# Chừa phần có thể chứa tóc, kể cả khi alpha của ảnh trải rộng.
				text_right = minf(text_right, _portrait_target_position.x - portrait_text_gap * scale)

	var text_left: float = float(_get_text_left_margin(panel))
	# Bust lớn hơn nên không được áp ngưỡng 36% cũ: nó sẽ tự ẩn Hyou
	# ở 1600x900! Luôn chừa tối thiểu ~30% bề ngang cho chữ.
	if display_bust and text_right - (panel.position.x + text_left) < panel.size.x * 0.30:
		portrait.visible = false
		text_right = panel.position.x + panel.size.x - content_right_margin
	_available_text_right = text_right

	# Khi đổi người nói, biên đọc có thể thay đổi: cập nhật đường kẻ ngay.
	var top_rule: ColorRect = %TopRule
	top_rule.size.x = maxf(0.0, text_right - panel.position.x)
	var accent_rule: ColorRect = %AccentRule
	accent_rule.size.x = maxf(0.0, text_right - accent_rule.position.x - 8.0)

	var margin: MarginContainer = panel.get_node("ContentMargin")
	margin.add_theme_constant_override(&"margin_left", int(round(text_left)))
	margin.add_theme_constant_override(&"margin_right", int(round(panel.position.x + panel.size.x - text_right)))
	# Phương thức này còn được gọi khi người nói đổi, không chỉ khi resize.
	%NextIndicator.position.x = text_right - 24.0 * scale


func _get_text_left_margin(panel: PanelContainer) -> int:
	# Theo base grid, giữ lề tương xứng giữa 720p và 900p.
	return maxi(content_left_margin, roundi(panel.size.x * 0.095))


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
