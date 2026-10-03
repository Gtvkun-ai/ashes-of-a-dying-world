extends Node2D

const TARGET_VIEWPORTS := [Vector2i(1600, 900), Vector2i(1280, 720)]
const LANES := ["Target", "Health", "Feedback", "Progression"]
const LANE_COLORS := [Color("62b8d9"), Color("7fa56d"), Color("c39a57"), Color("9a72c7")]

var viewport_index := 0
var actor_position := Vector2.ZERO
var lane_rects: Array[Rect2] = []


func _ready() -> void:
	_apply_target_viewport()
	_queue_showcase_frame()


func _apply_target_viewport() -> void:
	var target_size: Vector2i = TARGET_VIEWPORTS[viewport_index]
	get_viewport().size = target_size
	actor_position = Vector2(target_size.x * 0.5, target_size.y * 0.64)
	lane_rects.clear()
	for index in LANES.size():
		var lane_size := Vector2(252.0 if index != 3 else 300.0, 24.0 if index != 3 else 42.0)
		var lane_origin := actor_position + Vector2(-lane_size.x * 0.5, -188.0 + index * 40.0)
		lane_rects.append(Rect2(lane_origin, lane_size))
	queue_redraw()


func _queue_showcase_frame() -> void:
	await get_tree().create_timer(2.0).timeout
	viewport_index = (viewport_index + 1) % TARGET_VIEWPORTS.size()
	_apply_target_viewport()
	_queue_showcase_frame()


func _draw() -> void:
	var target_size: Vector2i = TARGET_VIEWPORTS[viewport_index]
	draw_rect(Rect2(Vector2.ZERO, Vector2(target_size)), Color("120f0c"))
	draw_string(ThemeDB.fallback_font, Vector2(24, 36), "WORLD HUD LANE SHOWCASE", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("f0e5d2"))
	draw_string(ThemeDB.fallback_font, Vector2(24, 62), "%d x %d" % [target_size.x, target_size.y], HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color("bfaf98"))

	for index in lane_rects.size():
		var lane_rect: Rect2 = lane_rects[index]
		draw_rect(lane_rect, Color(LANE_COLORS[index], 0.22), true)
		draw_rect(lane_rect, LANE_COLORS[index], false, 2.0)
		draw_string(ThemeDB.fallback_font, lane_rect.position + Vector2(12, 17), LANES[index], HORIZONTAL_ALIGNMENT_LEFT, -1, 13, LANE_COLORS[index])

	draw_circle(actor_position, 20.0, Color("b9574f"))
	draw_circle(actor_position, 13.0, Color("f0e5d2"))
	draw_string(ThemeDB.fallback_font, actor_position + Vector2(-40, 48), "actor", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color("bfaf98"))

