using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Data thuần cho một nhóm vật thể đổ bóng.
    ///
    /// Shadow Core V2.1 giữ profile data-driven, nhưng projection không còn inverse-sample pixel.
    /// Tất cả model dùng một shared shader và phép biến đổi affine ổn định trên quad.
    /// </summary>
    [GlobalClass]
    public partial class ShadowCasterProfile : Resource
    {
        public enum ProjectionModel
        {
            /// <summary>Billboard dựng đứng: cây, người, hoa, cỏ.</summary>
            Upright = 0,

            /// <summary>Khối thấp: đá, slime, thùng. Dùng cùng projection nhưng profile ngắn/dày hơn.</summary>
            Volume = 1,

            /// <summary>Layer lớn như cliff/tường. Chỉ dịch silhouette, không dựng chiều cao giả.</summary>
            RigidDrop = 2
        }

        [Export]
        public ProjectionModel Model { get; set; } = ProjectionModel.Upright;

        [Export(PropertyHint.Range, "0,1,0.001")]
        public float BaseY01 { get; set; } = 0.92f;

        /// <summary>Tỉ lệ chiều cao texture được xem là chiều cao dựng đứng.</summary>
        [Export(PropertyHint.Range, "0.02,1.5,0.01")]
        public float HeightRatio { get; set; } = 0.75f;

        /// <summary>Co/giãn bề ngang silhouette khi nằm xuống đất.</summary>
        [Export(PropertyHint.Range, "0.1,1.5,0.01")]
        public float WidthScale { get; set; } = 1.0f;

        [Export(PropertyHint.Range, "0,64,0.1")]
        public float NoonLengthWorld { get; set; } = 1.5f;

        [Export(PropertyHint.Range, "1,256,0.5")]
        public float MaxLengthWorld { get; set; } = 32f;

        /// <summary>Footprint depth tối thiểu lúc trưa, theo tỉ lệ chiều cao source.</summary>
        [Export(PropertyHint.Range, "0.01,0.8,0.01")]
        public float NoonFlatten { get; set; } = 0.10f;

        /// <summary>Footprint depth lúc thiên thể thấp.</summary>
        [Export(PropertyHint.Range, "0.01,0.8,0.01")]
        public float HorizonFlatten { get; set; } = 0.15f;

        [Export]
        public Color Tint { get; set; } = new Color(0.04f, 0.065f, 0.04f, 1f);

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float Opacity { get; set; } = 0.30f;

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float AlphaCutoff { get; set; } = 0.08f;

        [Export]
        public int ZIndex { get; set; } = -1;
    }
}
