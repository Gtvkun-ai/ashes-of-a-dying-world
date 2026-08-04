using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Skills;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Luật prerequisite và mở khóa cây kỹ năng.
    /// State thật nằm trong PlayerSkillCollection, không nằm trong Resource hay UI.
    /// </summary>
    public static class SkillTreeProgression
    {
        public static bool IsUnlocked(PlayerSkillCollection collection, SkillTreeNodeData node)
        {
            return collection != null && node?.Skill != null && collection.IsUnlocked(node.Skill);
        }

        public static bool AreRequirementsMet(
            PlayerSkillCollection collection,
            CharacterSkillTreeData tree,
            SkillTreeNodeData node)
        {
            if (node?.RequiredNodeIds == null || node.RequiredNodeIds.Count == 0)
            {
                return true;
            }

            Dictionary<string, SkillTreeNodeData> nodes = BuildNodeMap(tree);
            foreach (string requiredId in node.RequiredNodeIds)
            {
                if (string.IsNullOrWhiteSpace(requiredId)
                    || !nodes.TryGetValue(requiredId.Trim().ToLowerInvariant(), out SkillTreeNodeData required)
                    || !IsUnlocked(collection, required))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool CanUnlock(
            PlayerSkillCollection collection,
            CharacterSkillTreeData tree,
            SkillTreeNodeData node,
            int characterLevel,
            out string reason)
        {
            reason = "";
            if (collection == null || tree == null || node?.Skill == null)
            {
                reason = "Dữ liệu kỹ năng chưa hợp lệ.";
                return false;
            }
            if (IsUnlocked(collection, node))
            {
                reason = "Kỹ năng này đã được mở.";
                return false;
            }
            if (characterLevel < Mathf.Max(1, node.RequiredCharacterLevel))
            {
                reason = $"Yêu cầu cấp {Mathf.Max(1, node.RequiredCharacterLevel)}.";
                return false;
            }
            if (!AreRequirementsMet(collection, tree, node))
            {
                reason = "Chưa mở đủ kỹ năng tiên quyết.";
                return false;
            }
            int cost = Mathf.Max(0, node.SkillPointCost);
            if (collection.UnspentSkillPoints < cost)
            {
                reason = $"Cần {cost} điểm kỹ năng, hiện có {collection.UnspentSkillPoints}.";
                return false;
            }
            return true;
        }

        public static bool TryUnlock(
            PlayerSkillCollection collection,
            CharacterSkillTreeData tree,
            SkillTreeNodeData node,
            int characterLevel,
            out string message)
        {
            if (!CanUnlock(collection, tree, node, characterLevel, out message))
            {
                return false;
            }

            bool unlocked = collection.TryUnlock(node.Skill, node.SkillPointCost);
            message = unlocked
                ? $"Đã mở khóa {node.Skill.SkillName}."
                : "Không thể cập nhật trạng thái kỹ năng.";
            return unlocked;
        }

        public static SkillTreeNodeData FindNode(CharacterSkillTreeData tree, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }
            BuildNodeMap(tree).TryGetValue(nodeId.Trim().ToLowerInvariant(), out SkillTreeNodeData node);
            return node;
        }

        private static Dictionary<string, SkillTreeNodeData> BuildNodeMap(CharacterSkillTreeData tree)
        {
            var result = new Dictionary<string, SkillTreeNodeData>();
            if (tree?.Branches == null)
            {
                return result;
            }
            foreach (SkillTreeBranchData branch in tree.Branches)
            {
                if (branch?.Nodes == null) continue;
                foreach (SkillTreeNodeData node in branch.Nodes)
                {
                    if (node != null && !string.IsNullOrWhiteSpace(node.NodeId))
                    {
                        result[node.NodeId.Trim().ToLowerInvariant()] = node;
                    }
                }
            }
            return result;
        }
    }
}
