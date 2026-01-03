using Godot;
using AshesofaDyingWorld.Entities.Player;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 100f;
	[Export] public float RunSpeed { get; set; } = 200f;
	[Export] public float RunStaminaCost { get; set; } = 20f;
	[Export] public float MinStaminaToRun { get; set; } = 40f;
	[Export] public NodePath BodyPath { get; set; } = "Body";
	[Export] public int StopFrameIndex { get; set; } = 0;

	private bool _isExhausted = false; // Cờ đánh dấu đang kiệt sức

	private AnimatedSprite2D _body;
	private string _lastMoveAnim = "go_down";
	private bool _wasMoving = false;
	private bool _wasRunning = false; // Track trạng thái chạy trước đó
	private PlayerStats _stats;

	public override void _Ready()
	{
		_body = GetNodeOrNull<AnimatedSprite2D>(BodyPath);
		_stats = GetNodeOrNull<PlayerStats>("PlayerStats");
		_body?.Play("Idle");
	}

public override void _PhysicsProcess(double delta)
{

	//Dòng hiện các input
	Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
	bool moving = inputDir != Vector2.Zero; // Kiểm tra có di chuyển hay không
	bool wantsToRun = Input.IsKeyPressed(Key.Shift); // Giữ Shift để chạy
	
	// Tính lượng stamina sẽ mất trong frame này
	float staminaCostThisFrame = RunStaminaCost * (float)delta;


	if (_stats != null)
	{
		// QUAN TRỌNG: Thay vì so sánh với staminaCostThisFrame (quá nhỏ, chỉ 0.3),
		// Hãy đặt ngưỡng cứng là 1.0f. Nếu dưới 1.0 là coi như hết hơi luôn.
		// Điều này giúp thanh máu vừa cạn là nhân vật dừng ngay, không chạy ráng.
		if (_stats.CurrentStamina <= 3.0f) 
		{
			_isExhausted = true;
		}
		else if (_stats.CurrentStamina >= MinStaminaToRun)
		{
			_isExhausted = false;
		}
	}

	bool canRun = false;
	
	// Phải chưa kiệt sức VÀ stamina thực tế phải lớn hơn 0
	if (moving && wantsToRun && !_isExhausted && _stats != null && _stats.CurrentStamina > 0)
	{
		canRun = true;
		_stats.ConsumeStamina(staminaCostThisFrame);
	}
	else
	{
		// Nếu không chạy được, đảm bảo tắt trạng thái chạy
		canRun = false;
		
		// Nếu đang kiệt sức mà người chơi thả nút Shift, hoặc dừng lại,
		// thì vẫn giữ cờ _isExhausted để bắt buộc hồi phục
	}

	// DI CHUYỂN
	if (moving)
	{
		inputDir = inputDir.Normalized();
		Velocity = inputDir * (canRun ? RunSpeed : Speed);

		// Animation
		string action = canRun ? "run" : "go";
		string vDir = "";
		if (inputDir.Y > 0) vDir = "down";
		else if (inputDir.Y < 0) vDir = "up";
		
		string hDir = "";
		if (inputDir.X > 0) hDir = "right";
		else if (inputDir.X < 0) hDir = "left";
		
		string direction = (vDir != "" && hDir != "") ? $"{vDir}_{hDir}" : $"{vDir}{hDir}";
		string anim = $"{action}_{direction}";

		if (_body != null && _body.Animation != anim)
		{
			if (_body.SpriteFrames.HasAnimation(anim)) _body.Play(anim);
		}
		_lastMoveAnim = anim;
	}
	else
	{
		Velocity = Vector2.Zero;
		if (_body != null && _wasMoving)
		{
			string idleAnim = _lastMoveAnim.Replace("run", "go");
			if (_body.SpriteFrames.HasAnimation(idleAnim))
			{
				_body.Animation = idleAnim;
				_body.Frame = StopFrameIndex;
			}
			_body.Stop();
		}
	}
	
	MoveAndSlide();
	_wasMoving = moving;
}	
}
