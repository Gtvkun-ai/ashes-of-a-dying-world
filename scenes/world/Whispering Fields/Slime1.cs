using Godot;
using System;

public partial class Slime1 : CharacterBody2D
{
	[Export] public float Speed = 25f;
	[Export] public int Damage = 10;
	[Export] public float WanderRadius = 100f;
	[Export] public float AttackRange = 30f; // Khoảng cách để đánh cú tiếp theo

	private AnimatedSprite2D _animatedSprite;
	private Vector2 _targetPosition;
	private Vector2 _startPosition;
	private bool _isAttacking = false;
	private bool _isChasing = false;
	private Node2D _player;

	private string _currentDirection = "down";

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		UpdateTargetPosition();
	}

	public override void _PhysicsProcess(double delta)
	{
		// QUY TẮC 1: Đang múa combo thì cấm di chuyển
		if (_isAttacking) 
		{
			Velocity = Vector2.Zero;
			MoveAndSlide(); 
			return;
		}

		Vector2 direction = Vector2.Zero;

		if (_isChasing && _player != null)
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
			
			// [MỚI] Chỉ di chuyển nếu còn xa (ví dụ: xa hơn 25px)
			// Nếu đã đến gần (<= 25px) thì đứng yên (để chuẩn bị đánh), KHÔNG húc vào người nữa
			if (dist > 2f) 
			{
				direction = (_player.GlobalPosition - GlobalPosition).Normalized();
			}
			else 
			{
				// Đã áp sát -> Phanh lại ngay
				direction = Vector2.Zero;
			}
		}
		else
		{
			// ... (Logic đi lang thang giữ nguyên) ...
			if (GlobalPosition.DistanceTo(_targetPosition) < 5f)
			{
				UpdateTargetPosition();
			}
			direction = (_targetPosition - GlobalPosition).Normalized();
		}

		Velocity = direction * Speed;
		MoveAndSlide();
		for (int i = 0; i < GetSlideCollisionCount(); i++)
{
	var collision = GetSlideCollision(i);
	var body = collision.GetCollider() as Node;
	
	// Nếu vật va chạm là Player (kiểm tra group)
	if (body != null && body.IsInGroup("Player"))
	{
		var pusherPosition = (body as Node2D).GlobalPosition;
		var pushDirection = (GlobalPosition - pusherPosition).Normalized();
		
		// Đẩy Slime văng ra (Lực đẩy 300)
		Velocity += pushDirection * 300f; 
		if (_isAttacking) return;
		MoveAndSlide();
	}
}
		UpdateAnimation(direction);
	}

	// --- CÁC HÀM BỊ THIẾU ĐÃ ĐƯỢC THÊM LẠI Ở ĐÂY ---

	private void UpdateTargetPosition()
	{
		// Random điểm ngẫu nhiên trong vòng tròn WanderRadius
		Random random = new Random();
		float angle = (float)random.NextDouble() * Mathf.Pi * 2;
		float distance = (float)random.NextDouble() * WanderRadius;
		_targetPosition = _startPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
	}

	private void UpdateAnimation(Vector2 direction)
	{
		if (direction.Length() > 0)
		{
			if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
			{
				_currentDirection = direction.X > 0 ? "right" : "left";
			}
			else
			{
				_currentDirection = direction.Y > 0 ? "down" : "up";
			}
			_animatedSprite.Play($"go_{_currentDirection}");
		}
		else
		{
			_animatedSprite.Play("idle");
		}
	}

	// ---------------------------------------------------

	public void StartChasing(Node2D player)
	{
		_player = player;
		_isChasing = true;
	}

	public void StopChasing()
	{
		if (_isAttacking) return;

		_isChasing = false;
		_player = null;
		
		// QUY TẮC 4: Out findzone -> Quay lại chỗ cũ
		_startPosition = GlobalPosition; 
		UpdateTargetPosition();
	}

	public void Attack()
	{
		if (_isAttacking) return;

		// DEBUG: In ra để biết lệnh tấn công đã kích hoạt
		GD.Print("1. Bắt đầu Tấn công! Đang khóa di chuyển.");

		_isAttacking = true;
		Velocity = Vector2.Zero; // Phanh gấp

		// QUY TẮC 2: Tùy vào vị trí player mà chọn hướng đánh MỚI
		if (_player != null)
		{
			Vector2 dirToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
			UpdateDirectionString(dirToPlayer); 
		}

		string animName = $"at_{_currentDirection}";
		
		// DEBUG: In ra tên animation đang thử chạy
		GD.Print($"2. Đang thử chạy animation: {animName}");

		if (_animatedSprite.SpriteFrames.HasAnimation(animName))
		{
			_animatedSprite.Play(animName);
			// Đăng ký sự kiện: Đánh xong thì gọi hàm OnAttackFinished
			if (!_animatedSprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAttackFinished)))
			{
				_animatedSprite.AnimationFinished += OnAttackFinished;
			}
		}
		else
		{
			GD.PrintErr($"LỖI: Không tìm thấy animation tên '{animName}' trong SpriteFrames!");
			_isAttacking = false;
		}
	}

	private void OnAttackFinished()
	{
		// [ĐÃ SỬA LỖI TẠI ĐÂY]: Thêm .ToString() để chuyển đổi kiểu dữ liệu
		string currentAnim = _animatedSprite.Animation.ToString();
		
		if (currentAnim.StartsWith("at_"))
		{
			GD.Print("4. Đã kết thúc đòn đánh. Mở khóa di chuyển.");
			_animatedSprite.AnimationFinished -= OnAttackFinished;
			_isAttacking = false;

			// QUY TẮC 3: Đánh xong 1 chuỗi, giờ làm gì tiếp?
			DecideNextMove();
		}
	}

	private void DecideNextMove()
	{
		if (_player == null) 
		{
			StopChasing();
			return;
		}

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// TRƯỜNG HỢP A: Player vẫn đứng lỳ trong tầm đánh -> Đánh tiếp
		if (distance <= AttackRange)
		{
			Attack();
		}
		// TRƯỜNG HỢP B: Player chạy ra xa nhưng vẫn trong Find Zone -> Đuổi theo
		else if (_isChasing) 
		{
			_animatedSprite.Play($"go_{_currentDirection}");
		}
		// TRƯỜNG HỢP C: Player đã chạy quá xa -> Đi lang thang
		else
		{
			StopChasing();
		}
	}

	private void UpdateDirectionString(Vector2 direction)
	{
		if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
		{
			_currentDirection = direction.X > 0 ? "right" : "left";
		}
		else
		{
			_currentDirection = direction.Y > 0 ? "down" : "up";
		}
	}
}
