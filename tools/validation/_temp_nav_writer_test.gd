extends SceneTree


func _initialize() -> void:
	call_deferred("_run_test")


func _run_test() -> void:
	var scene := (load("res://scenes/world/whispering_fields/field_01.tscn") as PackedScene).instantiate()
	root.add_child(scene)
	for _frame in range(5):
		await process_frame
	var grid := scene.get_node("WorldNavigationGrid")
	var before_load := bool(grid.call("HasBakedGridData"))
	var requested := bool(grid.call("RequestEditorBake"))
	for _frame in range(180):
		await physics_frame
		if not bool(grid.call("IsEditorBakeRunning")):
			break
	var baked := bool(grid.call("HasBakedGridData"))
	var overrides: PackedByteArray = grid.call("GetManualOverrideSnapshot")
	var painted := bool(grid.call("PaintEditorCells", Vector2(900, 900), 2, 1))
	grid.call("ApplyManualOverrideSnapshot", overrides)
	var restored: PackedByteArray = grid.call("GetManualOverrideSnapshot")
	var data := grid.get("BakedData") as Resource

	var packed := PackedScene.new()
	var pack_error := packed.pack(scene)
	var save_path := "user://_temp_nav_writer_test.tscn"
	var save_error := ResourceSaver.save(packed, save_path)
	scene.queue_free()
	for _frame in range(3):
		await process_frame
	var reloaded := (load(save_path) as PackedScene).instantiate()
	root.add_child(reloaded)
	for _frame in range(3):
		await process_frame
	var reloaded_data := bool(reloaded.get_node("WorldNavigationGrid").call("HasBakedGridData"))
	print(
		"[NavWriterTest] before=", before_load,
		" requested=", requested,
		" baked=", baked,
		" painted=", painted,
		" restored=", overrides == restored,
		" class=", data.get_class(),
		" path=", data.resource_path,
		" pack=", pack_error,
		" save=", save_error,
		" reloaded=", reloaded_data
	)
	reloaded.queue_free()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(save_path))
	quit(0)
