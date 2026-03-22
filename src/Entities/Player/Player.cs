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
[Export] public NodePath BodyPath { get; set; } = "Body";
[Export] public int StopFrameIndex { get; set; } = 0;

private bool _isExhausted = false;

private AnimatedSprite2D _body;
private AnimatedSprite2D _weaponSprite;
private string _lastMoveAnim = "go_down";
private string _lastDirection = "down";
private bool _wasMoving = false;
private bool _wasRunning = false;
private bool _isAttacking = false;
private PlayerStats _stats;
private EquipmentManager _equipMgr;
private InventoryManager _inventory;

public override void _Ready()
{
_body = GetNodeOrNull<AnimatedSprite2D>(BodyPath);
_weaponSprite = GetNodeOrNull<AnimatedSprite2D>("WeaponSprite");
_stats = GetNodeOrNull<PlayerStats>("PlayerStats");
_equipMgr = GetNodeOrNull<EquipmentManager>("EquipmentManager");

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
_body.AnimationFinished += OnBodyAnimationFinished;
}
if (_weaponSprite != null)
{
_weaponSprite.AnimationFinished += OnWeaponAnimationFinished;
}
}

public override void _PhysicsProcess(double delta)
{
if (_isAttacking)
{
Velocity = Vector2.Zero;
MoveAndSlide();
return;
}

if (Input.IsActionJustPressed("attack"))
{
TryAttack();
return;
}

Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
bool moving = inputDir != Vector2.Zero;
bool wantsToRun = Input.IsKeyPressed(Key.Shift);
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

if (moving && wantsToRun && !_isExhausted && _stats != null && _stats.CurrentStamina > 0)
{
canRun = true;
_stats.ConsumeStamina(staminaCostThisFrame);
}
else
{
canRun = false;
}

if (moving)
{
inputDir = inputDir.Normalized();
Velocity = inputDir * (canRun ? RunSpeed : Speed);

string action = canRun ? "run" : "go";
string vDir = "";
if (inputDir.Y > 0) vDir = "down";
else if (inputDir.Y < 0) vDir = "up";

string hDir = "";
if (inputDir.X > 0) hDir = "right";
else if (inputDir.X < 0) hDir = "left";

string direction = (vDir != "" && hDir != "") ? $"{vDir}_{hDir}" : $"{vDir}{hDir}";
string anim = $"{action}_{direction}";

_lastDirection = direction;

if (_body.Animation != anim || !_body.IsPlaying())
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

private void TryAttack()
{
if (_equipMgr == null || !_equipMgr.HasWeaponEquipped)
{
GD.Print("[Player] Chua trang bi vu khi!");
return;
}

_isAttacking = true;
Velocity = Vector2.Zero;

string attackDir = GetAttackDirection();

string bodyAnim = $"sword_{attackDir}";
if (_body != null && _body.SpriteFrames.HasAnimation(bodyAnim))
{
_body.Play(bodyAnim);
}

if (_weaponSprite != null && _weaponSprite.SpriteFrames != null)
{
string weaponAnim = $"sword_{attackDir}";
if (_weaponSprite.SpriteFrames.HasAnimation(weaponAnim))
{
_weaponSprite.Visible = true;
_weaponSprite.Play(weaponAnim);
}
}

GD.Print($"[Player] Attack! Direction: {attackDir}");
}

private string GetAttackDirection()
{
if (_lastDirection.Contains("down")) return "down";
if (_lastDirection.Contains("up")) return "up";
if (_lastDirection.Contains("left")) return "left";
if (_lastDirection.Contains("right")) return "right";
return "down";
}

private void OnBodyAnimationFinished()
{
if (!_isAttacking) return;
FinishAttack();
}

private void OnWeaponAnimationFinished()
{
if (_weaponSprite != null)
{
_weaponSprite.Visible = false;
}
}

private void FinishAttack()
{
_isAttacking = false;

if (_weaponSprite != null)
{
_weaponSprite.Stop();
_weaponSprite.Visible = false;
}

if (_body != null)
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

private void OnWeaponVisualChanged(PackedScene weaponScene)
{
if (_weaponSprite == null) return;

if (weaponScene == null)
{
_weaponSprite.SpriteFrames = null;
_weaponSprite.Visible = false;
GD.Print("[Player] Weapon visual cleared.");
return;
}

var weaponInstance = weaponScene.Instantiate<AnimatedSprite2D>();
if (weaponInstance != null)
{
_weaponSprite.SpriteFrames = weaponInstance.SpriteFrames;
_weaponSprite.Visible = false;
weaponInstance.QueueFree();
GD.Print("[Player] Weapon visual loaded.");
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
