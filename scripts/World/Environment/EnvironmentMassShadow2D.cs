using System;
using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// V4.5 - lớp "mass shadow" bổ trợ cho ShadowCaster2D.
    ///
    /// Mục tiêu nghệ thuật:
    /// - Nhiều cây đứng sát nhau không còn nhìn như nhiều dấu oval rời rạc.
    /// - Hàng cây biên map tạo một vùng tối lớn, mềm và có hướng theo mặt trời.
    /// - Đây chỉ là penumbra/mass chung. Bóng chi tiết từng cây vẫn do ShadowCaster2D vẽ.
    ///
    /// Lớp này cố ý KHÔNG scan mỗi frame. Scene chỉ được phân tích một lần khi _Ready().
    /// Mỗi frame chỉ cập nhật transform + alpha của vài Sprite2D mềm, nên chi phí rất nhỏ.
    /// </summary>
    public partial class EnvironmentMassShadow2D : Node2D
    {
        private const string MassTexturePath = "res://assets/graphics/environment/shadows/mass_shadow_blob_v44.png";
        private const float AppleClusterDistance = 165f;
        private const float BorderBand = 92f;
        private const float BorderSegmentGap = 155f;

        private sealed class ClusterShadow
        {
            public Sprite2D Sprite;
            public Vector2 Center;
            public float NoonWidth;
            public float HorizonWidth;
            public float NoonDepth;
            public float HorizonDepth;
            public float Opacity;
        }

        private enum BorderEdge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private sealed class BorderShadow
        {
            public Sprite2D Sprite;
            public Vector2 Center;
            public float Width;
            public BorderEdge Edge;
        }

        private Texture2D _massTexture;
        private readonly List<ClusterShadow> _clusters = new();
        private readonly List<BorderShadow> _borders = new();
        private bool _built;

        public override void _Ready()
        {
            _massTexture = ResourceLoader.Exists(MassTexturePath)
                ? GD.Load<Texture2D>(MassTexturePath)
                : null;

            if (_massTexture == null)
            {
                GD.PushWarning($"[EnvironmentMassShadow2D] Thiếu texture mass shadow: {MassTexturePath}");
                Visible = false;
                return;
            }

            // Lớp mass shadow phải ở trên ground nhưng ở dưới toàn bộ props/actor.
            ZIndex = -1;

            // V4.5: CurrentScene của game thực tế là screen_main, KHÔNG phải field_01.
            // Vì vậy V4.4 tìm "Props/AppleTrees" từ sai root và luôn ra 0 mass shadow.
            // Ta đi ngược cây cha để tìm node map gần nhất có Props/Trees hoặc Props/AppleTrees.
            Node mapRoot = ResolveMapRoot();
            RebuildFromScene(mapRoot);
            SetProcess(false);
        }

        /// <summary>
        /// Phân tích vị trí cây một lần để dựng cluster + border pools.
        /// Có thể gọi lại thủ công nếu map spawn/despawn cây lớn ở runtime.
        /// </summary>
        public void RebuildFromScene(Node sceneRoot)
        {
            ClearRuntimeSprites();
            _built = false;

            if (sceneRoot == null || _massTexture == null)
            {
                return;
            }

            Node2D appleRoot = sceneRoot.GetNodeOrNull<Node2D>("Props/AppleTrees");
            Node2D treeRoot = sceneRoot.GetNodeOrNull<Node2D>("Props/Trees");

            if (appleRoot != null)
            {
                BuildInteriorAppleClusters(appleRoot);
            }

            if (treeRoot != null)
            {
                BuildBorderPools(treeRoot);
            }

            _built = true;
            GD.Print(
                $"[EnvironmentMassShadow2D] READY V4.5 | root={sceneRoot.GetPath()} " +
                $"cluster_mass={_clusters.Count} border_mass={_borders.Count}");
        }

        /// <summary>
        /// Tìm root của map đang chứa component này. Trong runtime thật CurrentScene thường là
        /// `screen_main`, còn Field 1 nằm sâu bên trong. Đi theo ancestor vừa nhanh vừa không phụ
        /// thuộc tên scene cụ thể. Fallback recursive chỉ dùng nếu component bị gắn ở nơi lạ.
        /// </summary>
        private Node ResolveMapRoot()
        {
            Node cursor = this;
            while (cursor != null)
            {
                if (HasEnvironmentProps(cursor))
                {
                    return cursor;
                }
                cursor = cursor.GetParent();
            }

            Node currentScene = GetTree()?.CurrentScene;
            Node fallback = FindMapRootRecursive(currentScene);
            if (fallback == null)
            {
                GD.PushWarning(
                    $"[EnvironmentMassShadow2D] Không tìm thấy map root có Props/Trees hoặc Props/AppleTrees. " +
                    $"CurrentScene={currentScene?.GetPath().ToString() ?? "<null>"}");
            }
            return fallback;
        }

        private static bool HasEnvironmentProps(Node node)
        {
            if (node == null)
            {
                return false;
            }

            return node.GetNodeOrNull<Node2D>("Props/Trees") != null
                || node.GetNodeOrNull<Node2D>("Props/AppleTrees") != null;
        }

        private static Node FindMapRootRecursive(Node root)
        {
            if (root == null)
            {
                return null;
            }

            if (HasEnvironmentProps(root))
            {
                return root;
            }

            foreach (Node child in root.GetChildren())
            {
                Node found = FindMapRootRecursive(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (!_built || state == null || _massTexture == null)
            {
                return;
            }

            Vector2 direction = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;

            float length01 = Mathf.Clamp(state.ShadowLength01, 0f, 1f);
            float lengthCurve = Mathf.Pow(length01, 0.72f);
            float daylight = Mathf.Clamp(state.Daylight, 0f, 1f);
            float night = Mathf.Clamp(state.NightFactor, 0f, 1f);
            float key = Mathf.Clamp(state.KeyLightStrength01, 0f, 1f);
            float cloud = Mathf.Clamp(state.Cloudiness, 0f, 1f);

            // Cluster mass vẫn tồn tại nhẹ lúc trưa. Khi low-sun, mass mở dài để các bóng cá thể
            // nhập vào nhau. Ban đêm giảm mạnh để moonlight không biến scene thành mảng bùn.
            float commonVisibility = (0.70f + 0.30f * Mathf.Sqrt(key))
                * (1f - cloud * 0.18f)
                * Mathf.Lerp(1f, 0.24f, night);

            Color dayTint = new(0.030f, 0.052f, 0.032f, 1f);
            Color nightTint = new(0.020f, 0.030f, 0.050f, 1f);
            Color tint = dayTint.Lerp(nightTint, night * 0.55f);

            Vector2 textureSize = _massTexture.GetSize();
            float texW = Mathf.Max(textureSize.X, 1f);
            float texH = Mathf.Max(textureSize.Y, 1f);
            float rotation = direction.Angle() - Mathf.Pi * 0.5f;

            foreach (ClusterShadow cluster in _clusters)
            {
                float width = Mathf.Lerp(cluster.NoonWidth, cluster.HorizonWidth, lengthCurve);
                float depth = Mathf.Lerp(cluster.NoonDepth, cluster.HorizonDepth, lengthCurve);

                // Đẩy tâm ra nửa depth: đầu gần của mass vẫn nằm dưới cụm thân cây.
                cluster.Sprite.Position = cluster.Center + direction * depth * 0.34f;
                cluster.Sprite.Rotation = rotation;
                cluster.Sprite.Scale = new Vector2(width / texW, depth / texH);

                float alpha = cluster.Opacity
                    * Mathf.Lerp(0.74f, 1.12f, lengthCurve)
                    * commonVisibility
                    * Mathf.Lerp(0.78f, 1f, daylight);

                cluster.Sprite.Modulate = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(alpha, 0f, 0.22f));
            }

            foreach (BorderShadow border in _borders)
            {
                Vector2 inward = InwardNormal(border.Edge);
                float inwardFactor = Mathf.Max(direction.Dot(inward), 0f);

                // Hàng cây chỉ đổ mass vào trong map khi ánh sáng thực sự quăng bóng vào map.
                // Khi hướng bóng đi ra ngoài, giữ một AO cực nhẹ thay vì một dải đen vô lý.
                float directionGate = 0.08f + 0.92f * Mathf.Pow(inwardFactor, 0.72f);
                float depth = Mathf.Lerp(28f, 148f, lengthCurve) * Mathf.Lerp(0.58f, 1f, inwardFactor);
                float width = border.Width * Mathf.Lerp(0.98f, 1.06f, lengthCurve);

                border.Sprite.Position = border.Center + direction * depth * 0.38f;
                border.Sprite.Rotation = rotation;
                border.Sprite.Scale = new Vector2(width / texW, depth / texH);

                float alpha = 0.082f
                    * Mathf.Lerp(0.62f, 1.12f, lengthCurve)
                    * directionGate
                    * commonVisibility;

                border.Sprite.Modulate = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(alpha, 0f, 0.105f));
            }
        }

        private void BuildInteriorAppleClusters(Node2D appleRoot)
        {
            List<Node2D> trees = CollectDirectNode2DChildren(appleRoot);
            if (trees.Count < 3)
            {
                return;
            }

            int[] parent = new int[trees.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            for (int i = 0; i < trees.Count; i++)
            {
                for (int j = i + 1; j < trees.Count; j++)
                {
                    if (trees[i].GlobalPosition.DistanceTo(trees[j].GlobalPosition) <= AppleClusterDistance)
                    {
                        Union(parent, i, j);
                    }
                }
            }

            Dictionary<int, List<Node2D>> components = new();
            for (int i = 0; i < trees.Count; i++)
            {
                int root = Find(parent, i);
                if (!components.TryGetValue(root, out List<Node2D> group))
                {
                    group = new List<Node2D>();
                    components[root] = group;
                }
                group.Add(trees[i]);
            }

            foreach (List<Node2D> group in components.Values)
            {
                if (group.Count < 3)
                {
                    continue;
                }

                // Tránh chain cực dài do nhiều cây nối đuôi nhau. Mass shadow chỉ dành cho grove nhỏ.
                Rect2 bounds = BoundsOf(group);
                if (bounds.Size.X > 420f || bounds.Size.Y > 420f)
                {
                    continue;
                }

                Vector2 globalCenter = AverageGlobalPosition(group);
                Vector2 localCenter = ToLocal(globalCenter);

                float spreadX = Mathf.Max(bounds.Size.X, 40f);
                float spreadY = Mathf.Max(bounds.Size.Y, 40f);

                var mass = new ClusterShadow
                {
                    Sprite = CreateMassSprite($"ClusterMass{_clusters.Count + 1}"),
                    Center = localCenter,
                    NoonWidth = Mathf.Max(150f, spreadX + 118f),
                    HorizonWidth = Mathf.Max(182f, spreadX + 150f),
                    NoonDepth = Mathf.Max(72f, spreadY * 0.42f + 38f),
                    HorizonDepth = Mathf.Max(150f, spreadY * 0.80f + 72f),
                    Opacity = Mathf.Clamp(0.078f + (group.Count - 3) * 0.009f, 0.078f, 0.112f)
                };

                mass.Sprite.Position = mass.Center;
                _clusters.Add(mass);
            }
        }

        private void BuildBorderPools(Node2D treeRoot)
        {
            List<Node2D> trees = CollectDirectNode2DChildren(treeRoot);
            if (trees.Count < 4)
            {
                return;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Node2D tree in trees)
            {
                Vector2 p = tree.GlobalPosition;
                minX = Mathf.Min(minX, p.X);
                maxX = Mathf.Max(maxX, p.X);
                minY = Mathf.Min(minY, p.Y);
                maxY = Mathf.Max(maxY, p.Y);
            }

            BuildBorderSegments(FilterBorder(trees, BorderEdge.Top, minX, maxX, minY, maxY), BorderEdge.Top);
            BuildBorderSegments(FilterBorder(trees, BorderEdge.Bottom, minX, maxX, minY, maxY), BorderEdge.Bottom);
            BuildBorderSegments(FilterBorder(trees, BorderEdge.Left, minX, maxX, minY, maxY), BorderEdge.Left);
            BuildBorderSegments(FilterBorder(trees, BorderEdge.Right, minX, maxX, minY, maxY), BorderEdge.Right);
        }

        private void BuildBorderSegments(List<Node2D> edgeTrees, BorderEdge edge)
        {
            if (edgeTrees.Count < 4)
            {
                return;
            }

            bool horizontal = edge == BorderEdge.Top || edge == BorderEdge.Bottom;
            edgeTrees.Sort((a, b) =>
            {
                float av = horizontal ? a.GlobalPosition.X : a.GlobalPosition.Y;
                float bv = horizontal ? b.GlobalPosition.X : b.GlobalPosition.Y;
                return av.CompareTo(bv);
            });

            List<Node2D> segment = new();
            float previous = float.NaN;

            foreach (Node2D tree in edgeTrees)
            {
                float current = horizontal ? tree.GlobalPosition.X : tree.GlobalPosition.Y;
                if (segment.Count > 0 && current - previous > BorderSegmentGap)
                {
                    CommitBorderSegment(segment, edge, horizontal);
                    segment.Clear();
                }

                segment.Add(tree);
                previous = current;
            }

            CommitBorderSegment(segment, edge, horizontal);
        }

        private void CommitBorderSegment(List<Node2D> segment, BorderEdge edge, bool horizontal)
        {
            if (segment.Count < 4)
            {
                return;
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            Vector2 sum = Vector2.Zero;

            foreach (Node2D tree in segment)
            {
                Vector2 p = tree.GlobalPosition;
                float tangent = horizontal ? p.X : p.Y;
                min = Mathf.Min(min, tangent);
                max = Mathf.Max(max, tangent);
                sum += p;
            }

            Vector2 center = sum / segment.Count;
            float width = Mathf.Max(160f, (max - min) + 128f);

            var shadow = new BorderShadow
            {
                Sprite = CreateMassSprite($"BorderMass{edge}{_borders.Count + 1}"),
                Center = ToLocal(center),
                Width = width,
                Edge = edge
            };

            shadow.Sprite.Position = shadow.Center;
            _borders.Add(shadow);
        }

        private List<Node2D> FilterBorder(
            List<Node2D> trees,
            BorderEdge edge,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            var result = new List<Node2D>();
            foreach (Node2D tree in trees)
            {
                Vector2 p = tree.GlobalPosition;
                bool match = edge switch
                {
                    BorderEdge.Top => p.Y <= minY + BorderBand,
                    BorderEdge.Bottom => p.Y >= maxY - BorderBand,
                    BorderEdge.Left => p.X <= minX + BorderBand,
                    BorderEdge.Right => p.X >= maxX - BorderBand,
                    _ => false
                };

                if (match)
                {
                    result.Add(tree);
                }
            }
            return result;
        }

        private Sprite2D CreateMassSprite(string name)
        {
            var sprite = new Sprite2D
            {
                Name = name,
                Texture = _massTexture,
                Centered = true,
                ZAsRelative = true,
                ZIndex = 0,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Modulate = new Color(0.03f, 0.05f, 0.03f, 0f)
            };
            AddChild(sprite);
            return sprite;
        }

        private void ClearRuntimeSprites()
        {
            foreach (ClusterShadow cluster in _clusters)
            {
                if (cluster.Sprite != null && GodotObject.IsInstanceValid(cluster.Sprite))
                {
                    cluster.Sprite.QueueFree();
                }
            }
            foreach (BorderShadow border in _borders)
            {
                if (border.Sprite != null && GodotObject.IsInstanceValid(border.Sprite))
                {
                    border.Sprite.QueueFree();
                }
            }
            _clusters.Clear();
            _borders.Clear();
        }

        private static List<Node2D> CollectDirectNode2DChildren(Node2D root)
        {
            var result = new List<Node2D>();
            foreach (Node child in root.GetChildren())
            {
                if (child is Node2D node2D)
                {
                    result.Add(node2D);
                }
            }
            return result;
        }

        private static Rect2 BoundsOf(List<Node2D> nodes)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Node2D node in nodes)
            {
                Vector2 p = node.GlobalPosition;
                minX = Mathf.Min(minX, p.X);
                minY = Mathf.Min(minY, p.Y);
                maxX = Mathf.Max(maxX, p.X);
                maxY = Mathf.Max(maxY, p.Y);
            }

            return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
        }

        private static Vector2 AverageGlobalPosition(List<Node2D> nodes)
        {
            Vector2 sum = Vector2.Zero;
            foreach (Node2D node in nodes)
            {
                sum += node.GlobalPosition;
            }
            float count = nodes.Count > 0 ? nodes.Count : 1f;
            return sum / count;
        }

        private static int Find(int[] parent, int value)
        {
            int root = value;
            while (parent[root] != root)
            {
                root = parent[root];
            }

            while (parent[value] != value)
            {
                int next = parent[value];
                parent[value] = root;
                value = next;
            }

            return root;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb)
            {
                parent[rb] = ra;
            }
        }

        private static Vector2 InwardNormal(BorderEdge edge)
        {
            return edge switch
            {
                BorderEdge.Top => Vector2.Down,
                BorderEdge.Bottom => Vector2.Up,
                BorderEdge.Left => Vector2.Right,
                BorderEdge.Right => Vector2.Left,
                _ => Vector2.Down
            };
        }
    }
}
