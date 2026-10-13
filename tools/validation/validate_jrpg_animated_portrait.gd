extends SceneTree
## Run: godot --headless --path . --script tools/validation/validate_jrpg_animated_portrait.gd
## Exercises the real JRPG speaker texture, rather than the isolated rig scene.

const TEXTBOX_SCENE := "res://scenes/ui/dialog/jrpg_textbox.tscn"
const CHARACTER_PATH := "res://data/dialogic_characters/hyou.dch"
const ORIGINAL_IMAGE := "res://assets/graphics/characters/hyou/neutral_dial.png"
const VIEWPORTS := [Vector2i(1280, 720), Vector2i(1600, 900)]

class ReducedMotionSettings extends Node:
	var enabled := true

	func IsReducedMotionEnabled() -> bool:
		return enabled

var _failures: Array[String] = []


func _initialize() -> void:
	_run.call_deferred()


func _check(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
		push_error(message)


func _character() -> DialogicCharacter:
	var character := DialogicCharacter.new()
	var data: Dictionary = str_to_var(FileAccess.get_file_as_string(CHARACTER_PATH))
	for key in data:
		if not str(key).begins_with("@"):
			character.set(key, data[key])
	return character


func _source_texture(portrait: TextureRect) -> Texture2D:
	return portrait.texture.atlas if portrait.texture is AtlasTexture else portrait.texture


func _run() -> void:
	await process_frame
	for viewport_size: Vector2i in VIEWPORTS:
		await _exercise_viewport(viewport_size)
	await _exercise_reduced_motion()
	if _failures.is_empty():
		print("JRPG animated portrait validation passed: dynamic speaker texture, 34 live layers, continuous idle, unchanged crop/layout at 1280/1600, mirroring, overrides, lifecycle, hidden pause and reduced motion.")
	quit(0 if _failures.is_empty() else 1)


func _textbox(viewport_size: Vector2i) -> Node:
	root.size = viewport_size
	await process_frame
	var textbox: Node = load(TEXTBOX_SCENE).instantiate()
	textbox.set("enable_entry_animation", false)
	root.add_child(textbox)
	DialogicUtil.apply_scene_export_overrides(textbox, {})
	DialogicUtil.autoload().Text.show_textbox(true)
	await process_frame
	return textbox


func _exercise_viewport(viewport_size: Vector2i) -> void:
	var textbox: Node = await _textbox(viewport_size)
	var character := _character()
	var portrait: TextureRect = textbox.get_node("Anchor/AnimationParent/SpeakerPortrait")
	var panel: PanelContainer = textbox.get_node("Anchor/AnimationParent/DialogTextPanel")
	textbox.call("_on_speaker_changed", character)
	await process_frame
	var suffix := " at %s" % viewport_size
	_check(_source_texture(portrait) is ViewportTexture, "JRPG speaker still uses a static texture instead of the custom portrait scene" + suffix)
	var viewports := textbox.find_children("*", "SubViewport", true, false)
	_check(viewports.size() == 1, "Speaker must own exactly one isolated portrait viewport" + suffix)
	if viewports.size() != 1:
		textbox.free()
		return
	var viewport := viewports[0] as SubViewport
	var idle: Node = viewport.get_child(0)
	_check(idle is DialogicPortrait, "Viewport must render a DialogicPortrait scene" + suffix)
	if not idle is DialogicPortrait:
		textbox.free()
		return
	_check(viewport.size == Vector2i(1024, 1536), "Custom source canvas changed the original portrait dimensions" + suffix)
	_check(viewport.transparent_bg and viewport.disable_3d, "Portrait viewport must isolate transparent 2D artwork" + suffix)
	_check((idle as Node2D).position == Vector2(512, 1536), "Covered rectangle was not translated into viewport coordinates" + suffix)
	_check(idle.find_children("*", "Sprite2D", true, false).size() == 34, "JRPG speaker did not render all 34 live rig layers" + suffix)
	_check(portrait.texture is AtlasTexture and (portrait.texture as AtlasTexture).region == Rect2(0, 0, 1024, 1213), "Animated portrait changed the original bust crop" + suffix)
	var original_texture: Texture2D = load(ORIGINAL_IMAGE)
	var dynamic_size := portrait.size
	var dynamic_position := portrait.position
	var dynamic_margin: int = panel.get_node("ContentMargin").get_theme_constant("margin_right")
	portrait.texture = textbox.call("_make_bust_texture", original_texture)
	textbox.call("_update_portrait_and_text_area", panel, clampf(float(viewport_size.y) / 720.0, 0.62, 1.6))
	_check(portrait.size == dynamic_size and portrait.position == dynamic_position, "Animated portrait changed the existing bust geometry" + suffix)
	_check(panel.get_node("ContentMargin").get_theme_constant("margin_right") == dynamic_margin, "Animated portrait changed the text lane width" + suffix)
	textbox.call("_show_character_portraits", character, "neutral")
	var body: Node2D = idle.call("get_joint", "body")
	var before := body.transform
	await create_timer(0.15).timeout
	_check(not body.transform.is_equal_approx(before), "The custom speaker rig did not animate while visible" + suffix)
	_check(_source_texture(portrait) == viewport.get_texture(), "Speaker texture stopped referencing the live render target" + suffix)
	var continued_pose := body.transform
	var elapsed: float = idle.get("_elapsed")
	textbox.call("_on_speaker_changed", character)
	_check(viewport == textbox.find_children("*", "SubViewport", true, false)[0], "Same speaker recreated its viewport" + suffix)
	_check(body.transform.is_equal_approx(continued_pose) and is_equal_approx(float(idle.get("_elapsed")), elapsed), "Repeated neutral line reset the idle phase" + suffix)
	character.mirror = true
	textbox.call("_show_character_portraits", character, "neutral")
	_check((idle.get_node("MirrorRoot") as Node2D).scale.x == -1.0, "Custom speaker did not mirror the whole rig" + suffix)
	character.mirror = false
	textbox.call("_show_character_portraits", character, "neutral")
	textbox.set("portrait_min_screen_width", 2000.0)
	textbox.call("_update_portrait_and_text_area", panel, 1.0)
	_check(not portrait.visible and viewport.render_target_update_mode == SubViewport.UPDATE_DISABLED, "Hidden narrow-screen portrait kept rendering" + suffix)
	var paused_pose := body.transform
	await create_timer(0.1).timeout
	_check(body.transform.is_equal_approx(paused_pose), "Hidden portrait kept processing its idle" + suffix)
	textbox.set("portrait_min_screen_width", 980.0)
	textbox.call("_update_portrait_and_text_area", panel, clampf(float(viewport_size.y) / 720.0, 0.62, 1.6))
	_check(portrait.visible and viewport.render_target_update_mode == SubViewport.UPDATE_ALWAYS, "Visible portrait did not resume rendering" + suffix)
	var static_character := DialogicCharacter.new()
	static_character.display_name = "Static validation portrait"
	static_character.default_portrait = "neutral"
	static_character.portraits = {"neutral": {"image": ORIGINAL_IMAGE, "scene": "res://addons/dialogic/Modules/Character/default_portrait.tscn"}}
	textbox.call("_show_character_portraits", static_character, "neutral")
	await process_frame
	_check(not is_instance_valid(viewport), "Static speaker did not free the custom viewport" + suffix)
	_check(_source_texture(portrait) == original_texture, "Standard portrait stopped using its original image" + suffix)
	character.portraits["neutral"]["export_overrides"] = {"intensity": "0.6", "not_a_portrait_property": 42}
	textbox.call("_show_character_portraits", character, "neutral")
	viewports = textbox.find_children("*", "SubViewport", true, false)
	var new_viewport := viewports[0] as SubViewport
	var new_idle := new_viewport.get_child(0)
	_check(is_equal_approx(float(new_idle.get("intensity")), 0.6), "Valid custom portrait export override was ignored" + suffix)
	textbox.call("_hide_character_portraits")
	await process_frame
	_check(not is_instance_valid(new_viewport) and portrait.texture == null, "Hiding the speaker leaked its viewport or texture" + suffix)
	textbox.free()
	await process_frame


func _exercise_reduced_motion() -> void:
	var settings := ReducedMotionSettings.new()
	settings.name = "SettingsManager"
	root.add_child(settings)
	var textbox: Node = await _textbox(Vector2i(1280, 720))
	var character := _character()
	textbox.call("_show_character_portraits", character, "neutral")
	var viewports := textbox.find_children("*", "SubViewport", true, false)
	if viewports.is_empty():
		_check(false, "Reduced-motion speaker failed to load its custom viewport")
	else:
		var idle := viewports[0].get_child(0)
		_check(not bool(idle.get("motion_enabled")), "Reduced-motion setting did not disable custom idle")
		var body: Node2D = idle.call("get_joint", "body")
		var neutral := body.transform
		await create_timer(0.15).timeout
		_check(body.transform.is_equal_approx(neutral), "Reduced-motion portrait did not stay at neutral")
		settings.enabled = false
		await create_timer(0.15).timeout
		_check(bool(idle.get("motion_enabled")) and not body.transform.is_equal_approx(neutral), "Changing reduced motion off did not resume the visible idle")
		settings.enabled = true
		await process_frame
		await process_frame
		_check(not bool(idle.get("motion_enabled")) and body.transform.is_equal_approx(neutral), "Changing reduced motion on did not restore the neutral portrait")
	textbox.free()
	settings.free()
	await process_frame
