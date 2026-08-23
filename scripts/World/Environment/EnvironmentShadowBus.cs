using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Cache toàn bộ ProjectedShadow2D trong scene để không tạo _Process C# riêng cho hàng trăm cây.
    /// Binder cập nhật chúng theo một nhịp chung; đây là cùng pattern cache với EnvironmentMaterialBus.
    /// </summary>
    public sealed class EnvironmentShadowBus
    {
        private readonly List<ProjectedShadow2D> _shadows = new();

        public int Count => _shadows.Count;

        public int Rebuild(Node root)
        {
            _shadows.Clear();
            if (root != null)
            {
                Collect(root);
            }
            return _shadows.Count;
        }

        public void Push(EnvironmentState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = _shadows.Count - 1; i >= 0; i--)
            {
                ProjectedShadow2D shadow = _shadows[i];
                if (shadow == null || !GodotObject.IsInstanceValid(shadow))
                {
                    _shadows.RemoveAt(i);
                    continue;
                }

                shadow.ApplyEnvironment(state);
            }
        }

        private void Collect(Node node)
        {
            if (node is ProjectedShadow2D shadow)
            {
                _shadows.Add(shadow);
            }

            foreach (Node child in node.GetChildren())
            {
                Collect(child);
            }
        }
    }
}
