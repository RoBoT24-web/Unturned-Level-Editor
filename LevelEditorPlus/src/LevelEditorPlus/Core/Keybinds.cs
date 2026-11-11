using System;
using System.Collections.Generic;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.Core
{
    public enum ShortcutAction { PickCorner, CancelFit, Duplicate, PasteAtCursor, FitAndCenter, FlipFacing }

    public sealed class KeyBinding
    {
        public KeyCode Key;
        public bool Ctrl;
        public bool Shift;
        public bool Alt;

        public override string ToString() => Key == KeyCode.None ? "Unbound" :
            (Ctrl ? "Ctrl+" : "") + (Shift ? "Shift+" : "") + (Alt ? "Alt+" : "") + Key;

        public bool SameAs(KeyBinding other) => other != null && Key == other.Key &&
            Ctrl == other.Ctrl && Shift == other.Shift && Alt == other.Alt;
    }

    public static class Keybinds
    {
        public static readonly ShortcutAction[] Actions = (ShortcutAction[])Enum.GetValues(typeof(ShortcutAction));
        private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        private static int blockedFrame = -1;
        private static int captureFrame;
        public static ShortcutAction? Capturing { get; private set; }
        public static bool SuppressGameInput => Capturing.HasValue || blockedFrame == Time.frameCount;
        public static string Status { get; private set; } = "Click a binding, then press a key combination.";

        public static string Name(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.PickCorner: return "Pick corner";
                case ShortcutAction.CancelFit: return "Cancel fitting";
                case ShortcutAction.Duplicate: return "Duplicate";
                case ShortcutAction.PasteAtCursor: return "Paste at cursor";
                case ShortcutAction.FitAndCenter: return "Fit + Center";
                default: return "Flip facing";
            }

        }

        public static KeyBinding Default(ShortcutAction action)
        {
            switch (action)
            {
                case ShortcutAction.PickCorner: return new KeyBinding { Key = KeyCode.F10 };
                case ShortcutAction.CancelFit: return new KeyBinding { Key = KeyCode.F11 };
                case ShortcutAction.Duplicate: return new KeyBinding { Key = KeyCode.D, Ctrl = true };
                case ShortcutAction.PasteAtCursor: return new KeyBinding { Key = KeyCode.G, Ctrl = true };
                case ShortcutAction.FitAndCenter: return new KeyBinding { Key = KeyCode.F10, Ctrl = true };
                default: return new KeyBinding { Key = KeyCode.G, Ctrl = true, Shift = true };
            }
        }

        public static KeyBinding Get(ShortcutAction action)
        {
            if (ModuleSettings.Data.Keybindings == null)
                ModuleSettings.Data.Keybindings = new Dictionary<string, KeyBinding>();
            if (ModuleSettings.Data.Keybindings.TryGetValue(action.ToString(), out KeyBinding binding) && binding != null)
                return binding;
            return Default(action);
        }

        public static string Label(ShortcutAction action) => Get(action).ToString();
        private static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        public static bool Pressed(ShortcutAction action)
        {
            KeyBinding binding = Get(action);
            return !Capturing.HasValue && blockedFrame != Time.frameCount && binding.Key != KeyCode.None &&
                EditorUI.window != null && EditorUI.window.isEnabled &&
                Glazier.Get().ShouldGameProcessInput && InputEx.GetKeyDown(binding.Key) &&
                binding.Ctrl == Ctrl && binding.Shift == Shift && binding.Alt == Alt;
        }

        public static void BeginCapture(ShortcutAction action)
        {
            Capturing = action;
            captureFrame = Time.frameCount;
            Status = $"Set {Name(action)}: press keys. Esc cancels; Delete clears.";
        }

        public static void CancelCapture()
        {
            if (!Capturing.HasValue) return;
            Capturing = null;
            blockedFrame = Time.frameCount;
            Status = "Rebinding cancelled.";
        }

        public static void UpdateCapture()
        {
            if (!Capturing.HasValue || captureFrame == Time.frameCount) return;
            blockedFrame = Time.frameCount;
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelCapture(); return; }
            foreach (KeyCode key in Keys)
            {
                if (key == KeyCode.None || (int)key >= (int)KeyCode.Mouse0 ||
                    key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                    key == KeyCode.LeftShift || key == KeyCode.RightShift ||
                    key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
                    key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
                    !Input.GetKeyDown(key)) continue;
                var binding = new KeyBinding { Key = key, Ctrl = Ctrl, Shift = Shift, Alt = Alt };
                if (key == KeyCode.Delete || key == KeyCode.Backspace)
                    binding = new KeyBinding();
                ShortcutAction action = Capturing.Value;
                foreach (ShortcutAction other in Actions)
                {
                    if (other != action && binding.Key != KeyCode.None && binding.SameAs(Get(other)))
                    {
                        Status = $"Already used by {Name(other)}. Choose another key or Esc.";
                        UnturnedLog.warn($"[{Nexus.Name}] {Status}");
                        return;
                    }
                }
                // Initialize missing settings before assigning a new binding.
                Get(action);
                ModuleSettings.Data.Keybindings[action.ToString()] = binding;
                ModuleSettings.MarkDirty();
                Capturing = null;
                Status = $"{Name(action)}: {binding}. Applied immediately.";
                return;
            }
        }

        public static void Reset()
        {
            CancelCapture();
            ModuleSettings.Data.Keybindings = new Dictionary<string, KeyBinding>();
            ModuleSettings.MarkDirty();
            blockedFrame = Time.frameCount;
            Status = "Default keybindings restored.";
        }
    }

    [HarmonyPatch(typeof(MenuConfigurationControlsUI), "get_ShouldGameIgnoreInput")]
    internal static class KeybindCaptureInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            __result |= Keybinds.SuppressGameInput;
        }
    }
}
