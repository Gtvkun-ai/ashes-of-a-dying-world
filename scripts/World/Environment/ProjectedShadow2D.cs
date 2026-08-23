using System;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Compatibility alias cho scene/branch cũ. Runtime mới dùng ShadowCaster2D + ShadowRenderer2D.
    /// Có thể xoá file này khi toàn bộ branch cũ đã migrate.
    /// </summary>
    [Obsolete("Shadow Core V2 dùng ShadowCaster2D.")]
    public partial class ProjectedShadow2D : ShadowCaster2D
    {
    }
}
