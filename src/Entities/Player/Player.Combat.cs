using Godot;
using AshesofaDyingWorld.Core.Data;

public partial class Player
{
private void QueueAttack()
{
if (_equipMgr == null || !_equipMgr.HasWeaponEquipped)
{
GD.Print("[Player] Chua trang bi vu khi!");
return;
}

const int maxComboHits = 2;

if (_isAttacking)
{
int totalPlannedHits = _comboHitCount + _queuedAttackCount;
if (totalPlannedHits < maxComboHits)
{
_queuedAttackCount++;
}
return;
}

_comboHitCount = 0;
_queuedAttackCount = 0;
StartAttack(1);
}

private void StartAttack(int attackStep)
{
if (_equipMgr == null || !_equipMgr.HasWeaponEquipped)
{
FinishAttack();
return;
}

// Tiêu hao Stamina dựa trên độ nặng của vũ khí
if (!TryConsumeAttackStamina(attackStep))
{
return;
}

_isAttacking = true;
_activeAttackStep = attackStep;
_comboHitCount = attackStep;
_isWaitingSecondHit = false;
_secondHitWaitTimer = 0f;
float attackSpeedMult = 1f;
if (_stats != null)
{
attackSpeedMult = _stats.AttackSpeed;
}

Vector2 lungeDir = GetAttackDirectionVector();
if (lungeDir != Vector2.Zero)
{
Velocity = lungeDir * AttackLungeSpeed * attackSpeedMult;
}
else
{
Velocity = Vector2.Zero;
}

if (_body != null)
{
_body.SpeedScale = _bodyBaseSpeedScale * attackSpeedMult;
}
if (_weaponSprite != null)
{
_weaponSprite.SpeedScale = _weaponBaseSpeedScale * attackSpeedMult;
}

string attackDir = GetAttackDirection();
UpdateHitboxForDirection(attackDir);
bool playedBodyAttack = false;
bool playedWeaponAttack = false;
if (!TryGetAttackFrameRange(attackStep, out int startFrame, out int endFrame))
{
FinishAttack();
return;
}

if (!TryGetAttackHitFrameRange(attackStep, out int hitStartFrame, out int hitEndFrame))
{
FinishAttack();
return;
}

string bodyAnim = $"sword_{attackDir}";
if (_body != null && _body.SpriteFrames.HasAnimation(bodyAnim))
{
int bodyFrameCount = _body.SpriteFrames.GetFrameCount(bodyAnim);
if (bodyFrameCount > 0)
{
_attackStartFrame = Mathf.Clamp(startFrame, 0, bodyFrameCount - 1);
_attackEndFrame = Mathf.Clamp(endFrame, _attackStartFrame, bodyFrameCount - 1);
_attackHitStartFrame = Mathf.Clamp(hitStartFrame, _attackStartFrame, _attackEndFrame);
_attackHitEndFrame = Mathf.Clamp(hitEndFrame, _attackHitStartFrame, _attackEndFrame);
_activeAttackAnim = bodyAnim;
_body.Play(bodyAnim);
_body.Frame = _attackStartFrame;
_body.FrameProgress = 0f;
playedBodyAttack = true;
}
}

if (_weaponSprite != null && _weaponSprite.SpriteFrames != null)
{
string weaponAnim = $"sword_{attackDir}";
if (_weaponSprite.SpriteFrames.HasAnimation(weaponAnim))
{
int weaponFrameCount = _weaponSprite.SpriteFrames.GetFrameCount(weaponAnim);
int weaponStartFrame = Mathf.Clamp(startFrame, 0, Mathf.Max(0, weaponFrameCount - 1));
_weaponAttackEndFrame = Mathf.Clamp(endFrame, weaponStartFrame, Mathf.Max(0, weaponFrameCount - 1));
_activeWeaponAttackAnim = weaponAnim;
_weaponSprite.Visible = true;
_weaponSprite.Play(weaponAnim);
_weaponSprite.Frame = weaponStartFrame;
_weaponSprite.FrameProgress = 0f;
playedWeaponAttack = true;
}
}

if (!playedBodyAttack)
{
FinishAttack();
return;
}

if (!playedWeaponAttack)
{
_activeWeaponAttackAnim = "";
}
}

private float ComputeAttackStaminaCost(int attackStep)
{
// Trọng lượng vũ khí: 1 = trung bình, >1 = nặng, <1 = nhẹ
float weaponWeight = 1f;
if (_equipMgr != null)
{
var mainWeapon = _equipMgr.GetEquippedItem(EquipmentSlot.MainHand);
if (mainWeapon != null && mainWeapon.WeaponWeight > 0f)
{
weaponWeight = mainWeapon.WeaponWeight;
}
}

// Đòn thứ 2 trong combo có thể tốn thêm một chút thể lực
float stepMultiplier = attackStep == 2 ? 1.2f : 1f;
return BaseAttackStaminaCost * weaponWeight * stepMultiplier;
}

private bool TryConsumeAttackStamina(int attackStep)
{
if (_stats == null)
{
return true;
}

float cost = ComputeAttackStaminaCost(attackStep);
if (!_stats.ConsumeStamina(cost))
{
GD.Print($"[Player] Not enough stamina to attack. Need {cost:F1}, current={_stats.CurrentStamina:F1}");

// Nếu đang trong combo mà không đủ thể lực cho hit tiếp theo thì kết thúc combo
if (_isAttacking)
{
FinishAttack();
}
return false;
}

return true;
}

private string GetAttackDirection()
{
if (_lastDirection.Contains("down")) return "down";
if (_lastDirection.Contains("up")) return "up";
if (_lastDirection.Contains("left")) return "left";
if (_lastDirection.Contains("right")) return "right";
return "down";
}

private void UpdateHitboxForDirection(string attackDir)
{
if (_hitbox == null) return;

float offset = 20f; // chỉnh cho hợp tầm với vũ khí
Vector2 localPos = Vector2.Zero;
switch (attackDir)
{
case "up":
localPos = new Vector2(0, -offset);
break;
case "down":
localPos = new Vector2(0, offset);
break;
case "left":
localPos = new Vector2(-offset, 0);
break;
case "right":
localPos = new Vector2(offset, 0);
break;
}

_hitbox.Position = localPos;
}

private Vector2 GetAttackDirectionVector()
{
string dir = GetAttackDirection();
switch (dir)
{
case "up":
return Vector2.Up;
case "down":
return Vector2.Down;
case "left":
return Vector2.Left;
case "right":
return Vector2.Right;
case "up_left":
return (Vector2.Up + Vector2.Left).Normalized();
case "up_right":
return (Vector2.Up + Vector2.Right).Normalized();
case "down_left":
return (Vector2.Down + Vector2.Left).Normalized();
case "down_right":
return (Vector2.Down + Vector2.Right).Normalized();
default:
return Vector2.Zero;
}
}

private void OnBodyFrameChanged()
{
if (!_isAttacking || _body == null) return;
if (_body.Animation != _activeAttackAnim) return;

bool isHitFrame = _body.Frame >= _attackHitStartFrame && _body.Frame <= _attackHitEndFrame;
SetHitboxActive(isHitFrame);
if (_body.Frame < _attackEndFrame) return;

_body.Stop();
_body.FrameProgress = 1f;

CompleteAttackStep();
}

private void CompleteAttackStep()
{
if (!_isAttacking || _isCompletingAttackStep) return;

_isCompletingAttackStep = true;
SetHitboxActive(false);

if (_activeAttackStep == 1)
{
if (_queuedAttackCount > 0)
{
_queuedAttackCount--;
StartAttack(2);
}
else
{
_isWaitingSecondHit = true;
float attackSpeedMult = _stats != null ? _stats.AttackSpeed : 1f;
_secondHitWaitTimer = ComboContinueWindow / Mathf.Max(0.1f, attackSpeedMult);
}

_isCompletingAttackStep = false;
return;
}

FinishAttack();

_isCompletingAttackStep = false;
}

private void OnWeaponAnimationFinished()
{
// Do nothing: combo now resolves on explicit frame windows.
}

private void OnWeaponFrameChanged()
{
if (!_isAttacking || _weaponSprite == null) return;
if (_activeWeaponAttackAnim == "") return;
if (_weaponSprite.Animation != _activeWeaponAttackAnim) return;
if (_weaponSprite.Frame < _weaponAttackEndFrame) return;

_weaponSprite.Stop();
_weaponSprite.FrameProgress = 1f;
_weaponSprite.Visible = false;
}

private void FinishAttack()
{
_isAttacking = false;
_queuedAttackCount = 0;
_comboHitCount = 0;
_activeAttackStep = 0;
_attackStartFrame = 0;
_attackEndFrame = 0;
_attackHitStartFrame = 0;
_attackHitEndFrame = 0;
_activeAttackAnim = "";
_weaponAttackEndFrame = 0;
_activeWeaponAttackAnim = "";
_isWaitingSecondHit = false;
_secondHitWaitTimer = 0f;

if (_weaponSprite != null)
{
_weaponSprite.Stop();
_weaponSprite.Visible = false;
}

if (_body != null)
{
_body.SpeedScale = _bodyBaseSpeedScale;
string idleAnim = _lastMoveAnim.Replace("run", "go");
if (_body.SpriteFrames.HasAnimation(idleAnim))
{
_body.Animation = idleAnim;
_body.Frame = StopFrameIndex;
}
_body.Stop();
}

if (_weaponSprite != null)
{
_weaponSprite.SpeedScale = _weaponBaseSpeedScale;
}

SetHitboxActive(false);
}

private bool TryGetAttackFrameRange(int attackStep, out int startFrame, out int endFrame)
{
startFrame = 0;
endFrame = 0;

if (attackStep == 1)
{
startFrame = 0;
endFrame = 4;
return true;
}

if (attackStep == 2)
{
startFrame = 5;
endFrame = 8;
return true;
}

return false;
}

private bool TryGetAttackHitFrameRange(int attackStep, out int startFrame, out int endFrame)
{
startFrame = 0;
endFrame = 0;

if (attackStep == 1)
{
startFrame = 2;
endFrame = 3;
return true;
}

if (attackStep == 2)
{
startFrame = 6;
endFrame = 7;
return true;
}

return false;
}
}
