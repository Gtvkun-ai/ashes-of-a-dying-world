extends Control

const PARTY_HUD_SCENE := preload("res://scenes/ui/hud/party_hud.tscn")
const PLAYER_STATS_SCRIPT := preload("res://scripts/Characters/Stats/PlayerStats.cs")
const EXPECTED_UNIT_WIDTH := 300.0
const EXPECTED_UNIT_HEIGHT := 97.0
const EXPECTED_GAP := 4.0
const EXPECTED_VIEWPORTS := [Vector2i(1600, 900), Vector2i(1280, 720)]

const CONFIG_PATHS := [
	"res://data/characters/main.tres",
	"res://data/characters/hyou.tres",
	"res://data/characters/main.tres",
]
const DISPLAY_NAMES := [
	"Nguyễn Ánh Dương",
	"Hyou",
	"Mai An - Người giữ mầm",
]
const LEVELS := [12, 9, 7]
const RESOURCE_VALUES := [
	Vector3(86.0, 44.0, 72.0),
	Vector3(24.0, 68.0, 41.0),
	Vector3(61.0, 18.0, 93.0),
]
const STATUS_COMBINATIONS := [
	[0, 1], # Nhiễm lạnh + Chậm
	[2],    # Đóng băng
	[0],    # Nhiễm lạnh
]

var _stats_nodes: Array[Node] = []
var _party_hud: CanvasLayer


func _ready() -> void:
	get_window().size = _requested_viewport_size()
	await get_tree().process_frame
	if not await _build_showcase():
		await _cleanup_showcase()
		get_tree().quit(1)
		return
	if not await _validate_showcase():
		await _cleanup_showcase()
		get_tree().quit(1)
		return
	print("Party HUD showcase validation passed at %dx%d" % [
		int(get_viewport_rect().size.x),
		int(get_viewport_rect().size.y),
	])
	await _cleanup_showcase()
	get_tree().quit(0)


func _build_showcase() -> bool:
	var manager := get_node_or_null("/root/PlayerManager")
	if not _require(manager != null, "PlayerManager autoload is missing"):
		return false
	manager.call("ResetParty")

	for index in DISPLAY_NAMES.size():
		var source_config = load(CONFIG_PATHS[index])
		if not _require(source_config != null, "Could not load %s" % CONFIG_PATHS[index]):
			return false
		var config = source_config.duplicate(true)
		config.set("Name", DISPLAY_NAMES[index])

		var stats: Node = PLAYER_STATS_SCRIPT.new()
		if not _require(stats != null, "Could not instantiate PlayerStats"):
			return false
		stats.name = "ShowcaseStats%d" % (index + 1)
		stats.set("ConfigData", config)
		stats.set("InitialLevel", LEVELS[index])
		stats.set("UseManualProfile", true)
		stats.set("ManualMaxHP", 100.0)
		stats.set("ManualMaxMP", 100.0)
		stats.set("ManualMaxStamina", 100.0)
		add_child(stats)
		_stats_nodes.append(stats)

	await get_tree().process_frame
	for index in _stats_nodes.size():
		var values: Vector3 = RESOURCE_VALUES[index]
		_stats_nodes[index].call("RestoreResourceValues", values.x, values.y, values.z)

	_party_hud = PARTY_HUD_SCENE.instantiate()
	add_child(_party_hud)
	await get_tree().process_frame
	await get_tree().process_frame

	var units := _unit_nodes()
	if not _require(units.size() == 3, "Party HUD must expose exactly three units"):
		return false
	for index in units.size():
		var unit: Control = units[index]
		unit.set_process(false)
		if not _reveal_status_combination(unit, STATUS_COMBINATIONS[index]):
			return false
	return true


func _reveal_status_combination(unit: Control, visible_indices: Array) -> bool:
	var strip := unit.get_node("Content/Columns/PortraitColumn/StatusFrame/StatusStrip")
	if not _require(strip.get_child_count() == 3, "StatusStrip must contain Nhiễm lạnh, Chậm and Đóng băng"):
		return false
	for child_index in strip.get_child_count():
		strip.get_child(child_index).visible = visible_indices.has(child_index)
	return true


func _validate_showcase() -> bool:
	var viewport_size := Vector2i(get_viewport_rect().size)
	if not _require(EXPECTED_VIEWPORTS.has(viewport_size), "Unexpected viewport %s" % viewport_size):
		return false

	var units := _unit_nodes()
	if not _require(units.size() == 3, "Expected three visible party members"):
		return false
	for index in units.size():
		var unit: Control = units[index]
		var minimum := unit.get_combined_minimum_size()
		if not _require(_near(minimum.x, EXPECTED_UNIT_WIDTH), "Unit %d minimum width changed: %.1f" % [index, minimum.x]):
			return false
		if not _require(_near(minimum.y, EXPECTED_UNIT_HEIGHT), "Unit %d minimum height changed: %.1f" % [index, minimum.y]):
			return false
		if not _require(_near(unit.size.x, EXPECTED_UNIT_WIDTH), "Unit %d rendered width changed: %.1f" % [index, unit.size.x]):
			return false
		if not _require(_near(unit.size.y, EXPECTED_UNIT_HEIGHT), "Unit %d rendered height changed: %.1f" % [index, unit.size.y]):
			return false
		if not _assert_active_skills(unit, index):
			return false
		if not _assert_status_is_reserved(unit, index):
			return false

	if not assert_no_overlap(units):
		return false
	var stack: Control = _party_hud.get_node("VBoxContainer")
	if not _require(_near(stack.get_global_rect().end.x, float(viewport_size.x)), "Party HUD is no longer right anchored"):
		return false
	return await assert_focus_round_trip(units[1])


func _assert_active_skills(unit: Control, index: int) -> bool:
	var strip: Control = unit.get_node("Content/Columns/StatsColumn/HeaderRow/ActiveSkillStrip")
	return (
		_require(strip.visible, "Unit %d active-skill strip is hidden" % index)
		and _require(strip.get_child_count() > 0, "Unit %d has no active-skill badge" % index)
	)


func _assert_status_is_reserved(unit: Control, index: int) -> bool:
	var unit_rect := unit.get_global_rect()
	var status_frame: Control = unit.get_node("Content/Columns/PortraitColumn/StatusFrame")
	var status_rect := status_frame.get_global_rect()
	if not _require(unit_rect.grow(0.1).encloses(status_rect), "Unit %d status frame escaped its 97px layout" % index):
		return false
	for badge in status_frame.get_node("StatusStrip").get_children():
		if badge.visible and not _require(status_rect.grow(0.1).encloses(badge.get_global_rect()), "Unit %d status badge overflowed reserved space" % index):
			return false
	return true


func assert_no_overlap(units: Array[Node]) -> bool:
	for index in units.size() - 1:
		var current: Control = units[index]
		var following: Control = units[index + 1]
		var expected_top := current.get_global_rect().end.y + EXPECTED_GAP
		if not _require(_near(following.get_global_rect().position.y, expected_top), "Party units overlap or shift between states"):
			return false
	return true


func assert_focus_round_trip(source: Control) -> bool:
	source.grab_focus()
	await get_tree().process_frame
	if not _require(get_viewport().gui_get_focus_owner() == source, "Party unit could not receive keyboard/gamepad focus"):
		return false

	var accept := InputEventAction.new()
	accept.action = "ui_accept"
	accept.pressed = true
	var gui_method := &"_gui_input" if source.has_method(&"_gui_input") else &"_GuiInput"
	if not _require(source.has_method(gui_method), "CharacterUnitHUD has no GUI input callback"):
		return false
	source.call(gui_method, accept)
	await get_tree().process_frame

	var menu: Control = _party_hud.get_node("CharacterCommandContextMenu")
	if not _require(menu.visible, "ui_accept did not open the companion command menu"):
		return false
	if not _require(get_viewport().gui_get_focus_owner() is Button, "Command menu did not focus its first enabled button"):
		return false

	var cancel := InputEventAction.new()
	cancel.action = "ui_cancel"
	cancel.pressed = true
	var unhandled_method := &"_unhandled_input" if _party_hud.has_method(&"_unhandled_input") else &"_UnhandledInput"
	if not _require(_party_hud.has_method(unhandled_method), "PartyHUDManager has no unhandled input callback"):
		return false
	_party_hud.call(unhandled_method, cancel)
	await get_tree().process_frame
	return (
		_require(not menu.visible, "ui_cancel did not close the companion command menu")
		and _require(get_viewport().gui_get_focus_owner() == source, "Closing the command menu did not restore unit focus")
	)


func _unit_nodes() -> Array[Node]:
	var result: Array[Node] = []
	if _party_hud == null:
		return result
	var stack := _party_hud.get_node("VBoxContainer")
	for child in stack.get_children():
		if child.visible:
			result.append(child)
	return result


func _near(actual: float, expected: float) -> bool:
	return absf(actual - expected) <= 0.1


func _requested_viewport_size() -> Vector2i:
	for argument in OS.get_cmdline_user_args():
		if not argument.begins_with("--showcase-size="):
			continue
		var dimensions := argument.trim_prefix("--showcase-size=").split("x")
		if dimensions.size() == 2:
			var parsed := Vector2i(dimensions[0].to_int(), dimensions[1].to_int())
			if EXPECTED_VIEWPORTS.has(parsed):
				return parsed
	return Vector2i(1600, 900)


func _cleanup_showcase() -> void:
	var manager := get_node_or_null("/root/PlayerManager")
	if manager != null:
		manager.call("ResetParty")
	if is_instance_valid(_party_hud):
		_party_hud.queue_free()
	for stats in _stats_nodes:
		if is_instance_valid(stats):
			stats.queue_free()
	_stats_nodes.clear()
	await get_tree().process_frame
	await get_tree().process_frame


func _require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error("Party HUD showcase validation failed: %s" % message)
	return false
