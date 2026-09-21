extends Node

const TEXTBOX_SCENE := "res://scenes/ui/dialog/jrpg_textbox.tscn"
const CHOICE_SCENE := "res://scenes/ui/dialog/jrpg_choice_layer.tscn"
const BASE_SCENE := "res://addons/dialogic/Modules/DefaultLayoutParts/Base_Default/default_layout_base.tscn"

func _ready() -> void:
	var layout: DialogicLayoutBase = load(BASE_SCENE).instantiate()
	add_child(layout)

	var textbox: Node = load(TEXTBOX_SCENE).instantiate()
	layout.add_layer(textbox)
	print(textbox.get_tree_string_pretty())
	_assert_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox")
	_assert_node(textbox, "Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox/DialogicNode_DialogText")
	_assert_node(textbox, "Anchor/AnimationParent/NamePlate/DialogicNode_NameLabel")
	DialogicUtil.apply_scene_export_overrides(textbox, {})

	var choices: Node = load(CHOICE_SCENE).instantiate()
	layout.add_layer(choices)
	_assert_node(choices, "Choices")
	DialogicUtil.apply_scene_export_overrides(choices, {})
	# Choices node tồn tại và có ít nhất 1 child (DialogicNode_ButtonSound).
	# Buttons thực sự được tạo khi timeline chạy, không phải lúc instantiate.
	var choices_node: Node = choices.get_node_or_null("Choices")
	if choices_node == null:
		push_error("Choices VBoxContainer not found after instantiate")
		get_tree().quit(1)
	elif choices_node.get_child_count() < 1:
		push_error("Choices has no children at all (expected at least DialogicNode_ButtonSound)")
		get_tree().quit(1)

	print("Dialogic layout smoke test passed")
	get_tree().quit(0)

func _assert_node(root_node: Node, path: String) -> void:
	if root_node.get_node_or_null(path) == null:
		push_error("Missing Dialogic layout node: %s" % path)
		get_tree().quit(1)
