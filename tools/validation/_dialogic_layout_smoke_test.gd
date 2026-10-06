extends Node

const TEXTBOX_SCENE := "res://scenes/ui/dialog/jrpg_textbox.tscn"
const CHOICE_SCENE := "res://scenes/ui/dialog/jrpg_choice_layer.tscn"
const BASE_SCENE := "res://addons/dialogic/Modules/DefaultLayoutParts/Base_Default/default_layout_base.tscn"
const REDUCED_MOTION_SETTINGS_FIXTURE := preload("res://tools/validation/DialogicReducedMotionSettingsFixture.cs")
const VIEWPORTS := [Vector2i(1280, 720), Vector2i(1600, 900)]
const LONG_CHOICES := [
	"Sáu lựa chọn tiếng Việt rất dài cần tự xuống dòng mà không bị cắt mất nội dung.",
	"Tiếp tục lắng nghe những ký ức còn sót lại giữa cánh đồng đang hồi sinh.",
	"Hỏi Hyou về lời hứa cũ và nguyên nhân cô ấy vẫn ở lại nơi này.",
	"Quan sát dấu vết băng mỏng trước khi quyết định con đường an toàn hơn.",
	"Tạm rời cuộc trò chuyện để kiểm tra hành trang và trạng thái của cả đội.",
	"Giữ im lặng, chấp nhận rằng đôi khi hy vọng không cần được nói thành lời."
]


func _ready() -> void:
	if not await _verify_reduced_motion_layout():
		get_tree().quit(1)
		return

	for viewport_size in VIEWPORTS:
		var passed: bool = await _exercise_viewport(viewport_size)
		if not passed:
			get_tree().quit(1)
			return

	print("Dialogic layout smoke test passed")
	get_tree().quit(0)


func _verify_reduced_motion_layout() -> bool:
	await get_tree().process_frame
	var settings_fixture := REDUCED_MOTION_SETTINGS_FIXTURE.new()
	settings_fixture.name = "SettingsManager"
	get_tree().root.add_child(settings_fixture)
	await get_tree().process_frame
	if not _require(
		settings_fixture.has_method(&"IsReducedMotionEnabled")
		and bool(settings_fixture.call(&"IsReducedMotionEnabled")),
		"Reduced-motion settings fixture did not expose an enabled SettingsManager query"
	):
		return false

	var textbox: Node = load(TEXTBOX_SCENE).instantiate()
	add_child(textbox)
	DialogicUtil.apply_scene_export_overrides(textbox, {})
	await get_tree().process_frame

	var dim := textbox.get_node_or_null("DimBackground") as ColorRect
	var panel := textbox.get_node_or_null("Anchor/AnimationParent/DialogTextPanel") as PanelContainer
	var name_plate := textbox.get_node_or_null("Anchor/AnimationParent/NamePlate") as PanelContainer
	var portrait := textbox.get_node_or_null("Anchor/AnimationParent/SpeakerPortrait") as TextureRect
	var next_indicator := textbox.get_node_or_null("Anchor/AnimationParent/NextIndicator") as Control
	if not _require(dim != null and panel != null and name_plate != null and portrait != null and next_indicator != null, "Reduced-motion dialog nodes are missing"):
		return false
	if not _require(dim.modulate == Color.WHITE, "Reduced motion did not snap the dialog dimmer to its final state"):
		return false
	if not _require(panel.modulate == Color.WHITE and name_plate.modulate == Color.WHITE, "Reduced motion did not snap the dialog chrome to its final state"):
		return false
	textbox.call("_animate_portrait_in", portrait)
	if not _require(portrait.modulate == Color.WHITE, "Reduced motion did not leave the portrait at its final opacity"):
		return false
	if not _require(next_indicator.get("animation") == 2, "Reduced motion did not disable the next-indicator blink"):
		return false

	textbox.queue_free()
	settings_fixture.queue_free()
	await get_tree().process_frame
	return true


func _exercise_viewport(viewport_size: Vector2i) -> bool:
	var window := get_window()
	if window != null:
		window.size = viewport_size
	await get_tree().process_frame

	var layout: DialogicLayoutBase = load(BASE_SCENE).instantiate()
	add_child(layout)

	var textbox: Node = load(TEXTBOX_SCENE).instantiate()
	layout.add_layer(textbox)
	if not _require_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox"):
		return false
	if not _require_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox/DialogicNode_DialogText"):
		return false
	if not _require_node(textbox, "Anchor/AnimationParent/NamePlate/DialogicNode_NameLabel"):
		return false
	DialogicUtil.apply_scene_export_overrides(textbox, {})

	var choices: Node = load(CHOICE_SCENE).instantiate()
	layout.add_layer(choices)
	if not _require_node(choices, "ChoiceScroll/Choices"):
		return false
	DialogicUtil.apply_scene_export_overrides(choices, {})
	await get_tree().process_frame

	var choice_scroll := choices.get_node_or_null("ChoiceScroll") as ScrollContainer
	var choices_node := choices.get_node_or_null("ChoiceScroll/Choices") as VBoxContainer
	if not _require(choice_scroll != null, "Choice ScrollContainer not found after instantiate"):
		return false
	if not _require(choices_node != null, "Choices VBoxContainer not found after instantiate"):
		return false

	var buttons: Array[Button] = []
	for child in choices_node.get_children():
		if child is Button:
			buttons.append(child)
	if not _require(buttons.size() >= LONG_CHOICES.size(), "Dialogic did not create at least six choice buttons"):
		return false

	if not await _verify_long_choices(choices, choice_scroll, buttons, viewport_size):
		return false
	if not await _verify_single_choice(choices, choice_scroll, buttons, viewport_size):
		return false

	layout.queue_free()
	await get_tree().process_frame
	return true


func _verify_long_choices(
	choices: Node,
	choice_scroll: ScrollContainer,
	buttons: Array[Button],
	viewport_size: Vector2i
) -> bool:
	for button in buttons:
		button.visible = false
	for index in LONG_CHOICES.size():
		var button := buttons[index]
		button.text = LONG_CHOICES[index]
		button.visible = true

	choices.call("_style_buttons")
	choices.call("_apply_responsive_layout")
	await get_tree().process_frame
	await get_tree().process_frame

	var suffix := " at %dx%d" % [viewport_size.x, viewport_size.y]
	if not _require(choice_scroll.size.x > 280.0, "Responsive choice width did not exceed the legacy 280px cap" + suffix):
		return false
	if not _require(choice_scroll.size.x <= 520.0, "Responsive choice width exceeded its 520px bound" + suffix):
		return false
	if not _require(choice_scroll.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_AUTO, "Choice overflow is not set to auto-scroll" + suffix):
		return false
	if not _require(choice_scroll.get_v_scroll_bar().max_value > choice_scroll.get_v_scroll_bar().page, "Six long choices did not produce scrollable overflow" + suffix):
		return false

	for index in LONG_CHOICES.size():
		var button := buttons[index]
		if not _require(button.autowrap_mode == TextServer.AUTOWRAP_WORD_SMART, "Choice autowrap is not enabled" + suffix):
			return false
		if not _require(button.text_overrun_behavior == TextServer.OVERRUN_NO_TRIMMING, "Choice text still trims overflow" + suffix):
			return false
		if not _require(not button.clip_text, "Choice text is still clipped" + suffix):
			return false
		if not _require(button.focus_mode == Control.FOCUS_ALL, "Choice is not keyboard/gamepad focusable" + suffix):
			return false

	var last_button := buttons[LONG_CHOICES.size() - 1]
	last_button.grab_focus()
	await get_tree().process_frame
	await get_tree().process_frame
	if not _require(choice_scroll.scroll_vertical > 0, "Focused final choice did not scroll into view" + suffix):
		return false
	if not _require(_is_fully_visible(last_button, choice_scroll), "Focused final choice is clipped by its scroller" + suffix):
		return false
	return true


func _verify_single_choice(
	choices: Node,
	choice_scroll: ScrollContainer,
	buttons: Array[Button],
	viewport_size: Vector2i
) -> bool:
	for button in buttons:
		button.visible = false
	var first_button := buttons[0]
	first_button.text = "Ở lại và lắng nghe."
	first_button.visible = true
	choice_scroll.scroll_vertical = 0

	choices.call("_style_buttons")
	choices.call("_apply_responsive_layout")
	await get_tree().process_frame
	await get_tree().process_frame

	var scroll_bottom := choice_scroll.global_position.y + choice_scroll.size.y
	var button_bottom := first_button.global_position.y + first_button.size.y
	var bottom_gap := absf(scroll_bottom - button_bottom)
	var suffix := " at %dx%d" % [viewport_size.x, viewport_size.y]
	if not _require(bottom_gap <= 4.0, "Single choice is not aligned near the textbox" + suffix):
		return false
	if not _require(_is_fully_visible(first_button, choice_scroll), "Single choice is clipped by its scroller" + suffix):
		return false
	return true


func _is_fully_visible(control: Control, scroll: ScrollContainer) -> bool:
	var control_top := control.global_position.y
	var control_bottom := control_top + control.size.y
	var viewport_top := scroll.global_position.y
	var viewport_bottom := viewport_top + scroll.size.y
	return control_top >= viewport_top - 1.0 and control_bottom <= viewport_bottom + 1.0


func _require_node(root_node: Node, path: String) -> bool:
	return _require(
		root_node.get_node_or_null(path) != null,
		"Missing Dialogic layout node: %s" % path
	)


func _require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	return false
