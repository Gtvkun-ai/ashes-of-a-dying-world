using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.HUD;

namespace AshesofaDyingWorld.Entities.NPC
{
	public partial class NpcCharacter : global::Player
	{
		[Export] public bool IsRecruitable { get; set; } = true;
		[Export] public bool RemoveFromWorldOnDeath { get; set; } = true;
		[Export] public float KnockbackForce { get; set; } = 120f;

		private Area2D _npcHurtbox;
		private PlayerStats _npcStats;
		private bool _isRecruited;

		public override void _Ready()
		{
			UsePlayerInput = false;
			base._Ready();

			_npcStats = GetStatsNode();
			_npcHurtbox = GetNodeOrNull<Area2D>("Hurtbox");

			if (_npcHurtbox != null)
			{
				_npcHurtbox.AreaEntered += OnNpcHurtboxAreaEntered;
			}

		}

		public void Recruit()
		{
			if (!IsRecruitable || _isRecruited || _npcStats == null)
			{
				return;
			}

			PlayerManager.Instance?.RegisterMember(_npcStats);
			_isRecruited = true;
		}

		public void TakeDamage(float amount, Node2D source = null)
		{
			if (_npcStats == null || amount <= 0f || _npcStats.CurrentHP <= 0f)
			{
				return;
			}

			_npcStats.ChangeHP(-amount);
			DamageNumberService.GetOrCreate(GetTree())?.ShowDamage(this, amount);

			if (source != null && KnockbackForce > 0f)
			{
				Vector2 direction = (GlobalPosition - source.GlobalPosition).Normalized();
				Velocity += direction * KnockbackForce;
			}

			if (_npcStats.CurrentHP <= 0f)
			{
				Die();
			}
		}

		private void OnNpcHurtboxAreaEntered(Area2D area)
		{
			var weaponSprite = area.GetParent() as Node2D;
			if (weaponSprite == null)
			{
				return;
			}

			var player = weaponSprite.GetParent() as global::Player;
			if (player == null || player == this || !player.IsAttackHitboxActive())
			{
				return;
			}

			if (player.IsInGroup("Player") && IsInGroup("Player"))
			{
				return;
			}

			float damage = 1f;
			var playerStats = player.GetStatsNode();
			if (playerStats != null)
			{
				damage = Mathf.Max(1f, playerStats.AttackDamage);
			}

			TakeDamage(damage, player);
		}

		private void Die()
		{
			if (_npcStats != null)
			{
				PlayerManager.Instance?.UnregisterMember(_npcStats);
			}

			if (RemoveFromWorldOnDeath)
			{
				QueueFree();
			}
		}
	}
}
