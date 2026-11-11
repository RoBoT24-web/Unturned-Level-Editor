using LevelEditorPlus.Core;
using SDG.Framework.Modules;
using SDG.Unturned;
using UnityEngine;
using HarmonyLib;

namespace LevelEditorPlus
{
    /// <summary>
    /// Module entry point. Unturned's ModuleHook finds this class because it
    /// implements IModuleNexus, and calls initialize()/shutdown().
    /// </summary>
    public class Nexus : IModuleNexus
    {
        public const string Name = "LevelEditorPlus";
        public const string Version = "0.1.0";

        public static GameObject HostObject { get; private set; }
        private Harmony harmony;

        public void initialize()
        {
            LogCapture.Begin();
            ModuleSettings.Load();
            harmony = new Harmony("LevelEditorPlus.KeybindCapture");
            harmony.CreateClassProcessor(typeof(KeybindCaptureInputPatch)).Patch();

            // Persistent host object so our Update loop survives scene changes.
            HostObject = new GameObject(Name);
            Object.DontDestroyOnLoad(HostObject);
            HostObject.AddComponent<EditorPlusBehaviour>();

            UnturnedLog.info($"[{Name}] v{Version} initialized.");
        }

        public void shutdown()
        {
            ModuleSettings.SaveNow();
            if (HostObject != null)
            {
                Object.Destroy(HostObject);
                HostObject = null;
            }
            LogCapture.End();
            harmony?.UnpatchAll("LevelEditorPlus.KeybindCapture");
            UnturnedLog.info($"[{Name}] shut down.");
        }
    }
}
