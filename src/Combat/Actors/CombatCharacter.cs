using Godot;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Projectiles;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.HUD;

namespace AshesofaDyingWorld.Combat.Actors
{
    /// <summary>
    /// Lõi chung của Player, companion và enemy.
    /// Mọi actor chỉ khác nguồn intent và AI, không còn mỗi class tự phát minh damage/block/knockback.
    /// </summary>
    public abstract partial class CombatCharacter : CharacterBody2D
    {
        [Signal] public delegate void HitResolvedEventHandler(float hpDamage, bool blocked, bool guardBroken);
        [Signal] public delegate void DefeatedEventHandler(Node attacker);

        [ExportGroup("Combat Identity")]
        [Export] public string CombatantId { get; set; } = "combatant";
        [Export] public CombatFaction Faction { get; set; } = CombatFaction.Neutral;
        [Export] public bool RemoveFromWorldOnDeath { get; set; } = false;
        [Export] public WeaponMovesetData DefaultMoveset { get; set; }

        [ExportGroup("Scene Bindings")]
        [Export] public NodePath BodyPath { get; set; } = new NodePath("Body");
        [Export] public NodePath StatsPath { get; set; } = new NodePath("PlayerStats");
        [Export] public NodePath EquipmentPath { get; set; } = new NodePath("EquipmentManager");
        [Export] public NodePath HurtboxPath { get; set; } = new NodePath("Hurtbox");
        [Export] public NodePath WeaponSpritePath { get; set; } = new NodePath("WeaponSprite");

        [ExportGroup("Movement")]
        [Export] public float Speed { get; set; } = 100f;
        [Export] public float RunSpeed { get; set; } = 180f;
        [Export] public float RunStaminaCost { get; set; } = 20f;
        [Export] public float MinStaminaToRun { get; set; } = 25f;
        [Export] public float Acceleration { get; set; } = 1200f;
        [Export] public float Deceleration { get; set; } = 1600f;
        [Export] public float ExternalForceDecay { get; set; } = 900f;
        [Export] public float ActionLungeMultiplier { get; set; } = 1f;
        [Export] public int StopFrameIndex { get; set; } = 0;

        [ExportGroup("Footstep Audio")]
        [Export] public float FootstepMinSpeed { get; set; } = 12f;

        public PlayerStats Stats { get; private set; }
        public EquipmentManager Equipment { get; private set; }
        public CombatStateMachine StateMachine { get; private set; }
        public CombatActionRunner Actions { get; private set; }
        public CombatAbilityRunner Abilities { get; private set; }
        public AnimatedSprite2D BodySprite => _body;
        public bool IsAlive => Stats == null || Stats.CurrentHP > 0f;
        public bool IsBlocking => StateMachine?.Current == CombatStateId.Blocking;
        public bool IsPerformingAttack => Actions?.IsRunning == true;
        public Vector2 FacingDirection => DirectionToVector(_facingCardinal);
        public string FacingCardinal => _facingCardinal;
        public Vector2 CombatCenter => _hurtboxShape != null
            && GodotObject.IsInstanceValid(_hurtboxShape)
                ? _hurtboxShape.GlobalPosition
                : (_hurtbox?.GlobalPosition ?? GlobalPosition);

        public WeaponMovesetData ActiveMoveset
        {
            get
            {
                EquipmentItemData weapon = Equipment?.GetEquippedItem(EquipmentSlot.MainHand);
                return weapon?.Moveset ?? DefaultMoveset;
            }
        }

        private AnimatedSprite2D _body;
        private AnimatedSprite2D _weaponSprite;
        private Area2D _hurtbox;
        private CollisionShape2D _hurtboxShape;
        private CombatHitbox _combatHitbox;
        private Vector2 _moveCommand;
        private bool _runCommand;
        private bool _isActuallyRunning;
        private float _moveSpeedScale = 1f;
        private bool _preserveFacingWhileMoving;
        private bool _blockCommand;
        private bool _isExhausted;
        private Vector2 _locomotionVelocity;
        private Vector2 _externalVelocity;
        private string _facingCardinal = "down";
        private string _lastMoveAnimation = "go_down";
        private bool _wasMoving;
        private AudioCueData _footstepCueA;
        private AudioCueData _footstepCueB;
        private string _lastFootstepAnimation = string.Empty;
        private int _lastFootstepPhase = -1;
        private bool _isResolvingHit;
        private bool _defeatHandled;

        private const string FootstepCueAPath = "res://assets/resources/data/audio/footsteps/normal_step_01.tres";
        private const string FootstepCueBPath = "res://assets/resources/data/audio/footsteps/normal_step_02.tres";

        public override void _Ready()
        {
            AddToGroup("Combatant");
            ConfigureFactionGroups();
            ResolveCoreNodes();

            StateMachine = new CombatStateMachine();
            StateMachine.StateChanged += OnCombatStateChanged;

            _combatHitbox = new CombatHitbox();
            AddChild(_combatHitbox);
            _combatHitbox.Initialize(this);

            Actions = new CombatActionRunner(this, _body, _combatHitbox, StateMachine);
            Actions.ActionReleased += OnActionReleased;
            Actions.ActionEventTriggered += OnActionEventTriggered;
            Abilities = new CombatAbilityRunner(this);

            if (_body != null)
            {
                _body.FrameChanged += OnBodyFrameChanged;
                PlayIdleFrame();
            }

            if (Equipment != null)
            {
                Equipment.WeaponVisualChanged += OnWeaponVisualChanged;
            }

            if (Stats != null)
            {
                Stats.Defeated += OnStatsDefeated;
            }

            LoadFootstepCues();
            OnCombatReady();
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            if (StateMachine == null)
            {
                return;
            }

            Abilities?.Update(dt);
            UpdateControlSource(dt);
            StateMachine.Tick(dt);
            Actions?.Update(dt);
            SynchronizeBlocking();
            UpdateMovement(dt);
            UpdateAnimation();

            Stats?.UpdateRegeneration(
                dt,
                StateMachine.CanRegenerateStamina && !_isActuallyRunning,
                StateMachine.CanRegenerateGuard,
                StateMachine.CanRegeneratePoise,
                StateMachine.CanRegenerateMana);

            MoveAndSlide();
            _wasMoving = _locomotionVelocity.LengthSquared() > 1f;
        }

        public override void _ExitTree()
        {
            if (Equipment != null)
            {
                Equipment.WeaponVisualChanged -= OnWeaponVisualChanged;
            }

            if (Stats != null)
            {
                Stats.Defeated -= OnStatsDefeated;
            }

            if (_body != null)
            {
                _body.FrameChanged -= OnBodyFrameChanged;
            }

            if (StateMachine != null)
            {
                StateMachine.StateChanged -= OnCombatStateChanged;
            }

            if (Actions != null)
            {
                Actions.ActionReleased -= OnActionReleased;
                Actions.ActionEventTriggered -= OnActionEventTriggered;
            }

            Abilities?.Clear();
            OnCombatExitTree();
        }

        /// <summary>
        /// Đặt hướng di chuyển. preserveFacing = true dùng cho strafe/backpedal trong combat:
        /// nhân vật vẫn nhìn mục tiêu dù đang lùi hoặc đi ngang.
        ///
        /// speedScale cho phép motor giảm tốc mượt khi tới formation anchor. Trước đây mọi lệnh
        /// chỉ có 0 hoặc 100% tốc độ, nên companion liên tục vượt điểm dừng rồi quay lại.
        /// </summary>
        public void SetMoveInput(
            Vector2 direction,
            bool wantsRun = false,
            bool preserveFacing = false,
            float speedScale = 1f)
        {
            _moveCommand = direction == Vector2.Zero ? Vector2.Zero : direction.Normalized();
            _runCommand = wantsRun && _moveCommand != Vector2.Zero;
            _moveSpeedScale = _moveCommand == Vector2.Zero
                ? 1f
                : Mathf.Clamp(speedScale, 0.08f, 1f);
            _preserveFacingWhileMoving = preserveFacing && _moveCommand != Vector2.Zero;
        }

        public void StopMoveInput()
        {
            SetMoveInput(Vector2.Zero, false, false);
        }

        public void SetBlocking(bool value)
        {
            _blockCommand = value;
        }

        public void ReleaseBlock()
        {
            _blockCommand = false;
            StateMachine?.EndBlock();
        }

        public bool RequestAttack()
        {
            if (!IsAlive)
            {
                return false;
            }

            return Actions?.RequestLightAttack() == true;
        }

        public void FaceToward(Vector2 worldPosition)
        {
            // Dùng CombatCenter thay vì root GlobalPosition. Hurtbox của nhiều actor có offset,
            // nếu trừ từ root thì một vector "up" ngắn có thể bị offset kéo thành "down".
            Vector2 direction = worldPosition - CombatCenter;
            FaceDirection(direction);
        }

        public void FaceDirection(Vector2 direction)
        {
            if (direction.LengthSquared() <= 0.001f)
            {
                return;
            }

            _facingCardinal = ResolveCardinalDirection(direction.Normalized());
            _lastMoveAnimation = $"go_{_facingCardinal}";
        }

        public void ApplyExternalForce(Vector2 force, float animLockTime = -1f)
        {
            _externalVelocity += force;
            if (animLockTime > 0f && StateMachine?.Current != CombatStateId.Dead)
            {
                StateMachine.EnterHitstun(animLockTime);
            }
        }

        public bool IsBlockingAttackFrom(Vector2 attackerPosition)
        {
            if (!IsBlocking || !IsAlive || (Stats != null && Stats.CurrentGuard <= 0f))
            {
                return false;
            }

            Vector2 toAttacker = (attackerPosition - GlobalPosition).Normalized();
            if (toAttacker == Vector2.Zero)
            {
                return true;
            }

            float guardArc = ActiveMoveset?.GuardArcDegrees ?? 140f;
            float minimumDot = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(guardArc, 1f, 360f) * 0.5f));
            return FacingDirection.Dot(toAttacker) >= minimumDot;
        }

        public HitResult TryResolveHit(CombatCharacter target, CombatActionData action, HitProfileData profile)
        {
            Vector2 direction = target == null
                ? FacingDirection
                : (target.CombatCenter - CombatCenter).Normalized();
            return TryResolveHit(target, action, profile, CombatCenter, direction);
        }

        /// <summary>
        /// Overload cho projectile: block arc và knockback phải dựa trên điểm va chạm/hướng bay,
        /// không phải vị trí caster đã chạy sang chỗ khác sau khi bắn.
        /// </summary>
        public HitResult TryResolveHit(
            CombatCharacter target,
            CombatActionData action,
            HitProfileData profile,
            Vector2 hitOrigin,
            Vector2 attackDirection)
        {
            if (target == null || profile == null)
            {
                return HitResult.Rejected(HitRejectionReason.InvalidRequest);
            }

            Vector2 safeDirection = attackDirection.LengthSquared() <= 0.001f
                ? (target.CombatCenter - hitOrigin).Normalized()
                : attackDirection.Normalized();
            return target.ReceiveHit(new HitRequest
            {
                Attacker = this,
                Target = target,
                Action = action,
                Profile = profile,
                HitOrigin = hitOrigin,
                AttackDirection = safeDirection
            });
        }

        public virtual HitResult ReceiveHit(HitRequest request)
        {
            _isResolvingHit = true;
            HitResult result;
            try
            {
                result = CombatResolver.Resolve(request);
            }
            finally
            {
                _isResolvingHit = false;
            }

            if (!result.Applied)
            {
                return result;
            }

            if (result.Killed)
            {
                Actions?.Cancel();
                Abilities?.CancelActiveEffects();
                StateMachine.EnterDead();
            }
            else if (result.GuardBroken)
            {
                Actions?.Cancel();
                _blockCommand = false;
                StateMachine.EnterGuardBreak(0.75f);
            }
            else if (result.Staggered)
            {
                Actions?.Cancel();
                StateMachine.EnterStagger(0.42f);
            }
            else if (result.WasBlocked)
            {
                StateMachine.EnterBlockStun(0.1f);
            }
            else if (result.HitstunSeconds > 0f)
            {
                Actions?.Cancel();
                StateMachine.EnterHitstun(result.HitstunSeconds);
            }

            if (result.Knockback != Vector2.Zero && (!result.WasBlocked || result.GuardBroken))
            {
                _externalVelocity += result.Knockback;
            }

            if (result.HpDamage > 0f)
            {
                DamageNumberService.GetOrCreate(GetTree())?.ShowDamage(this, result.HpDamage);
            }

            EmitSignal(SignalName.HitResolved, result.HpDamage, result.WasBlocked, result.GuardBroken);
            OnHitReceived(request, result);

            if (result.Killed)
            {
                HandleDefeat(request.Attacker);
            }

            return result;
        }

        public void ResetCombatRuntime()
        {
            Actions?.Cancel();
            _combatHitbox?.DisableHitbox();
            _moveCommand = Vector2.Zero;
            _runCommand = false;
            _isActuallyRunning = false;
            _moveSpeedScale = 1f;
            _preserveFacingWhileMoving = false;
            _blockCommand = false;
            _isExhausted = false;
            _locomotionVelocity = Vector2.Zero;
            _externalVelocity = Vector2.Zero;
            Velocity = Vector2.Zero;

            bool alive = Stats == null || Stats.CurrentHP > 0f;
            _defeatHandled = !alive;
            if (alive)
            {
                StateMachine?.Reset();
                PlayIdleFrame();
            }
            else
            {
                StateMachine?.EnterDead();
            }

            ProcessMode = ProcessModeEnum.Inherit;
            SetPhysicsProcess(alive || !RemoveFromWorldOnDeath);
        }

        protected virtual void UpdateControlSource(float delta)
        {
        }

        protected virtual void OnCombatReady()
        {
        }

        protected virtual void OnCombatExitTree()
        {
        }

        protected virtual void OnHitReceived(HitRequest request, HitResult result)
        {
        }

        protected virtual void OnDefeated(CombatCharacter attacker)
        {
        }

        protected virtual float GetRuntimeMoveSpeedMultiplier()
        {
            return 1f;
        }


        private void OnActionEventTriggered(
            CombatActionData action,
            CombatActionEventData actionEvent,
            Vector2 direction)
        {
            CombatActionEventDispatcher.Dispatch(this, action, actionEvent, direction);
        }

        private void OnActionReleased(CombatActionData action, Vector2 direction)
        {
            // Resource cũ chưa có Events vẫn chạy được. Action mới có event sẽ không đi qua
            // legacy bridge, tránh spawn hai projectile từ một lần release.
            CombatActionEventDispatcher.DispatchLegacyDelivery(this, action, direction);
        }

        private void ResolveCoreNodes()
        {
            Stats = ResolveNode<PlayerStats>(StatsPath, "PlayerStats");
            Equipment = ResolveNode<EquipmentManager>(EquipmentPath, "EquipmentManager");
            _hurtbox = ResolveNode<Area2D>(HurtboxPath, "Hurtbox");
            _hurtboxShape = ResolveHurtboxShape(_hurtbox);
            _weaponSprite = ResolveNode<AnimatedSprite2D>(WeaponSpritePath, "WeaponSprite");
            ResolveBodySprite();

            if (_hurtbox != null)
            {
                // Hurtbox chỉ cần được hitbox khác nhìn thấy. Nó không cần tự quét Area khác.
                // Giữ Monitoring = false còn giúp giảm query thừa và tránh callback chồng chéo.
                _hurtbox.Monitoring = false;
                _hurtbox.Monitorable = true;
            }
        }


        private static CollisionShape2D ResolveHurtboxShape(Area2D hurtbox)
        {
            if (hurtbox == null)
            {
                return null;
            }

            foreach (Node child in hurtbox.GetChildren())
            {
                if (child is CollisionShape2D shape)
                {
                    return shape;
                }
            }

            return null;
        }

        private T ResolveNode<T>(NodePath configuredPath, string fallbackName) where T : Node
        {
            string path = configuredPath.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                T configured = GetNodeOrNull<T>(path);
                if (configured != null)
                {
                    return configured;
                }
            }

            return GetNodeOrNull<T>(fallbackName);
        }

        private void ResolveBodySprite()
        {
            string path = BodyPath.ToString();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "Body";
            }

            _body = GetNodeOrNull<AnimatedSprite2D>(path);
            if (_body != null)
            {
                BodyPath = new NodePath(path);
                return;
            }

            PackedScene bodyScene = Stats?.ConfigData?.BodyScene;
            if (bodyScene == null)
            {
                GD.PrintErr($"[{GetType().Name}] Không tìm thấy Body và CharacterConfig cũng không có BodyScene.");
                return;
            }

            Node bodyNode = bodyScene.Instantiate();
            if (bodyNode is not AnimatedSprite2D bodySprite)
            {
                GD.PrintErr($"[{GetType().Name}] BodyScene phải có root AnimatedSprite2D.");
                bodyNode.QueueFree();
                return;
            }

            bodySprite.Name = "Body";
            AddChild(bodySprite);
            _body = bodySprite;
            BodyPath = new NodePath("Body");
        }

        private void SynchronizeBlocking()
        {
            if (!IsAlive || StateMachine.Current == CombatStateId.Dead)
            {
                _blockCommand = false;
                return;
            }

            if (_blockCommand && Actions?.IsRunning != true && StateMachine.CanStartBlock)
            {
                StateMachine.TryBeginBlock();
            }
            else if (!_blockCommand)
            {
                StateMachine.EndBlock();
            }
        }

        private void UpdateMovement(float delta)
        {
            bool hasInput = _moveCommand != Vector2.Zero;
            bool canRun = false;

            if (Stats != null)
            {
                if (Stats.CurrentStamina <= 2f)
                {
                    _isExhausted = true;
                }
                else if (Stats.CurrentStamina >= MinStaminaToRun)
                {
                    _isExhausted = false;
                }
            }

            if (StateMachine.CanMove && hasInput && _runCommand && !_isExhausted)
            {
                float cost = RunStaminaCost * delta;
                canRun = Stats == null || Stats.ConsumeStamina(cost);
            }
            _isActuallyRunning = canRun;

            Vector2 targetVelocity = Vector2.Zero;
            if (StateMachine.CanMove && hasInput)
            {
                float moveSpeed = canRun ? RunSpeed : Speed;
                if (StateMachine.Current == CombatStateId.Blocking)
                {
                    moveSpeed *= ActiveMoveset?.GuardMoveSpeedMultiplier ?? 0.35f;
                }

                moveSpeed *= Abilities?.MoveSpeedMultiplier ?? 1f;
                moveSpeed *= GetRuntimeMoveSpeedMultiplier();
                moveSpeed *= _moveSpeedScale;
                targetVelocity = _moveCommand * moveSpeed;
                if (!_preserveFacingWhileMoving)
                {
                    _facingCardinal = ResolveCardinalDirection(_moveCommand);
                }
            }

            float step = (targetVelocity == Vector2.Zero ? Deceleration : Acceleration) * delta;
            _locomotionVelocity = _locomotionVelocity.MoveToward(targetVelocity, step);
            _externalVelocity = _externalVelocity.MoveToward(Vector2.Zero, ExternalForceDecay * delta);

            if (_locomotionVelocity.LengthSquared() < 0.5f)
            {
                _locomotionVelocity = Vector2.Zero;
            }

            Vector2 actionVelocity = Actions?.MovementVelocity ?? Vector2.Zero;
            Velocity = _locomotionVelocity + actionVelocity + _externalVelocity;
        }

        private void UpdateAnimation()
        {
            if (_body == null || _body.SpriteFrames == null || !IsAlive)
            {
                return;
            }

            if (Actions?.IsRunning == true)
            {
                return;
            }

            if (StateMachine.Current == CombatStateId.Blocking || StateMachine.Current == CombatStateId.BlockStun)
            {
                PlayBlockAnimation();
                return;
            }

            if (StateMachine.IsForcedState)
            {
                return;
            }

            bool moving = _locomotionVelocity.LengthSquared() > 1f;
            if (moving)
            {
                // Khi backpedal/strafe, giữ animation theo hướng mặt thay vì hướng vận tốc.
                // Nhờ vậy Hyou lùi khỏi slime mà không quay lưng rồi vẫn gây damage.
                string direction = _preserveFacingWhileMoving
                    ? _facingCardinal
                    : ResolveEightDirection(_locomotionVelocity.Normalized());
                bool running = _isActuallyRunning && !_preserveFacingWhileMoving;
                string animation = $"{(running ? "run" : "go")}_{direction}";
                _lastMoveAnimation = animation;
                if (_body.SpriteFrames.HasAnimation(animation)
                    && (_body.Animation.ToString() != animation || !_body.IsPlaying()))
                {
                    _body.Play(animation);
                }
            }
            else if (_wasMoving || _body.IsPlaying())
            {
                PlayIdleFrame();
            }

            if (_weaponSprite != null)
            {
                _weaponSprite.Visible = false;
            }
        }

        private void PlayBlockAnimation()
        {
            string movesetAnimation = ActiveMoveset?.ResolveGuardAnimation(_facingCardinal) ?? string.Empty;
            string genericAnimation = $"block_{_facingCardinal}";
            string selected = !string.IsNullOrWhiteSpace(movesetAnimation)
                && _body.SpriteFrames.HasAnimation(movesetAnimation)
                    ? movesetAnimation
                    : genericAnimation;

            if (_body.SpriteFrames.HasAnimation(selected)
                && (_body.Animation.ToString() != selected || !_body.IsPlaying()))
            {
                _body.Play(selected);
            }
        }

        private void PlayIdleFrame()
        {
            if (_body == null || _body.SpriteFrames == null)
            {
                return;
            }

            string idle = $"go_{_facingCardinal}";
            if (!_body.SpriteFrames.HasAnimation(idle))
            {
                idle = _body.SpriteFrames.HasAnimation("idle") ? "idle" : "Idle";
            }

            if (_body.SpriteFrames.HasAnimation(idle))
            {
                _body.Animation = idle;
                int frameCount = _body.SpriteFrames.GetFrameCount(idle);
                _body.Frame = Mathf.Clamp(StopFrameIndex, 0, Mathf.Max(0, frameCount - 1));
                _body.Stop();
            }
        }

        private void OnWeaponVisualChanged(PackedScene weaponScene)
        {
            if (_weaponSprite == null)
            {
                return;
            }

            if (weaponScene == null)
            {
                _weaponSprite.SpriteFrames = null;
                _weaponSprite.Visible = false;
                return;
            }

            Node instance = weaponScene.Instantiate();
            if (instance is AnimatedSprite2D source)
            {
                _weaponSprite.SpriteFrames = source.SpriteFrames;
                _weaponSprite.Visible = false;
            }

            instance.QueueFree();
        }

        private void OnBodyFrameChanged()
        {
            Actions?.HandleBodyFrameChanged();
            TryPlayFootstep();
        }

        private void OnCombatStateChanged(CombatStateId previous, CombatStateId current)
        {
            if (current == CombatStateId.Dead)
            {
                _combatHitbox?.DisableHitbox();
                _locomotionVelocity = Vector2.Zero;
                Velocity = Vector2.Zero;
            }
        }

        private void OnStatsDefeated()
        {
            // ApplyDamage phát signal đồng bộ ngay giữa CombatResolver. Khi đó ReceiveHit vẫn chưa
            // nhận HitResult và chưa biết killer. Để ReceiveHit xử lý một lần duy nhất.
            if (_isResolvingHit)
            {
                return;
            }

            if (StateMachine?.Current != CombatStateId.Dead)
            {
                Actions?.Cancel();
                Abilities?.CancelActiveEffects();
                StateMachine?.EnterDead();
                HandleDefeat(null);
            }
        }

        private void HandleDefeat(CombatCharacter attacker)
        {
            if (_defeatHandled)
            {
                return;
            }

            _defeatHandled = true;
            EmitSignal(SignalName.Defeated, attacker);
            OnDefeated(attacker);
            if (RemoveFromWorldOnDeath)
            {
                CallDeferred(nameof(QueueFree));
            }
        }

        private void ConfigureFactionGroups()
        {
            switch (Faction)
            {
                case CombatFaction.Player:
                    AddToGroup("Player");
                    break;
                case CombatFaction.Companion:
                    AddToGroup("Companion");
                    break;
                case CombatFaction.Enemy:
                    AddToGroup("Enemy");
                    break;
            }
        }

        private void LoadFootstepCues()
        {
            _footstepCueA = GD.Load<AudioCueData>(FootstepCueAPath);
            _footstepCueB = GD.Load<AudioCueData>(FootstepCueBPath);
        }

        private void TryPlayFootstep()
        {
            if (_body == null || AudioManager.Instance == null || Actions?.IsRunning == true)
            {
                return;
            }

            string animation = _body.Animation.ToString();
            if (!_body.IsPlaying()
                || Velocity.Length() < FootstepMinSpeed
                || (!animation.StartsWith("go_") && !animation.StartsWith("run_")))
            {
                ResetFootstepCycle();
                return;
            }

            bool running = animation.StartsWith("run_");
            int phase = running
                ? (_body.Frame <= 2 ? 0 : (_body.Frame <= 5 ? 1 : -1))
                : (_body.Frame <= 1 ? 0 : (_body.Frame <= 3 ? 1 : -1));
            if (phase < 0)
            {
                return;
            }

            if (_lastFootstepAnimation != animation)
            {
                _lastFootstepAnimation = animation;
                _lastFootstepPhase = -1;
            }

            if (phase == _lastFootstepPhase)
            {
                return;
            }

            AudioCueData cue = phase == 0 ? _footstepCueA : _footstepCueB;
            if (cue?.Stream != null)
            {
                AudioManager.Instance.PlaySfx(cue);
                _lastFootstepPhase = phase;
            }
        }

        private void ResetFootstepCycle()
        {
            _lastFootstepAnimation = string.Empty;
            _lastFootstepPhase = -1;
        }

        private static string ResolveCardinalDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            {
                return direction.X >= 0f ? "right" : "left";
            }

            return direction.Y >= 0f ? "down" : "up";
        }

        private string ResolveEightDirection(Vector2 direction)
        {
            string vertical = direction.Y > 0.2f ? "down" : (direction.Y < -0.2f ? "up" : string.Empty);
            string horizontal = direction.X > 0.2f ? "right" : (direction.X < -0.2f ? "left" : string.Empty);
            if (string.IsNullOrEmpty(vertical) && string.IsNullOrEmpty(horizontal))
            {
                return _facingCardinal;
            }

            return !string.IsNullOrEmpty(vertical) && !string.IsNullOrEmpty(horizontal)
                ? $"{vertical}_{horizontal}"
                : $"{vertical}{horizontal}";
        }

        private static Vector2 DirectionToVector(string direction)
        {
            return direction switch
            {
                "up" => Vector2.Up,
                "left" => Vector2.Left,
                "right" => Vector2.Right,
                _ => Vector2.Down
            };
        }
    }
}
