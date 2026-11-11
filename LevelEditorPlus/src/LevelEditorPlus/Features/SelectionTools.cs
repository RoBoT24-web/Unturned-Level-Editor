using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.Features
{
    /// <summary>
    /// Helpers around the editor's object selection. Several members used here
    /// (selection, clearSelection, handles, ...) are private in the game and
    /// exposed at compile time by the publicizer.
    /// </summary>
    public static class SelectionTools
    {
        /// <summary>Cap for bulk selection so highlighting doesn't melt the frame rate.</summary>
        public const int MaxBulkSelect = 500;

        public static List<EditorSelection> Selection => EditorObjects.selection;

        public static int Count => EditorObjects.selection?.Count ?? 0;

        public static Transform Primary
        {
            get
            {
                List<EditorSelection> sel = EditorObjects.selection;
                if (sel == null || sel.Count == 0)
                {
                    return null;
                }
                return sel[sel.Count - 1]?.transform;
            }
        }

        public static void ClearSelection()
        {
            EditorObjects.clearSelection();
        }

        public static void Select(Transform transform, bool additive)
        {
            if (!additive)
            {
                EditorObjects.clearSelection();
            }
            if (transform != null)
            {
                if (additive && EditorObjects.containsSelection(transform))
                {
                    EditorObjects.removeSelection(transform);
                }
                else if (!EditorObjects.containsSelection(transform))
                {
                    EditorObjects.addSelection(transform);
                }
            }
        }

        public static void FocusCameraOn(Vector3 position)
        {
            Transform rig = MainCamera.instance.transform.parent;
            rig.position = position - 15f * MainCamera.instance.transform.forward;
        }

        public static void FocusCameraOnSelection()
        {
            if (Count > 0)
            {
                FocusCameraOn(EditorObjects.handles.GetPivotPosition());
            }
        }

        public static IEnumerable<LevelObject> EnumerateLevelObjects()
        {
            List<LevelObject>[,] grid = LevelObjects.objects;
            if (grid == null)
            {
                yield break;
            }
            for (int x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (int y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    List<LevelObject> cell = grid[x, y];
                    if (cell == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < cell.Count; i++)
                    {
                        yield return cell[i];
                    }
                }
            }
        }

        public static IEnumerable<LevelBuildableObject> EnumerateBuildables()
        {
            List<LevelBuildableObject>[,] grid = LevelObjects.buildables;
            if (grid == null)
            {
                yield break;
            }
            for (int x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (int y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    List<LevelBuildableObject> cell = grid[x, y];
                    if (cell == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < cell.Count; i++)
                    {
                        yield return cell[i];
                    }
                }
            }
        }

        /// <summary>Total objects + buildables currently registered (cheap count for change detection).</summary>
        public static int TotalObjectCount()
        {
            int total = 0;
            List<LevelObject>[,] objects = LevelObjects.objects;
            List<LevelBuildableObject>[,] buildables = LevelObjects.buildables;
            if (objects == null)
            {
                return 0;
            }
            for (int x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (int y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    total += objects[x, y]?.Count ?? 0;
                    total += buildables[x, y]?.Count ?? 0;
                }
            }
            return total;
        }

        /// <summary>Selects every object that shares the primary selection's asset.</summary>
        public static int SelectSameAsset()
        {
            Transform primary = Primary;
            if (primary == null)
            {
                return 0;
            }
            LevelObjects.getAssetEditor(primary, out ObjectAsset objectAsset, out ItemAsset itemAsset);
            if (objectAsset == null && itemAsset == null)
            {
                return 0;
            }

            EditorObjects.clearSelection();
            int added = 0;
            if (objectAsset != null)
            {
                foreach (LevelObject obj in EnumerateLevelObjects())
                {
                    if (added >= MaxBulkSelect)
                    {
                        break;
                    }
                    if (obj.asset == objectAsset && obj.transform != null)
                    {
                        EditorObjects.addSelection(obj.transform);
                        added++;
                    }
                }
            }
            else
            {
                foreach (LevelBuildableObject b in EnumerateBuildables())
                {
                    if (added >= MaxBulkSelect)
                    {
                        break;
                    }
                    if (b.asset == itemAsset && b.transform != null)
                    {
                        EditorObjects.addSelection(b.transform);
                        added++;
                    }
                }
            }
            return added;
        }

        /// <summary>
        /// Drops each selected object straight down onto the first surface below
        /// it (ignoring the object itself). Registers in the undo history.
        /// </summary>
        public static int DropToGround()
        {
            List<EditorSelection> sel = EditorObjects.selection;
            if (sel == null || sel.Count == 0)
            {
                return 0;
            }

            EditorObjects.pointSelection();
            int moved = 0;
            foreach (EditorSelection s in sel)
            {
                if (s.transform == null)
                {
                    continue;
                }
                Vector3 origin = s.transform.position + Vector3.up * 0.5f;
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 4096f, RayMasks.EDITOR_WORLD);
                float bestDistance = float.MaxValue;
                Vector3 bestPoint = default;
                bool found = false;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.transform == null || hit.transform.IsChildOf(s.transform))
                    {
                        continue;
                    }
                    if (hit.distance < bestDistance)
                    {
                        bestDistance = hit.distance;
                        bestPoint = hit.point;
                        found = true;
                    }
                }
                if (found)
                {
                    s.transform.position = bestPoint;
                    moved++;
                }
            }
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            return moved;
        }

        /// <summary>Duplicates the current selection in place and selects the copies.</summary>
        public static int DuplicateSelection()
        {
            List<EditorSelection> sel = EditorObjects.selection;
            if (sel == null || sel.Count == 0)
            {
                return 0;
            }

            var pending = new List<(Vector3 position, Quaternion rotation, Vector3 scale, ObjectAsset objectAsset, ItemAsset itemAsset)>();
            foreach (EditorSelection s in sel)
            {
                if (s.transform == null)
                {
                    continue;
                }
                LevelObjects.getAssetEditor(s.transform, out ObjectAsset objectAsset, out ItemAsset itemAsset);
                if (objectAsset != null || itemAsset != null)
                {
                    pending.Add((s.transform.position, s.transform.rotation, s.transform.localScale, objectAsset, itemAsset));
                }
            }
            if (pending.Count == 0)
            {
                return 0;
            }

            EditorObjects.clearSelection();
            LevelObjects.step++;
            int added = 0;
            foreach (var p in pending)
            {
                Transform copy = LevelObjects.registerAddObject(p.position, p.rotation, p.scale, p.objectAsset, p.itemAsset);
                if (copy != null)
                {
                    EditorObjects.addSelection(copy);
                    added++;
                }
            }
            return added;
        }
    }
}
