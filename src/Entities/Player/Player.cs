using Godot;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 100f;
	[Export] public float RunSpeed { get; set; } = 200f;
	[Export] public NodePath BodyPath { get; set; } = "Body";
	[Export] public int StopFrameIndex { get; set; } = 0;

	private AnimatedSprite2D _body;
	private string _lastMoveAnim = "go_down";
	private bool _wasMoving = false;

	public override void _Ready()
	{
		_body = GetNodeOrNull<AnimatedSprite2D>(BodyPath);

		_body?.Play("Idle");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 dir = Vector2.Zero;
		if (Input.IsKeyPressed(Key.Left)) dir.X -= 1;
		if (Input.IsKeyPressed(Key.Right)) dir.X += 1;
		if (Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
		if (Input.IsKeyPressed(Key.Down)) dir.Y += 1;

		bool moving = dir != Vector2.Zero;
		bool isRunning = Input.IsKeyPressed(Key.Shift);

		if (moving)
		{
			dir = dir.Normalized();
			Velocity = dir * (isRunning ? RunSpeed : Speed);

			// Xác định hành động (không có dấu _)
			string action = isRunning ? "run" : "go";

			// Xác định hướng (không có dấu _)
			string direction = (Mathf.Abs(dir.X) > Mathf.Abs(dir.Y))
						? (dir.X > 0 ? "right" : "left")
						: (dir.Y > 0 ? "down" : "up");

			//  Tự động thêm dấu _ vào giữa => Đảm bảo chỉ có 1 dấu gạch dưới
			string anim = $"{action}_{direction}"; 

			if (_body != null && (_body.Animation != anim || !_body.IsPlaying()))
				_body.Play(anim);

			_lastMoveAnim = anim;
		}
		else
		{
			Velocity = Vector2.Zero;

			if (_body != null && _wasMoving)
			{
				string idleAnim = _lastMoveAnim;

				// Chuyển từ chạy về đi bộ để lấy frame đứng
				if (idleAnim.StartsWith("run"))
				{
					idleAnim = idleAnim.Replace("run", "go");
				}

				// Kiểm tra kỹ xem animation "go_..." có tồn tại không trước khi gán
				var frames = _body.SpriteFrames;
				if (frames != null && frames.HasAnimation(idleAnim))
				{
					_body.Animation = idleAnim;
					int max = frames.GetFrameCount(idleAnim);
					_body.Frame = Mathf.Clamp(StopFrameIndex, 0, max - 1);
				}
				else 
				{
					// Fallback nếu không tìm thấy animation (tránh crash)
					GD.PrintErr($"Không tìm thấy animation: {idleAnim}");
				}

				_body.Stop();
			}
		}
		// Di chuyển nhân vật
		MoveAndSlide();
		_wasMoving = moving;
	}
}
