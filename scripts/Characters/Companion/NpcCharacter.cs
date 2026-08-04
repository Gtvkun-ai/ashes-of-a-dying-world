using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.Entities.NPC
{
    public partial class NpcCharacter : CombatCharacter
    {
        [Export] public bool IsRecruitable { get; set; } = true;
        [Export] public bool StartRecruited { get; set; } = false;

        private bool _isRecruited;

        protected override void OnCombatReady()
        {
            Faction = CombatFaction.Companion;
            AddToGroup("Companion");
            if (StartRecruited)
            {
                Recruit();
            }
        }

        public void Recruit()
        {
            if (!IsRecruitable || _isRecruited || Stats == null)
            {
                return;
            }

            PlayerManager.Instance?.RegisterMember(Stats);
            _isRecruited = true;
        }

        protected override void OnDefeated(CombatCharacter attacker)
        {
            if (_isRecruited && Stats != null)
            {
                PlayerManager.Instance?.UnregisterMember(Stats);
                _isRecruited = false;
            }
        }
    }
}
