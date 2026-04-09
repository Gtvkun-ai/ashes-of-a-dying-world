using Godot;
using AshesofaDyingWorld.Core.Data;

public partial class Player
{
private void OnHurtboxBodyEntered(Node2D body)
{
// TODO: Gọi TakeDamage từ dữ liệu body (enemy, projectile...)
}

private void OnHurtboxAreaEntered(Area2D area)
{
// TODO: Gọi TakeDamage khi trúng Hitbox_Enemy
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
public float ReceiveMeleeHit(float rawDamage, Vector2 attackerDirection)
{
if (_stats == null || rawDamage <= 0f)
{
return 0f;
}

float hpDamage = rawDamage;

GD.Print($"[Player] ReceiveMeleeHit: raw={rawDamage}, isBlocking={_isBlocking}, attackerDir={attackerDirection}, facing={GetAttackDirectionVector()}, staminaBefore={_stats.CurrentStamina}");

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

GD.Print($"[Player] Blocked hit: blockedDamage={blockedDamage}, staminaCost={staminaCost}, usedStamina={usedStamina}, hpDamage={hpDamage}, staminaAfter={_stats.CurrentStamina}");
}
else
{
// Không còn stamina để block: vẫn giảm nhẹ sát thương nhờ tư thế thủ
float damageThrough = rawDamage * 0.75f;
hpDamage = Mathf.Max(0f, damageThrough);
GD.Print($"[Player] No stamina to block, reduced damage to {hpDamage}");
}
}

if (hpDamage > 0f)
{
_stats.ChangeHP(-hpDamage);
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
