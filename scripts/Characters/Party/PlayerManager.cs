using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Entities.NPC;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class PlayerManager : Node
    {
        public const int MaxPartySize = 3;

        public static PlayerManager Instance { get; private set; }
        public List<PlayerStats> PartyMembers = new List<PlayerStats>();

        [Signal] public delegate void PartyUpdatedEventHandler();
        [Signal] public delegate void ActiveCharacterChangedEventHandler(int index);

        public int ActiveCharacterIndex { get; private set; } = 0;

        public static PlayerManager GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            PlayerManager existing = tree.Root.GetNodeOrNull<PlayerManager>("PlayerManager");
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                Instance = existing;
                return existing;
            }

            var manager = new PlayerManager { Name = "PlayerManager" };
            tree.Root.AddChild(manager);
            GD.Print("[PlayerManager] Created runtime fallback at /root/PlayerManager");
            return manager;
        }

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            ApplyControlOwnership();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            int targetIndex = key.Unicode switch
            {
                (uint)'1' => 0,
                (uint)'2' => 1,
                (uint)'3' => 2,
                _ => -1
            };

            if (targetIndex >= 0 && SwitchToCharacter(targetIndex))
            {
                GetViewport()?.SetInputAsHandled();
            }
        }

        private bool SwitchToCharacter(int index)
        {
            if (index < 0 || index >= PartyMembers.Count || index == ActiveCharacterIndex)
            {
                return false;
            }

            ActiveCharacterIndex = index;
            ApplyControlOwnership();
            EmitSignal(SignalName.ActiveCharacterChanged, index);
            return true;
        }

        public void SetActiveCharacter(int index)
        {
            if (index == ActiveCharacterIndex)
            {
                ApplyControlOwnership();
                return;
            }
            SwitchToCharacter(index);
        }

        public bool SetPartyLeader(int index)
        {
            if (index < 0 || index >= PartyMembers.Count)
            {
                return false;
            }

            if (index == ActiveCharacterIndex)
            {
                ApplyControlOwnership();
                return true;
            }

            return SwitchToCharacter(index);
        }

        public CombatCharacter GetActiveCombatCharacter()
        {
            if (ActiveCharacterIndex < 0 || ActiveCharacterIndex >= PartyMembers.Count)
            {
                return null;
            }
            return ResolveCombatCharacter(PartyMembers[ActiveCharacterIndex]);
        }

        public CombatCharacter GetCombatCharacter(PlayerStats stats)
        {
            return ResolveCombatCharacter(stats);
        }

        public bool MoveMember(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= PartyMembers.Count
                || toIndex < 0 || toIndex >= PartyMembers.Count
                || fromIndex == toIndex)
            {
                return false;
            }

            PlayerStats activeMember = ActiveCharacterIndex >= 0 && ActiveCharacterIndex < PartyMembers.Count
                ? PartyMembers[ActiveCharacterIndex]
                : null;

            PlayerStats movingMember = PartyMembers[fromIndex];
            PartyMembers.RemoveAt(fromIndex);
            PartyMembers.Insert(toIndex, movingMember);

            int previousActiveIndex = ActiveCharacterIndex;
            ActiveCharacterIndex = activeMember != null
                ? Mathf.Max(0, PartyMembers.IndexOf(activeMember))
                : 0;

            ApplyControlOwnership();
            EmitSignal(SignalName.PartyUpdated);
            if (previousActiveIndex != ActiveCharacterIndex)
            {
                EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
            }
            return true;
        }

        public List<string> CapturePartyOrder()
        {
            var result = new List<string>();
            foreach (PlayerStats member in PartyMembers)
            {
                string id = member?.ConfigData?.ID;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        public Dictionary<string, int> CaptureCompanionCommands()
        {
            var result = new Dictionary<string, int>();
            foreach (PlayerStats member in PartyMembers)
            {
                if (ResolveCombatCharacter(member) is not NpcCharacter companion)
                {
                    continue;
                }

                string id = member?.ConfigData?.ID;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[id] = (int)companion.CommandMode;
                }
            }
            return result;
        }

        public void RestoreCompanionCommands(IReadOnlyDictionary<string, int> savedModes)
        {
            if (savedModes == null || savedModes.Count == 0)
            {
                return;
            }

            foreach (PlayerStats member in PartyMembers)
            {
                string id = member?.ConfigData?.ID;
                if (string.IsNullOrWhiteSpace(id)
                    || !savedModes.TryGetValue(id, out int rawMode)
                    || ResolveCombatCharacter(member) is not NpcCharacter companion)
                {
                    continue;
                }

                CompanionCommandMode mode = rawMode >= (int)CompanionCommandMode.Follow
                    && rawMode <= (int)CompanionCommandMode.Wander
                        ? (CompanionCommandMode)rawMode
                        : CompanionCommandMode.Follow;
                companion.SetCommandMode(mode);
            }
        }

        public void RestorePartyOrder(IReadOnlyList<string> characterIds)
        {
            if (characterIds == null || characterIds.Count == 0 || PartyMembers.Count <= 1)
            {
                return;
            }

            var reordered = new List<PlayerStats>();
            foreach (string characterId in characterIds)
            {
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    continue;
                }

                PlayerStats match = PartyMembers.Find(member =>
                    member?.ConfigData != null
                    && member.ConfigData.ID == characterId
                    && !reordered.Contains(member));
                if (match != null)
                {
                    reordered.Add(match);
                }
            }

            foreach (PlayerStats member in PartyMembers)
            {
                if (member != null && !reordered.Contains(member))
                {
                    reordered.Add(member);
                }
            }

            if (reordered.Count != PartyMembers.Count)
            {
                return;
            }

            PartyMembers.Clear();
            PartyMembers.AddRange(reordered);
            ActiveCharacterIndex = Mathf.Clamp(ActiveCharacterIndex, 0, PartyMembers.Count - 1);
            ApplyControlOwnership();
            EmitSignal(SignalName.PartyUpdated);
        }

        public void RegisterMember(PlayerStats member)
        {
            if (member == null || PartyMembers.Contains(member) || PartyMembers.Count >= MaxPartySize)
            {
                return;
            }

            // Nhân vật Player thật luôn đứng đầu party. Hyou có thể enter tree trước vì nằm sẵn
            // trong world scene, nếu chỉ Add() thì game sẽ tự chọn Hyou làm leader lúc boot.
            if (member.GetParent() is global::Player)
            {
                PartyMembers.Insert(0, member);
                ActiveCharacterIndex = 0;
            }
            else
            {
                PartyMembers.Add(member);
            }

            ApplyControlOwnership();
            EmitSignal(SignalName.PartyUpdated);
            EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
        }

        public void UnregisterMember(PlayerStats member)
        {
            if (member == null)
            {
                return;
            }

            int removedIndex = PartyMembers.IndexOf(member);
            if (removedIndex < 0 || !PartyMembers.Remove(member))
            {
                return;
            }

            if (PartyMembers.Count == 0)
            {
                ActiveCharacterIndex = 0;
            }
            else if (removedIndex < ActiveCharacterIndex)
            {
                ActiveCharacterIndex--;
            }
            else if (ActiveCharacterIndex >= PartyMembers.Count)
            {
                ActiveCharacterIndex = PartyMembers.Count - 1;
            }

            ApplyControlOwnership();
            EmitSignal(SignalName.PartyUpdated);
            EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
        }

        public void ResetParty()
        {
            foreach (PlayerStats stats in PartyMembers)
            {
                CombatCharacter actor = ResolveCombatCharacter(stats);
                if (actor is global::Player player)
                {
                    player.UsePlayerInput = false;
                }
                else if (actor is NpcCharacter npc)
                {
                    npc.SetPlayerControlled(false);
                }
            }

            PartyMembers.Clear();
            ActiveCharacterIndex = 0;
            EmitSignal(SignalName.PartyUpdated);
            EmitSignal(SignalName.ActiveCharacterChanged, ActiveCharacterIndex);
        }

        private void ApplyControlOwnership()
        {
            if (PartyMembers.Count == 0)
            {
                return;
            }

            ActiveCharacterIndex = Mathf.Clamp(ActiveCharacterIndex, 0, PartyMembers.Count - 1);
            Camera2D sourceCamera = null;
            foreach (PlayerStats stats in PartyMembers)
            {
                if (ResolveCombatCharacter(stats) is global::Player main)
                {
                    sourceCamera = main.GetNodeOrNull<Camera2D>("follow");
                    if (sourceCamera != null) break;
                }
            }

            for (int i = 0; i < PartyMembers.Count; i++)
            {
                CombatCharacter actor = ResolveCombatCharacter(PartyMembers[i]);
                if (actor == null)
                {
                    continue;
                }

                bool active = i == ActiveCharacterIndex;
                if (actor is global::Player player)
                {
                    player.UsePlayerInput = active;
                    if (!active)
                    {
                        player.StopMoveInput();
                        player.SetBlocking(false);
                    }
                }
                else if (actor is NpcCharacter npc)
                {
                    npc.SetPlayerControlled(active);
                }

                Camera2D camera = actor.GetNodeOrNull<Camera2D>("follow");
                if (camera == null)
                {
                    continue;
                }

                camera.Enabled = active;
                if (!active)
                {
                    continue;
                }

                if (sourceCamera != null && sourceCamera != camera)
                {
                    camera.Zoom = sourceCamera.Zoom;
                    camera.LimitLeft = sourceCamera.LimitLeft;
                    camera.LimitTop = sourceCamera.LimitTop;
                    camera.LimitRight = sourceCamera.LimitRight;
                    camera.LimitBottom = sourceCamera.LimitBottom;
                }
                camera.CallDeferred("make_current");
            }
        }

        private static CombatCharacter ResolveCombatCharacter(PlayerStats stats)
        {
            return stats?.GetParent() as CombatCharacter;
        }
    }
}
