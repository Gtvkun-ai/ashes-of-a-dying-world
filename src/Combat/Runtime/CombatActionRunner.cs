using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Chạy action từ Resource: stamina, frame window, combo buffer, hitbox và recovery.
    /// Player không còn chứa các nhánh attackStep == 1/2.
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
        private bool _hitboxOpened;
        private FallbackPhase _fallbackPhase;
        private float _fallbackRemaining;

        public CombatActionData CurrentAction => _currentAction;
        public bool IsRunning => _currentAction != null;
        public bool IsHitboxActive => _hitbox?.IsActive == true;

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

                return _owner.FacingDirection
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
            if (action == null || _currentAction != null)
            {
                return false;
            }

            return TryStartResolvedAction(action, -1, false);
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
            return TryStartResolvedAction(action, comboIndex, allowChain);
        }

        private bool TryStartResolvedAction(CombatActionData action, int comboIndex, bool allowChain)
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
            _hitboxOpened = false;
            _hitbox.DisableHitbox();

            string animationName = action.ResolveAnimation(_owner.FacingCardinal);
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
            if (!_hitboxOpened && frame >= _currentAction.ActiveStartFrame)
            {
                _state.EnterAttackActive();
                _hitbox.EnableHitbox(_currentAction, _owner.FacingDirection);
                _hitboxOpened = true;
            }

            if (_hitboxOpened && frame > _currentAction.ActiveEndFrame)
            {
                _hitbox.DisableHitbox();
                _state.EnterAttackRecovery();
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
                    _state.EnterAttackActive();
                    _hitbox.EnableHitbox(_currentAction, _owner.FacingDirection);
                    _hitboxOpened = true;
                    break;
                case FallbackPhase.Active:
                    _fallbackPhase = FallbackPhase.Recovery;
                    _fallbackRemaining = Mathf.Max(0.01f, _currentAction.RecoverySeconds);
                    _hitbox.DisableHitbox();
                    _state.EnterAttackRecovery();
                    break;
                case FallbackPhase.Recovery:
                    CompleteAction();
                    break;
            }
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
                _currentAction = null;
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
            _hitbox?.DisableHitbox();
            _currentAction = null;
            _comboIndex = -1;
            _bufferRemaining = 0f;
            _hitboxOpened = false;
            _usingFrameAnimation = false;
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
        }
    }
}
