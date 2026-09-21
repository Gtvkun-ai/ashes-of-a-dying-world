@tool
extends DialogicLayoutLayer
## Custom JRPG Textbox Layer
##
## Triết lý UI:
## - Portrait đứng BÊN NGOÀI textbox, không có khung bao quanh.
## - Portrait chỉ đè nhẹ lên mép trái của hộp thoại.
## - Nameplate nhỏ nằm trên viền hộp thoại.
## - Textbox giữ màu tối/ấm để hòa với HUD hiện tại, không dùng cyan glow.

# ── Exports ─────────────────────────────────────────────────────────────
@export_group("Box")
@export_file("*.tres") var box_panel: String = ""
@export var box_size: Vector2 = Vector2(860, 132)
@export var box_distance: int = 18

@export_group("Text")
@export var text_use_global_size: bool = false
@export var text_custom_size: int = 15
@export var text_use_global_color: bool = false
@export var text_custom_color: Color = Color(0.94, 0.91, 0.86, 1.0)

@export_group("Name Label")
@export var name_label_color_mode: int = 1  # 0=GLOBAL, 1=CUSTOM, 2=CHARACTER
@export var name_label_custom_color: Color = Color(0.82, 0.66, 0.40, 1.0)
@export var name_label_use_global_size: bool = false
@export var name_label_custom_size: int = 17

@export_group("Portrait")
@export var portrait_size: Vector2 = Vector2(150, 165)
@export var portrait_show: bool = true
## Số pixel portrait chồng vào bên trong panel tính từ mép trái.
@export var portrait_overlap: int = 132
## Số pixel portrait nhô lên trên mép trên của panel.
@export var portrait_lift: int = 44

const VIETNAMESE_FONT_PATH := "res://addons/dialogic/Example Assets/Fonts/Roboto-Regular.ttf"


## Dialogic gọi hàm này trước khi timeline bắt đầu chạy.
func _apply_export_overrides() -> void:
	if !is_inside_tree():
		await ready

	# Dialogic lấy character bằng identifier (tên file). Timeline hiện dùng "Hyou"
	# trong khi file là hyou.dch, vì vậy tạo alias theo display_name trước khi chạy timeline.
	if not Engine.is_editor_hint():
		_ensure_dch_directory()

	# ── Resize / đặt panel ───────────────────────────────────────────────
	var panel: PanelContainer = %DialogTextPanel
	panel.custom_minimum_size = box_size
	panel.position = Vector2(-box_size.x * 0.5, -box_size.y - box_distance)
	panel.size = box_size

	if box_panel and ResourceLoader.exists(box_panel):
		panel.add_theme_stylebox_override(&"panel", load(box_panel))

	# ── Portrait nằm ngoài khung ─────────────────────────────────────────
	# Canh portrait theo mép trái + đáy của panel để box đổi kích thước vẫn giữ bố cục.
	var portrait: TextureRect = %SpeakerPortrait
	portrait.visible = portrait_show
	portrait.size = portrait_size
	portrait.position = Vector2(
		panel.position.x - portrait_size.x + portrait_overlap,
		panel.position.y - portrait_lift
	)

	# Chừa text khỏi vùng portrait đè lên panel.
	var content_margin: MarginContainer = panel.get_node("ContentMargin")
	content_margin.add_theme_constant_override(&"margin_left", portrait_overlap + 26)

	# Nameplate bắt đầu ngay sau portrait để tên và mặt nhân vật đọc thành một cụm.
	var name_plate: PanelContainer = %NamePlate
	name_plate.position = Vector2(panel.position.x + portrait_overlap + 8, panel.position.y - 13)
	name_plate.size = Vector2(122, 33)

	# ── Dialog text ─────────────────────────────────────────────────────
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

	# ── Name label ──────────────────────────────────────────────────────
	var name_label: DialogicNode_NameLabel = %DialogicNode_NameLabel
	if vietnamese_font:
		name_label.add_theme_font_override(&"font", vietnamese_font)
	name_label.add_theme_font_size_override(&"font_size", name_label_custom_size)
	match name_label_color_mode:
		0:  # GLOBAL
			name_label.add_theme_color_override(&"font_color",
				get_global_setting(&'font_color', name_label_custom_color) as Color)
		1:  # CUSTOM
			name_label.use_character_color = false
			name_label.add_theme_color_override(&"font_color", name_label_custom_color)
		2:  # CHARACTER
			name_label.use_character_color = true

	# ── Connect speaker signal ───────────────────────────────────────────
	if not Engine.is_editor_hint():
		_connect_speaker_signal()


## Đảm bảo Dialogic biết character bằng cả identifier file lẫn display_name.
## Ví dụ: file "hyou.dch" có identifier "hyou", timeline lại ghi "Hyou".
## Không load resource ở bước này để tránh phụ thuộc cache ResourceLoader khi directory vừa dựng.
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


## Hỗ trợ cả định dạng .dch kiểu dictionary hiện tại và .tres cũ.
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

	# Nếu layer được tạo khi speaker đã tồn tại thì đồng bộ ngay.
	var current_speaker: String = Dialogic.current_state_info.get("speaker", "")
	if not current_speaker.is_empty():
		var char_res: DialogicCharacter = DialogicResourceUtil.get_character_resource(current_speaker)
		if char_res:
			_on_speaker_changed(char_res)


## Dialogic đổi speaker -> đổi portrait neutral / emotion tương ứng.
func _on_speaker_changed(character: DialogicCharacter) -> void:
	var portrait_rect: TextureRect = %SpeakerPortrait
	var name_plate: Control = %NamePlate

	if character == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		name_plate.visible = false
		return

	name_plate.visible = true
	var tex: Texture2D = _get_character_portrait_texture(character)

	if tex == null:
		portrait_rect.texture = null
		portrait_rect.visible = false
		return

	portrait_rect.texture = tex
	portrait_rect.visible = portrait_show


## Lấy portrait theo default_portrait. Hyou hiện có neutral -> icon.png.
## Hàm này cũng chấp nhận cấu trúc export_overrides của Dialogic để sau này
## thêm annoyed/thinking/soft... không phải sửa UI nữa.
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

	# Fallback cho character cũ chưa khai báo portraits đúng format.
	var fallback_path := _find_image_in_dch(character.display_name)
	if not fallback_path.is_empty() and ResourceLoader.exists(fallback_path):
		return load(fallback_path) as Texture2D
	return null


## Fallback đọc raw .dch, hỗ trợ cả key "image" lẫn ext_resource Texture2D.
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
