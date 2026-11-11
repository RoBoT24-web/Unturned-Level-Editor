using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SDG.Unturned;

namespace LevelEditorPlus.Core
{
    public class WindowLayout
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public bool Visible = true;
        public bool Collapsed;
    }

    public class SettingsData
    {
        public Dictionary<string, WindowLayout> Windows = new Dictionary<string, WindowLayout>();
        public bool ShowSlotOutlines = true;
        public bool UprightSlotFitting = true;
        public Dictionary<string, KeyBinding> Keybindings = new Dictionary<string, KeyBinding>();
    }

    /// <summary>
    /// JSON settings stored next to the module DLL (window layout etc).
    /// </summary>
    public static class ModuleSettings
    {
        public static SettingsData Data = new SettingsData();

        private static bool dirty;
        private static float lastSaveRealtime;

        private static string FilePath
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(ModuleSettings).Assembly.Location);
                return Path.Combine(dir ?? ".", "LevelEditorPlus.settings.json");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Data = JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(FilePath)) ?? new SettingsData();
                }
            }
            catch (Exception e)
            {
                UnturnedLog.exception(e, $"[{Nexus.Name}] Failed to load settings:");
                Data = new SettingsData();
            }
        }

        public static WindowLayout GetOrCreateLayout(string windowId, float defaultX, float defaultY, float defaultW, float defaultH, bool defaultVisible)
        {
            if (!Data.Windows.TryGetValue(windowId, out WindowLayout layout))
            {
                layout = new WindowLayout { X = defaultX, Y = defaultY, Width = defaultW, Height = defaultH, Visible = defaultVisible };
                Data.Windows[windowId] = layout;
            }
            return layout;
        }

        public static void MarkDirty()
        {
            dirty = true;
        }

        /// <summary>Debounced save, call every frame; writes at most once per second.</summary>
        public static void SaveIfDirty()
        {
            if (!dirty || UnityEngine.Time.realtimeSinceStartup - lastSaveRealtime < 1f)
            {
                return;
            }
            SaveNow();
        }

        public static void SaveNow()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Data, Formatting.Indented));
                dirty = false;
                lastSaveRealtime = UnityEngine.Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                UnturnedLog.exception(e, $"[{Nexus.Name}] Failed to save settings:");
            }
        }
    }
}
