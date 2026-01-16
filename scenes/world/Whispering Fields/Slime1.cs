using Godot;
using System;

public partial class Slime1 : CharacterBody2D
{
	[Export] public float Speed = 25f;
	[Export] public int Damage = 10;
	[Export] public float WanderRadius = 100f;

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
		if (_isAttacking) return;

		Vector2 direction = Vector2.Zero;

		if (_isChasing && _player != null)
		{
			// Logic đuổi theo người chơi
			direction = ( _player.GlobalPosition - GlobalPosition).Normalized();
		}
		else
		{
			// Logic di chuyển ngẫu nhiên trong vòng tròn 20px
			if (GlobalPosition.DistanceTo(_targetPosition) < 2)
			{
				UpdateTargetPosition();
			}
			direction = ( _targetPosition - GlobalPosition).Normalized();
		}

		Velocity = direction * Speed;
		MoveAndSlide();
		UpdateAnimation(direction);

		// Kiểm tra va chạm với người chơi để trừ HP
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			if (collision.GetCollider().HasMethod("TakeDamage"))
			{
				collision.GetCollider().Call("TakeDamage", Damage);
			}
		}
	}

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

	// Các hàm này sẽ được gọi từ FindZone.cs và AttackZone.cs
	public void StartChasing(Node2D player)
	{
		_player = player;
		_isChasing = true;
	}

	public void StopChasing()
	{
		_isChasing = false;
		_player = null;
	}

		public void Attack()
	{
		if (_isAttacking) return; 

		_isAttacking = true;
		_animatedSprite.Play($"at_{_currentDirection}");

	// ĐĂNG KÝ: Chỉ sử dụng dấu += ở đây
		_animatedSprite.AnimationFinished += OnAttackFinished;
	}

	private void OnAttackFinished()
	{
		_isAttacking = false;
	// HỦY ĐĂNG KÝ: Để tránh lỗi chạy chồng chéo lần sau
		_animatedSprite.AnimationFinished -= OnAttackFinished;
	}
}
