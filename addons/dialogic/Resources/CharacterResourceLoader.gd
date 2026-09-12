@tool
class_name DialogicCharacterFormatLoader
extends ResourceFormatLoader



## .dch files hiện dùng .tres format chuẩn của Godot → dùng built-in loader thay vì custom loader này.
## Custom loader này chỉ hỗ trợ format dict text cũ và bị broken với format hiện tại.
func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray()


## Returns "Resource" if this file can/should be loaded by this script
func _get_resource_type(path: String) -> String:
	return ""


## Returns the script class associated with a Resource
func _get_resource_script_class(path: String) -> String:
	var ext := path.get_extension().to_lower()
	if ext == "dch":
		return "DialogicCharacter"

	return ""


## Return true if this type is handled
func _handles_type(typename: StringName) -> bool:
	return ClassDB.is_parent_class(typename, "Resource")


## Parse the file and return a resource
func _load(path: String, _original_path: String, _use_sub_threads: bool, _cache_mode: int) -> Variant:
	# .dch files hiện dùng Godot .tres format (standard resource format).
	# Không thể dùng dict_to_inst(str_to_var(...)) vì format đã thay đổi.
	# Dùng ResourceLoader với CACHE_MODE_IGNORE để load theo Godot built-in loader.
	var res := ResourceLoader.load(path, "Resource", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
	if res == null:
		push_error("[Dialogic] Cannot load character resource: " + path)
		return ERR_CANT_OPEN
	return res



func _get_dependencies(path:String, _add_type:bool) -> PackedStringArray:
	var depends_on: PackedStringArray = []
	var character: DialogicCharacter = load(path)
	for p in character.portraits.values():
		if 'scene' in p and p.scene:
			depends_on.append(p.scene)
		for i in p.get("export_overrides", []):
			if typeof(p.export_overrides[i]) == TYPE_STRING and "://" in p.export_overrides[i]:
				depends_on.append(p.export_overrides[i].trim_prefix('"').trim_suffix('"'))
	return depends_on


func _rename_dependencies(path: String, renames: Dictionary) -> Error:
	var character: DialogicCharacter = load(path)
	for p in character.portraits.values():
		if 'scene' in p and p.scene in renames:
			p.scene = renames[p.scene]

		for i in p.get("export_overrides", []):
			if typeof(p.export_overrides[i]) == TYPE_STRING and "://" in p.export_overrides[i]:
				var i_path := str(p.export_overrides[i]).trim_prefix('"').trim_suffix('"')
				if i_path in renames:
					p.export_overrides[i] = '"'+renames[i_path]+'"'

	ResourceSaver.save(character, path)
	return OK
