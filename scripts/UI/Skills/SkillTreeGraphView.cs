using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.Core.Skills;

namespace AshesofaDyingWorld.UI.Skills
{
    /// <summary>
    /// Vùng vẽ cây kỹ năng. Connections được vẽ bằng DrawLine nên bản đầu tiên
    /// không cần asset đường nối riêng; designer vẫn có thể thay bằng texture sau.
    /// </summary>
    public partial class SkillTreeGraphView : Control
    {
        public event Action<SkillTreeNodeData> NodeSelected;

        private SkillTreeBranchData _branch;
        private CharacterSkillTreeData _tree;
        private PlayerSkillCollection _collection;
        private int _characterLevel;
        private Color _accent = InventoryPanelChrome.AccentColor;
        private Texture2D _backgroundTexture;
        private SkillTreeNodeData _selectedNode;
        private readonly Dictionary<string, SkillTreeNodeData> _nodesById = new();
        private readonly Dictionary<string, SkillTreeNodeView> _viewsById = new();

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Pass;
            ClipContents = false;
        }

        public void ShowBranch(
            SkillTreeBranchData branch,
            CharacterSkillTreeData tree,
            PlayerSkillCollection collection,
            int characterLevel,
            Color accent,
            Texture2D backgroundTexture,
            SkillTreeNodeData selectedNode = null)
        {
            _branch = branch;
            _tree = tree;
            _collection = collection;
            _characterLevel = Mathf.Max(1, characterLevel);
            _accent = accent;
            _backgroundTexture = backgroundTexture;
            _selectedNode = selectedNode;

            RebuildNodes();
            QueueRedraw();
        }

        public void SetSelectedNode(SkillTreeNodeData node)
        {
            _selectedNode = node;
            RefreshNodeStyles();
        }

        public override void _Draw()
        {
            if (_backgroundTexture != null)
            {
                DrawTextureRect(
                    _backgroundTexture,
                    new Rect2(Vector2.Zero, Size),
                    false,
                    new Color(1f, 1f, 1f, 0.10f));
            }

            if (_branch?.Nodes == null)
            {
                return;
            }

            foreach (SkillTreeNodeData child in _branch.Nodes)
            {
                if (child?.RequiredNodeIds == null)
                {
                    continue;
                }

                foreach (string parentId in child.RequiredNodeIds)
                {
                    if (string.IsNullOrWhiteSpace(parentId)
                        || !_nodesById.TryGetValue(parentId.Trim().ToLowerInvariant(), out SkillTreeNodeData parent))
                    {
                        continue;
                    }

                    Vector2 from = parent.GraphPosition + SkillTreeNodeView.NodeSize * 0.5f;
                    Vector2 to = child.GraphPosition + SkillTreeNodeView.NodeSize * 0.5f;
                    Color lineColor = ResolveConnectionColor(parent, child);

                    // Viền tối làm đường nối đọc được trên cả nền nhân vật sáng lẫn tối.
                    DrawLine(from, to, new Color(0f, 0f, 0f, 0.55f), 7f, true);
                    DrawLine(from, to, lineColor, 3f, true);
                }
            }
        }

        private void RebuildNodes()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _nodesById.Clear();
            _viewsById.Clear();

            float maxX = 760f;
            float maxY = 450f;
            if (_branch?.Nodes != null)
            {
                foreach (SkillTreeNodeData node in _branch.Nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    {
                        continue;
                    }

                    string id = node.NodeId.Trim().ToLowerInvariant();
                    _nodesById[id] = node;
                    maxX = Mathf.Max(maxX, node.GraphPosition.X + SkillTreeNodeView.NodeSize.X + 60f);
                    maxY = Mathf.Max(maxY, node.GraphPosition.Y + SkillTreeNodeView.NodeSize.Y + 60f);
                }

                foreach (SkillTreeNodeData node in _branch.Nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    {
                        continue;
                    }

                    string id = node.NodeId.Trim().ToLowerInvariant();
                    var view = new SkillTreeNodeView
                    {
                        Position = node.GraphPosition
                    };
                    view.Pressed += () => OnNodePressed(node);
                    AddChild(view);
                    _viewsById[id] = view;
                }
            }

            CustomMinimumSize = new Vector2(maxX, maxY);
            RefreshNodeStyles();
        }

        private void RefreshNodeStyles()
        {
            foreach (var pair in _viewsById)
            {
                SkillTreeNodeData node = _nodesById[pair.Key];
                pair.Value.Configure(
                    node,
                    ResolveNodeState(node),
                    _accent,
                    node == _selectedNode);
            }

            QueueRedraw();
        }

        private SkillTreeNodeVisualState ResolveNodeState(SkillTreeNodeData node)
        {
            if (SkillTreeProgression.IsUnlocked(_collection, node))
            {
                return SkillTreeNodeVisualState.Unlocked;
            }

            return SkillTreeProgression.CanUnlock(
                _collection,
                _tree,
                node,
                _characterLevel,
                out _)
                    ? SkillTreeNodeVisualState.Unlockable
                    : SkillTreeNodeVisualState.Locked;
        }

        private Color ResolveConnectionColor(SkillTreeNodeData parent, SkillTreeNodeData child)
        {
            bool parentUnlocked = SkillTreeProgression.IsUnlocked(_collection, parent);
            bool childUnlocked = SkillTreeProgression.IsUnlocked(_collection, child);
            if (parentUnlocked && childUnlocked)
            {
                return _accent;
            }

            if (parentUnlocked && SkillTreeProgression.AreRequirementsMet(_collection, _tree, child))
            {
                return new Color(_accent.R, _accent.G, _accent.B, 0.62f);
            }

            return new Color(
                InventoryPanelChrome.BorderColor.R,
                InventoryPanelChrome.BorderColor.G,
                InventoryPanelChrome.BorderColor.B,
                0.52f);
        }

        private void OnNodePressed(SkillTreeNodeData node)
        {
            _selectedNode = node;
            RefreshNodeStyles();
            NodeSelected?.Invoke(node);
        }
    }
}
