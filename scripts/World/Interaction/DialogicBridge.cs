using Godot;
using System;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Thin C# wrapper around the GDScript Dialogic autoload.<br/>
    /// Dialogic là GDScript singleton nên phải dùng Engine.GetSingleton + Call() thay vì gọi trực tiếp.<br/>
    /// Toàn bộ code C# trong project nên đi qua class này thay vì gọi Engine.GetSingleton mỗi chỗ.
    /// </summary>
    public static class DialogicBridge
    {
        private const string AutoloadName = "Dialogic";

        // ── Truy cập singleton ──────────────────────────────────────────────

        /// <summary>
        /// Trả về Dialogic autoload node, hoặc null nếu không tìm thấy.<br/>
        /// Dialogic là GDScript autoload → phải lấy qua SceneTree.Root, KHÔNG dùng Engine.GetSingleton.
        /// </summary>
        public static GodotObject GetDialogic()
        {
            // GDScript autoload nằm ở /root/Dialogic trong SceneTree.
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            if (tree?.Root == null)
            {
                return null;
            }

            Node dialogic = tree.Root.GetNodeOrNull("/root/Dialogic");
            if (dialogic == null)
            {
                GD.PrintErr("[DialogicBridge] Không tìm thấy /root/Dialogic. Kiểm tra plugin Dialogic đã enable trong Project Settings → Plugins chưa.");
            }
            return dialogic;
        }

        // ── Trạng thái ─────────────────────────────────────────────────────

        /// <summary>
        /// Trả về true nếu Dialogic đang chạy một timeline.<br/>
        /// Kiểm tra <c>Dialogic.current_timeline != null</c>.
        /// </summary>
        public static bool IsDialogActive()
        {
            GodotObject dialogic = GetDialogic();
            if (dialogic == null)
            {
                return false;
            }

            // current_timeline là null khi không có dialog đang chạy.
            Variant timeline = dialogic.Get("current_timeline");
            return timeline.VariantType != Variant.Type.Nil
                && timeline.AsGodotObject() != null;
        }

        // ── Điều khiển timeline ────────────────────────────────────────────

        /// <summary>
        /// Bắt đầu chạy một Dialogic timeline và đảm bảo layout scene tồn tại.<br/>
        /// Tương đương GDScript: <c>Dialogic.start(timelineName)</c>.<br/>
        /// Trả về layout node hoặc null nếu thất bại.
        /// </summary>
        /// <param name="timelineName">
        /// Tên timeline đã đăng ký trong Dialogic editor (không cần extension .dtl).<br/>
        /// Hoặc đường dẫn res:// đầy đủ đến file .dtl.
        /// </param>
        public static GodotObject StartTimeline(string timelineName)
        {
            if (string.IsNullOrWhiteSpace(timelineName))
            {
                GD.PrintErr("[DialogicBridge] StartTimeline called với timelineName rỗng.");
                return null;
            }

            GodotObject dialogic = GetDialogic();
            if (dialogic == null)
            {
                GD.PrintErr("[DialogicBridge] Dialogic autoload không tìm thấy. Kiểm tra plugin đã enable chưa.");
                return null;
            }

            GD.Print($"[DialogicBridge] Calling Dialogic.start('{timelineName}')...");
            Variant result = dialogic.Call("start", timelineName);
            GD.Print($"[DialogicBridge] Dialogic.start returned: type={result.VariantType}");
            if (result.VariantType == Variant.Type.Nil || result.AsGodotObject() == null)
            {
                GD.PrintErr($"[DialogicBridge] Dialogic.start('{timelineName}') returned null. Kiểm tra: style có tồn tại không? Timeline name đúng chưa?");
                return null;
            }
            GD.Print($"[DialogicBridge] Dialogic.start OK → layout node = {result.AsGodotObject()}");
            return result.AsGodotObject();
        }

        // ── Kết nối signal ─────────────────────────────────────────────────

        /// <summary>
        /// Subscribe vào signal <c>timeline_ended</c> của Dialogic.<br/>
        /// Callback sẽ được gọi một lần rồi tự disconnect.
        /// </summary>
        public static void OnTimelineEnded(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            GodotObject dialogic = GetDialogic();
            if (dialogic == null)
            {
                return;
            }

            // Dùng Callable.From để wrap Action thành Godot Callable.
            Callable callable = Callable.From(callback);

            // ConnectFlags.OneShot = 4: tự disconnect sau lần gọi đầu tiên.
            dialogic.Connect("timeline_ended", callable, (uint)GodotObject.ConnectFlags.OneShot);
        }

        /// <summary>
        /// Subscribe vào signal <c>timeline_started</c> của Dialogic.<br/>
        /// Callback sẽ được gọi một lần rồi tự disconnect.
        /// </summary>
        public static void OnTimelineStarted(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            GodotObject dialogic = GetDialogic();
            if (dialogic == null)
            {
                return;
            }

            Callable callable = Callable.From(callback);
            dialogic.Connect("timeline_started", callable, (uint)GodotObject.ConnectFlags.OneShot);
        }
    }
}
