using LevelEditorPlus.Core;
using LevelEditorPlus.UI;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus
{
    /// <summary>
    /// Watches for the level editor to load/unload and attaches our window
    /// manager to the editor's UI root when it exists.
    /// </summary>
    public class EditorPlusBehaviour : MonoBehaviour
    {
        public static EditorPlusBehaviour Instance { get; private set; }

        private WindowManager windowManager;
        private SleekWindow attachedTo;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            bool editorReady = Level.isEditor && Level.isLoaded && EditorUI.window != null;

            if (editorReady)
            {
                // EditorUI.window is recreated on every editor load, so compare
                // against the instance we attached to previously.
                if (windowManager == null || !ReferenceEquals(attachedTo, EditorUI.window))
                {
                    windowManager?.Dispose();
                    attachedTo = EditorUI.window;
                    try
                    {
                        windowManager = new WindowManager(EditorUI.window);
                        Features.SlotVisualizer.Attach();
                        UnturnedLog.info($"[{Nexus.Name}] Editor UI attached.");
                    }
                    catch (System.Exception e)
                    {
                        UnturnedLog.exception(e, $"[{Nexus.Name}] Failed to attach editor UI:");
                        windowManager = null;
                        enabled = false; // stop spamming if something is broken
                        return;
                    }
                }

                try
                {
                    windowManager.Update();
                }
                catch (System.Exception e)
                {
                    UnturnedLog.exception(e, $"[{Nexus.Name}] Error in UI update:");
                }

                ModuleSettings.SaveIfDirty();
            }
            else if (windowManager != null)
            {
                windowManager.Dispose();
                windowManager = null;
                attachedTo = null;
                Features.SlotVisualizer.Detach();
                ModuleSettings.SaveNow();
                UnturnedLog.info($"[{Nexus.Name}] Editor UI detached.");
            }
        }

        private void OnDestroy()
        {
            windowManager?.Dispose();
        }
    }
}
