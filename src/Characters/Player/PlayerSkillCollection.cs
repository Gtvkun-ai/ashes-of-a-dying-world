using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Core.Skills
{
    /// <summary>
    /// Quản lý tập kỹ năng, tiến trình và loadout của người chơi ở runtime.
    ///
    /// CharacterConfig chỉ cung cấp danh sách định nghĩa ban đầu.
    /// Lớp này tuyệt đối không đổi thứ tự hoặc sửa nội dung Resource khi người chơi trang bị kỹ năng.
    /// </summary>
    public sealed class PlayerSkillCollection
    {
        public const int SlotCount = 4;

        private readonly Dictionary<string, SkillData> _definitionsById = new();
        private readonly Dictionary<string, PlayerSkillState> _statesById = new();
        private readonly SkillData[] _equippedSlots = new SkillData[SlotCount];

        public event Action Changed;

        public int UnspentSkillPoints { get; private set; }

        /// <summary>
        /// Khởi tạo từ CharacterConfig. ActiveSkills hiện được dùng như danh sách kỹ năng mặc định,
        /// nhưng sau khi khởi tạo mọi thay đổi loadout chỉ diễn ra trong runtime state.
        /// </summary>
        public void Initialize(CharacterConfig config)
        {
            _definitionsById.Clear();
            _statesById.Clear();
            Array.Clear(_equippedSlots, 0, _equippedSlots.Length);
            UnspentSkillPoints = 0;

            if (config == null)
            {
                Changed?.Invoke();
                return;
            }

            RegisterDefinitions(config.ActiveSkills);
            RegisterDefinitions(config.ComboSequence);
            RegisterDefinitionsFromTree(config.SkillTree);

            // Giữ hành vi cũ: các kỹ năng chủ động đầu tiên trong ActiveSkills được xếp vào slot mặc định.
            // Điểm khác biệt là chúng không còn bị tráo trực tiếp trong CharacterConfig nữa.
            int nextSlot = 0;
            if (config.ActiveSkills != null)
            {
                foreach (SkillData skill in config.ActiveSkills)
                {
                    if (skill == null || skill.Category != SkillCategory.Active || nextSlot >= SlotCount)
                    {
                        continue;
                    }

                    string skillId = NormalizeSkillId(skill);
                    if (!_statesById.TryGetValue(skillId, out PlayerSkillState state) || !state.IsUnlocked)
                    {
                        continue;
                    }

                    EquipInternal(skill, state, nextSlot);
                    nextSlot++;
                }
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<SkillData> GetDefinitions()
        {
            return new List<SkillData>(_definitionsById.Values);
        }

        public bool Contains(SkillData skill)
        {
            string skillId = NormalizeSkillId(skill);
            return !string.IsNullOrWhiteSpace(skillId) && _definitionsById.ContainsKey(skillId);
        }

        /// <summary>
        /// Kiểm tra quyền sở hữu thực tế. Contains chỉ nói rằng định nghĩa đã được đăng ký,
        /// còn IsUnlocked mới phản ánh tiến trình của nhân vật.
        /// </summary>
        public bool IsUnlocked(SkillData skill)
        {
            PlayerSkillState state = GetState(skill);
            return state != null && state.IsUnlocked;
        }

        /// <summary>
        /// Mở một kỹ năng đã có trong cây và trừ điểm. Luật prerequisite/level được
        /// SkillTreeProgression kiểm tra trước; collection chỉ thực hiện thay đổi state.
        /// </summary>
        public bool TryUnlock(SkillData skill, int pointCost)
        {
            string skillId = NormalizeSkillId(skill);
            int cost = Math.Max(0, pointCost);
            if (!_definitionsById.TryGetValue(skillId, out SkillData definition)
                || !_statesById.TryGetValue(skillId, out PlayerSkillState state)
                || state.IsUnlocked
                || UnspentSkillPoints < cost)
            {
                return false;
            }

            state.IsUnlocked = true;
            state.Level = Math.Clamp(state.Level, 1, Math.Max(1, definition.MaxLevel));
            UnspentSkillPoints -= cost;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Đồng bộ điểm kỹ năng với cấp nhân vật. Bản hiện tại cấp một điểm mỗi level;
        /// điểm đã tiêu được suy ra từ node mở khóa và cấp kỹ năng, nên mở lại panel
        /// không thể vô tình phát điểm lần hai.
        /// </summary>
        public void RecalculateUnspentSkillPoints(int characterLevel, CharacterSkillTreeData tree)
        {
            int earned = Math.Max(1, characterLevel);
            int spent = 0;

            foreach (var pair in _statesById)
            {
                PlayerSkillState state = pair.Value;
                if (state == null || !state.IsUnlocked)
                {
                    continue;
                }

                spent += Math.Max(0, state.Level - 1);
                SkillData definition = _definitionsById.TryGetValue(pair.Key, out SkillData found) ? found : null;
                SkillTreeNodeData node = FindTreeNodeBySkillId(tree, pair.Key);
                if (definition == null || definition.DefaultUnlocked || node?.GrantedByDefault == true)
                {
                    continue;
                }

                if (node != null)
                {
                    spent += Math.Max(0, node.SkillPointCost);
                }
            }

            UnspentSkillPoints = Math.Max(0, earned - spent);
            Changed?.Invoke();
        }

        public SkillData GetDefinition(string skillId)
        {
            string normalized = NormalizeSkillId(skillId);
            return _definitionsById.TryGetValue(normalized, out SkillData skill) ? skill : null;
        }

        public PlayerSkillState GetState(SkillData skill)
        {
            return GetState(NormalizeSkillId(skill));
        }

        public PlayerSkillState GetState(string skillId)
        {
            string normalized = NormalizeSkillId(skillId);
            return _statesById.TryGetValue(normalized, out PlayerSkillState state) ? state : null;
        }

        public SkillData GetEquippedSkill(int slot)
        {
            return slot >= 0 && slot < SlotCount ? _equippedSlots[slot] : null;
        }

        public bool TryEquip(SkillData skill, int slot)
        {
            if (skill == null || slot < 0 || slot >= SlotCount || skill.Category != SkillCategory.Active)
            {
                return false;
            }

            string skillId = NormalizeSkillId(skill);
            if (!_definitionsById.TryGetValue(skillId, out SkillData registeredSkill)
                || !_statesById.TryGetValue(skillId, out PlayerSkillState state)
                || !state.IsUnlocked)
            {
                return false;
            }

            // Nếu kỹ năng đang nằm ở slot khác, dọn slot cũ trước.
            if (state.EquippedSlot >= 0 && state.EquippedSlot < SlotCount)
            {
                _equippedSlots[state.EquippedSlot] = null;
            }

            // Kỹ năng đang chiếm slot đích được tháo ra nhưng không bị khóa hay mất cấp.
            SkillData previous = _equippedSlots[slot];
            if (previous != null)
            {
                PlayerSkillState previousState = GetState(previous);
                if (previousState != null)
                {
                    previousState.EquippedSlot = -1;
                }
            }

            EquipInternal(registeredSkill, state, slot);
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip(int slot)
        {
            if (slot < 0 || slot >= SlotCount || _equippedSlots[slot] == null)
            {
                return false;
            }

            PlayerSkillState state = GetState(_equippedSlots[slot]);
            if (state != null)
            {
                state.EquippedSlot = -1;
            }

            _equippedSlots[slot] = null;
            Changed?.Invoke();
            return true;
        }

        public bool TryUpgrade(SkillData skill)
        {
            if (skill == null || UnspentSkillPoints <= 0)
            {
                return false;
            }

            PlayerSkillState state = GetState(skill);
            int maxLevel = Math.Max(1, skill.MaxLevel);
            if (state == null || !state.IsUnlocked || state.Level >= maxLevel)
            {
                return false;
            }

            state.Level++;
            UnspentSkillPoints--;
            Changed?.Invoke();
            return true;
        }

        public void GrantSkillPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            UnspentSkillPoints += amount;
            Changed?.Invoke();
        }

        public void SetUnspentSkillPoints(int amount)
        {
            UnspentSkillPoints = Math.Max(0, amount);
            Changed?.Invoke();
        }

        public List<PlayerSkillState> CaptureStates()
        {
            var result = new List<PlayerSkillState>();
            foreach (PlayerSkillState state in _statesById.Values)
            {
                result.Add(state.Clone());
            }
            return result;
        }

        /// <summary>
        /// Áp dụng dữ liệu save lên tập định nghĩa hiện tại.
        /// SkillId không còn tồn tại trong config sẽ được bỏ qua thay vì tạo Resource giả.
        /// </summary>
        public void RestoreStates(IEnumerable<PlayerSkillState> savedStates, int unspentSkillPoints)
        {
            Array.Clear(_equippedSlots, 0, _equippedSlots.Length);

            foreach (PlayerSkillState state in _statesById.Values)
            {
                state.EquippedSlot = -1;
            }

            if (savedStates != null)
            {
                foreach (PlayerSkillState saved in savedStates)
                {
                    if (saved == null)
                    {
                        continue;
                    }

                    string skillId = NormalizeSkillId(saved.SkillId);
                    if (!_statesById.TryGetValue(skillId, out PlayerSkillState runtimeState)
                        || !_definitionsById.TryGetValue(skillId, out SkillData definition))
                    {
                        continue;
                    }

                    runtimeState.IsUnlocked = saved.IsUnlocked;
                    runtimeState.Level = Math.Clamp(saved.Level, 1, Math.Max(1, definition.MaxLevel));

                    if (runtimeState.IsUnlocked
                        && definition.Category == SkillCategory.Active
                        && saved.EquippedSlot >= 0
                        && saved.EquippedSlot < SlotCount
                        && _equippedSlots[saved.EquippedSlot] == null)
                    {
                        EquipInternal(definition, runtimeState, saved.EquippedSlot);
                    }
                }
            }

            UnspentSkillPoints = Math.Max(0, unspentSkillPoints);
            Changed?.Invoke();
        }

        private void RegisterDefinitionsFromTree(CharacterSkillTreeData tree)
        {
            if (tree?.Branches == null)
            {
                return;
            }

            foreach (SkillTreeBranchData branch in tree.Branches)
            {
                if (branch?.Nodes == null)
                {
                    continue;
                }

                foreach (SkillTreeNodeData node in branch.Nodes)
                {
                    if (node?.Skill == null)
                    {
                        continue;
                    }

                    RegisterDefinitions(new[] { node.Skill });

                    // GrantedByDefault thuộc dữ liệu cây. Nó có thể cấp node gốc miễn phí
                    // ngay cả khi designer quên bật DefaultUnlocked trên SkillData.
                    string skillId = NormalizeSkillId(node.Skill);
                    if (node.GrantedByDefault && _statesById.TryGetValue(skillId, out PlayerSkillState state))
                    {
                        state.IsUnlocked = true;
                        state.Level = Math.Max(1, state.Level);
                    }
                }
            }
        }

        private static SkillTreeNodeData FindTreeNodeBySkillId(CharacterSkillTreeData tree, string skillId)
        {
            string normalized = NormalizeSkillId(skillId);
            if (tree?.Branches == null || string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            foreach (SkillTreeBranchData branch in tree.Branches)
            {
                if (branch?.Nodes == null)
                {
                    continue;
                }

                foreach (SkillTreeNodeData node in branch.Nodes)
                {
                    if (NormalizeSkillId(node?.Skill) == normalized)
                    {
                        return node;
                    }
                }
            }

            return null;
        }

        private void RegisterDefinitions(IEnumerable<SkillData> skills)
        {
            if (skills == null)
            {
                return;
            }

            foreach (SkillData skill in skills)
            {
                if (skill == null)
                {
                    continue;
                }

                string skillId = NormalizeSkillId(skill);
                if (string.IsNullOrWhiteSpace(skillId) || _definitionsById.ContainsKey(skillId))
                {
                    continue;
                }

                _definitionsById[skillId] = skill;
                _statesById[skillId] = new PlayerSkillState
                {
                    SkillId = skillId,
                    Level = 1,
                    IsUnlocked = skill.DefaultUnlocked,
                    EquippedSlot = -1
                };
            }
        }

        private void EquipInternal(SkillData skill, PlayerSkillState state, int slot)
        {
            _equippedSlots[slot] = skill;
            state.EquippedSlot = slot;
        }

        public static string NormalizeSkillId(SkillData skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(skill.SkillId))
            {
                return NormalizeSkillId(skill.SkillId);
            }

            if (!string.IsNullOrWhiteSpace(skill.ResourcePath))
            {
                return NormalizeSkillId(skill.ResourcePath);
            }

            return NormalizeSkillId(skill.SkillName);
        }

        public static string NormalizeSkillId(string skillId)
        {
            return string.IsNullOrWhiteSpace(skillId)
                ? string.Empty
                : skillId.Trim().ToLowerInvariant();
        }
    }
}
