using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.UI.HUD;

public partial class Player
{
private void OnHurtboxBodyEntered(Node2D body)
{
// TODO: Gọi TakeDamage từ dữ liệu body (enemy, projectile...)
}

private void OnHurtboxAreaEntered(Area2D area)
{
	if (_stats == null || area == null)
	{
		return;
	}

	// Melee hitbox từ nhân vật khác (Player -> WeaponSprite -> Hitbox)
	var weaponSprite = area.GetParent() as Node2D;
	if (weaponSprite == null)
	{
		return;
	}

	var attacker = weaponSprite.GetParent() as Player;
	if (attacker == null || attacker == this)
	{
		return;
	}

	if (!attacker.IsAttackHitboxActive())
	{
		return;
	}

	if (!attacker.TryRegisterAttackHit(this))
	{
		return;
	}

	// Vector từ người bị đánh -> kẻ tấn công (dùng cho check block phía trước)
	Vector2 attackerDirection = (attacker.GlobalPosition - GlobalPosition).Normalized();
	ReceiveMeleeHit(attacker.GetMeleeAttackDamage(), attackerDirection);
}

public bool TryRegisterAttackHit(Node target)
{
	if (!_isAttacking || _hitbox == null || target == null)
	{
		return false;
	}

	if (_attackHitTargets.Contains(target))
	{
		return false;
	}

	_attackHitTargets.Add(target);
	return true;
}

private void ApplyCurrentHitboxOverlaps()
{
	if (!IsAttackHitboxActive())
	{
		return;
	}

	_hitbox.ForceUpdateTransform();

	foreach (Area2D area in _hitbox.GetOverlappingAreas())
	{
		ApplyHitboxOverlap(area);
	}
}

private void ApplyHitboxOverlap(Area2D area)
{
	if (area == null)
	{
		return;
	}

	if (area.GetParent() is Slime1 slime)
	{
		slime.ReceivePlayerAttack(this);
		return;
	}

	if (area.GetParent() is Player target && target != this)
	{
		if (!TryRegisterAttackHit(target))
		{
			return;
		}

		Vector2 attackerDirection = (GlobalPosition - target.GlobalPosition).Normalized();
		target.ReceiveMeleeHit(GetMeleeAttackDamage(), attackerDirection);
	}
}

private float GetMeleeAttackDamage()
{
	if (_stats == null)
	{
		return 1f;
	}

	return Mathf.Max(1f, _stats.AttackDamage);
}

// Gọi từ enemy / môi trường để đẩy lùi player nhưng giữ nguyên animation hiện tại
public void ApplyExternalForce(Vector2 force, float animLockTime = -1f)
{
Velocity += force;

// Nếu không truyền thời lượng riêng, dùng giá trị export mặc định
float lockTime = animLockTime >= 0f ? animLockTime : KnockbackAnimLockTime;
if (lockTime > 0f)
{
_knockbackAnimTimer = Mathf.Max(_knockbackAnimTimer, lockTime);
}
}

// Gọi từ enemy khi chuẩn bị gây sát thương tay đôi (melee)
// Trả về lượng sát thương thực sự áp vào HP sau khi tính block/stamina
public virtual float ReceiveMeleeHit(float rawDamage, Vector2 attackerDirection)
{
if (_stats == null || rawDamage <= 0f)
{
return 0f;
}

float hpDamage = rawDamage;


// Nếu đang block và hướng tấn công nằm phía trước
if (_isBlocking && IsAttackInFront(attackerDirection))
{
// Lấy thông tin block từ vũ khí
float weaponBlockReduction = 0.5f;
float staminaPerDamage = 1.0f;
if (_equipMgr != null)
{
var mainWeapon = _equipMgr.GetEquippedItem(EquipmentSlot.MainHand);
if (mainWeapon != null)
{
weaponBlockReduction = mainWeapon.BlockDamageReduction;
staminaPerDamage = mainWeapon.BlockStaminaPerDamage;
}
}

// Ảnh hưởng của Strength & Defense lên hiệu quả giảm sát thương khi block
int str = 0;
int def = 0;
if (_stats.FinalAttributes != null)
{
_stats.FinalAttributes.TryGetValue(AttributeType.Strength, out str);
_stats.FinalAttributes.TryGetValue(AttributeType.Defense, out def);
}

// Mỗi 10 STR/DEF cộng thêm ~5% hiệu quả giảm sát thương, tối đa 95%
float attrBonus = ((str + def) / 10f) * 0.05f;
float effectiveBlockReduction = Mathf.Clamp(weaponBlockReduction + attrBonus, 0f, 0.95f);

// Lượng sát thương lý thuyết được chặn
float blockedDamage = rawDamage * effectiveBlockReduction;
float staminaCost = blockedDamage * staminaPerDamage;

if (staminaCost > 0f && _stats.CurrentStamina > 0f)
{
// Chỉ chặn được tương ứng với lượng stamina hiện có
float availableStamina = _stats.CurrentStamina;
float usedStamina = Mathf.Min(availableStamina, staminaCost);
_stats.ChangeStamina(-usedStamina);

float protectedRatio = staminaCost > 0f ? usedStamina / staminaCost : 1f;
float effectiveBlockedDamage = blockedDamage * protectedRatio;
float damageThrough = rawDamage - effectiveBlockedDamage;
hpDamage = Mathf.Max(0f, damageThrough);

}
else
{
// Không còn stamina để block: vẫn giảm nhẹ sát thương nhờ tư thế thủ
float damageThrough = rawDamage * 0.75f;
hpDamage = Mathf.Max(0f, damageThrough);
}
}

if (hpDamage > 0f)
{
_stats.ChangeHP(-hpDamage);
DamageNumberService.GetOrCreate(GetTree())?.ShowDamage(this, hpDamage);
}
return hpDamage;
}

private bool IsAttackInFront(Vector2 attackerDirection)
{
// Hướng đang nhìn
Vector2 facing = GetAttackDirectionVector();
if (facing == Vector2.Zero)
{
return true; // nếu không xác định được thì cho phép block
}

attackerDirection = attackerDirection.Normalized();
// Nếu góc giữa hướng nhìn và hướng tấn công <= 90 độ thì coi là phía trước
float dot = facing.Dot(attackerDirection);
return dot > 0f;
}
}
