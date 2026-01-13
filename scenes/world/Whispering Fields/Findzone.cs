using Godot;
using System;

public partial class Slime : CharacterBody2D
{
	[Export] public float Speed = 50.0f;
	
	private Node2D _target = null; // Lưu trữ người chơi khi phát hiện thấy
	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		// Khởi tạo reference đến AnimatedSprite2D
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		if (_target != null)
		{
			// 1. Tính toán hướng di chuyển tới người chơi
			Vector2 direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
			velocity = direction * Speed;

			// 2. Cập nhật Animation di chuyển
			UpdateMoveAnimation(direction);
		}
		else
		{
			// 3. Đứng yên và chạy animation nghỉ nếu không thấy ai
			velocity = Vector2.Zero;
			_sprite.Play("idle");
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private void UpdateMoveAnimation(Vector2 dir)
	{
		// Chọn animation go_up, go_down, go_left, go_right dựa trên hướng
		if (Mathf.Abs(dir.X) > Mathf.Abs(dir.Y))
		{
			_sprite.Play(dir.X > 0 ? "go_right" : "go_left");
		}
		else
		{
			_sprite.Play(dir.Y > 0 ? "go_down" : "go_up");
		}
	}

	// --- KẾT NỐI SIGNAL TỪ FINDZONE ---

	// Khi có vật thể đi vào FindZone
	private void _on_findzone_body_entered(Node2D body)
	{
		// Kiểm tra nếu đó là Player (nên đặt tên node là Player hoặc dùng Group)
		if (body.Name == "Player" || body.IsInGroup("player"))
		{
			_target = body;
		}
	}

	// Khi vật thể rời khỏi FindZone
	private void _on_findzone_body_exited(Node2D body)
	{
		if (body == _target)
		{
			_target = null;
		}
	}
}
