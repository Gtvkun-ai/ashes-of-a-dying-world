@tool
extends DialogicLayoutLayer
## Custom JRPG Textbox Layer
## Hiển thị: [avatar nhỏ] [Tên nhân vật] phía trên text
## Avatar được load từ Dialogic character resource (.dch)

# ── Exports ─────────────────────────────────────────────────────────────
@export_group("Box")
@export_file("*.tres") var box_panel: String = ""
@export var box_size: Vector2 = Vector2(860, 145)
@export var box_distance: int = 20

@export_group("Text")
@export var text_use_global_size: bool = false
@export var text_custom_size: int = 15
@export var text_use_global_color: bool = false
@export var text_custom_color: Color = Color(0.93, 0.93, 0.96, 1.0)

@export_group("Name Label")
@export var name_label_color_mode: int = 2  # 2 = CHARACTER_COLOR
@export var name_label_custom_color: Color = Color(0.78, 0.65, 0.40, 1.0)
@export var name_label_use_global_size: bool = false
@export var name_label_custom_size: int = 17

@export_group("Avatar")
@export var avatar_size: int = 36
@export var avatar_show: bool = true


## Called by Dialogic whenever export overrides might change.
## Đây là nơi an toàn nhất để init vì Dialogic gọi hàm này
## trước khi timeline bắt đầu process event.
func _apply_export_overrides() -> void:
	if !is_inside_tree():
		await ready

	# ── Build Dialogic character directory nếu chưa có ──────────────────
	# Cần làm trước khi timeline process để get_character_resource() tìm được .dch
	if not Engine.is_editor_hint():
		_ensure_dch_directory()

	# ── Resize box ──────────────────────────────────────────────────────
	var panel: PanelContainer = %DialogTextPanel
	panel.custom_minimum_size = box_size
	panel.position = Vector2(-box_size.x * 0.5, -box_size.y - box_distance)

	if box_panel and ResourceLoader.exists(box_panel):
		panel.add_theme_stylebox_override(&"panel", load(box_panel))

	# ── Dialog text ─────────────────────────────────────────────────────
	var dialog_text: DialogicNode_DialogText = %DialogicNode_DialogText
	dialog_text.add_theme_font_size_override(&"normal_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"bold_font_size", text_custom_size)
	dialog_text.add_theme_font_size_override(&"italics_font_size", text_custom_size)
	dialog_text.add_theme_color_override(&"default_color", text_custom_color)

	# ── Name label ──────────────────────────────────────────────────────
	var name_label: DialogicNode_NameLabel = %DialogicNode_NameLabel
	name_label.add_theme_font_size_override(&"font_size", name_label_custom_size)
	match name_label_color_mode:
		0:  # GLOBAL
			name_label.add_theme_color_override(&"font_color",
				get_global_setting(&'font_color', name_label_custom_color) as Color)
		1:  # CUSTOM
			name_label.add_theme_color_override(&"font_color", name_label_custom_color)
		2:  # CHARACTER
			name_label.use_character_color = true

	# ── Avatar ──────────────────────────────────────────────────────────
	var avatar_rect: TextureRect = %SpeakerAvatar
	avatar_rect.visible = avatar_show
	avatar_rect.custom_minimum_size = Vector2(avatar_size, avatar_size)
	var avatar_frame: Control = avatar_rect.get_parent()
	if avatar_frame:
		avatar_frame.visible = avatar_show

	# ── Connect speaker signal ───────────────────────────────────────────
	if not Engine.is_editor_hint():
		_connect_speaker_signal()


## Đảm bảo Dialogic biết character bằng cả display_name lẫn filename.
## Dialogic tự build directory với key = filename ("hyou"),
## nhưng timeline dùng display_name ("Hyou") → cần thêm alias.
## KHÔNG dùng ResourceLoader.load() vì CharacterResourceLoader của Dialogic bị lỗi với format .tres.
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
			# Đọc display_name từ file text — tránh ResourceLoader.load() gây crash
			display_key = _read_display_name_from_file(path)
		elif value is Resource:
			display_key = value.get("display_name") if "display_name" in value else ""
			
		if not display_key.is_empty() and not display_key in dir and not display_key in keys_to_add:
			keys_to_add[display_key] = value

	if not keys_to_add.is_empty():
		dir.merge(keys_to_add)
		Engine.set_meta("dch_directory", dir)
		print("[Avatar] added display_name aliases: ", keys_to_add.keys())


## Đọc trực tiếp dòng display_name từ file .dch (format .tres) bằng FileAccess.
func _read_display_name_from_file(path: String) -> String:
	var file := FileAccess.open(path, FileAccess.READ)
	if not file:
		return ""
	while not file.eof_reached():
		var line := file.get_line()
		if line.begins_with("display_name = "):
			file.close()
			return line.trim_prefix("display_name = \"").trim_suffix("\"")
	file.close()
	return ""





func _connect_speaker_signal() -> void:
	if not Dialogic.has_subsystem("Text"):
		return
	if not Dialogic.Text.speaker_updated.is_connected(_on_speaker_changed):
		Dialogic.Text.speaker_updated.connect(_on_speaker_changed)
		print("[Avatar] speaker_updated signal connected OK")
	# Load avatar cho speaker hiện tại nếu đã có
	var current_speaker: String = Dialogic.current_state_info.get("speaker", "")
	if not current_speaker.is_empty():
		var char_res: DialogicCharacter = DialogicResourceUtil.get_character_resource(current_speaker)
		if char_res:
			_on_speaker_changed(char_res)


## Called by Dialogic when the speaker changes — update avatar
func _on_speaker_changed(character: DialogicCharacter) -> void:
	var avatar_rect: TextureRect = %SpeakerAvatar
	var avatar_frame: Control = avatar_rect.get_parent()

	if character == null:
		avatar_rect.texture = null
		avatar_rect.visible = false
		if avatar_frame: avatar_frame.visible = false
		return

	var tex: Texture2D = null
	var portraits: Dictionary = character.portraits

	if not portraits.is_empty():
		# Đường đi bình thường: lấy texture từ portraits dict
		var default_key: String = character.default_portrait
		if default_key.is_empty() or not default_key in portraits:
			default_key = portraits.keys()[0]
		var portrait_data = portraits[default_key]
		if portrait_data is Dictionary:
			tex = portrait_data.get("texture", null)
			if tex == null and portrait_data.get("image", "") != "":
				tex = load(portrait_data.get("image"))
		elif portrait_data is Texture2D:
			tex = portrait_data

	if tex == null:
		# Fallback: đọc thẳng file .dch bằng text để lấy texture path
		# Không dùng ResourceLoader để tránh crash với CharacterResourceLoader
		var tex_path := _find_texture_in_dch(character.display_name)
		print("[Avatar] fallback tex_path='", tex_path, "' for '", character.display_name, "'")
		if not tex_path.is_empty() and ResourceLoader.exists(tex_path):
			tex = load(tex_path)

	print("[Avatar] final tex=", tex)

	if tex == null:
		avatar_rect.visible = false
		if avatar_frame: avatar_frame.visible = false
		return

	if avatar_frame: avatar_frame.visible = avatar_show
	avatar_rect.visible = avatar_show
	avatar_rect.texture = tex
	print("[Avatar] SUCCESS!")


## Quét tất cả file .dch bằng text, tìm character có display_name khớp,
## trả về path của Texture2D đầu tiên trong file đó.
## Không dùng ResourceLoader — đọc raw text để tránh CharacterResourceLoader crash.
func _find_texture_in_dch(display_name_to_find: String) -> String:
	if display_name_to_find.is_empty():
		return ""
	for raw_path in DialogicResourceUtil.list_resources_of_type("dch"):
		var path: String = raw_path
		var file := FileAccess.open(path, FileAccess.READ)
		if not file:
			continue
		var content := file.get_as_text()
		file.close()
		# Kiểm tra đây có phải character cần tìm không
		if ('display_name = "' + display_name_to_find + '"') not in content:
			continue
		# Tìm Texture2D ext_resource đầu tiên trong file
		var re := RegEx.new()
		re.compile(r'\[ext_resource type="Texture2D"[^\]]*path="([^"]*)"')
		var m := re.search(content)
		if m:
			return m.get_string(1)
	return ""
