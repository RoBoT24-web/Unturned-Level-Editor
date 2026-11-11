using System;
using System.Collections.Generic;
using SDG.Framework.Rendering;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.Features
{
    /// <summary>
    /// Draws wireframe outlines + facing arrows for barricade slot colliders
    /// ("Door"/"Gate"/"Slot"/"Hatch" children) so invisible slot objects are
    /// visible in the editor. Our custom BarricadeSlots assets are always shown;
    /// vanilla slots are shown within a radius of the camera.
    /// </summary>
    public static class SlotVisualizer
    {
        private struct TrackedSlot
        {
            public BoxCollider Collider;
            public Color Color;
            public bool UseRightAxisTest; // window "Slot" children test the right axis
        }

        private const int MaxTracked = 512;
        private const float VanillaScanRadius = 100f;
        private const float RefreshInterval = 2f;

        // GUIDs of the BarricadeSlots asset pack (always outlined, any distance).
        private static readonly HashSet<Guid> CustomSlotGuids = new HashSet<Guid>
        {
            new Guid("87723a88b4064413bcaa587735e84b0d"), // Door
            new Guid("bf042a597e6b4c67b61d5d66da70f9f6"), // Gate
            new Guid("88e5d9bce2694d5dbbf87e08e0e8e074"), // Window
            new Guid("271f3bda1178400187d6578ee4634c2a"), // Hatch
        };

        private static readonly Dictionary<string, Color> SlotColors = new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            { "Door", new Color(0.35f, 0.7f, 1f) },
            { "Gate", new Color(1f, 0.6f, 0.15f) },
            { "Slot", new Color(1f, 0.95f, 0.3f) },
            { "Hatch", new Color(0.45f, 1f, 0.45f) },
        };

        private static readonly List<TrackedSlot> tracked = new List<TrackedSlot>();
        private static readonly List<BoxCollider> colliderBuffer = new List<BoxCollider>();
        private static bool attached;
        private static float nextRefresh;

        public static bool IsCustomSlot(Guid guid) => CustomSlotGuids.Contains(guid);

        public static bool Enabled
        {
            get
            {
                return Core.ModuleSettings.Data.ShowSlotOutlines;
            }
            set
            {
                Core.ModuleSettings.Data.ShowSlotOutlines = value;
                Core.ModuleSettings.MarkDirty();
            }
        }

        public static void Attach()
        {
            if (!attached)
            {
                attached = true;
                GLRenderer.render += HandleRender;
                nextRefresh = 0f;
            }
        }

        public static void Detach()
        {
            if (attached)
            {
                attached = false;
                GLRenderer.render -= HandleRender;
                tracked.Clear();
            }
        }

        /// <summary>Called every frame by the window manager.</summary>
        public static void Update()
        {
            if (!Enabled || Time.realtimeSinceStartup < nextRefresh)
            {
                return;
            }
            nextRefresh = Time.realtimeSinceStartup + RefreshInterval;
            Refresh();
        }

        private static void Refresh()
        {
            tracked.Clear();
            Vector3 cameraPosition = MainCamera.instance != null
                ? MainCamera.instance.transform.position
                : Vector3.zero;

            foreach (LevelObject obj in SelectionTools.EnumerateLevelObjects())
            {
                if (tracked.Count >= MaxTracked)
                {
                    break;
                }
                if (obj.transform == null || obj.asset == null)
                {
                    continue;
                }
                bool isCustomSlot = CustomSlotGuids.Contains(obj.asset.GUID);
                if (!isCustomSlot &&
                    (obj.transform.position - cameraPosition).sqrMagnitude > VanillaScanRadius * VanillaScanRadius)
                {
                    continue;
                }

                obj.transform.GetComponentsInChildren(includeInactive: true, colliderBuffer);
                foreach (BoxCollider collider in colliderBuffer)
                {
                    string name = collider.gameObject.name;
                    if (!SlotColors.TryGetValue(name, out Color color))
                    {
                        continue;
                    }
                    // Doors only match by name in the game; others also need the Logic tag.
                    if (name != "Door" && !collider.CompareTag("Logic"))
                    {
                        continue;
                    }
                    tracked.Add(new TrackedSlot
                    {
                        Collider = collider,
                        Color = color,
                        UseRightAxisTest = name == "Slot",
                    });
                    if (tracked.Count >= MaxTracked)
                    {
                        break;
                    }
                }
            }
        }

        private static void HandleRender()
        {
            if (!Enabled || tracked.Count == 0)
            {
                return;
            }
            GLUtility.LINE_FLAT_COLOR.SetPass(0);
            GL.Begin(GL.LINES);
            foreach (TrackedSlot slot in tracked)
            {
                if (slot.Collider != null && slot.Collider.transform != null)
                {
                    DrawSlot(slot);
                }
            }
            GL.End();
        }

        private static void DrawSlot(TrackedSlot slot)
        {
            Transform t = slot.Collider.transform;
            Vector3 c = slot.Collider.center;
            Vector3 e = slot.Collider.size * 0.5f;

            // The 8 local-space corners transformed to world space.
            Vector3 p0 = t.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z));
            Vector3 p1 = t.TransformPoint(c + new Vector3(e.x, -e.y, -e.z));
            Vector3 p2 = t.TransformPoint(c + new Vector3(e.x, -e.y, e.z));
            Vector3 p3 = t.TransformPoint(c + new Vector3(-e.x, -e.y, e.z));
            Vector3 p4 = t.TransformPoint(c + new Vector3(-e.x, e.y, -e.z));
            Vector3 p5 = t.TransformPoint(c + new Vector3(e.x, e.y, -e.z));
            Vector3 p6 = t.TransformPoint(c + new Vector3(e.x, e.y, e.z));
            Vector3 p7 = t.TransformPoint(c + new Vector3(-e.x, e.y, e.z));

            GL.Color(slot.Color);
            Line(p0, p1); Line(p1, p2); Line(p2, p3); Line(p3, p0); // bottom
            Line(p4, p5); Line(p5, p6); Line(p6, p7); Line(p7, p4); // top
            Line(p0, p4); Line(p1, p5); Line(p2, p6); Line(p3, p7); // verticals

            // Facing arrow, replicating the game's axis checks: the barricade's
            // yaw comes from the slot's forward axis if the tested axis is
            // vertical, otherwise from its up axis.
            Vector3 testedAxis = slot.UseRightAxisTest ? t.right : t.up;
            Vector3 facing = Mathf.Abs(Vector3.Dot(testedAxis, Vector3.up)) > 0.5f ? t.forward : t.up;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f)
            {
                return;
            }
            facing.Normalize();

            Vector3 center = t.TransformPoint(c);
            Vector3 tip = center + facing * 0.9f;
            Vector3 headLeft = Quaternion.Euler(0f, 150f, 0f) * facing * 0.3f;
            Vector3 headRight = Quaternion.Euler(0f, -150f, 0f) * facing * 0.3f;
            Line(center, tip);
            Line(tip, tip + headLeft);
            Line(tip, tip + headRight);
        }

        private static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }
    }
}
