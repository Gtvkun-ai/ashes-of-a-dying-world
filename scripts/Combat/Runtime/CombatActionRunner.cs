using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Chạy action từ Resource: stamina, frame window, combo buffer, delivery và recovery.
    /// Melee mở CombatHitbox; event timeline phát payload đúng một lần cho dispatcher.
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
        private bool _initializingFrameAnimation;
        private bool _deliveryOpened;
        private Vector2 _actionFacing = Vector2.Down;
        private string _actionFacingCardinal = "down";
        private CombatCharacter _aimTarget;
        private FallbackPhase _fallbackPhase;
        private float _fallbackRemaining;
        private float _actionElapsedSeconds;
        private float _actionDurationSeconds;
        private readonly HashSet<int> _triggeredEventIndices = new();

        public event Action<CombatActionData, Vector2> ActionStarted;
        public event Action<CombatActionData, Vector2> ActionReleased;
        public event Action<CombatActionData, CombatActionEventData, Vector2> ActionEventTriggered;
        public event Action<CombatActionData, bool> ActionFinished;

        public CombatActionData CurrentAction => _currentAction;
        public bool IsRunning => _currentAction != null;
        public bool IsHitboxActive => _hitbox?.IsActive == true;
        public Vector2 ActionFacing => _actionFacing;
        public CombatCharacter CurrentAimTarget => IsUsableAimTarget(_aimTarget) ? _aimTarget : null;

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
            return TryStartAbilityAction(action, aimDirection, null);
        }

        /// <summary>
        /// Giữ target thật của projectile trong suốt cast. Hướng animation vẫn khóa lúc bắt đầu,
        /// nhưng delivery có thể ngắm lại target ở frame release thay vì bắn vào vị trí cũ.
        /// </summary>
        public bool TryStartAbilityAction(
            CombatActionData action,
            Vector2 aimDirection,
            CombatCharacter aimTarget)
        {
            if (action == null || _currentAction != null)
            {
                return false;
            }

            Vector2? forcedFacing = aimDirection.LengthSquared() > 0.001f
                ? aimDirection.Normalized()
                : null;
            return TryStartResolvedAction(action, -1, false, forcedFacing, aimTarget);
        }

        public void Update(float delta)
        {
            float dt = Mathf.Max(0f, delta);
            _bufferRemaining = Mathf.Max(0f, _bufferRemaining - dt);
            if (_currentAction == null)
            {
                return;
            }

            _actionElapsedSeconds = Mathf.Min(
                Mathf.Max(0.01f, _actionDurationSeconds),
                _actionElapsedSeconds + dt);

            if (_usingFrameAnimation)
            {
                EvaluateAnimationFrame();
                return;
            }

            EvaluateActionEventsNormalized(
                _actionDurationSeconds <= 0f
                    ? 1f
                    : _actionElapsedSeconds / _actionDurationSeconds);
            UpdateFallback(dt);
        }

        public void HandleBodyFrameChanged()
        {
            // AnimatedSprite2D phát FrameChanged đồng bộ ngay khi đổi Animation/Frame.
            // Trong lúc khởi tạo action, callback đó không được phép đánh giá frame cũ
            // (thường là EndFrame=7 của lần cast trước) rồi kết thúc action vừa sinh ra.
            if (_initializingFrameAnimation)
            {
                return;
            }

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
            return TryStartResolvedAction(action, comboIndex, allowChain, null, null);
        }

        private bool TryStartResolvedAction(
            CombatActionData action,
            int comboIndex,
            bool allowChain,
            Vector2? forcedFacing,
            CombatCharacter aimTarget)
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
            _aimTarget = IsUsableAimTarget(aimTarget) ? aimTarget : null;
            _comboIndex = comboIndex;
            _bufferRemaining = 0f;
            _deliveryOpened = false;
            _actionElapsedSeconds = 0f;
            _actionDurationSeconds = Mathf.Max(
                0.01f,
                action.StartupSeconds + action.ActiveSeconds + action.RecoverySeconds);
            _triggeredEventIndices.Clear();
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

            string animationName = action.ResolveAnimation(_actionFacingCardinal);
            _usingFrameAnimation = _body?.SpriteFrames != null
                && !string.IsNullOrWhiteSpace(animationName)
                && _body.SpriteFrames.HasAnimation(animationName);

            if (_usingFrameAnimation)
            {
                float attackSpeedScale = action.ScalePlaybackWithAttackSpeed
                    ? Mathf.Max(
                        0.1f,
                        (_owner.Stats?.AttackSpeed ?? 1f) * _owner.RuntimeAttackSpeedMultiplier)
                    : 1f;

                // Quan trọng: chuẩn bị body hoàn chỉnh TRƯỚC khi phát ActionStarted.
                // Trước đây ActionStarted bật VFX khi body vẫn có thể còn ở frame 7.
                // Việc đổi animation/frame sau đó phát FrameChanged đồng bộ, callback thấy
                // frame cuối và CompleteAction ngay lập tức. Kết quả là CAST START/STOP
                // lặp mỗi physics frame, còn người chơi thấy... không khí.
                _initializingFrameAnimation = true;
                try
                {
                    _body.SpeedScale = _baseBodySpeedScale
                        * attackSpeedScale
                        * Mathf.Max(0.1f, action.PlaybackSpeedMultiplier);
                    _body.Animation = animationName;
                    _body.SpriteFrames.SetAnimationLoop(animationName, false);
                    _body.Frame = Mathf.Clamp(
                        action.StartFrame,
                        0,
                        _body.SpriteFrames.GetFrameCount(animationName) - 1);
                    _body.Play();
                }
                finally
                {
                    _initializingFrameAnimation = false;
                }
            }
            else
            {
                _fallbackPhase = FallbackPhase.Startup;
                _fallbackRemaining = Mathf.Max(0.01f, action.StartupSeconds);
            }

            // Listener presentation chỉ được thấy action sau khi runtime đã ở trạng thái
            // nhất quán. Đây là ranh giới ownership, không phải nghi lễ phát signal cho vui.
            ActionStarted?.Invoke(_currentAction, _actionFacing);
            CombatFeedbackService.GetOrCreate(_owner.GetTree())?
                .PlayActionStarted(_owner, _currentAction);

            if (_usingFrameAnimation)
            {
                EvaluateAnimationFrame();
            }
            else
            {
                EvaluateActionEventsNormalized(0f);
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
            EvaluateActionEventsFrame(frame);
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
                    EvaluateActionEventsNormalized(1f);
                    CompleteAction();
                    break;
            }
        }

        private void EvaluateActionEventsFrame(int currentFrame)
        {
            if (_currentAction?.Events == null)
            {
                return;
            }

            for (int index = 0; index < _currentAction.Events.Count; index++)
            {
                CombatActionEventData actionEvent = _currentAction.Events[index];
                if (actionEvent == null
                    || _triggeredEventIndices.Contains(index)
                    || !actionEvent.IsDueAtFrame(_currentAction, currentFrame))
                {
                    continue;
                }

                TriggerActionEvent(index, actionEvent);
            }
        }

        private void EvaluateActionEventsNormalized(float normalizedTime)
        {
            if (_currentAction?.Events == null)
            {
                return;
            }

            float safeNormalized = Mathf.Clamp(normalizedTime, 0f, 1f);
            for (int index = 0; index < _currentAction.Events.Count; index++)
            {
                CombatActionEventData actionEvent = _currentAction.Events[index];
                if (actionEvent == null
                    || _triggeredEventIndices.Contains(index)
                    || !actionEvent.IsDueAtNormalizedTime(_currentAction, safeNormalized))
                {
                    continue;
                }

                TriggerActionEvent(index, actionEvent);
            }
        }

        private void TriggerActionEvent(int index, CombatActionEventData actionEvent)
        {
            // Ghi fired trước khi gọi listener để callback re-entrant không thể phát đôi event.
            _triggeredEventIndices.Add(index);
            ActionEventTriggered?.Invoke(_currentAction, actionEvent, _actionFacing);
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
                CombatFeedbackService.GetOrCreate(_owner.GetTree())?
                    .PlaySwing(_owner, _currentAction, _actionFacing);
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
                _aimTarget = null;
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
            _initializingFrameAnimation = false;
            _actionFacing = Vector2.Down;
            _actionFacingCardinal = "down";
            _aimTarget = null;
            _fallbackPhase = FallbackPhase.None;
            _fallbackRemaining = 0f;
            _actionElapsedSeconds = 0f;
            _actionDurationSeconds = 0f;
            _triggeredEventIndices.Clear();

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

        private static bool IsUsableAimTarget(CombatCharacter target)
        {
            return target != null
                && GodotObject.IsInstanceValid(target)
                && !target.IsQueuedForDeletion()
                && target.IsAlive;
        }
    }
}
