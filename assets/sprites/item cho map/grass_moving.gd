extends Area2D
@onready var sprite = $Sprite2D

# Cấu hình Gió (Nhẹ nhàng)
@export var wind_angle = 5.0      # Góc nghiêng gió (độ)
@export var wind_speed = 2.0      # Tốc độ gió (giây)

# Cấu hình Tương tác (Mạnh bạo)
@export var push_force = 25.0     # Góc nghiêng khi bị đụng (độ)
@export var push_speed = 0.2      # Tốc độ bị đẩy (cực nhanh)
@export var bounce_speed = 0.5    # Tốc độ bật lại

var active_tween: Tween # Biến để lưu cái tween đang chạy

func _ready():
	# Bắt đầu game là gió thổi ngay
	start_wind()

# --- HÀM 1: TẠO GIÓ ---
func start_wind():
	# Luôn nhớ kill tween cũ trước khi tạo mới để tránh lỗi
	if active_tween: active_tween.kill()
	
	active_tween = create_tween().set_loops() # Lặp vô tận
	
	# Nghiêng phải -> Nghiêng trái nhẹ nhàng
	active_tween.tween_property(sprite, "skew", deg_to_rad(wind_angle), wind_speed).set_trans(Tween.TRANS_SINE)
	active_tween.tween_property(sprite, "skew", deg_to_rad(-wind_angle), wind_speed).set_trans(Tween.TRANS_SINE)

# --- HÀM 2: TƯƠNG TÁC ---
func _on_body_entered(body: Node2D):
	if body.is_in_group("player"): # Nhớ set Group cho nhân vật là "player" nhé
		apply_push(body.global_position)

func apply_push(player_pos):
	# 1. NGẮT GIÓ NGAY LẬP TỨC
	if active_tween: active_tween.kill()
	
	# 2. Tính hướng đẩy (Người bên trái thì cỏ nghiêng sang phải và ngược lại)
	var direction = 1
	if player_pos.x > global_position.x:
		direction = -1 # Cỏ nghiêng sang trái
		
	# 3. Tạo tween rung mạnh (Không lặp)
	active_tween = create_tween()
	
	# Giai đoạn A: Bị đẩy mạnh
	var target_skew = deg_to_rad(push_force * direction)
	active_tween.tween_property(sprite, "skew", target_skew, push_speed).set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)
	
	# Giai đoạn B: Trả về vị trí 0 (đứng thẳng)
	active_tween.tween_property(sprite, "skew", 0.0, bounce_speed)
	
	# 4. QUAN TRỌNG NHẤT: Sau khi rung xong -> Gọi lại hàm gió
	active_tween.tween_callback(start_wind)
