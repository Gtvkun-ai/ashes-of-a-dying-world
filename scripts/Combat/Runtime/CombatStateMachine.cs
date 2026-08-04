using System;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    public sealed class CombatStateMachine
    {
        public CombatStateId Current { get; private set; } = CombatStateId.Locomotion;
        public float Elapsed { get; private set; }
        public float Remaining { get; private set; }

        public event Action<CombatStateId, CombatStateId> StateChanged;

        public bool IsAttackState => Current == CombatStateId.AttackStartup
            || Current == CombatStateId.AttackActive
            || Current == CombatStateId.AttackRecovery;

        public bool IsForcedState => Current == CombatStateId.BlockStun
            || Current == CombatStateId.Hitstun
            || Current == CombatStateId.Stagger
            || Current == CombatStateId.GuardBreak
            || Current == CombatStateId.Dead;

        public bool CanMove => Current == CombatStateId.Locomotion || Current == CombatStateId.Blocking;
        public bool CanStartAttack => Current == CombatStateId.Locomotion;
        public bool CanStartBlock => Current == CombatStateId.Locomotion || Current == CombatStateId.Blocking;
        public bool CanRegenerateMana => Current == CombatStateId.Locomotion || Current == CombatStateId.Blocking;
        public bool CanRegenerateStamina => Current == CombatStateId.Locomotion;
        public bool CanRegenerateGuard => Current != CombatStateId.Blocking && !IsAttackState && Current != CombatStateId.Dead;
        public bool CanRegeneratePoise => Current == CombatStateId.Locomotion || Current == CombatStateId.Blocking;

        public void Tick(float delta)
        {
            Elapsed += MathF.Max(0f, delta);
            if (Remaining <= 0f)
            {
                return;
            }

            Remaining = MathF.Max(0f, Remaining - delta);
            if (Remaining <= 0f && IsTimedForcedState(Current))
            {
                TransitionTo(CombatStateId.Locomotion);
            }
        }

        public bool TryBeginBlock()
        {
            if (!CanStartBlock)
            {
                return false;
            }

            TransitionTo(CombatStateId.Blocking);
            return true;
        }

        public void EndBlock()
        {
            if (Current == CombatStateId.Blocking)
            {
                TransitionTo(CombatStateId.Locomotion);
            }
        }

        public bool TryBeginAttack(bool allowChain = false)
        {
            if (!CanStartAttack && !(allowChain && IsAttackState))
            {
                return false;
            }

            TransitionTo(CombatStateId.AttackStartup);
            return true;
        }

        public void EnterAttackActive()
        {
            if (IsAttackState)
            {
                TransitionTo(CombatStateId.AttackActive);
            }
        }

        public void EnterAttackRecovery()
        {
            if (IsAttackState)
            {
                TransitionTo(CombatStateId.AttackRecovery);
            }
        }

        public void FinishAttack()
        {
            if (IsAttackState)
            {
                TransitionTo(CombatStateId.Locomotion);
            }
        }

        public void EnterBlockStun(float seconds) => TransitionTo(CombatStateId.BlockStun, seconds);
        public void EnterHitstun(float seconds) => TransitionTo(CombatStateId.Hitstun, seconds);
        public void EnterStagger(float seconds) => TransitionTo(CombatStateId.Stagger, seconds);
        public void EnterGuardBreak(float seconds) => TransitionTo(CombatStateId.GuardBreak, seconds);
        public void EnterDead() => TransitionTo(CombatStateId.Dead);

        public void Reset()
        {
            TransitionTo(CombatStateId.Locomotion);
        }

        private static bool IsTimedForcedState(CombatStateId state)
        {
            return state == CombatStateId.BlockStun
                || state == CombatStateId.Hitstun
                || state == CombatStateId.Stagger
                || state == CombatStateId.GuardBreak;
        }

        private void TransitionTo(CombatStateId next, float duration = 0f)
        {
            CombatStateId previous = Current;
            Current = next;
            Elapsed = 0f;
            Remaining = MathF.Max(0f, duration);
            if (previous != next)
            {
                StateChanged?.Invoke(previous, next);
            }
        }
    }
}
