using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class StatHexagonChart : Control
    {
        private Dictionary<string, float> _stats = new Dictionary<string, float>();
        private List<Label> _statLabels = new List<Label>();
        
        [ExportGroup("Chart Settings")]
        [Export] public float MaxValue { get; set; } = 20f;
        [Export] public float ChartRadiusOffset { get; set; } = 100f; // Khoảng cách từ viền vào tâm

        [ExportGroup("Visual - Colors")]
        // Màu chính của biểu đồ (ví dụ: Cyan cho Sci-fi, Gold cho Fantasy)
        [Export] public Color MainColor { get; set; } = new Color("00f2ff"); 
        
        // Màu nền của đa giác (thường là MainColor nhưng rất trong suốt)
        [Export] public float FillOpacity { get; set; } = 0.15f;
        
        // Màu của lưới và trục (Nên là màu xám hoặc trắng mờ)
        [Export] public Color GridColor { get; set; } = new Color(1, 1, 1, 0.1f);
        [Export] public Color AxisColor { get; set; } = new Color(1, 1, 1, 0.2f);

        [ExportGroup("Visual - Text")]
        [Export] public Color TextColor { get; set; } = Colors.White;
        [Export] public int FontSize { get; set; } = 14;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(200, 200);
            // Bật antialias cho 2D drawing nếu project setting chưa bật (Godot 4.x)
            // RenderingServer.SetDefaultClearColor(Colors.Transparent); 
        }

        public void SetStat(string statName, float value)
        {
            _stats[statName] = Mathf.Clamp(value, 0, MaxValue); 
            QueueRedraw();
        }
        
        public void UpdateAllStats() => UpdateLabels();
        
        public void ClearStats()
        {
            _stats.Clear();
            foreach (var label in _statLabels) label.QueueFree();
            _statLabels.Clear();
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_stats.Count < 3) return; // Cần ít nhất 3 điểm để vẽ đa giác

            Vector2 center = Size / 2;
            float maxRadius = Mathf.Min(Size.X, Size.Y) / 2 - ChartRadiusOffset;

            // 1. Vẽ nền tối cho biểu đồ (giúp nổi bật trên nền game)
            DrawCircle(center, maxRadius, new Color(0, 0, 0, 0.3f));

            // 2. Vẽ lưới (Grid)
            DrawGridCircles(center, maxRadius);
            DrawAxes(center, maxRadius);

            // 3. Vẽ biểu đồ chỉ số (Phần quan trọng nhất)
            DrawStatPolygon(center, maxRadius);
        }

        private void DrawGridCircles(Vector2 center, float maxRadius)
        {
            // Vẽ 4 vòng (25%, 50%, 75%, 100%) thay vì 5 để thoáng hơn
            int rings = 4;
            for (int i = 1; i <= rings; i++)
            {
                float radius = maxRadius * (i / (float)rings);
                // Vẽ đa giác đều thay vì hình tròn để khớp với các đỉnh chỉ số (nhìn tech hơn)
                DrawPolyLineShape(center, radius, _stats.Count, GridColor, 1.0f);
            }
        }
        
        // Hàm phụ trợ để vẽ grid dạng đa giác (Hexagon/Octagon...) thay vì tròn
        private void DrawPolyLineShape(Vector2 center, float radius, int sides, Color color, float width)
        {
            var points = new Vector2[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                float angle = GetAngleForIndex(i % sides, sides);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            DrawPolyline(points, color, width, true);
        }

        private void DrawAxes(Vector2 center, float maxRadius)
        {
            for (int i = 0; i < _stats.Count; i++)
            {
                float angle = GetAngleForIndex(i, _stats.Count);
                Vector2 endPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * maxRadius;
                DrawLine(center, endPoint, AxisColor, 1.0f, true);
            }
        }

        private void DrawStatPolygon(Vector2 center, float maxRadius)
        {
            Vector2[] points = new Vector2[_stats.Count];
            int index = 0;

            foreach (var stat in _stats)
            {
                float percentage = stat.Value / MaxValue;
                // Hiệu ứng "nảy": Giá trị nhỏ nhất hiển thị là 5% bán kính để không bị tụt hẳn vào tâm
                float visualPercentage = Mathf.Max(percentage, 0.05f); 
                
                float angle = GetAngleForIndex(index, _stats.Count);
                points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (maxRadius * visualPercentage);
                index++;
            }

            // 1. Vẽ Fill (Màu nền)
            Color fillColor = MainColor;
            fillColor.A = FillOpacity;
            DrawColoredPolygon(points, fillColor);

            // 2. Vẽ Border (Viền) - Tạo hiệu ứng Glow bằng cách vẽ 2 lần
            // Lớp Glow mờ bên ngoài
            Color glowColor = MainColor;
            glowColor.A = 0.3f;
            DrawPolyline(ClosePolygon(points), glowColor, 8.0f, true); // Viền dày, mờ

            // Lớp Core sáng bên trong
            Color coreColor = MainColor;
            coreColor.A = 1.0f; 
            DrawPolyline(ClosePolygon(points), coreColor, 2.0f, true); // Viền mỏng, rõ

            // 3. Vẽ các điểm nút (Dots)
            foreach (var point in points)
            {
                // Điểm sáng trắng ở tâm nút
                DrawCircle(point, 4f, Colors.White);
                // Viền màu bao quanh nút
                DrawArc(point, 6f, 0, Mathf.Tau, 32, MainColor, 2f, true);
            }
        }

        // Hàm nối điểm cuối về điểm đầu để vẽ viền khép kín
        private Vector2[] ClosePolygon(Vector2[] points)
        {
            var closed = new Vector2[points.Length + 1];
            points.CopyTo(closed, 0);
            closed[points.Length] = points[0];
            return closed;
        }

        private void UpdateLabels()
        {
            foreach (var label in _statLabels) label.QueueFree();
            _statLabels.Clear();

            if (_stats.Count == 0 || Size.X == 0) return;

            Vector2 center = Size / 2;
            float maxRadius = Mathf.Min(Size.X, Size.Y) / 2 - ChartRadiusOffset;
            int index = 0;

            foreach (var stat in _stats)
            {
                float angle = GetAngleForIndex(index, _stats.Count);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Đẩy label ra xa hơn một chút so với bán kính tối đa
                Vector2 labelPos = center + direction * (maxRadius + 30);

                var label = new Label();
                label.Text = $"{stat.Key}\n{stat.Value:F0}"; // Bỏ phần /Max cho gọn, hoặc giữ lại tùy bạn
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                
                // Căn chỉnh vị trí để label không bị lệch
                label.Position = labelPos - new Vector2(50, 25);
                label.Size = new Vector2(100, 50);
                
                label.AddThemeFontSizeOverride("font_size", FontSize);
                label.AddThemeColorOverride("font_color", TextColor);
                // Thêm shadow thay vì outline dày để trông hiện đại hơn
                label.AddThemeColorOverride("font_shadow_color", new Color(0,0,0,0.8f));
                label.AddThemeConstantOverride("shadow_offset_x", 1);
                label.AddThemeConstantOverride("shadow_offset_y", 1);

                AddChild(label);
                _statLabels.Add(label);
                index++;
            }
        }

        private float GetAngleForIndex(int index, int total)
        {
            return Mathf.DegToRad(-90 + (360f * index / total));
        }
    }
}