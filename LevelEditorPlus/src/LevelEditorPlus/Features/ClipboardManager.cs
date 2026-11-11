using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.Features
{
    /// <summary>
    /// Extends the editor's Ctrl+C clipboard: persists it to disk (works across
    /// restarts and maps) and adds paste-at-cursor and duplicate hotkeys.
    /// </summary>
    public class ClipboardManager
    {
        private class ClipEntry
        {
            public string Guid;
            public bool IsItem;
            public float PX, PY, PZ;
            public float RX, RY, RZ, RW;
            public float SX, SY, SZ;
        }

        private float lastWatch;
        private float lastSignature = float.NaN;

        private static string FilePath
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(ClipboardManager).Assembly.Location);
                return Path.Combine(dir ?? ".", "LevelEditorPlus.clipboard.json");
            }
        }

        public ClipboardManager()
        {
            // Restore the persisted clipboard when entering the editor.
            try
            {
                List<EditorCopy> copies = EditorObjects.copies;
                if (copies != null && copies.Count == 0 && File.Exists(FilePath))
                {
                    var entries = JsonConvert.DeserializeObject<List<ClipEntry>>(File.ReadAllText(FilePath));
                    if (entries != null)
                    {
                        foreach (ClipEntry e in entries)
                        {
                            if (!System.Guid.TryParse(e.Guid, out Guid guid))
                            {
                                continue;
                            }
                            ObjectAsset objectAsset = e.IsItem ? null : Assets.find<ObjectAsset>(guid);
                            ItemAsset itemAsset = e.IsItem ? Assets.find<ItemAsset>(guid) : null;
                            if (objectAsset == null && itemAsset == null)
                            {
                                continue;
                            }
                            copies.Add(new EditorCopy(
                                new Vector3(e.PX, e.PY, e.PZ),
                                new Quaternion(e.RX, e.RY, e.RZ, e.RW),
                                new Vector3(e.SX, e.SY, e.SZ),
                                objectAsset,
                                itemAsset));
                        }
                        if (copies.Count > 0)
                        {
                            UnturnedLog.info($"[{Nexus.Name}] Restored {copies.Count} clipboard object(s). Ctrl+V to paste, {Core.Keybinds.Label(Core.ShortcutAction.PasteAtCursor)} to paste at cursor.");
                        }
                    }
                }
                lastSignature = ComputeSignature();
            }
            catch (Exception e)
            {
                UnturnedLog.exception(e, $"[{Nexus.Name}] Failed to restore clipboard:");
            }
        }

        public void Update()
        {
            HandleHotkeys();
            WatchForChanges();
        }

        private void HandleHotkeys()
        {
            if (!EditorObjects.isBuilding || !Glazier.Get().ShouldGameProcessInput || EditorInteract.isFlying)
            {
                return;
            }
            if (Core.Keybinds.Pressed(Core.ShortcutAction.Duplicate))
            {
                int added = SelectionTools.DuplicateSelection();
                if (added > 0)
                {
                    UnturnedLog.info($"[{Nexus.Name}] Duplicated {added} object(s).");
                }
            }

            if (Core.Keybinds.Pressed(Core.ShortcutAction.PasteAtCursor))
            {
                PasteAtCursor();
            }
        }

        private void PasteAtCursor()
        {
            List<EditorCopy> copies = EditorObjects.copies;
            if (copies == null || copies.Count == 0 || EditorInteract.worldHit.transform == null)
            {
                return;
            }

            Vector3 centroid = Vector3.zero;
            foreach (EditorCopy c in copies)
            {
                centroid += c.position;
            }
            centroid /= copies.Count;
            Vector3 delta = EditorInteract.worldHit.point - centroid;

            EditorObjects.clearSelection();
            LevelObjects.step++;
            int added = 0;
            foreach (EditorCopy c in copies)
            {
                Transform t = LevelObjects.registerAddObject(c.position + delta, c.rotation, c.scale, c.objectAsset, c.itemAsset);
                if (t != null)
                {
                    EditorObjects.addSelection(t);
                    added++;
                }
            }
            if (added > 0)
            {
                UnturnedLog.info($"[{Nexus.Name}] Pasted {added} object(s) at cursor.");
            }
        }

        private void WatchForChanges()
        {
            if (Time.realtimeSinceStartup - lastWatch < 0.5f)
            {
                return;
            }
            lastWatch = Time.realtimeSinceStartup;

            float signature = ComputeSignature();
            if (!signature.Equals(lastSignature))
            {
                lastSignature = signature;
                Save();
            }
        }

        private static float ComputeSignature()
        {
            List<EditorCopy> copies = EditorObjects.copies;
            if (copies == null)
            {
                return -1f;
            }
            float sig = copies.Count * 31f;
            foreach (EditorCopy c in copies)
            {
                sig += c.position.x + c.position.y * 7f + c.position.z * 13f;
            }
            return sig;
        }

        private static void Save()
        {
            try
            {
                List<EditorCopy> copies = EditorObjects.copies;
                if (copies == null)
                {
                    return;
                }
                var entries = new List<ClipEntry>(copies.Count);
                foreach (EditorCopy c in copies)
                {
                    Asset asset = (Asset)c.objectAsset ?? c.itemAsset;
                    if (asset == null)
                    {
                        continue;
                    }
                    entries.Add(new ClipEntry
                    {
                        Guid = asset.GUID.ToString("N"),
                        IsItem = c.itemAsset != null,
                        PX = c.position.x, PY = c.position.y, PZ = c.position.z,
                        RX = c.rotation.x, RY = c.rotation.y, RZ = c.rotation.z, RW = c.rotation.w,
                        SX = c.scale.x, SY = c.scale.y, SZ = c.scale.z,
                    });
                }
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(entries));
            }
            catch (Exception e)
            {
                UnturnedLog.exception(e, $"[{Nexus.Name}] Failed to save clipboard:");
            }
        }
    }
}
