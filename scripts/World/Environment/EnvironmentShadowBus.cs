using System;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// V1 compatibility marker. Shadow Core V2 không còn scan/update caster qua bus.
    /// EnvironmentBinder2D -> EnvironmentMaterialBus -> cloned per-caster shadow materials.
    /// </summary>
    [Obsolete("Shadow Core V2 không còn dùng EnvironmentShadowBus.")]
    public sealed class EnvironmentShadowBus
    {
    }
}
