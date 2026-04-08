using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

public partial class Player : CharacterBody2D
{
[Export] public float Speed { get; set; } = 100f;
[Export] public float RunSpeed { get; set; } = 200f;
[Export] public float RunStaminaCost { get; set; } = 20f;
[Export] public float MinStaminaToRun { get; set; } = 40f;
[Export] public float Acceleration { get; set; } = 1200f;
[Export] public float Deceleration { get; set; } = 1600f;
[Export] public float ComboContinueWindow { get; set; } = 1f;
[Export] public NodePath BodyPath { get; set; } = "Body";
[Export] public int StopFrameIndex { get; set; } = 0;
[Export] public float AttackLungeSpeed { get; set; } = 60f;

private bool _isExhausted = false;

private AnimatedSprite2D _body;
private AnimatedSprite2D _weaponSprite;
private Area2D _hurtbox;
private Area2D _hitbox;
private string _lastMoveAnim = "go_down";
private string _lastDirection = "down";
private bool _wasMoving = false;
private bool _isAttacking = false;
private int _queuedAttackCount = 0;
private int _comboHitCount = 0;
private int _activeAttackStep = 0;
private int _attackStartFrame = 0;
private int _attackEndFrame = 0;
private string _activeAttackAnim = "";
private int _weaponAttackEndFrame = 0;
private string _activeWeaponAttackAnim = "";
private bool _isCompletingAttackStep = false;
private bool _isWaitingSecondHit = false;
private float _secondHitWaitTimer = 0f;
private PlayerStats _stats;
private EquipmentManager _equipMgr;
private InventoryManager _inventory;

private float _bodyBaseSpeedScale = 1f;
private float _weaponBaseSpeedScale = 1f;

public override void _Ready()
{
_weaponSprite = GetNodeOrNull<AnimatedSprite2D>("WeaponSprite");
_hurtbox = GetNodeOrNull<Area2D>("Hurtbox");
_hitbox = GetNodeOrNull<Area2D>("WeaponSprite/Hitbox");
_stats = GetNodeOrNull<PlayerStats>("PlayerStats");
_equipMgr = GetNodeOrNull<EquipmentManager>("EquipmentManager");

ResolveBodySprite();

_inventory = GetNodeOrNull<InventoryManager>("InventoryManager");
if (_inventory == null)
{
_inventory = new InventoryManager();
_inventory.Name = "InventoryManager";
AddChild(_inventory);
}

_body?.Play("Idle");

if (_equipMgr != null)
{
_equipMgr.WeaponVisualChanged += OnWeaponVisualChanged;
}

if (_body != null)
{
_body.FrameChanged += OnBodyFrameChanged;
}
if (_weaponSprite != null)
{
_weaponSprite.FrameChanged += OnWeaponFrameChanged;
}

if (_hurtbox != null)
{
_hurtbox.BodyEntered += OnHurtboxBodyEntered;
_hurtbox.AreaEntered += OnHurtboxAreaEntered;
}

if (_body != null)
{
_bodyBaseSpeedScale = _body.SpeedScale;
}
if (_weaponSprite != null)
{
_weaponBaseSpeedScale = _weaponSprite.SpeedScale;
}
}

public override void _PhysicsProcess(double delta)
{
if (Input.IsActionJustPressed("attack"))
{
QueueAttack();
}

if (_isAttacking)
{
if (_isWaitingSecondHit)
{
_secondHitWaitTimer -= (float)delta;
if (_queuedAttackCount > 0)
{
_queuedAttackCount--;
StartAttack(2);
}
else if (_secondHitWaitTimer <= 0f)
{
FinishAttack();
}
}

Velocity = Velocity.MoveToward(Vector2.Zero, Deceleration * (float)delta);
MoveAndSlide();
return;
}

Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
bool hasInput = inputDir != Vector2.Zero;
if (hasInput)
{
inputDir = inputDir.Normalized();
}

bool wantsToRun = Input.IsKeyPressed(Key.Shift);
if (InputMap.HasAction("run"))
{
wantsToRun = wantsToRun || Input.IsActionPressed("run");
}

float staminaCostThisFrame = RunStaminaCost * (float)delta;

if (_stats != null)
{
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

if (hasInput && wantsToRun && !_isExhausted && _stats != null && _stats.CurrentStamina > 0)
{
canRun = true;
_stats.ConsumeStamina(staminaCostThisFrame);
}

float targetSpeed = canRun ? RunSpeed : Speed;
Vector2 targetVelocity = hasInput ? inputDir * targetSpeed : Vector2.Zero;
float velocityStep = (hasInput ? Acceleration : Deceleration) * (float)delta;
Velocity = Velocity.MoveToward(targetVelocity, velocityStep);

if (!hasInput && Velocity.LengthSquared() < 1f)
{
Velocity = Vector2.Zero;
}

bool moving = Velocity.LengthSquared() > 1f;

if (moving)
{
Vector2 animDir = hasInput ? inputDir : Velocity.Normalized();

string action = canRun ? "run" : "go";
string direction = ResolveDirection(animDir);
string anim = $"{action}_{direction}";

_lastDirection = direction;

if (_body != null && (_body.Animation != anim || !_body.IsPlaying()))
{
if (_body.SpriteFrames.HasAnimation(anim))
{
_body.Play(anim);
if (_body.SpriteFrames.GetFrameCount(anim) > 1)
{
_body.Frame = 1;
}
}
}
_lastMoveAnim = anim;

if (_weaponSprite != null && _weaponSprite.Visible)
{
_weaponSprite.Visible = false;
}
}
else
{
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

// Resolve body để đảm bảo nó luôn tồn tại và có thể được truy cập, ngay cả khi 
private void ResolveBodySprite()
{
string bodyPathText = BodyPath.ToString(); // Dùng để lưu giá trị gốc của BodyPath trước khi có thể thay đổi nó nếu cần thiết
if (string.IsNullOrEmpty(bodyPathText))
{
bodyPathText = "Body";
}

_body = GetNodeOrNull<AnimatedSprite2D>(bodyPathText);
if (_body != null)
{
if (BodyPath.ToString() != bodyPathText)
{
BodyPath = new NodePath(bodyPathText);
}
return;
}

PackedScene bodyScene = _stats?.ConfigData?.BodyScene;
if (bodyScene == null)
{
GD.PrintErr("[Player] Body scene is missing in CharacterConfig.");
return;
}

var bodyInstance = bodyScene.Instantiate<AnimatedSprite2D>();
if (bodyInstance == null)
{
GD.PrintErr("[Player] BodyScene root must be AnimatedSprite2D.");
return;
}

bodyInstance.Name = "Body";
AddChild(bodyInstance);
_body = bodyInstance;
BodyPath = new NodePath("Body");
}

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

string bodyAnim = $"sword_{attackDir}";
if (_body != null && _body.SpriteFrames.HasAnimation(bodyAnim))
{
int bodyFrameCount = _body.SpriteFrames.GetFrameCount(bodyAnim);
if (bodyFrameCount > 0)
{
_attackStartFrame = Mathf.Clamp(startFrame, 0, bodyFrameCount - 1);
_attackEndFrame = Mathf.Clamp(endFrame, _attackStartFrame, bodyFrameCount - 1);
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

GD.Print($"[Player] Attack step {attackStep}! Direction: {attackDir} | Frame {_attackStartFrame}->{_attackEndFrame}");

if (!playedBodyAttack)
{
FinishAttack();
return;
}

if (!playedWeaponAttack)
{
_activeWeaponAttackAnim = "";
}

if (_hitbox != null)
{
_hitbox.Monitoring = true;
}
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
if (_body.Frame < _attackEndFrame) return;

_body.Stop();
_body.FrameProgress = 1f;

CompleteAttackStep();
}

private void CompleteAttackStep()
{
if (!_isAttacking || _isCompletingAttackStep) return;

_isCompletingAttackStep = true;

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

private void OnHurtboxBodyEntered(Node2D body)
{
// TODO: Gọi TakeDamage từ dữ liệu body (enemy, projectile...)
}

private void OnHurtboxAreaEntered(Area2D area)
{
// TODO: Gọi TakeDamage khi trúng Hitbox_Enemy
}

private void FinishAttack()
{
_isAttacking = false;
_queuedAttackCount = 0;
_comboHitCount = 0;
_activeAttackStep = 0;
_attackStartFrame = 0;
_attackEndFrame = 0;
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

if (_hitbox != null)
{
_hitbox.Monitoring = false;
}
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

private string ResolveDirection(Vector2 dir)
{
string vDir = "";
if (dir.Y > 0.2f) vDir = "down";
else if (dir.Y < -0.2f) vDir = "up";

string hDir = "";
if (dir.X > 0.2f) hDir = "right";
else if (dir.X < -0.2f) hDir = "left";

if (vDir == "" && hDir == "")
{
return _lastDirection;
}

return (vDir != "" && hDir != "") ? $"{vDir}_{hDir}" : $"{vDir}{hDir}";
}

private void OnWeaponVisualChanged(PackedScene weaponScene)
{
if (_weaponSprite == null) return;

// Xóa hitbox cũ (nếu có)
if (_hitbox != null)
{
_hitbox.QueueFree();
_hitbox = null;
}

if (weaponScene == null)
{
_weaponSprite.SpriteFrames = null;
_weaponSprite.Visible = false;
GD.Print("[Player] Weapon visual cleared.");
return;
}

Node weaponInstance = weaponScene.Instantiate();
if (weaponInstance is AnimatedSprite2D spriteSource)
{
_weaponSprite.SpriteFrames = spriteSource.SpriteFrames;
_weaponSprite.Visible = false;

// Tìm Hitbox trong weapon scene (nếu có) và gắn sang WeaponSprite
Area2D newHitbox = weaponInstance.GetNodeOrNull<Area2D>("Hitbox");
if (newHitbox != null && _weaponSprite != null)
{
newHitbox.GetParent()?.RemoveChild(newHitbox);
_weaponSprite.AddChild(newHitbox);
_hitbox = newHitbox;
}

weaponInstance.QueueFree();
GD.Print("[Player] Weapon visual (and hitbox) loaded.");
}
}

public void EquipFromInventory(string itemId)
{
if (_inventory == null || _equipMgr == null) return;

var item = _inventory.GetItem(itemId);
if (item != null)
{
_equipMgr.EquipItem(item);
GD.Print($"[Player] Equipped {item.ItemName} from inventory.");
}
}

public void AutoEquipStarterWeapon()
{
CallDeferred(nameof(DoAutoEquip));
}

private void DoAutoEquip()
{
EquipFromInventory("weapon_wood_sword");
}
}
