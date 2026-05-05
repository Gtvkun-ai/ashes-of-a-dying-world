using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
[ExportGroup("Footstep Audio")]
[Export] public float FootstepWalkInterval { get; set; } = 0.4f;
[Export] public float FootstepRunInterval { get; set; } = 0.2f;
[Export] public float FootstepMinSpeed { get; set; } = 12f;

[Export] public float Speed { get; set; } = 100f;
[Export] public float RunSpeed { get; set; } = 200f;
[Export] public float RunStaminaCost { get; set; } = 20f;
[Export] public float MinStaminaToRun { get; set; } = 40f;
	[Export] public float BaseAttackStaminaCost { get; set; } = 15f;
[Export] public float Acceleration { get; set; } = 1200f;
[Export] public float Deceleration { get; set; } = 1600f;
[Export] public float ComboContinueWindow { get; set; } = 1f;
[Export] public NodePath BodyPath { get; set; } = "Body";
[Export] public int StopFrameIndex { get; set; } = 0;
[Export] public float AttackLungeSpeed { get; set; } = 60f;
[Export] public float KnockbackAnimLockTime { get; set; } = 0.15f;
[Export] public bool UsePlayerInput { get; set; } = true;

private bool _isExhausted = false;
private Vector2 _moveInputDirection = Vector2.Zero;
private bool _wantsRun = false;
private bool _commandBlocking = false;

private AnimatedSprite2D _body;
private AnimatedSprite2D _weaponSprite;
private Area2D _hurtbox;
private Area2D _hitbox;
private string _lastMoveAnim = "go_down";
private string _lastDirection = "down";
private bool _wasMoving = false;
private bool _isAttacking = false;
private bool _isBlocking = false;
private int _queuedAttackCount = 0;
private int _comboHitCount = 0;
private int _activeAttackStep = 0;
private int _attackStartFrame = 0;
private int _attackEndFrame = 0;
private int _attackHitStartFrame = 0;
private int _attackHitEndFrame = 0;
private string _activeAttackAnim = "";
private int _weaponAttackEndFrame = 0;
private string _activeWeaponAttackAnim = "";
private bool _isCompletingAttackStep = false;
private bool _isWaitingSecondHit = false;
private float _secondHitWaitTimer = 0f;
private readonly HashSet<Node> _attackHitTargets = new();
private PlayerStats _stats;
private EquipmentManager _equipMgr;
private InventoryManager _inventory;

private float _bodyBaseSpeedScale = 1f;
private float _weaponBaseSpeedScale = 1f;
private float _knockbackAnimTimer = 0f;
private readonly Dictionary<SkillData, float> _skillCooldowns = new();
private SkillData _activeTimedSkill;
private float _activeTimedSkillRemaining = 0f;
private float _activeMoveSpeedMultiplier = 1f;
private int _activeDexterityBonus = 0;
private AudioCueData _normalFootstepCue01;
private AudioCueData _normalFootstepCue02;
private string _lastFootstepAnim = "";
private int _lastFootstepPhase = -1;

private const string NormalFootstepCue01Path = "res://assets/resources/data/audio/footsteps/normal_step_01.tres";
private const string NormalFootstepCue02Path = "res://assets/resources/data/audio/footsteps/normal_step_02.tres";

private const string SkillSlot1Action = "skill_1";

public override void _Ready()
{
_weaponSprite = GetNodeOrNull<AnimatedSprite2D>("WeaponSprite");
_hurtbox = GetNodeOrNull<Area2D>("Hurtbox");
_hitbox = GetNodeOrNull<Area2D>("WeaponSprite/Hitbox");
_stats = GetNodeOrNull<PlayerStats>("PlayerStats");
_equipMgr = GetNodeOrNull<EquipmentManager>("EquipmentManager");

EnsureDefaultSkills();

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

LoadFootstepCue();

SetHitboxActive(false);
}

public override void _PhysicsProcess(double delta)
{
UpdateControlCommands();
UpdateSkillTimers((float)delta);

if (UsePlayerInput && Input.IsActionJustPressed(SkillSlot1Action))
{
TryActivateSkillSlot(0);
}

if (UsePlayerInput && Input.IsActionJustPressed("attack"))
{
RequestAttack();
}

// Đếm ngược thời gian khóa animation khi bị knockback (trượt lùi)
if (_knockbackAnimTimer > 0f)
{
_knockbackAnimTimer -= (float)delta;
if (_knockbackAnimTimer < 0f)
{
_knockbackAnimTimer = 0f;
}
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

if (IsAttackHitboxActive())
{
ApplyCurrentHitboxOverlaps();
}

Velocity = Velocity.MoveToward(Vector2.Zero, Deceleration * (float)delta);
MoveAndSlide();
return;
}

// Khi đang bị knockback, chỉ trượt lùi bằng ngoại lực.
// Không nhận input di chuyển và không đổi hướng mặt/animation.
if (_knockbackAnimTimer > 0f)
{
string knockbackAnim = _lastMoveAnim.Replace("run", "go");
if (_body != null && _body.SpriteFrames.HasAnimation(knockbackAnim))
{
if (_body.Animation != knockbackAnim || !_body.IsPlaying())
{
_body.Play(knockbackAnim);
if (_body.SpriteFrames.GetFrameCount(knockbackAnim) > 1 && _body.Frame == StopFrameIndex)
{
_body.Frame = 1;
}
}
}

if (_weaponSprite != null && _weaponSprite.Visible)
{
_weaponSprite.Visible = false;
}

Velocity = Velocity.MoveToward(Vector2.Zero, Deceleration * (float)delta);
MoveAndSlide();
_wasMoving = Velocity.LengthSquared() > 1f;
return;
}

Vector2 inputDir = _moveInputDirection;
bool hasInput = inputDir != Vector2.Zero;
bool wantsToRun = _wantsRun;

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

float targetSpeed = (canRun ? RunSpeed : Speed) * GetMoveSpeedMultiplier();
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
_lastMoveAnim = anim;
bool allowAnimChange = true;

if (allowAnimChange && _body != null && (_body.Animation != anim || !_body.IsPlaying()))
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

if (_weaponSprite != null && _weaponSprite.Visible)
{
_weaponSprite.Visible = false;
}
}
else
{
	// Chỉ update về idle khi không còn trong trạng thái knockback
	// hoặc khi vừa thoát khỏi animation block (nhả phím X)
	if (_body != null && _knockbackAnimTimer <= 0f)
	{
		string currentAnimName = _body.Animation.ToString();
		bool wasMovingOrLeavingBlock = _wasMoving
			|| (!string.IsNullOrEmpty(currentAnimName) && currentAnimName.StartsWith("block") && !_isBlocking);
		if (wasMovingOrLeavingBlock)
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
}

	// Nếu đang giữ block và không tấn công/knockback thì ưu tiên animation block
	if (_isBlocking && !_isAttacking && _knockbackAnimTimer <= 0f && _body != null && _body.SpriteFrames != null)
	{
		string blockDir = GetAttackDirection(); // dùng hướng nhìn hiện tại

		// Nếu có vũ khí (ví dụ Wood Sword), ưu tiên animation block_woodSword_huong
		string desiredAnim = $"block_{blockDir}";
		if (_equipMgr != null && _equipMgr.HasWeaponEquipped)
		{
			string weaponBlockAnim = $"block_woodSword_{blockDir}";
			if (_body.SpriteFrames.HasAnimation(weaponBlockAnim))
			{
				desiredAnim = weaponBlockAnim;
			}
		}

		if (_body.SpriteFrames.HasAnimation(desiredAnim))
		{
			if (_body.Animation != desiredAnim || !_body.IsPlaying())
			{
				_body.Play(desiredAnim);
			}
		}
	}

MoveAndSlide();
_wasMoving = moving;
UpdateFootstepAudio((float)delta, moving, canRun);

// Hồi Stamina: chỉ khi không chạy và không đánh
if (_stats != null && !_isAttacking && !canRun && _stats.CurrentStamina < _stats.MaxStamina)
{
	_stats.ChangeStamina(_stats.StaminaRegenRate * (float)delta);
}
}

private void UpdateControlCommands()
{
if (!UsePlayerInput)
{
_isBlocking = _commandBlocking;
return;
}

SetBlocking(Input.IsKeyPressed(Key.X) || Input.IsActionPressed("block"));

Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
bool wantsToRun = Input.IsKeyPressed(Key.Shift);
if (InputMap.HasAction("run"))
{
wantsToRun = wantsToRun || Input.IsActionPressed("run");
}

SetMoveInput(inputDir, wantsToRun);
}

public void SetMoveInput(Vector2 direction, bool wantsRun = false)
{
_moveInputDirection = direction == Vector2.Zero ? Vector2.Zero : direction.Normalized();
_wantsRun = wantsRun;
}

public void StopMoveInput()
{
SetMoveInput(Vector2.Zero);
}

public void SetBlocking(bool value)
{
_commandBlocking = value;
_isBlocking = value;
}

public void RequestAttack()
{
QueueAttack();
}

public void FaceToward(Vector2 worldPosition)
{
Vector2 toTarget = worldPosition - GlobalPosition;
if (toTarget.LengthSquared() <= 0.001f)
{
return;
}

string direction = ResolveCardinalDirection(toTarget.Normalized());
_lastDirection = direction;
_lastMoveAnim = $"go_{direction}";
}

public bool IsBlocking => _isBlocking;
public bool IsPerformingAttack => _isAttacking;
public Vector2 FacingDirection => GetAttackDirectionVector();

private void LoadFootstepCue()
{
_normalFootstepCue01 = GD.Load<AudioCueData>(NormalFootstepCue01Path);
if (_normalFootstepCue01 == null)
{
GD.PrintErr($"[Player] Failed to load footstep cue: {NormalFootstepCue01Path}");
}

_normalFootstepCue02 = GD.Load<AudioCueData>(NormalFootstepCue02Path);
if (_normalFootstepCue02 == null)
{
GD.PrintErr($"[Player] Failed to load footstep cue: {NormalFootstepCue02Path}");
}
}

private void UpdateFootstepAudio(float delta, bool moving, bool isRunning)
{
	if (moving && Velocity.Length() >= FootstepMinSpeed)
	{
		return;
	}

	ResetFootstepCycle();
}

private void TryPlayFootstepForCurrentFrame()
{
	if (_body == null || AudioManager.Instance == null || _isAttacking)
	{
		return;
	}

	string anim = _body.Animation.ToString();
	if (string.IsNullOrEmpty(anim) || (!anim.StartsWith("go_") && !anim.StartsWith("run_")))
	{
		ResetFootstepCycle();
		return;
	}

	if (!_body.IsPlaying() || Velocity.Length() < FootstepMinSpeed)
	{
		ResetFootstepCycle();
		return;
	}

	bool isRunAnim = anim.StartsWith("run_");
	int phase = GetFootstepPhase(_body.Frame, isRunAnim);
	if (phase < 0)
	{
		return;
	}

	if (_lastFootstepAnim != anim)
	{
		_lastFootstepAnim = anim;
		_lastFootstepPhase = -1;
	}

	if (phase == _lastFootstepPhase)
	{
		return;
	}

	AudioCueData cue = phase == 0 ? _normalFootstepCue01 : _normalFootstepCue02;
	if (cue?.Stream == null)
	{
		return;
	}

	AudioManager.Instance.PlaySfx(cue);
	_lastFootstepPhase = phase;
}

private int GetFootstepPhase(int frame, bool isRunAnim)
{
	if (isRunAnim)
	{
		if (frame >= 0 && frame <= 2) return 0;
		if (frame >= 3 && frame <= 5) return 1;
		return -1;
	}

	if (frame >= 0 && frame <= 1) return 0;
	if (frame >= 2 && frame <= 3) return 1;
	return -1;
}

private void ResetFootstepCycle()
{
	_lastFootstepAnim = "";
	_lastFootstepPhase = -1;
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

private string ResolveCardinalDirection(Vector2 dir)
{
	if (Mathf.Abs(dir.X) > Mathf.Abs(dir.Y))
	{
		return dir.X > 0f ? "right" : "left";
	}

	return dir.Y > 0f ? "down" : "up";
}

public override void _ExitTree()
{
	EndActiveTimedSkill();
}

public void ResetTransientStateAfterLoad()
{
// Dọn trạng thái runtime có thể làm kẹt input sau khi load giữa lúc đang combat/knockback.
FinishAttack();

_isBlocking = false;
_isExhausted = false;
_knockbackAnimTimer = 0f;
_wasMoving = false;
Velocity = Vector2.Zero;

if (_body != null)
{
_body.SpeedScale = _bodyBaseSpeedScale;
string idleAnim = _lastMoveAnim.Replace("run", "go");
if (_body.SpriteFrames != null && _body.SpriteFrames.HasAnimation(idleAnim))
{
_body.Animation = idleAnim;
_body.Frame = StopFrameIndex;
}
_body.Stop();
}

if (_weaponSprite != null)
{
_weaponSprite.SpeedScale = _weaponBaseSpeedScale;
_weaponSprite.Stop();
_weaponSprite.Visible = false;
}

SetHitboxActive(false);

ProcessMode = ProcessModeEnum.Inherit;
SetProcess(true);
SetPhysicsProcess(true);
SetProcessInput(true);
SetProcessUnhandledInput(true);
}
}
