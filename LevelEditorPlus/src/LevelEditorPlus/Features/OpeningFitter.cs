using System;
using LevelEditorPlus.Core;
using SDG.Framework.Rendering;
using SDG.Unturned;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace LevelEditorPlus.Features
{
    public sealed class OpeningFitter : IDisposable
    {
        private readonly Vector3[] points = new Vector3[3];
        private Transform target;
        private BoxCollider slot;
        private int pointCount;
        private bool resize;
        private float inset;
        private float depthOffset;
        private bool upright;
        private Vector3 firstNormal;
        private enum FitStage { Corners, Frame, Interior, Preview }
        private FitStage stage;
        private OpeningGeometry preview;
        private Vector3 probeRight;
        private Vector3 probeUp;
        private readonly RaycastHit[] probeHits = new RaycastHit[64];

        public bool Active { get; private set; }
        public bool HasPreview => Active && stage == FitStage.Preview;
        public string Status { get; private set; } = "Select ONE Barricade Slot, then choose a fitting mode.";

        public void Begin(bool resizeToOpening, float edgeInset, float depth)
        {
            Cancel();
            if (!TryGetSlot(out target, out slot))
            {
                return;
            }
            if (!Finite(edgeInset) || edgeInset < 0f || !Finite(depth))
            {
                Report("Inset must be non-negative; both offsets must be finite.");
                return;
            }
            resize = resizeToOpening;
            inset = resize ? edgeInset : 0f;
            depthOffset = depth;
            upright = Core.ModuleSettings.Data.UprightSlotFitting;
            pointCount = 0;
            stage = FitStage.Corners;
            preview = null;
            Active = true;
            GLRenderer.render += Render;
            Prompt();
        }

        public void BeginAuto(float edgeInset, float depth)
        {
            Begin(true, edgeInset, depth);
            if (!Active)
                return;
            stage = FitStage.Frame;
            Status = $"Aim at the solid FRONT face of the frame; press {Keybinds.Label(ShortcutAction.PickCorner)}.\nThen pick inside the opening. No changes until confirmed.";
        }

        public void Cancel()
        {
            if (Active)
            {
                Active = false;
                preview = null;
                GLRenderer.render -= Render;
                Status = "Fitting cancelled. No changes made.";
            }
        }

        public void Dispose() => Cancel();

        private void Report(string message)
        {
            Status = message;
            UnturnedLog.warn($"[{Nexus.Name}] Fit slot: {message}");
        }

        public static bool IsSlotSelected()
        {
            Transform selected = SelectionTools.Primary;
            if (!EditorObjects.isBuilding || SelectionTools.Count != 1 || selected == null)
                return false;
            LevelObjects.getAssetEditor(selected, out ObjectAsset asset, out _);
            return asset != null && SlotVisualizer.IsCustomSlot(asset.GUID);
        }

        private bool TryGetSlot(out Transform root, out BoxCollider collider)
        {
            root = null;
            collider = null;
            if (!EditorObjects.isBuilding || SelectionTools.Count != 1)
            {
                Report("Open Objects and select exactly ONE Barricade Slot.");
                return false;
            }
            root = SelectionTools.Primary;
            if (!IsSlotSelected())
            {
                Report("Select a standalone Barricade Slot, not the building or a barricade.");
                return false;
            }
            foreach (BoxCollider candidate in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (candidate.CompareTag("Logic") &&
                    (candidate.name == "Door" || candidate.name == "Gate" ||
                     candidate.name == "Slot" || candidate.name == "Hatch"))
                {
                    if (collider != null)
                    {
                        Report("This asset has multiple slots; fitting it is not supported.");
                        return false;
                    }
                    collider = candidate;
                }
            }
            if (collider == null)
            {
                Report("No slot BoxCollider was found in the selected asset.");
                return false;
            }
            Vector3 scale = root.localScale;
            Vector3 parentScale = root.parent != null ? root.parent.lossyScale : Vector3.one;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
                (parentScale - Vector3.one).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(root.rotation, collider.transform.rotation) > 0.01f ||
                collider.size.x <= 0f || collider.size.y <= 0f || collider.size.z <= 0f)
            {
                Report("Fitting requires positive scale, an unscaled parent and axis-aligned slot child.");
                return false;
            }
            return true;
        }

        public void Update(bool cursorOverTools)
        {
            if (!Active)
            {
                return;
            }
            if (!EditorObjects.isBuilding || target == null || slot == null ||
                SelectionTools.Count != 1 || SelectionTools.Primary != target)
            {
                Cancel();
                Report("Selection changed. Fitting cancelled without changes.");
                return;
            }
            if (Keybinds.Pressed(ShortcutAction.CancelFit))
            {
                Cancel();
                return;
            }
            if (!Keybinds.Pressed(ShortcutAction.PickCorner))
            {
                return;
            }
            if (cursorOverTools || !Glazier.Get().ShouldGameProcessInput ||
                EditorInteract.isFlying || !EditorUI.window.isEnabled)
            {
                Report($"Move onto the frame, release right mouse, then press {Keybinds.Label(ShortcutAction.PickCorner)}.");
                return;
            }
            if (HasPreview)
            {
                ConfirmPreview();
                return;
            }
            if (stage == FitStage.Interior)
            {
                DetectOpening();
                return;
            }
            if (!TryRaycast(out Vector3 hit, out Vector3 normal))
            {
                Report($"No surface under cursor. Aim at the frame edge; press {Keybinds.Label(ShortcutAction.PickCorner)} again.");
                return;
            }
            if (stage == FitStage.Frame)
            {
                if (upright && Mathf.Abs(normal.y) > 0.01f)
                {
                    Report("Auto-fit needs a vertical FRONT frame face. Disable Upright for hatches/slopes, or use manual fitting.");
                    return;
                }
                firstNormal = upright ? new Vector3(normal.x, 0f, normal.z).normalized : normal.normalized;
                Vector3 height = upright ? Vector3.up :
                    (HeightAxis(slot) == 2 ? slot.transform.forward : slot.transform.up);
                probeUp = Vector3.ProjectOnPlane(height, firstNormal).normalized;
                if (probeUp.sqrMagnitude < 0.5f)
                {
                    Report("Slot height points into this face. Rotate the slot to match the opening, or use manual fitting.");
                    return;
                }
                probeRight = Vector3.Cross(probeUp, firstNormal).normalized;
                points[0] = hit;
                pointCount = 1;
                stage = FitStage.Interior;
                Status = $"Aim INSIDE the empty opening; press {Keybinds.Label(ShortcutAction.PickCorner)}.\nUse the same frame face. Closed/obstructed openings need manual fitting.";
                return;
            }
            if (pointCount == 0)
            {
                if (upright && Mathf.Abs(normal.y) > 0.25f)
                {
                    Report("Pick the FRONT face of the frame, not its floor/ledge. Or turn Upright / wall-aligned off.");
                    return;
                }
                firstNormal = normal;
            }
            points[pointCount++] = hit;
            if (pointCount == 3)
            {
                Apply();
            }
            else
            {
                Prompt();
            }
        }

        private void DetectOpening()
        {
            Camera camera = MainCamera.instance;
            if (camera == null)
            {
                Report("No editor camera available. Auto-fit has not changed the slot.");
                return;
            }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            float denominator = Vector3.Dot(ray.direction, firstNormal);
            float distance = Mathf.Abs(denominator) < 0.1f ? -1f :
                Vector3.Dot(points[0] - ray.origin, firstNormal) / denominator;
            if (distance < 0f || distance > 2048f)
            {
                Report("Face the opening directly and pick inside it again.");
                return;
            }
            try
            {
                preview = OpeningDetector.Detect(ToNumerics(ray.GetPoint(distance)),
                    ToNumerics(probeRight), ToNumerics(probeUp), ProbeSolid, inset, depthOffset);
            }
            catch (ArgumentException e)
            {
                Report(e.Message + "\nPick inside again, or choose manual Fit + Center.");
                return;
            }
            catch (InvalidOperationException e)
            {
                Report(e.Message);
                return;
            }
            stage = FitStage.Preview;
            Status = $"Preview: {preview.Width:0.###} x {preview.Height:0.###} m. Check the cyan outline.\n{Keybinds.Label(ShortcutAction.PickCorner)} / Confirm applies; {Keybinds.Label(ShortcutAction.CancelFit)} cancels. Vanilla item size is unchanged.";
        }

        private bool ProbeSolid(NVector3 point)
        {
            // Probe only a shallow slab at the picked face, not the far wall
            // visible through the opening.
            Vector3 origin = ToUnity(point) + firstNormal * 0.05f;
            int count = Physics.RaycastNonAlloc(origin, -firstNormal, probeHits, 0.2f,
                RayMasks.EDITOR_WORLD, QueryTriggerInteraction.Ignore);
            if (count == probeHits.Length)
                throw new InvalidOperationException("Too many overlapping colliders for reliable auto-fit. Use manual Fit + Center.");
            for (int i = 0; i < count; i++)
            {
                Transform hit = probeHits[i].transform;
                if (hit != null && !hit.IsChildOf(target))
                    return true;
            }
            return false;
        }

        public void ConfirmPreview()
        {
            if (!HasPreview)
            {
                Report("Detect an opening before confirming its preview.");
                return;
            }
            Apply(preview);
        }

        private void Prompt()
        {
            string[] corners = { "BOTTOM LEFT", "BOTTOM RIGHT", "TOP LEFT" };
            Status = $"{pointCount + 1}/3: Aim at {corners[pointCount]}, press {Keybinds.Label(ShortcutAction.PickCorner)}.\n" +
                $"Use the same face. {Keybinds.Label(ShortcutAction.CancelFit)} cancels.";
        }

        private bool TryRaycast(out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            Camera camera = MainCamera.instance;
            if (camera == null)
            {
                return false;
            }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 2048f, RayMasks.EDITOR_WORLD,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                // Ignore our own editor selection marker as well as slot logic.
                if (hit.transform == null || hit.transform.IsChildOf(target) ||
                    hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                point = hit.point;
                normal = hit.normal;
                found = true;
            }
            return found;
        }

        private void Apply()
        {
            OpeningGeometry opening;
            try
            {
                opening = OpeningGeometry.Measure(ToNumerics(points[0]), ToNumerics(points[1]),
                    ToNumerics(points[2]), inset, depthOffset,
                    upright ? ToNumerics(firstNormal) : (NVector3?)null);
            }
            catch (ArgumentException e)
            {
                pointCount = 0;
                Report(e.Message + $"\nStart again with {Keybinds.Label(ShortcutAction.PickCorner)}: left, right, top left.");
                return;
            }

            Apply(opening);
        }

        private void Apply(OpeningGeometry opening)
        {
            if (!TryGetSlot(out Transform current, out BoxCollider currentSlot))
            {
                string error = Status;
                Cancel();
                Report(error);
                return;
            }
            if (current != target || currentSlot != slot)
            {
                Cancel();
                Report("Selection or slot changed. Fitting cancelled without changes.");
                return;
            }
            try
            {
                // Support both original Y-up and corrected Z-up prefabs. Hatch
                // height is its second in-plane axis, not its thickness.
                int heightAxis = HeightAxis(slot);
                Vector3 nativeSize = new Vector3(
                    target.InverseTransformVector(slot.transform.TransformVector(Vector3.right * slot.size.x)).magnitude,
                    target.InverseTransformVector(slot.transform.TransformVector(Vector3.up * slot.size.y)).magnitude,
                    target.InverseTransformVector(slot.transform.TransformVector(Vector3.forward * slot.size.z)).magnitude);
                Vector3 localCenter = target.InverseTransformPoint(slot.transform.TransformPoint(slot.center));
                Quaternion r = target.rotation;
                var pose = opening.Place(ToNumerics(localCenter), ToNumerics(nativeSize), heightAxis,
                    ToNumerics(target.localScale), new System.Numerics.Quaternion(r.x, r.y, r.z, r.w), resize);
                EditorObjects.pointSelection();
                target.rotation = new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
                target.localScale = ToUnity(pose.Scale);
                target.position = ToUnity(pose.Position);
            }
            catch (ArgumentException e)
            {
                Cancel();
                Report(e.Message);
                return;
            }
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            Active = false;
            preview = null;
            GLRenderer.render -= Render;
            Status = resize
                ? $"Fitted and centered: {opening.Width:0.###} x {opening.Height:0.###} m.\nCtrl+Z undoes. Flip facing if needed."
                : "Slot centered in frame; rotation and scale preserved.\nCtrl+Z undoes.";
            if (opening.IgnoredSurfaceDirection)
            {
                Status += "\nSide-face direction ignored; aligned across your bottom picks.";
            }
            UnturnedLog.info($"[{Nexus.Name}] {Status}");
        }

        private static int HeightAxis(BoxCollider collider) => collider.name == "Hatch"
            ? (collider.size.y > collider.size.z ? 1 : 2)
            : (collider.size.z > collider.size.y ? 2 : 1);

        public void FlipFacing()
        {
            Cancel();
            if (!TryGetSlot(out Transform root, out BoxCollider collider))
            {
                return;
            }
            Vector3 center = collider.transform.TransformPoint(collider.center);
            Vector3 rotationAxis = collider.name == "Hatch" ? collider.transform.forward :
                (collider.size.z > collider.size.y ? collider.transform.forward : collider.transform.up);
            if (collider.name == "Hatch" && collider.size.y < collider.size.z)
            {
                rotationAxis = collider.transform.up;
            }
            EditorObjects.pointSelection();
            root.RotateAround(center, rotationAxis, 180f);
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            Status = "Facing flipped 180 degrees; slot center unchanged. Ctrl+Z undoes.";
        }

        private void Render()
        {
            if (!Active || pointCount == 0)
            {
                return;
            }
            GLUtility.LINE_FLAT_COLOR.SetPass(0);
            GL.Begin(GL.LINES);
            try
            {
                GL.Color(Color.cyan);
                if (HasPreview)
                {
                    Vector3 center = ToUnity(preview.Center);
                    Vector3 halfWidth = ToUnity(preview.Right) * (preview.Width * 0.5f);
                    Vector3 halfHeight = ToUnity(preview.Up) * (preview.Height * 0.5f);
                    Line(center - halfWidth - halfHeight, center + halfWidth - halfHeight);
                    Line(center + halfWidth - halfHeight, center + halfWidth + halfHeight);
                    Line(center + halfWidth + halfHeight, center - halfWidth + halfHeight);
                    Line(center - halfWidth + halfHeight, center - halfWidth - halfHeight);
                    Line(center, center + ToUnity(preview.Normal) * 0.3f);
                    return;
                }
                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 p = points[i];
                    Line(p - Vector3.right * 0.06f, p + Vector3.right * 0.06f);
                    Line(p - Vector3.up * 0.06f, p + Vector3.up * 0.06f);
                    Line(p - Vector3.forward * 0.06f, p + Vector3.forward * 0.06f);
                }
                if (pointCount > 1)
                {
                    Line(points[0], points[1]);
                }
            }
            finally
            {
                GL.End();
            }
        }

        private static void Line(Vector3 a, Vector3 b) { GL.Vertex(a); GL.Vertex(b); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static NVector3 ToNumerics(Vector3 v) => new NVector3(v.x, v.y, v.z);
        private static Vector3 ToUnity(NVector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
