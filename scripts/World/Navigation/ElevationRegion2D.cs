using Godot;

namespace AshesofaDyingWorld.World.Navigation
{
    /// <summary>
    /// Vùng semantic cho biết một điểm world đang thuộc cao độ gameplay nào.
    /// Đây không phải chiều cao vật lý 3D; nó chỉ là "tầng đi lại" để AI hiểu cliff/stairs.
    /// </summary>
    public partial class ElevationRegion2D : Polygon2D
    {
        [Export] public string RegionId { get; set; } = "elevation_region";
        [Export] public int ElevationLevel { get; set; } = 0;
        [Export] public int Priority { get; set; } = 0;

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            Vector2[] polygon = Polygon;
            if (polygon == null || polygon.Length < 3)
            {
                return false;
            }

            Vector2 point = ToLocal(worldPoint);
            bool inside = false;
            int previous = polygon.Length - 1;
            for (int current = 0; current < polygon.Length; current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                bool crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (crosses)
                {
                    float denominator = a.Y - b.Y;
                    if (Mathf.Abs(denominator) < 0.00001f)
                    {
                        denominator = denominator < 0f ? -0.00001f : 0.00001f;
                    }

                    float xAtY = (b.X - a.X) * (point.Y - a.Y) / denominator + a.X;
                    if (point.X < xAtY)
                    {
                        inside = !inside;
                    }
                }

                previous = current;
            }

            return inside;
        }
    }
}
