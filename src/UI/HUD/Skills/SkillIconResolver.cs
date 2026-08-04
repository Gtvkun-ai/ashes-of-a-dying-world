using Godot;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.UI.HUD.Skills
{
    /// <summary>
    /// Một nơi duy nhất quyết định icon hiển thị cho kỹ năng.
    /// Nhờ vậy resource quên gắn icon vẫn có fallback, thay vì mỗi màn hình tự vẽ một dấu chấm khác nhau.
    /// </summary>
    public static class SkillIconResolver
    {
        private const string DefaultIconPath = "res://assets/resources/data/icon/default_skill.tres";
        private static Texture2D _cachedDefaultIcon;

        public static Texture2D Resolve(SkillData skill)
        {
            if (skill?.Icon != null)
            {
                return skill.Icon;
            }

            _cachedDefaultIcon ??= ResourceLoader.Exists(DefaultIconPath)
                ? GD.Load<Texture2D>(DefaultIconPath)
                : null;

            return _cachedDefaultIcon;
        }
    }
}
