@tool
extends DialogicPortrait
## Dialogic portrait built from 34 cropped PNG layers with source-space pivots.
## The root stays at the same bottom-center anchor as the original portrait.

const RIG_DIRECTORY := "res://assets/graphics/characters/hyou/neutral_dial_rig_v1/dialogue_idle/"
const MANIFEST_PATH := RIG_DIRECTORY + "rig_manifest.json"
const CROP_MANIFEST_PATH := RIG_DIRECTORY + "crop_manifest.json"
const LOOP_SECONDS := 3.84
const MOTION_MARGIN := 64.0

@export_range(0.0, 1.25, 0.05) var intensity: float = 1.0:
	set(value):
		intensity = clampf(value, 0.0, 1.25)
		if is_instance_valid(_body):
			_apply_idle_pose(_elapsed)

@export var motion_enabled: bool = true:
	set(value):
		motion_enabled = value
		if not motion_enabled:
			reset_idle()
		if is_inside_tree():
			set_process(motion_enabled and not Engine.is_editor_hint())

var _canvas_size := Vector2(1024, 1536)
var _mirror_root: Node2D
var _body: Node2D
var _joints: Dictionary = {}
var _sprites: Dictionary = {}
var _source_pivots: Dictionary = {}
var _rest_positions: Dictionary = {}
var _elapsed := 0.0
var _mirrored := false


func _ready() -> void:
	_build_rig()
	reset_idle()
	set_process(motion_enabled and not Engine.is_editor_hint())


func _update_portrait(passed_character: DialogicCharacter, passed_portrait: String) -> void:
	apply_character_and_portrait(passed_character, passed_portrait)
	# Dialogic calls this before the new scene is added to the tree.
	_build_rig()
	reset_idle()


func _get_covered_rect() -> Rect2:
	# Dialogic uses the returned size to scale its portrait containers. Retain the
	# source canvas so switching from the static portrait cannot shrink Hyou.
	return Rect2(_canvas_size * Vector2(-0.5, -1.0), _canvas_size)


func get_motion_bounds() -> Rect2:
	# Separate padded bounds for previews/culling; motion never changes UI sizing.
	return _get_covered_rect().grow(MOTION_MARGIN)


func _set_mirror(is_mirrored: bool) -> void:
	_mirrored = is_mirrored
	if is_instance_valid(_mirror_root):
		_mirror_root.scale = Vector2(-1.0 if _mirrored else 1.0, 1.0)


func _process(delta: float) -> void:
	_elapsed = fposmod(_elapsed + delta, LOOP_SECONDS)
	_apply_idle_pose(_elapsed)


func reset_idle() -> void:
	_elapsed = 0.0
	if is_instance_valid(_body):
		_apply_idle_pose(0.0)


## Deterministic sampling also lets a paused preview inspect any point in the loop.
func set_idle_time(seconds: float) -> void:
	_elapsed = fposmod(seconds, LOOP_SECONDS)
	if is_instance_valid(_body):
		_apply_idle_pose(_elapsed)


func get_joint(layer_name: String) -> Node2D:
	return _joints.get(layer_name) as Node2D


func get_layer_sprite(layer_name: String) -> Sprite2D:
	return _sprites.get(layer_name) as Sprite2D


func _build_rig() -> void:
	if is_instance_valid(_mirror_root):
		return
	var manifest_value: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	if not manifest_value is Dictionary:
		push_error("Hyou dialogue idle: invalid rig manifest at " + MANIFEST_PATH)
		return
	var manifest: Dictionary = manifest_value
	var crop_value: Variant = JSON.parse_string(FileAccess.get_file_as_string(CROP_MANIFEST_PATH))
	if not crop_value is Dictionary:
		push_error("Hyou dialogue idle: invalid crop manifest at " + CROP_MANIFEST_PATH)
		return
	var crops: Dictionary = {}
	for crop: Dictionary in crop_value["layers"]:
		crops[crop["name"]] = crop
	_canvas_size = _vector(manifest["canvas"])
	_mirror_root = Node2D.new()
	_mirror_root.name = "MirrorRoot"
	_mirror_root.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(_mirror_root)
	var canvas := Node2D.new()
	canvas.name = "SourceCanvas"
	canvas.position = _get_covered_rect().position
	_mirror_root.add_child(canvas)

	# Create every pivot before attaching, since draw order differs from hierarchy.
	for joint_name: String in manifest["joints"]:
		_create_joint(joint_name, _vector(manifest["joints"][joint_name]["pivot"]))
	for layer: Dictionary in manifest["layers"]:
		_create_joint(layer["name"], _vector(layer["pivot"]))
	for joint_name: String in manifest["joints"]:
		_attach_joint(joint_name, manifest["joints"][joint_name]["parent"], canvas)
	for layer: Dictionary in manifest["layers"]:
		var layer_name: String = layer["name"]
		_attach_joint(layer_name, layer["parent"], canvas)
		var sprite := Sprite2D.new()
		sprite.name = "Artwork"
		var cropped: Dictionary = crops[layer_name]
		sprite.texture = _load_layer_texture(RIG_DIRECTORY + cropped["file"])
		sprite.centered = false
		# Crop offsets retain exactly the same source-space placement and pivots.
		sprite.position = _vector(cropped["offset"]) - _source_pivots[layer_name]
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		sprite.z_as_relative = false
		sprite.z_index = layer["z_index"]
		(_joints[layer_name] as Node2D).add_child(sprite)
		_sprites[layer_name] = sprite
	_body = _joints["body"]
	_set_mirror(_mirrored)


func _create_joint(joint_name: String, source_pivot: Vector2) -> void:
	var joint := Node2D.new()
	joint.name = joint_name
	_joints[joint_name] = joint
	_source_pivots[joint_name] = source_pivot


func _attach_joint(joint_name: String, parent_name: Variant, canvas: Node2D) -> void:
	var joint: Node2D = _joints[joint_name]
	var parent: Node2D = canvas if parent_name == null else _joints[parent_name]
	var parent_pivot: Vector2 = Vector2.ZERO if parent_name == null else _source_pivots[parent_name]
	joint.position = _source_pivots[joint_name] - parent_pivot
	_rest_positions[joint_name] = joint.position
	parent.add_child(joint)


func _load_layer_texture(path: String) -> Texture2D:
	if ResourceLoader.exists(path, "Texture2D"):
		var imported_texture := ResourceLoader.load(path, "Texture2D") as Texture2D
		if imported_texture != null:
			return imported_texture
	# New layers can be previewed before the editor finishes their first import.
	var source_image := Image.load_from_file(path)
	if source_image == null:
		push_error("Hyou dialogue idle: cannot load layer " + path)
		return null
	return ImageTexture.create_from_image(source_image)


func _apply_idle_pose(seconds: float) -> void:
	var phase := TAU * fposmod(seconds, LOOP_SECONDS) / LOOP_SECONDS
	for joint_name: String in _joints:
		var joint: Node2D = _joints[joint_name]
		joint.position = _rest_positions[joint_name]
		joint.rotation = 0.0
	_body.position += Vector2(7.0 * sin(phase), -3.0 * (1.0 - cos(phase))) * intensity
	_body.rotation = deg_to_rad(0.35) * sin(phase) * intensity
	# The face, eyes, mouth, accessories and legs inherit only the rigid body pose.
	# Hair and sleeves add one slow periodic sway, with no random/high-speed shake.
	_sway("hair_back_screenleft", phase, 1.8)
	_sway("hair_back_screenright", phase, 1.8, PI / 7.0)
	_sway("hair_front_screenleft", phase, 0.4)
	_sway("hair_front_center", phase, 0.4, PI / 9.0)
	_sway("hair_front_screenright", phase, 0.4, PI / 5.0)
	_sway("arm_front_sleeve", phase, 0.4)
	_sway("arm_back_sleeve", phase, 0.2, PI / 5.0)


func _sway(joint_name: String, phase: float, amplitude_degrees: float, phase_offset: float = 0.0) -> void:
	var offset_sine := sin(phase_offset)
	var wave := (sin(phase + phase_offset) - offset_sine) / (1.0 + absf(offset_sine))
	(_joints[joint_name] as Node2D).rotation = deg_to_rad(amplitude_degrees) * wave * intensity


func _vector(value: Array) -> Vector2:
	return Vector2(value[0], value[1])
