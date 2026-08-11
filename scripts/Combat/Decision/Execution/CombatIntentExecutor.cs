using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Movement;
using AshesofaDyingWorld.Combat.Decision.Profiles;
using AshesofaDyingWorld.Combat.Decision.Runtime;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Decision.Execution
{
    /// <summary>
    /// Cầu nối duy nhất từ intent sang CombatCharacter mechanics.
    ///
    /// Quyết định chiến thuật chạy theo nhịp chậm, còn motor chạy mỗi physics frame. Tách hai nhịp
    /// này là bắt buộc: nếu chỉ cập nhật hướng sau mỗi 0.12 giây, companion sẽ đi kiểu stop-motion
    /// dù utility, scheduler và log đều trông rất trí tuệ.
    /// </summary>
    public sealed class CombatIntentExecutor
    {
        private const float FollowRadiusBehind = 54f;
        private const float FollowRadiusSide = 24f;
        private const float FollowStopDistance = 7f;
        private const float FollowSlowDistance = 42f;
        private const float FollowSeparationDistance = 36f;
        private const float FollowRunEnterDistance = 150f;
        private const float FollowRunExitDistance = 105f;
        private const float FollowRunEnterStaminaRatio = 0.40f;
        private const float FollowRunExitStaminaRatio = 0.24f;
        private const float FollowHeadingResponse = 4.5f;

        private readonly CombatCharacter _self;
        private readonly CombatClassProfile _classProfile;
        private readonly float _followSide;

        private Vector2 _followForward = Vector2.Down;
        private bool _followForwardInitialized;
        private bool _followRunLatched;

        public CombatIntentExecutor(CombatCharacter self, CombatClassProfile classProfile)
        {
            _self = self;
            _classProfile = classProfile;
            _followSide = self != null && (self.GetInstanceId() & 1UL) == 0UL ? 1f : -1f;
        }

        /// <summary>
        /// Thực thi phần rời rạc của một quyết định mới: guard hoặc kích hoạt skill.
        /// Movement không được bấm ở đây; TickMotor sẽ giữ nó mượt ở tần số physics.
        /// </summary>
        public bool Execute(
            in CombatIntent intent,
            in CombatSnapshot snapshot,
            in MovementCommand movement,
            CombatBlackboard blackboard)
        {
            if (_self == null || !_self.IsAlive || blackboard == null)
            {
                return false;
            }

            if (!snapshot.HasTarget)
            {
                _self.SetBlocking(false);
                return false;
            }

            if (intent.IsNone)
            {
                _self.SetBlocking(false);
                return false;
            }

            // Bộ kỹ năng hiện có chưa có dash riêng. PanicEvade dùng chính run locomotion:
            // ngắt cast/đòn đang chuẩn bị, quay theo hướng thoát và chạy thật bằng stamina.
            if (intent.Type == CombatIntentType.PanicEvade)
            {
                if (!blackboard.TryBeginPanicEvade(_classProfile?.PanicEvadeCooldownSeconds ?? 1.25f))
                {
                    return false;
                }

                _self.SetBlocking(false);
                if (_self.IsPerformingAttack)
                {
                    _self.Actions?.Cancel();
                    blackboard.RecentCastInterruptsWindow = Mathf.Max(
                        blackboard.RecentCastInterruptsWindow,
                        1.2f);
                }

                return movement.HasMovement;
            }

            _self.FaceToward(snapshot.TargetPosition);

            if (intent.Type == CombatIntentType.Guard)
            {
                _self.StopMoveInput();
                _self.SetBlocking(snapshot.CanBlock && snapshot.ThreatBlockable);
                return true;
            }

            _self.SetBlocking(false);

            // MeleePrimary dùng moveset đang cầm. Với Hyou, default moveset v7 là combo kiếm 2 nhát.
            // RequestAttack khi action đang chạy chỉ ghi input buffer, vì vậy combo vẫn đi qua
            // CombatActionRunner chứ executor không tự nhảy frame hay spawn hitbox ngoài luồng.
            if (intent.Type == CombatIntentType.MeleePrimary)
            {
                _self.StopMoveInput();
                bool wasRunning = _self.IsPerformingAttack;
                bool accepted = _self.RequestAttack();
                if (accepted && !wasRunning)
                {
                    blackboard.RecordActionExecution(new StringName("melee_primary"), 3.2f);
                    GD.Print($"[CombatRhythm] actor={_self.CombatantId} action=melee_primary mode=sword");
                }
                return accepted;
            }

            if (_self.IsPerformingAttack)
            {
                return true;
            }

            if (IsCastIntent(intent.Type))
            {
                _self.StopMoveInput();
                SkillData skill = ResolveSkill(intent.ActionId.ToString());
                if (skill == null)
                {
                    blackboard.FailedActionCooldowns[new StringName(intent.ActionId.ToString())] = 0.5f;
                    return false;
                }

                if (_self.Abilities == null || _self.Abilities.GetCooldownRemaining(skill) > 0f)
                {
                    blackboard.FailedActionCooldowns[new StringName(skill.SkillId ?? "skill")] = 0.10f;
                    return false;
                }

                Vector2 aimDirection = snapshot.DirectionToTarget.LengthSquared() <= 0.001f
                    ? _self.FacingDirection
                    : snapshot.DirectionToTarget;
                CombatCharacter aimTarget = ResolveCombatant(snapshot.TargetId);
                bool activated = _self.Abilities.TryActivate(skill, aimDirection, aimTarget);
                StringName skillKey = new(skill.SkillId ?? "skill");
                if (activated)
                {
                    blackboard.ActionCooldowns[skillKey] = Mathf.Max(0f, skill.Cooldown);
                    blackboard.FailedActionCooldowns.Remove(skillKey);
                    blackboard.RecordActionExecution(
                        skillKey,
                        _classProfile?.RepositionAfterActionSeconds ?? 3.8f);
                }
                else
                {
                    blackboard.FailedActionCooldowns[skillKey] = 0.18f;
                }
                return activated;
            }

            // Approach/backpedal/strafe/reposition chỉ thay đổi policy. Motor áp lệnh mỗi frame.
            return movement.HasMovement;
        }

        /// <summary>
        /// Motor liên tục. Khi chưa có target, Hyou giữ một formation anchor ổn định cạnh Player
        /// thay vì pursuit thẳng vào tâm Player rồi bật qua lại giữa hai ngưỡng khoảng cách.
        /// </summary>
        public MovementCommand TickMotor(
            CombatCharacter leader,
            bool hasTarget,
            in CombatIntent intent,
            in MovementCommand tacticalMovement,
            float deltaSeconds)
        {
            if (_self == null || !_self.IsAlive)
            {
                return MovementCommand.Stop(Vector2.Zero);
            }

            if (!hasTarget)
            {
                return TickFollowLeader(leader, deltaSeconds);
            }

            _followRunLatched = false;
            if (_self.IsPerformingAttack
                || intent.Type == CombatIntentType.Guard
                || IsCastIntent(intent.Type)
                || _self.StateMachine?.CanMove != true)
            {
                _self.StopMoveInput();
                return MovementCommand.Stop(tacticalMovement.FacePosition);
            }

            if (tacticalMovement.HasMovement)
            {
                if (tacticalMovement.PreserveFacing)
                {
                    _self.FaceToward(tacticalMovement.FacePosition);
                }
                _self.SetMoveInput(
                    tacticalMovement.Direction,
                    tacticalMovement.WantsRun,
                    tacticalMovement.PreserveFacing);
                return tacticalMovement;
            }

            _self.StopMoveInput();
            return MovementCommand.Stop(tacticalMovement.FacePosition);
        }

        public void ReleaseCommands()
        {
            if (_self == null || !GodotObject.IsInstanceValid(_self))
            {
                return;
            }

            _self.StopMoveInput();
            _self.SetBlocking(false);
            _followRunLatched = false;
            _followForwardInitialized = false;
        }

        private MovementCommand TickFollowLeader(CombatCharacter leader, float deltaSeconds)
        {
            _self.SetBlocking(false);
            if (!IsUsable(leader)
                || !leader.IsAlive
                || _self.StateMachine?.CanMove != true
                || _self.IsPerformingAttack)
            {
                _self.StopMoveInput();
                _followRunLatched = false;
                return MovementCommand.Stop(IsUsable(leader) ? leader.CombatCenter : _self.CombatCenter);
            }

            float dt = Mathf.Max(0f, deltaSeconds);
            Vector2 selfPosition = _self.CombatCenter;
            Vector2 leaderPosition = leader.CombatCenter;
            Vector2 relative = leaderPosition - selfPosition;
            Vector2 leaderVelocity = leader.Velocity;

            if (!_followForwardInitialized)
            {
                _followForward = relative.LengthSquared() > 0.001f
                    ? relative.Normalized()
                    : SafeDirection(leader.FacingDirection, Vector2.Down);
                _followForwardInitialized = true;
            }

            // Chỉ xoay formation theo hướng chuyển động thật. Khi Player đứng yên và đổi mặt,
            // anchor không nhảy 90 độ quanh người như một vệ tinh bị lỗi firmware.
            if (leaderVelocity.LengthSquared() > 64f)
            {
                Vector2 desiredForward = leaderVelocity.Normalized();
                float blend = 1f - Mathf.Exp(-FollowHeadingResponse * dt);
                Vector2 blended = _followForward.Lerp(desiredForward, blend);
                if (blended.LengthSquared() > 0.001f)
                {
                    _followForward = blended.Normalized();
                }
            }

            Vector2 side = new(-_followForward.Y, _followForward.X);
            Vector2 anchor = leaderPosition
                - _followForward * FollowRadiusBehind
                + side * (FollowRadiusSide * _followSide);
            Vector2 toAnchor = anchor - selfPosition;

            // Tách thân mềm nếu Player quay lại đè đúng lên Hyou.
            float leaderDistance = relative.Length();
            if (leaderDistance > 0.001f && leaderDistance < FollowSeparationDistance)
            {
                float pressure = 1f - leaderDistance / FollowSeparationDistance;
                toAnchor += (-relative.Normalized()) * (FollowSeparationDistance * pressure * 1.35f);
            }

            float anchorDistance = toAnchor.Length();
            float staminaRatio = _self.Stats?.MaxStamina > 0.001f
                ? Mathf.Clamp(_self.Stats.CurrentStamina / _self.Stats.MaxStamina, 0f, 1f)
                : 1f;

            if (_followRunLatched)
            {
                if (anchorDistance <= FollowRunExitDistance || staminaRatio <= FollowRunExitStaminaRatio)
                {
                    _followRunLatched = false;
                }
            }
            else if (anchorDistance >= FollowRunEnterDistance
                && staminaRatio >= FollowRunEnterStaminaRatio)
            {
                _followRunLatched = true;
            }

            if (anchorDistance <= FollowStopDistance)
            {
                _self.StopMoveInput();
                return MovementCommand.Stop(anchor);
            }

            Vector2 direction = toAnchor / Mathf.Max(anchorDistance, 0.001f);
            float speedScale = _followRunLatched
                ? 1f
                : Mathf.Clamp(
                    (anchorDistance - FollowStopDistance)
                    / Mathf.Max(1f, FollowSlowDistance - FollowStopDistance),
                    0.18f,
                    1f);

            _self.SetMoveInput(direction, _followRunLatched, false, speedScale);
            float score = Mathf.Clamp(anchorDistance / FollowSlowDistance, 0f, 1f);
            return new MovementCommand(
                direction,
                _followRunLatched,
                false,
                anchor,
                -2, // -2 = formation follow, để log không còn giả vờ move=(0,0).
                score);
        }

        private SkillData ResolveSkill(string actionId)
        {
            string wanted = (actionId ?? string.Empty).Trim();
            if (_classProfile?.GrantedSkills != null)
            {
                foreach (SkillData skill in _classProfile.GrantedSkills)
                {
                    if (SkillMatches(skill, wanted))
                    {
                        return skill;
                    }
                }
            }

            var activeSkills = _self.Stats?.ConfigData?.ActiveSkills;
            if (activeSkills != null)
            {
                foreach (SkillData skill in activeSkills)
                {
                    if (SkillMatches(skill, wanted))
                    {
                        return skill;
                    }
                }
            }

            return null;
        }

        private static bool SkillMatches(SkillData skill, string wanted)
        {
            if (skill == null)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(wanted)
                || string.Equals(skill.SkillId, wanted, System.StringComparison.OrdinalIgnoreCase);
        }

        private CombatCharacter ResolveCombatant(ulong? instanceId)
        {
            if (!instanceId.HasValue || _self?.GetTree() == null)
            {
                return null;
            }

            foreach (Node node in _self.GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is CombatCharacter combatant
                    && combatant.GetInstanceId() == instanceId.Value
                    && combatant.IsAlive
                    && !combatant.IsQueuedForDeletion())
                {
                    return combatant;
                }
            }

            return null;
        }

        private static bool IsCastIntent(CombatIntentType type)
        {
            return type == CombatIntentType.CastPrimary
                || type == CombatIntentType.CastSecondary
                || type == CombatIntentType.CastDefensive;
        }

        private static Vector2 SafeDirection(Vector2 candidate, Vector2 fallback)
        {
            return candidate.LengthSquared() > 0.001f ? candidate.Normalized() : fallback;
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
