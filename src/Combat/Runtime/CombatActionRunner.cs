using Godot;
using System;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Chạy action từ Resource: stamina, frame window, combo buffer, delivery và recovery.
    /// Melee mở CombatHitbox; projectile chỉ phát release event để world spawner tạo đạn.
    /// </summary>
    public sealed class CombatActionRunner
    {
        private enum FallbackPhase
        {
            None,
            Startup,
            Active,
            Recovery
        }

        private readonly CombatCharacter _owner;
        private readonly AnimatedSprite2D _body;
        private readonly CombatHitbox _hitbox;
        private readonly CombatStateMachine _state;
        private readonly float _baseBodySpeedScale;

        private CombatActionData _currentAction;
        private int _comboIndex = -1;
        private float _bufferRemaining;
        private bool _usingFrameAnimation;
        private bool _deliveryOpened;
        private Vector2 _actionFacing = Vector2.Down;
        private string _actionFacingCardinal = "down";
        private FallbackPhase _fallbackPhase;
        private float _fallbackRemaining;

        public event Action<CombatActionData, Vector2> ActionStarted;
        public event Action<CombatActionData, Vector2> ActionReleased;
        public event Action<CombatActionData, bool> ActionFinished;

        public CombatActionData CurrentAction => _currentAction;
        public bool IsRunning => _currentAction != null;
        public bool IsHitboxActive => _hitbox?.IsActive == true;
        public Vector2 ActionFacing => _actionFacing;

        public Vector2 MovementVelocity
        {
            get
            {
                if (_currentAction == null)
                {
                    return Vector2.Zero;
                }

                if (_state.Current != CombatStateId.AttackStartup && _state.Current != CombatStateId.AttackActive)
                {
                    return Vector2.Zero;
                }

                return _actionFacing
                    * _currentAction.LungeSpeed
                    * Mathf.Max(0f, _owner.ActionLungeMultiplier);
            }
        }

        public CombatActionRunner(
            CombatCharacter owner,
            AnimatedSprite2D body,
            CombatHitbox hitbox,
            CombatStateMachine state)
        {
            _owner = owner;
            _body = body;
            _hitbox = hitbox;
            _state = state;
            _baseBodySpeedScale = body?.SpeedScale ?? 1f;
        }

        public bool RequestLightAttack()
        {
            float buffer = _currentAction?.InputBufferSeconds ?? 0.2f;
            _bufferRemaining = Mathf.Max(_bufferRemaining, buffer);
            if (_currentAction == null)
            {
                return TryStartAction(0, false);
            }

            // Đang chạy action thì input đã được nhận vào buffer.
            return true;
        }

        public bool TryStartAbilityAction(CombatActionData action)
        {
            return TryStartAbilityAction(action, Vector2.Zero);
        }

        /// <summary>
        /// Ability ranged có thể truyền aimDirection liên tục, trong khi animation vẫn dùng
        /// cardinal direction của actor. Nhờ vậy bolt không bị khóa vào bốn đường thẳng ngu ngơ.
        /// </summary>
        public bool TryStartAbilityAction(CombatActionData action, Vector2 aimDirection)
        {
            if (action == null || _currentAction != null)
            {
                return false;
            }

            Vector2? forcedFacing = aimDirection.LengthSquared() > 0.001f
                ? aimDirection.Normalized()
                : null;
            return TryStartResolvedAction(action, -1, false, forcedFacing);
        }

        public void Update(float delta)
        {
            _bufferRemaining = Mathf.Max(0f, _bufferRemaining - Mathf.Max(0f, delta));
            if (_currentAction == null)
            {
                return;
            }

            if (_usingFrameAnimation)
            {
                EvaluateAnimationFrame();
                return;
            }

            UpdateFallback(delta);
        }

        public void HandleBodyFrameChanged()
        {
            if (_currentAction != null && _usingFrameAnimation)
            {
                EvaluateAnimationFrame();
            }
        }

        public void Cancel()
        {
            FinishAction(false);
        }

        private bool TryStartAction(int comboIndex, bool allowChain)
        {
            WeaponMovesetData moveset = _owner.ActiveMoveset;
            CombatActionData action = moveset?.GetLightAction(comboIndex);
            return TryStartResolvedAction(action, comboIndex, allowChain, null);
        }

        private bool TryStartResolvedAction(
            CombatActionData action,
            int comboIndex,
            bool allowChain,
            Vector2? forcedFacing)
        {
            if (action == null)
            {
                return false;
            }

            // Kiểm tra quyền chuyển state trước khi trừ stamina. Nếu không, một lần bấm sai nhịp
            // cũng âm thầm ăn tài nguyên, đúng kiểu máy bán hàng nuốt tiền nhưng không nhả nước.
            bool canEnterAttack = _state.CanStartAttack || (allowChain && _state.IsAttackState);
            if (!canEnterAttack)
            {
                return false;
            }

            if (_owner.Stats != null && !_owner.Stats.ConsumeStamina(action.StaminaCost))
            {
                return false;
            }

            if (!_state.TryBeginAttack(allowChain))
            {
                return false;
            }

            _currentAction = action;
            _comboIndex = comboIndex;
            _bufferRemaining = 0f;
            _deliveryOpened = false;
            _hitbox.DisableHitbox();

            // Khóa hướng ngay lúc action được nhận. Animation dùng cardinal, còn projectile
            // giữ vector aim chính xác để không bỏ lỡ mục tiêu chéo góc.
            _actionFacing = forcedFacing ?? _owner.FacingDirection;
            if (_actionFacing.LengthSquared() <= 0.001f)
            {
                _actionFacing = Vector2.Down;
            }
            else
            {
                _actionFacing = _actionFacing.Normalized();
            }
            _actionFacingCardinal = _owner.FacingCardinal;
            ActionStarted?.Invoke(_currentAction, _actionFacing);

            string animationName = action.ResolveAnimation(_actionFacingCardinal);
            _usingFrameAnimation = _body?.SpriteFrames != null
                && !string.IsNullOrWhiteSpace(animationName)
                && _body.SpriteFrames.HasAnimation(animationName);

            if (_usingFrameAnimation)
            {
                _body.SpeedScale = _baseBodySpeedScale
                    * Mathf.Max(0.1f, _owner.Stats?.AttackSpeed ?? 1f)
                    * Mathf.Max(0.1f, action.PlaybackSpeedMultiplier);
                _body.Animation = animationName;
                _body.Frame = Mathf.Clamp(action.StartFrame, 0, _body.SpriteFrames.GetFrameCount(animationName) - 1);
                _body.Play();
                EvaluateAnimationFrame();
            }
            else
            {
                _fallbackPhase = FallbackPhase.Startup;
                _fallbackRemaining = Mathf.Max(0.01f, action.StartupSeconds);
            }

            return true;
        }

        private void EvaluateAnimationFrame()
        {
            if (_currentAction == null || _body == null)
            {
                return;
            }

            int frame = _body.Frame;
            if (!_deliveryOpened && frame >= _currentAction.ActiveStartFrame)
            {
                BeginActiveWindow();
            }

            if (_deliveryOpened && frame > _currentAction.ActiveEndFrame)
            {
                EndActiveWindow();
            }

            if (frame >= _currentAction.EndFrame)
            {
                CompleteAction();
            }
        }

        private void UpdateFallback(float delta)
        {
            _fallbackRemaining -= Mathf.Max(0f, delta);
            if (_fallbackRemaining > 0f)
            {
                return;
            }

            switch (_fallbackPhase)
            {
                case FallbackPhase.Startup:
                    _fallbackPhase = FallbackPhase.Active;
                    _fallbackRemaining = Mathf.Max(0.01f, _currentAction.ActiveSeconds);
                    BeginActiveWindow();
                    break;
                case FallbackPhase.Active:
                    _fallbackPhase = FallbackPhase.Recovery;
                    _fallbackRemaining = Mathf.Max(0.01f, _currentAction.RecoverySeconds);
                    EndActiveWindow();
                    break;
                case FallbackPhase.Recovery:
                    CompleteAction();
                    break;
            }
        }

        private void BeginActiveWindow()
        {
            if (_currentAction == null || _deliveryOpened)
            {
                return;
            }

            _state.EnterAttackActive();
            if (_currentAction.DeliveryMode == CombatDeliveryMode.MeleeHitbox)
            {
                _hitbox.EnableHitbox(_currentAction, _actionFacing);
            }
            else
            {
                _hitbox.DisableHitbox();
            }

            _deliveryOpened = true;
            ActionReleased?.Invoke(_currentAction, _actionFacing);
        }

        private void EndActiveWindow()
        {
            if (_currentAction == null)
            {
                return;
            }

            _hitbox.DisableHitbox();
            _state.EnterAttackRecovery();
        }

        private void CompleteAction()
        {
            WeaponMovesetData moveset = _owner.ActiveMoveset;
            int nextIndex = _comboIndex + 1;
            bool canChain = _comboIndex >= 0
                && _bufferRemaining > 0f
                && moveset?.LightCombo != null
                && nextIndex < moveset.LightCombo.Count;

            if (canChain)
            {
                _hitbox.DisableHitbox();
                CombatActionData completedAction = _currentAction;
                _currentAction = null;
                ActionFinished?.Invoke(completedAction, true);
                if (!TryStartAction(nextIndex, true))
                {
                    // Combo đã được buffer nhưng không đủ stamina hoặc state vừa bị cưỡng bức.
                    // Phải kết thúc action cũ, nếu không actor sẽ mắc kẹt vĩnh viễn ở recovery.
                    FinishAction(true);
                }
                return;
            }

            FinishAction(true);
        }

        private void FinishAction(bool completed)
        {
            CombatActionData finishedAction = _currentAction;
            _hitbox?.DisableHitbox();
            _currentAction = null;
            _comboIndex = -1;
            _bufferRemaining = 0f;
            _deliveryOpened = false;
            _usingFrameAnimation = false;
            _actionFacing = Vector2.Down;
            _actionFacingCardinal = "down";
            _fallbackPhase = FallbackPhase.None;
            _fallbackRemaining = 0f;

            if (_body != null)
            {
                _body.SpeedScale = _baseBodySpeedScale;
                if (completed)
                {
                    _body.Stop();
                }
            }

            _state.FinishAttack();
            if (finishedAction != null)
            {
                ActionFinished?.Invoke(finishedAction, completed);
            }
        }
    }
}
