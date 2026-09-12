using System.Collections.Generic;
using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Policy collision mềm cho Decision Core.
    ///
    /// Đồng minh vẫn được giữ khoảng cách bằng formation + RVO/separation, nhưng thân CharacterBody2D
    /// không khóa cứng nhau. Nếu để physics body chặn nhau, AI có thể "né" đúng ở tầng steering
    /// nhưng vẫn bị Player/đồng minh ghì chân ở tầng MoveAndSlide.
    ///
    /// Đây là policy chung, không còn phụ thuộc HyouAI cũ. Mọi DecisionAgent có thể dùng lại.
    /// </summary>
    public sealed class CombatAllyCollisionPolicy
    {
        private readonly CombatCharacter _self;
        private readonly HashSet<ulong> _knownAllies = new();

        public CombatAllyCollisionPolicy(CombatCharacter self)
        {
            _self = self;
        }

        public int Refresh()
        {
            if (_self == null
                || !GodotObject.IsInstanceValid(_self)
                || !_self.IsInsideTree())
            {
                return 0;
            }

            int added = 0;
            foreach (Node node in _self.GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter ally
                    || ally == _self
                    || !GodotObject.IsInstanceValid(ally)
                    || ally.IsQueuedForDeletion()
                    || !FactionRules.AreAllies(_self.Faction, ally.Faction))
                {
                    continue;
                }

                ulong allyId = ally.GetInstanceId();
                if (!_knownAllies.Add(allyId))
                {
                    continue;
                }

                // Thêm hai chiều để thứ tự _PhysicsProcess không tạo một frame "A xuyên B nhưng B vẫn chặn A".
                _self.AddCollisionExceptionWith(ally);
                ally.AddCollisionExceptionWith(_self);
                added++;
            }

            return added;
        }
    }
}
