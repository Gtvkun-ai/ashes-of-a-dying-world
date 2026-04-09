using Godot;
using System;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.UI.HUD;

public partial class Slime1 : CharacterBody2D
{
	[Export] public float Speed = 25f;
	[Export] public int Damage = 10;
	[Export] public float WanderRadius = 100f;
	[Export] public float AttackRange = 30f; // Khoảng cách để đánh cú tiếp theo
	[Export] public int Level = 5;            // Level mặc định của slime
	[Export] public int MaxHP = 600;          // Máu tối đa của slime cơ bản
	[Export] public float Weight = 10f;           // Trọng lượng cơ bản của slime
	[Export] public float KnockbackChance = 0.2f; // Tỉ lệ base, sẽ cộng thêm theo STR, vũ khí, trọng lượng
	[Export] public float KnockbackForce = 300f;  // Lực đẩy base, sẽ scale theo STR, vũ khí, trọng lượng
	[Export] public float AttackKnockbackChance = 0.5f; // Tỉ lệ base quái đẩy lùi player
	[Export] public float AttackKnockbackForce = 400f;  // Lực base quái đẩy lùi player
	[Export] public float AttackDashSpeed = 80f;         // Tốc độ lao/bật trong animation at_*
	public int CurrentHP { get; private set; }

	private AnimatedSprite2D _animatedSprite;
	private Area2D _hurtbox;
	private Vector2 _targetPosition;
	private Vector2 _startPosition;
	private bool _isAttacking = false;
	private bool _isChasing = false;
	private bool _hasAppliedAttackHit = false;
	private Node2D _player;

	private string _currentDirection = "down";

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		InitStats();

		_hurtbox = GetNodeOrNull<Area2D>("Hurtbox");
		if (_hurtbox != null)
		{
			_hurtbox.AreaEntered += OnHurtboxAreaEntered;
		}

		UpdateTargetPosition();

		// Đăng ký thanh máu nổi trên đầu với EnemyHealthBarService
		if (EnemyHealthBarService.Instance != null)
		{
			EnemyHealthBarService.Instance.RegisterEnemy(
				this,
				() => CurrentHP,
				() => MaxHP,
				() => Level
			);
		}
	}
	private void InitStats()
	{
		if (Level < 1) Level = 1;
		if (MaxHP < 1) MaxHP = 1;
		CurrentHP = MaxHP;
		GD.Print($"[Slime] Init stats: Lv{Level}, HP {CurrentHP}/{MaxHP}");
	}

	public override void _PhysicsProcess(double delta)
	{
		// QUY TẮC 1: Đang múa combo thì chỉ cho di chuyển theo animation at_ (lao vào rồi bật lùi)
		if (_isAttacking) 
		{
			Vector2 motion = Vector2.Zero;
			if (_animatedSprite != null)
			{
				string anim = _animatedSprite.Animation.ToString();
				if (anim.StartsWith("at_"))
				{
					// 0–2: lao vào player, 3–4: bật lùi ra
					int frame = _animatedSprite.Frame;
					Vector2 dir = DirectionFromCurrent();

					// Đòn đánh trúng được tính ở khoảng frame 1–2 (lúc đang lao tới).
					// Sau khi đã tính hit (dù trúng hay miss vì ngoài range), slime sẽ
					// chuyển sang pha bật lùi ngay để không "dính" vào người player.
					if (!_hasAppliedAttackHit && frame >= 1 && frame <= 2)
					{
						TryApplyAttackHitToPlayer();
						_hasAppliedAttackHit = true;
					}

					// Nếu chưa xử lý hit thì cho phép tiếp tục lao tới (frame 0–2),
					// còn nếu đã xử lý hit rồi thì luôn bật lùi ra.
					bool shouldDashIn = !_hasAppliedAttackHit && frame <= 2;
					if (shouldDashIn)
					{
						motion = dir * AttackDashSpeed * (float)delta;
					}
					else
					{
						motion = -dir * AttackDashSpeed * (float)delta;
					}
				}
			}

			Velocity = motion / (float)delta; // chuyển sang vận tốc để MoveAndSlide xử lý
			MoveAndSlide(); 
			return;
		}
		Vector2 direction = Vector2.Zero;

		if (_isChasing && _player != null)
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
			// Giữ khoảng cách an toàn ~AttackRange trước khi ra đòn
			// để slime không áp sát quá sát rồi mới lao, tránh kéo dính player
			float stopDistance = AttackRange; // có thể chỉnh trong Inspector qua AttackRange
			if (dist > stopDistance)
			{
				direction = (_player.GlobalPosition - GlobalPosition).Normalized();
			}
			else
			{
				// Đã vào tầm đánh mong muốn -> đứng lại, chờ logic Attack quyết định
				direction = Vector2.Zero;

				// Nếu đã trong tầm đánh và chưa tấn công, chủ động ra đòn luôn
				if (!_isAttacking && dist <= AttackRange)
				{
					Attack();
				}
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
		// Nếu đang tấn công thì không override animation at_*
		if (_isAttacking) return;

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

		_hasAppliedAttackHit = false;
		_isAttacking = true;
		Velocity = Vector2.Zero; // Phanh gấp

		// QUY TẮC 2: Tùy vào vị trí player mà chọn hướng đánh MỚI
		if (_player != null)
		{
			Vector2 dirToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
			UpdateDirectionString(dirToPlayer); 
		}

		string animName = $"at_{_currentDirection}";

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

	public void TakeDamage(int amount, Node2D source = null)
	{
		if (amount <= 0 || CurrentHP <= 0) return;

		CurrentHP = Mathf.Max(0, CurrentHP - amount);
		GD.Print($"[Slime] Took {amount} damage. HP: {CurrentHP}/{MaxHP}");

		if (CurrentHP <= 0)
		{
			Die(source);
		}
	}

	private void OnHurtboxAreaEntered(Area2D area)
	{
		// Bị trúng hitbox của Player?
		// Cấu trúc: Player(CharacterBody2D) -> WeaponSprite -> Hitbox (Area2D)
		var weaponSprite = area.GetParent() as Node2D;
		if (weaponSprite == null) return;

		var player = weaponSprite.GetParent() as Player;
		if (player == null) return;
		if (!player.IsAttackHitboxActive()) return;

		// Lấy damage + STR + vũ khí từ PlayerStats nếu có
		int damage = 1;
		int strength = 0;
		float weaponWeight = 1f;
		var stats = player.GetNodeOrNull<AshesofaDyingWorld.Entities.Player.PlayerStats>("PlayerStats");
		if (stats != null)
		{
			// Sát thương
			damage = Mathf.Max(1, (int)stats.AttackDamage);

			// STR
			if (stats.FinalAttributes != null &&
				stats.FinalAttributes.TryGetValue(AttributeType.Strength, out int strVal))
			{
				strength = strVal;
			}

			// Độ nặng vũ khí từ EquipmentManager (nếu có)
			if (stats.EquipmentMgr != null)
			{
				var mainWeapon = stats.EquipmentMgr.GetEquippedItem(EquipmentSlot.MainHand);
				if (mainWeapon != null && mainWeapon.WeaponWeight > 0f)
				{
					weaponWeight = mainWeapon.WeaponWeight;
				}
			}
		}

		TakeDamage(damage, player);

		// TÍNH KNOCKBACK: phụ thuộc STR, độ nặng vũ khí, và trọng lượng quái
		float enemyWeight = Mathf.Max(1f, Weight);
		float powerRatio = (strength * weaponWeight) / enemyWeight; // STR & vũ khí càng lớn, quái càng nhẹ => ratio càng cao

		// Chance = KnockbackChance(base) + 0.02 * ratio, clamp [0..1]
		float chance = Mathf.Clamp(KnockbackChance + 0.02f * powerRatio, 0f, 1f);
		if (GD.Randf() <= chance)
		{
			// Force = KnockbackForce(base) * (1 + 0.02*STR) * (0.5 + weaponWeight) / enemyWeight
			float forceScaleFromStr = 1f + strength * 0.02f;
			float forceScaleFromWeapon = 0.5f + weaponWeight; // vũ khí càng nặng, hệ số càng lớn
			float force = KnockbackForce * forceScaleFromStr * forceScaleFromWeapon / enemyWeight;

			Vector2 dir = (GlobalPosition - player.GlobalPosition).Normalized();
			Velocity += dir * force;
		}
	}

	private void Die(Node2D killer)
	{
		GD.Print("[Slime] Died.");

		// Hủy đăng ký thanh máu khi slime chết
		if (EnemyHealthBarService.Instance != null)
		{
			EnemyHealthBarService.Instance.UnregisterEnemy(this);
		}

		// Nếu kẻ giết là một slime khác, cho nó +1 level
		if (killer is Slime1 slimeKiller)
		{
			slimeKiller.GainLevel(1);
		}

		// Khi respawn bằng PackedScene, Level sẽ quay về giá trị export mặc định (ví dụ: 5).

		QueueFree();
	}

	public void GainLevel(int amount = 1)
	{
		if (amount <= 0) return;

		Level += amount;
		// Tăng nhẹ chỉ số cho "quái bá" sau khi giết được mục tiêu
		MaxHP += 10 * amount;
		CurrentHP = MaxHP; // Hồi đầy máu khi lên level
		Damage += 2 * amount;

		GD.Print($"[Slime] Leveled up! New Lv{Level}, HP {CurrentHP}/{MaxHP}, Damage {Damage}");
	}

	private void OnAttackFinished()
	{
		// [ĐÃ SỬA LỖI TẠI ĐÂY]: Thêm .ToString() để chuyển đổi kiểu dữ liệu
		string currentAnim = _animatedSprite.Animation.ToString();
		
		if (currentAnim.StartsWith("at_"))
		{
			// Nếu vì lý do nào đó mà chưa kịp tính hit trong lúc lao tới,
			// fallback: tính một lần tại cuối animation (nhưng bình thường sẽ không vào nhánh này)
			if (!_hasAppliedAttackHit)
			{
				TryApplyAttackHitToPlayer();
			}
			_animatedSprite.AnimationFinished -= OnAttackFinished;
			_isAttacking = false;
			_hasAppliedAttackHit = false;

			// QUY TẮC 3: Đánh xong 1 chuỗi, giờ làm gì tiếp?
			DecideNextMove();
		}
	}

	private void TryApplyAttackHitToPlayer()
	{
		if (_player == null) return;

		var player = _player as Player;
		if (player == null) return;

		// Chỉ coi là trúng nếu player vẫn còn trong tầm đánh
		float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
		if (distance > AttackRange)
		{
			GD.Print($"[Slime] Attack miss: distance={distance} > range={AttackRange}");
			return;
		}

		var stats = player.GetNodeOrNull<AshesofaDyingWorld.Entities.Player.PlayerStats>("PlayerStats");
		int damage = Damage;

		if (stats != null)
		{
			// Check block theo hướng kẻ tấn công đứng tương đối với player.
			// Ví dụ slime đứng bên phải player thì vector này là Right.
			Vector2 attackerDirection = (GlobalPosition - player.GlobalPosition).Normalized();
			Vector2 hitDir = DirectionFromCurrent();
			float hpDamage = player.ReceiveMeleeHit(damage, attackerDirection);
			GD.Print($"[Slime] Hit player for {hpDamage} dmg (raw {damage}). SlimePos={GlobalPosition}, PlayerPos={player.GlobalPosition}");

			// Tính tỉ lệ bị knockback sau khi áp dụng Defense (chỉ nếu còn sát thương vào HP)
			if (hpDamage > 0f)
			{
				float finalChance = stats.ComputeKnockbackChance(AttackKnockbackChance);
				if (GD.Randf() <= finalChance)
				{
					// Tính lực knockback sau khi áp dụng Vitality
					float finalForce = stats.ComputeKnockbackForce(AttackKnockbackForce);
					// Hướng knockback dựa trên hướng tấn công (at_*),
					// và dùng ApplyExternalForce để player trượt lùi nhưng giữ nguyên animation
					GD.Print($"[Slime] Knockback dir={_currentDirection}, hitDir={hitDir}, force={finalForce}");
					player.ApplyExternalForce(hitDir * finalForce);
				}
			}
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

	private Vector2 DirectionFromCurrent()
	{
		switch (_currentDirection)
		{
			case "up": return Vector2.Up;
			case "down": return Vector2.Down;
			case "left": return Vector2.Left;
			case "right": return Vector2.Right;
			default: return Vector2.Down;
		}
	}
}
