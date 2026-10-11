extends Control


func _ready() -> void:
	var viewport_size := Vector2i(1600, 900)
	var capture_path := ""
	for argument in OS.get_cmdline_user_args():
		if argument == "--720p":
			viewport_size = Vector2i(1280, 720)
		if argument.begins_with("--capture="):
			capture_path = argument.trim_prefix("--capture=")
	get_window().size = viewport_size
	await get_tree().process_frame
	var world := TextureRect.new()
	world.texture = load("res://assets/graphics/world/whispering_fields/field_01.png")
	world.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	world.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	world.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	world.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(world)
	var layout = load("res://addons/dialogic/Modules/DefaultLayoutParts/Base_Default/default_layout_base.tscn").instantiate()
	add_child(layout)
	var style = load("res://scenes/ui/dialog/jrpg_style.tres")
	var textbox = load("res://scenes/ui/dialog/jrpg_textbox.tscn").instantiate()
	layout.add_layer(textbox)
	textbox.set_deferred("size", viewport_size)
	DialogicUtil.apply_scene_export_overrides(textbox, style.layer_info["10"].overrides)
	var character := DialogicCharacter.new()
	var character_data: Dictionary = str_to_var(FileAccess.get_file_as_string("res://data/dialogic_characters/hyou.dch"))
	for key in character_data:
		if not str(key).begins_with("@"):
			character.set(key, character_data[key])
	textbox.call("_show_character_portraits", character, "neutral")
	textbox.get_node("Anchor/AnimationParent/NamePlate/DialogicNode_NameLabel").text = "Hyou"
	var text: RichTextLabel = textbox.get_node("Anchor/AnimationParent/DialogTextPanel/ContentMargin/VBox/DialogicNode_DialogText")
	text.text = "Lâu lắm rồi ta không gặp một linh hồn còn sống."
	text.visible_characters = -1
	var choices = load("res://scenes/ui/dialog/jrpg_choice_layer.tscn").instantiate()
	layout.add_layer(choices)
	choices.set_deferred("size", viewport_size)
	DialogicUtil.apply_scene_export_overrides(choices, style.layer_info["13"].overrides)
	await get_tree().process_frame
	var buttons: Array[Button] = []
	for child in choices.get_node("ChoiceScroll/Choices").get_children():
		if child is Button:
			child.hide()
			buttons.append(child)
	buttons[0].text = "Ai ngươi vậy?"
	buttons[1].text = "Thôi ta phải đi."
	buttons[0].show()
	buttons[1].show()
	choices.call("_style_buttons")
	buttons[0].grab_focus()
	await get_tree().create_timer(0.5).timeout
	textbox.show()
	textbox.get_node("Anchor/AnimationParent").show()
	text.show()
	await get_tree().process_frame
	var ui_scale := float(viewport_size.y) / 720.0
	var panel: PanelContainer = textbox.get_node("Anchor/AnimationParent/DialogTextPanel")
	var name_plate: PanelContainer = textbox.get_node("Anchor/AnimationParent/NamePlate")
	var choice_scroll: ScrollContainer = choices.get_node("ChoiceScroll")
	var name_label: Label = name_plate.get_node("DialogicNode_NameLabel")
	print("Preview viewport=", get_viewport().get_visible_rect().size, " roots=", textbox.size, "/", choices.size, " panel=", panel.global_position, " choices=", choice_scroll.global_position)
	var valid := text.get_theme_font_size("normal_font_size") == roundi(22.0 * ui_scale) \
		and name_label.get_theme_font_size("font_size") == roundi(20.0 * ui_scale) \
		and buttons[0].get_theme_font_size("font_size") == roundi(19.0 * ui_scale) \
		and name_plate.size.y >= 48.0 * ui_scale \
		and choice_scroll.global_position.y + choice_scroll.size.y < name_plate.global_position.y \
		and panel.has_node("PixelFrame") and buttons[0].has_node("PixelFrame")
	if not valid:
		push_error("Dialogic reference geometry/typography mismatch at %s" % viewport_size)
		get_tree().quit(1)
		return
	if not capture_path.is_empty():
		await RenderingServer.frame_post_draw
		var result := get_viewport().get_texture().get_image().save_png(capture_path)
		if result != OK:
			get_tree().quit(1)
			return
	print("Dialogic reference preview passed at %s" % viewport_size)
	layout.queue_free()
	await get_tree().process_frame
	get_tree().quit(0)
