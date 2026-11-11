// Managed stand-ins for input/UI so the real keybinding service can run without Unity.
namespace UnityEngine
{
    public enum KeyCode
    {
        None = 0, Backspace = 8, Escape = 27, D = 100, G = 103, K = 107,
        Delete = 127, F10 = 291, F11 = 292, F12 = 293, RightShift = 303,
        LeftShift = 304, RightControl = 305, LeftControl = 306,
        RightAlt = 307, LeftAlt = 308, RightCommand = 309, LeftCommand = 310, Mouse0 = 323
    }
    public static class Time { public static int frameCount; }
    public static class Input
    {
        public static readonly HashSet<KeyCode> Held = new();
        public static readonly HashSet<KeyCode> Down = new();
        public static bool GetKey(KeyCode key) => Held.Contains(key) || Down.Contains(key);
        public static bool GetKeyDown(KeyCode key) => Down.Contains(key);
    }
}
namespace SDG.Unturned
{
    public sealed class SleekWindow { public bool isEnabled = true; }
    public static class EditorUI { public static SleekWindow window = new(); }
    public sealed class Glazier
    {
        private static readonly Glazier instance = new();
        public bool ShouldGameProcessInput = true;
        public static Glazier Get() => instance;
    }
    public static class InputEx
    {
        public static bool Typing;
        public static bool GetKeyDown(UnityEngine.KeyCode key) =>
            !Typing && UnityEngine.Input.GetKeyDown(key);
    }
    public static class UnturnedLog { public static void warn(string message) { } }
    public class MenuConfigurationControlsUI { }
}
namespace HarmonyLib
{
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
}
namespace LevelEditorPlus
{
    public static class Nexus { public const string Name = "Test"; }
}
namespace LevelEditorPlus.Core
{
    public sealed class SettingsData { public Dictionary<string, KeyBinding> Keybindings = new(); }
    public static class ModuleSettings
    {
        public static SettingsData Data = new();
        public static bool Dirty;
        public static void MarkDirty() => Dirty = true;
    }
}
