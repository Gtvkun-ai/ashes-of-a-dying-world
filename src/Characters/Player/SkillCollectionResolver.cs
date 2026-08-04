using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Skills
{
    /// <summary>
    /// Trả về PlayerSkillCollection đúng cho một PlayerStats.
    /// Player chính dùng collection thật trong Player; companion dùng collection runtime riêng.
    /// </summary>
    public static class SkillCollectionResolver
    {
        private sealed class CompanionEntry
        {
            public string ConfigPath = "";
            public PlayerSkillCollection Collection = new();
        }

        private static readonly Dictionary<ulong, CompanionEntry> CompanionCollections = new();

        public static PlayerSkillCollection Resolve(PlayerStats stats)
        {
            if (stats == null)
            {
                return null;
            }

            SceneTree tree = stats.GetTree();
            if (tree != null)
            {
                foreach (Node node in tree.GetNodesInGroup("Player"))
                {
                    if (node is Player player && player.GetStatsNode() == stats)
                    {
                        PlayerSkillCollection collection = player.GetSkillCollection();
                        collection?.RecalculateUnspentSkillPoints(stats.CurrentLevel, stats.ConfigData?.SkillTree);
                        return collection;
                    }
                }
            }

            ulong id = stats.GetInstanceId();
            string configPath = stats.ConfigData?.ResourcePath ?? stats.ConfigData?.ID ?? "";
            if (!CompanionCollections.TryGetValue(id, out CompanionEntry entry)
                || entry.ConfigPath != configPath)
            {
                entry = new CompanionEntry { ConfigPath = configPath };
                entry.Collection.Initialize(stats.ConfigData);
                CompanionCollections[id] = entry;
            }

            entry.Collection.RecalculateUnspentSkillPoints(stats.CurrentLevel, stats.ConfigData?.SkillTree);
            return entry.Collection;
        }
    }
}
