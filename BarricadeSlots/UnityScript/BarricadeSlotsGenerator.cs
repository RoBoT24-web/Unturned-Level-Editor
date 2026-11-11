#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BarricadeSlots
{
    /// <summary>
    /// Generates the invisible "barricade slot" object prefabs and exports them
    /// as a master bundle for Unturned.
    ///
    /// How to use (inside the U3-SDK Unity project):
    ///   1. Copy this file into Assets/Editor/ (create the folder if needed).
    ///   2. Menu: Tools > Barricade Slots > 1. Generate Prefabs
    ///   3. Menu: Tools > Barricade Slots > 2. Export Master Bundle...
    ///      and pick the Content/BarricadeSlots folder of this mod (the folder
    ///      that contains MasterBundle.dat).
    ///
    /// Slot conventions (from the game's UseableBarricade placement code):
    ///   - Doors   attach to a collider named "Door"  (SLOTS_INTERACT layers)
    ///   - Gates   attach to a collider named "Gate"  with tag "Logic"
    ///   - Boards/Shutters/Glass attach to "Slot"     with tag "Logic"
    ///   - Hatches attach to a collider named "Hatch" with tag "Logic"
    /// The slot collider lives on the LOGIC layer (8): doors/windows raycast it,
    /// but it never blocks players, placement overlap checks, or editor clicks.
    /// The root has a small SMALL-layer (17) collider placed ABOVE the opening
    /// so the object can be selected in the level editor without blocking the
    /// walkway.
    /// </summary>
    public static class BarricadeSlotsGenerator
    {
        private const string RootFolder = "Assets/Bundles/BarricadeSlots";
        private const string BundleName = "barricadeslots.masterbundle";

        private class Variant
        {
            public string Name;
            public string SlotName;
            public Vector3 SlotCenter;
            public Vector3 SlotSize;
            public Vector3 MarkerCenter;
        }

        private static readonly Variant[] Variants =
        {
            // All coordinates are authored in prefab-local Z-up space:
            // x = width, y = depth (-y = facing), z = height. The level editor
            // places objects rotated -90 degrees around X (vanilla assets are
            // authored Z-up), which turns prefab +Z into world up. After
            // placement, the object's LOCAL GREEN (Y) arrow is the direction
            // the door/board faces - matching the old devkit mapping guide.

            // Door: typical vanilla doorway ~1.1m wide, ~2.1m tall. Slot child
            // centered in the opening; door spawns at the child's position.
            new Variant
            {
                Name = "Slot_Door", SlotName = "Door",
                SlotCenter = new Vector3(0f, 0f, 1.05f),
                SlotSize = new Vector3(1.1f, 0.15f, 2.0f),
                MarkerCenter = new Vector3(0f, 0f, 2.25f),
            },
            // Gate: garage-style opening.
            new Variant
            {
                Name = "Slot_Gate", SlotName = "Gate",
                SlotCenter = new Vector3(0f, 0f, 1.35f),
                SlotSize = new Vector3(3.6f, 0.2f, 2.6f),
                MarkerCenter = new Vector3(0f, 0f, 2.85f),
            },
            // Window: boards/shutters/glass.
            new Variant
            {
                Name = "Slot_Window", SlotName = "Slot",
                SlotCenter = new Vector3(0f, 0f, 1.45f),
                SlotSize = new Vector3(1.1f, 0.15f, 0.95f),
                MarkerCenter = new Vector3(0f, 0f, 2.1f),
            },
            // Hatch: horizontal trapdoor opening; pivot sits on the floor plane.
            new Variant
            {
                Name = "Slot_Hatch", SlotName = "Hatch",
                SlotCenter = new Vector3(0f, 0f, 0f),
                SlotSize = new Vector3(1.2f, 1.2f, 0.15f),
                MarkerCenter = new Vector3(0.8f, -0.8f, 0.15f),
            },
        };

        [MenuItem("Tools/Barricade Slots/1. Generate Prefabs")]
        public static void GeneratePrefabs()
        {
            foreach (Variant variant in Variants)
            {
                string folder = $"{RootFolder}/Objects/{variant.Name}";
                Directory.CreateDirectory(folder);

                GameObject root = new GameObject("Object");
                try
                {
                    root.tag = "Small";
                    root.layer = 17; // SMALL

                    // Small selection marker above the opening so the mapper can
                    // click the object in the editor without blocking players.
                    BoxCollider marker = root.AddComponent<BoxCollider>();
                    marker.center = variant.MarkerCenter;
                    marker.size = new Vector3(0.35f, 0.35f, 0.35f);

                    GameObject slot = new GameObject(variant.SlotName);
                    slot.tag = "Logic";
                    slot.layer = 8; // LOGIC
                    slot.transform.SetParent(root.transform, false);
                    // The barricade spawns at the slot child's position, so the
                    // child sits at the opening center with the collider
                    // centered on it. After placement, the object's local green
                    // (Y) arrow is the direction the door/board faces.
                    slot.transform.localPosition = variant.SlotCenter;
                    BoxCollider slotCollider = slot.AddComponent<BoxCollider>();
                    slotCollider.size = variant.SlotSize;

                    string prefabPath = $"{folder}/Object.prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[BarricadeSlots] Saved {prefabPath}");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.Refresh();

            // Tag the whole folder as our master bundle.
            AssetImporter importer = AssetImporter.GetAtPath(RootFolder);
            if (importer != null)
            {
                importer.assetBundleName = BundleName;
                importer.SaveAndReimport();
            }

            Debug.Log($"[BarricadeSlots] Done. Folder '{RootFolder}' assigned to bundle '{BundleName}'.");
        }

        [MenuItem("Tools/Barricade Slots/2. Export Master Bundle...")]
        public static void ExportBundle()
        {
            string output = EditorUtility.OpenFolderPanel(
                "Select the mod folder containing MasterBundle.dat", "", "");
            if (string.IsNullOrEmpty(output))
            {
                return;
            }
            SDG.Unturned.Tools.EditorAssetBundleHelper.Build(BundleName, output, false);
            Debug.Log($"[BarricadeSlots] Exported {BundleName} to {output}");
            EditorUtility.RevealInFinder(output);
        }
    }
}
#endif
