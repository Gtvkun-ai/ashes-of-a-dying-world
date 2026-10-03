extends Node

const TEXTBOX_SCENE := "res://scenes/ui/dialog/jrpg_textbox.tscn"
const CHOICE_SCENE := "res://scenes/ui/dialog/jrpg_choice_layer.tscn"
const BASE_SCENE := "res://addons/dialogic/Modules/DefaultLayoutParts/Base_Default/default_layout_base.tscn"
const LONG_CHOICES := [
	"Sáu lựa chọn tiếng Việt rất dài cần tự xuống dòng mà không bị cắt mất nội dung.",
	"Tiếp tục lắng nghe những ký ức còn sót lại giữa cánh đồng đang hồi sinh.",
	"Hỏi Hyou về lời hứa cũ và nguyên nhân cô ấy vẫn ở lại nơi này.",
	"Quan sát dấu vết băng mỏng trước khi quyết định con đường an toàn hơn.",
	"Tạm rời cuộc trò chuyện để kiểm tra hành trang và trạng thái của cả đội.",
	"Giữ im lặng, chấp nhận rằng đôi khi hy vọng không cần được nói thành lời."
]


func _ready() -> void:
	var window := get_window()
	if window != null:
		window.size = Vector2i(1280, 720)

	var layout: DialogicLayoutBase = load(BASE_SCENE).instantiate()
	add_child(layout)

	var textbox: Node = load(TEXTBOX_SCENE).instantiate()
	layout.add_layer(textbox)
	if not _require_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox"):
		return
	if not _require_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox/DialogicNode_DialogText"):
		return
	if not _require_node(textbox, "Anchor/AnimationParent/NamePlate/DialogicNode_NameLabel"):
		return
	DialogicUtil.apply_scene_export_overrides(textbox, {})

	var choices: Node = load(CHOICE_SCENE).instantiate()
	layout.add_layer(choices)
	if not _require_node(choices, "ChoiceScroll/Choices"):
		return
	DialogicUtil.apply_scene_export_overrides(choices, {})
	await get_tree().process_frame

	var choice_scroll := choices.get_node_or_null("ChoiceScroll") as ScrollContainer
	var choices_node := choices.get_node_or_null("ChoiceScroll/Choices") as VBoxContainer
	if not _require(choice_scroll != null, "Choice ScrollContainer not found after instantiate"):
		return
	if not _require(choices_node != null, "Choices VBoxContainer not found after instantiate"):
		return

	var buttons: Array[Button] = []
	for child in choices_node.get_children():
		if child is Button:
			buttons.append(child)
	if not _require(buttons.size() >= LONG_CHOICES.size(), "Dialogic did not create at least six choice buttons"):
		return

	for index in LONG_CHOICES.size():
		var button := buttons[index]
		button.text = LONG_CHOICES[index]
		button.visible = true
		button.custom_minimum_size.y = 64.0

	choices.call("_style_buttons")
	choices.call("_apply_responsive_layout")
	await get_tree().process_frame
	await get_tree().process_frame

	if not _require(choice_scroll.size.x > 280.0, "Responsive choice width did not exceed the legacy 280px cap"):
		return
	if not _require(choice_scroll.size.x <= 520.0, "Responsive choice width exceeded its 520px bound"):
		return
	if not _require(choice_scroll.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_AUTO, "Choice overflow is not set to auto-scroll"):
		return
	if not _require(choice_scroll.get_v_scroll_bar().max_value > choice_scroll.get_v_scroll_bar().page, "Six long choices did not produce scrollable overflow"):
		return

	for index in LONG_CHOICES.size():
		var button := buttons[index]
		if not _require(button.autowrap_mode == TextServer.AUTOWRAP_WORD_SMART, "Choice autowrap is not enabled"):
			return
		if not _require(button.text_overrun_behavior == TextServer.OVERRUN_NO_TRIMMING, "Choice text still trims overflow"):
			return
		if not _require(not button.clip_text, "Choice text is still clipped"):
			return
		if not _require(button.focus_mode == Control.FOCUS_ALL, "Choice is not keyboard/gamepad focusable"):
			return

	layout.queue_free()
	await get_tree().process_frame
	print("Dialogic layout smoke test passed")
	get_tree().quit(0)


func _require_node(root_node: Node, path: String) -> bool:
	return _require(
		root_node.get_node_or_null(path) != null,
		"Missing Dialogic layout node: %s" % path
	)


func _require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	get_tree().quit(1)
	return false
