extends SceneTree
## Run: godot --headless --path . --script tools/validation/validate_hyou_dialogue_idle.gd

const SCENE_PATH := "res://scenes/ui/dialog/hyou_rig/hyou_dialogue_idle.tscn"
const MANIFEST_PATH := "res://assets/graphics/characters/hyou/neutral_dial_rig_v1/dialogue_idle/rig_manifest.json"
const CROP_MANIFEST_PATH := "res://assets/graphics/characters/hyou/neutral_dial_rig_v1/dialogue_idle/crop_manifest.json"
const CANVAS_OFFSET := Vector2(-512, -1536)
const CANVAS_SIZE := Vector2(1024, 1536)

var _failures: Array[String] = []


func _initialize() -> void:
	_run.call_deferred()


func _check(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
		push_error(message)


func _run() -> void:
	if not FileAccess.file_exists(SCENE_PATH):
		push_error("Hyou Dialogic idle scene is missing.")
		quit(1)
		return
	var packed_scene := load(SCENE_PATH) as PackedScene
	if packed_scene == null:
		push_error("Hyou Dialogic idle scene cannot load.")
		quit(1)
		return
	var idle := packed_scene.instantiate()
	idle.set("motion_enabled", false)
	root.add_child(idle)
	await process_frame
	_check(idle is Node2D, "Portrait root must retain Node2D for Dialogic transforms.")
	_check(idle is DialogicPortrait, "Portrait must implement the DialogicPortrait contract.")
	if not idle is DialogicPortrait:
		idle.free()
		quit(1)
		return
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	var crop_manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(CROP_MANIFEST_PATH))
	var crops: Dictionary = {}
	for crop: Dictionary in crop_manifest["layers"]:
		crops[crop["name"]] = crop
	var texture_pixels := 0
	var sprites: Array[Node] = idle.find_children("*", "Sprite2D", true, false)
	_check(sprites.size() == 34, "Expected all 34 original PNG layers, got %d." % sprites.size())
	idle.call("set_idle_time", 0.0)
	var neutral_transforms: Dictionary = {}
	for layer: Dictionary in manifest["layers"]:
		var joint: Node2D = idle.call("get_joint", layer["name"])
		var sprite: Sprite2D = idle.call("get_layer_sprite", layer["name"])
		_check(joint != null and sprite != null, "Missing layer %s." % layer["name"])
		if joint == null or sprite == null:
			continue
		var pivot := Vector2(layer["pivot"][0], layer["pivot"][1])
		var crop: Dictionary = crops[layer["name"]]
		var offset := Vector2(crop["offset"][0], crop["offset"][1])
		var cropped_size := Vector2(crop["size"][0], crop["size"][1])
		_check(joint.global_position.is_equal_approx(CANVAS_OFFSET + pivot), "Incorrect source pivot: %s." % layer["name"])
		_check(sprite.global_position.is_equal_approx(CANVAS_OFFSET + offset), "Cropped PNG top-left is not source aligned: %s." % layer["name"])
		_check((sprite.global_transform * (CANVAS_SIZE / 2.0 - offset)).is_equal_approx(Vector2(0, -768)), "Source canvas center drift: %s." % layer["name"])
		_check(sprite.texture != null and sprite.texture.get_size() == cropped_size, "Runtime texture was not cropped: %s." % layer["name"])
		texture_pixels += int(sprite.texture.get_width()) * int(sprite.texture.get_height())
		_check(not sprite.centered and not sprite.z_as_relative and sprite.z_index == layer["z_index"], "Incorrect draw order or texture origin: %s." % layer["name"])
		_check(sprite.texture_filter == CanvasItem.TEXTURE_FILTER_NEAREST, "Layer must use nearest filtering: %s." % layer["name"])
		_check(joint.get_parent() == idle.call("get_joint", layer["parent"]), "Metadata hierarchy changed: %s." % layer["name"])
		neutral_transforms[layer["name"]] = sprite.global_transform
	_check(texture_pixels < 5000000, "Idle artwork should use under 20 MiB RGBA, got %d pixels." % texture_pixels)
	_check(idle.call("_get_covered_rect") == Rect2(CANVAS_OFFSET, CANVAS_SIZE), "Dialogic sizing must match the static portrait canvas.")
	_check(idle.call("get_motion_bounds") == Rect2(CANVAS_OFFSET, CANVAS_SIZE).grow(64), "Motion bounds must reserve a 64px margin.")
	var body: Node2D = idle.call("get_joint", "body")
	var coherent_names := ["head_face", "head_scalp", "headband", "head_crystal_screenleft", "head_crystal_screenright", "eyebrow_screenleft", "eyebrow_screenright", "eye_open_screenleft", "eye_iris_screenleft", "eye_lash_screenleft", "eye_open_screenright", "eye_iris_screenright", "eye_lash_screenright", "mouth_neutral", "leg_back_thigh", "leg_back_calf", "leg_front_thigh", "leg_front_calf", "boot_back", "boot_front"]
	var body_relative: Dictionary = {}
	for layer_name: String in coherent_names:
		var sprite: Sprite2D = idle.call("get_layer_sprite", layer_name)
		body_relative[layer_name] = body.global_transform.affine_inverse() * sprite.global_transform
	for sample_time: float in [0.96, 1.92, 2.88, 3.84, 3.84 * 1024.0]:
		idle.call("set_idle_time", sample_time)
		for layer_name: String in coherent_names:
			var sprite: Sprite2D = idle.call("get_layer_sprite", layer_name)
			_check((body.global_transform.affine_inverse() * sprite.global_transform).is_equal_approx(body_relative[layer_name]), "Face/legs/boots detached from body: %s at %f." % [layer_name, sample_time])
		if sample_time >= 3.84:
			for layer_name: String in neutral_transforms:
				var sprite: Sprite2D = idle.call("get_layer_sprite", layer_name)
				_check(sprite.global_transform.is_equal_approx(neutral_transforms[layer_name]), "Loop does not return to neutral: %s at %f." % [layer_name, sample_time])
	idle.call("set_idle_time", 0.96)
	_check(body.position.is_equal_approx(Vector2(7, -3)), "Quarter-cycle body must move 7px horizontally and -3px vertically.")
	_check(is_equal_approx(body.rotation, deg_to_rad(0.35)), "Quarter-cycle body rotation must be 0.35 degrees.")
	_check(is_equal_approx((idle.call("get_joint", "hair_back_screenleft") as Node2D).rotation, deg_to_rad(1.8)), "Rear hair sway must reach 1.8 degrees.")
	idle.call("set_idle_time", 0.4)
	var unmirrored: Dictionary = {}
	for layer_name: String in neutral_transforms:
		unmirrored[layer_name] = (idle.call("get_layer_sprite", layer_name) as Sprite2D).global_position
	idle.call("_set_mirror", true)
	for layer_name: String in neutral_transforms:
		var sprite: Sprite2D = idle.call("get_layer_sprite", layer_name)
		_check(sprite.global_position.is_equal_approx(unmirrored[layer_name] * Vector2(-1, 1)), "Mirroring must reflect complete rig: %s." % layer_name)
	_check(idle.call("_get_covered_rect") == Rect2(CANVAS_OFFSET, CANVAS_SIZE), "Mirrored covered rectangle must remain bottom centered.")
	idle.call("_set_mirror", false)
	idle.set("intensity", 0.0)
	idle.call("set_idle_time", 0.96)
	for layer_name: String in neutral_transforms:
		_check((idle.call("get_layer_sprite", layer_name) as Sprite2D).global_transform.is_equal_approx(neutral_transforms[layer_name]), "Zero intensity must restore neutral: %s." % layer_name)
	idle.set("intensity", 1.0)
	idle.set("motion_enabled", true)
	idle.call("set_idle_time", 1.3)
	idle.set("motion_enabled", false)
	for layer_name: String in neutral_transforms:
		_check((idle.call("get_layer_sprite", layer_name) as Sprite2D).global_transform.is_equal_approx(neutral_transforms[layer_name]), "Disabling motion must restore neutral: %s." % layer_name)
	var character_data: Dictionary = str_to_var(FileAccess.get_file_as_string("res://data/dialogic_characters/hyou.dch"))
	_check(character_data[&"portraits"]["neutral"]["scene"] == SCENE_PATH, "Hyou neutral portrait must use the idle scene.")
	_check(character_data[&"portraits"]["neutral"]["image"] == "res://assets/graphics/characters/hyou/neutral_dial.png", "Original image reference must be retained.")
	idle.free()
	if _failures.is_empty():
		print("Hyou dialogue idle validation passed: 34 cropped PNG layers, source alignment, hierarchy, seamless 3.84s loop, rigid face/legs, mirror, reset and Dialogic sizing. RGBA texture pixels=", texture_pixels)
	quit(0 if _failures.is_empty() else 1)
