using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Entities.NPC
{
	public partial class HyouAI : Node
	{
		private enum AIState
		{
			FollowPlayer,
			ChaseEnemy,
			Attack,
			Block,
			Reposition
		}

		private const string StarterWeaponPath = "res://assets/resources/data/weapons/sword/WoodSword.tres";

		[Export] public bool Enabled { get; set; } = true;
		[Export] public NodePath CharacterPath { get; set; } = "..";
		[Export] public NodePath LeaderPath { get; set; } = "";
		[Export] public float EnemySearchRadius { get; set; } = 180f;
		[Export] public float FollowDistance { get; set; } = 52f;
		[Export] public float FollowStopDistance { get; set; } = 32f;
		[Export] public float AttackRange { get; set; } = 34f;
		[Export] public float RepositionRange { get; set; } = 20f;
		[Export] public float AttackCooldown { get; set; } = 0.35f;
		[Export] public float EnemyRefreshInterval { get; set; } = 0.2f;
		[Export] public float BlockRange { get; set; } = 45f;
		[Export] public float MinStaminaToBlock { get; set; } = 10f;
		[Export] public float ThreatFacingDot { get; set; } = 0.4f;
		[Export] public float ReactionDelayMin { get; set; } = 0.15f;
		[Export] public float ReactionDelayMax { get; set; } = 0.3f;

		private global::Player _character;
		private PlayerStats _stats;
		private Node2D _leader;
		private Node2D _targetEnemy;
		private Node2D _blockThreat;
		private Area2D _characterHurtbox;
		private Node2D _playerAggressor;
		private readonly RandomNumberGenerator _rng = new();
		private AIState _state = AIState.FollowPlayer;
		private bool _initialized = false;
		private float _attackTimer = 0f;
		private float _enemyRefreshTimer = 0f;
		private float _blockReactionTimer = 0f;

		public override void _Ready()
		{
			_rng.Randomize(); //để random phản xạ block
			//để chạy init sau khi scene tree/parent ổn định.
			CallDeferred(nameof(InitializeAfterParentReady));
		}

		private void InitializeAfterParentReady()
		{
			_character = ResolveCharacter(); //Thử lấy global::Player theo CharacterPath
			if (_character == null)
			{
				GD.PrintErr("[HyouAI] Cannot find Player/NpcCharacter controller.");
				return;
			}

			_character.UsePlayerInput = false; //không nhận điều khiển từ player
			_character.AddToGroup("Player");  //để các logic khác (đặc biệt là ResolveLeader()) có thể tìm leader dựa trên group
			_stats = _character.GetStatsNode(); //để đọc HP/Stamina phục vụ quyết định block/stop khi chết.
			_leader = ResolveLeader(); //để xác định ai là “người dẫn” mà Hyou sẽ follow khi không có enemy.

			_characterHurtbox = _character.GetNodeOrNull<Area2D>("Hurtbox");
			if (_characterHurtbox != null)
			{
				_characterHurtbox.AreaEntered += OnCharacterHurtboxAreaEntered;
			}

			SetupPlayerBodyCollisionExceptions();

			AutoEquipStarterWeapon();
			_initialized = true;
		}

		private void SetupPlayerBodyCollisionExceptions()
		{
			if (_character == null)
			{
				return;
			}

			SceneTree tree = GetTree();
			if (tree == null)
			{
				return;
			}

			foreach (Node node in tree.GetNodesInGroup("Player"))
			{
				if (node is global::Player other && other != _character)
				{
					EnsureNoBodyCollisionWith(other);
				}
			}

			if (_leader is global::Player leaderPlayer && leaderPlayer != _character)
			{
				EnsureNoBodyCollisionWith(leaderPlayer);
			}
		}

		private void EnsureNoBodyCollisionWith(global::Player other)
		{
			if (other == null || other == _character)
			{
				return;
			}

			if (!IsNodeUsable(other) || !IsNodeUsable(_character))
			{
				return;
			}

			_character.AddCollisionExceptionWith(other);
			other.AddCollisionExceptionWith(_character);
		}

		private void OnCharacterHurtboxAreaEntered(Area2D area)
		{
			if (!Enabled || _character == null || area == null)
			{
				return;
			}

			// Cấu trúc: Player -> WeaponSprite -> Hitbox (Area2D)
			var weaponSprite = area.GetParent() as Node2D;
			if (weaponSprite == null)
			{
				return;
			}

			var player = weaponSprite.GetParent() as global::Player;
			if (player == null || player == _character)
			{
				return;
			}

			if (!player.IsAttackHitboxActive())
			{
				return;
			}

			EnsureNoBodyCollisionWith(player);
			_playerAggressor = player;
			_targetEnemy = player;
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!Enabled || !_initialized || _character == null)
			{
				ReleaseCommands(); //đảm bảo không còn lệnh nào bị “kẹt” nếu AI bị tắt hoặc chưa init xong.
				return;
			}

			float dt = (float)delta;
			_attackTimer = Mathf.Max(0f, _attackTimer - dt);
			_enemyRefreshTimer -= dt;

			if (_stats != null && _stats.CurrentHP <= 0f)
			{
				ReleaseCommands(); //đảm bảo không còn lệnh nào bị “kẹt” nếu AI bị tắt hoặc chưa init xong.
				return;
			}

			if (_leader == null || !IsNodeUsable(_leader))
			{
				_leader = ResolveLeader();
				SetupPlayerBodyCollisionExceptions();
			}

			if (!IsNodeUsable(_playerAggressor))
			{
				_playerAggressor = null;
			}
			else if (_playerAggressor != null
				&& _character.GlobalPosition.DistanceTo(_playerAggressor.GlobalPosition) > EnemySearchRadius)
			{
				_playerAggressor = null;
			}

			if (IsNodeUsable(_playerAggressor) && _playerAggressor != _character)
			{
				_targetEnemy = _playerAggressor;
			}

			if (_playerAggressor == null && (_enemyRefreshTimer <= 0f || !IsEnemyUsable(_targetEnemy)))
			{
				_enemyRefreshTimer = EnemyRefreshInterval;
				_targetEnemy = FindBestEnemy();
			}

			Node2D threat = FindBlockingThreat();
			if (ShouldBlockAfterReaction(threat, dt))
			{
				RunBlock(threat);
				return;
			}

			_character.SetBlocking(false);

			if (IsEnemyUsable(_targetEnemy))
			{
				RunCombat(_targetEnemy);
				return;
			}

			// Tạm thời: không follow leader/player -> đứng yên khi không có target.
			ReleaseCommands();
		}

		private global::Player ResolveCharacter()
		{
			string pathText = CharacterPath.ToString();
			if (!string.IsNullOrEmpty(pathText))
			{
				var fromPath = GetNodeOrNull<global::Player>(CharacterPath);
				if (fromPath != null)
				{
					return fromPath;
				}
			}

			return GetParentOrNull<global::Player>();
		}

		private Node2D ResolveLeader()
		{
			string pathText = LeaderPath.ToString();
			if (!string.IsNullOrEmpty(pathText))
			{
				var fromPath = GetNodeOrNull<Node2D>(LeaderPath);
				if (fromPath != null && fromPath != _character)
				{
					return fromPath;
				}
			}

			SceneTree tree = GetTree();
			if (tree == null)
			{
				return null;
			}

			foreach (Node node in tree.GetNodesInGroup("Player"))
			{
				if (node == _character)
				{
					continue;
				}

				if (node is global::Player player)
				{
					return player;
				}
			}

			return null;
		}

		private void AutoEquipStarterWeapon()
		{
			EquipmentManager equipment = _character.GetEquipmentManager();
			if (equipment == null || equipment.HasWeaponEquipped)
			{
				return;
			}

			EquipmentItemData starterWeapon = GD.Load<EquipmentItemData>(StarterWeaponPath);
			if (starterWeapon == null)
			{
				GD.PrintErr($"[HyouAI] Cannot load starter weapon: {StarterWeaponPath}");
				return;
			}

			equipment.EquipItem(starterWeapon);
		}

		private void RunCombat(Node2D enemy)
		{
			//Tính distance, quay mặt về enemy, rồi quyết định reposition/chase/attack dựa trên distance.
			float distance = _character.GlobalPosition.DistanceTo(enemy.GlobalPosition);
			_character.FaceToward(enemy.GlobalPosition);

			if (distance < RepositionRange)
			{
				SetState(AIState.Reposition); // reposition là lùi lại để tạo khoảng cách, không phải chạy vòng vì Hyou có thể phản ứng nhanh với tấn công của enemy.
				MoveAwayFrom(enemy, false); // lùi lại nhưng không chạy để giữ khả năng phản ứng nhanh nếu enemy tấn công.
				return;
			}

			if (distance <= AttackRange)
			{
				SetState(AIState.Attack); // tấn công nếu trong range, không chase nữa vì Hyou có thể phản ứng nhanh với tấn công của enemy.
				_character.StopMoveInput();

				if (_attackTimer <= 0f && !_character.IsBlocking)
				{
					_character.RequestAttack();
					_attackTimer = AttackCooldown; 
				}
				return;
			}

			SetState(AIState.ChaseEnemy); // chase nếu quá xa để tấn công, nhưng không phải quá xa vì Hyou có thể phản ứng nhanh với tấn công của enemy.
			Vector2 direction = (enemy.GlobalPosition - _character.GlobalPosition).Normalized(); // hướng di chuyển về phía enemy
			_character.SetMoveInput(direction, distance > AttackRange * 2f); // chạy nếu quá xa để tấn công, đi bộ nếu gần để giữ khả năng phản ứng nhanh nếu enemy tấn công.
		}

		private void RunBlock(Node2D threat)
		{
			SetState(AIState.Block);
			_character.FaceToward(threat.GlobalPosition);
			_character.StopMoveInput();
			_character.SetBlocking(true);
		}

		private void RunFollowPlayer()
		{
			SetState(AIState.FollowPlayer);

			if (_leader == null || !IsNodeUsable(_leader))
			{
				ReleaseCommands();
				return;
			}

			float distance = _character.GlobalPosition.DistanceTo(_leader.GlobalPosition);
			_character.FaceToward(_leader.GlobalPosition);

			if (distance <= FollowStopDistance)
			{
				_character.StopMoveInput();
				return;
			}

			Vector2 direction = (_leader.GlobalPosition - _character.GlobalPosition).Normalized();
			_character.SetMoveInput(direction, distance > FollowDistance * 1.75f);
		}

		private void MoveAwayFrom(Node2D enemy, bool wantsRun)
		{
			Vector2 away = (_character.GlobalPosition - enemy.GlobalPosition).Normalized();
			if (away == Vector2.Zero)
			{
				away = -_character.FacingDirection;
			}

			_character.SetMoveInput(away, wantsRun);
		}

		private bool ShouldBlockAfterReaction(Node2D threat, float delta)
		{
			if (threat == null)
			{
				_blockThreat = null;
				_blockReactionTimer = 0f;
				return false;
			}

			if (threat != _blockThreat)
			{
				_blockThreat = threat;
				_blockReactionTimer = _rng.RandfRange(ReactionDelayMin, ReactionDelayMax);
				return false;
			}

			_blockReactionTimer -= delta;
			return _blockReactionTimer <= 0f;
		}

		private Node2D FindBlockingThreat()
		{
			Node2D bestThreat = null;
			float bestDistance = float.MaxValue;

			foreach (Node2D enemy in EnumerateEnemies())
			{
				if (!IsThreatening(enemy))
				{
					continue;
				}

				// Nếu có nhiều threat, ưu tiên threat gần nhất để block.
				float distance = _character.GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestThreat = enemy;
				}
			}

			return bestThreat;
		}

		private bool IsThreatening(Node2D enemy)
		{
			if (!IsEnemyUsable(enemy) || _stats == null || _stats.CurrentStamina < MinStaminaToBlock)
			{
				return false;
			}

			float distance = _character.GlobalPosition.DistanceTo(enemy.GlobalPosition);
			if (distance > BlockRange)
			{
				return false;
			}

			if (enemy is Slime1 slime)
			{
				if (!slime.IsAttacking)
				{
					return false;
				}

				Vector2 toHyou = (_character.GlobalPosition - slime.GlobalPosition).Normalized();
				float facingDot = slime.AttackDirection.Dot(toHyou);
				return facingDot > ThreatFacingDot;
			}

			return false;
		}

		private Node2D FindBestEnemy()
		{
			Node2D bestEnemy = null;
			float bestScore = float.MaxValue;

			foreach (Node2D enemy in EnumerateEnemies())
			{
				if (!IsEnemyUsable(enemy))
				{
					continue;
				}

				float distanceToHyou = _character.GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distanceToHyou > EnemySearchRadius)
				{
					continue;
				}

				float distanceToLeader = _leader != null && IsNodeUsable(_leader)
					? _leader.GlobalPosition.DistanceTo(enemy.GlobalPosition)
					: distanceToHyou;
				float score = Mathf.Min(distanceToHyou, distanceToLeader + FollowDistance);

				if (score < bestScore)
				{
					bestScore = score;
					bestEnemy = enemy;
				}
			}

			return bestEnemy;
		}

		private Godot.Collections.Array<Node2D> EnumerateEnemies()
		{
			var enemies = new Godot.Collections.Array<Node2D>();
			SceneTree tree = GetTree();
			if (tree == null)
			{
				return enemies;
			}

			foreach (Node node in tree.GetNodesInGroup("Enemies"))
			{
				if (node is Node2D enemy)
				{
					enemies.Add(enemy);
				}
			}

			if (enemies.Count == 0)
			{
				Node root = tree.CurrentScene ?? tree.Root;
				CollectSlimes(root, enemies);
			}

			return enemies;
		}

		private void CollectSlimes(Node node, Godot.Collections.Array<Node2D> enemies)
		{
			if (node == null)
			{
				return;
			}

			if (node is Slime1 slime)
			{
				enemies.Add(slime);
			}

			foreach (Node child in node.GetChildren())
			{
				CollectSlimes(child, enemies);
			}
		}

		private bool IsEnemyUsable(Node2D enemy)
		{
			if (!IsNodeUsable(enemy) || enemy == _character)
			{
				return false;
			}

			if (enemy is Slime1 slime && slime.CurrentHP <= 0)
			{
				return false;
			}

			return true;
		}

		private bool IsNodeUsable(Node node)
		{
			return node != null
				&& GodotObject.IsInstanceValid(node)
				&& node.IsInsideTree()
				&& !node.IsQueuedForDeletion();
		}

		private void ReleaseCommands()
		{
			if (_character == null)
			{
				return;
			}

			_character.StopMoveInput();
			_character.SetBlocking(false);
		}

		private void SetState(AIState state)
		{
			_state = state;
		}
	}
}
