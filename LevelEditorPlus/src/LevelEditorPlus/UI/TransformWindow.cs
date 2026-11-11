using System.Collections.Generic;
using LevelEditorPlus.Features;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// Unity-style transform inspector: numeric position/rotation/scale input
    /// for the current object selection, plus snapping presets and utilities.
    /// </summary>
    public class TransformWindow : DraggableWindow
    {
        private const float RowHeight = 24f;
        private const float RowSpacing = 28f;
        private const float LabelWidth = 64f;

        private readonly ISleekLabel statusLabel;
        private readonly ISleekFloat32Field posX, posY, posZ;
        private readonly ISleekFloat32Field rotX, rotY, rotZ;
        private readonly ISleekFloat32Field sclX, sclY, sclZ;
        private readonly ISleekFloat32Field snapMoveField;
        private readonly ISleekFloat32Field snapRotateField;

        private Transform cachedPrimary;
        private int cachedCount;
        private Vector3 cachedPosition;
        private Vector3 cachedEuler;
        private Vector3 cachedScale;

        private static readonly float[] MovePresets = { 0.1f, 0.25f, 0.5f, 1f, 2f, 5f };
        private static readonly float[] RotatePresets = { 1f, 5f, 15f, 30f, 45f, 90f };

        public TransformWindow(WindowManager manager)
            : base(manager, "transform", "Transform", 40f, 60f, 360f, 320f, defaultVisible: true)
        {
            float y = 4f;

            statusLabel = Glazier.Get().CreateLabel();
            statusLabel.PositionOffset_X = 4f;
            statusLabel.PositionOffset_Y = y;
            statusLabel.SizeScale_X = 1f;
            statusLabel.SizeOffset_X = -8f;
            statusLabel.SizeOffset_Y = RowHeight;
            statusLabel.TextAlignment = TextAnchor.MiddleLeft;
            statusLabel.Text = "Nothing selected";
            content.AddChild(statusLabel);
            y += RowSpacing;

            BuildVectorRow("Position", y, OnPositionSubmitted, out posX, out posY, out posZ);
            y += RowSpacing;
            BuildVectorRow("Rotation", y, OnRotationSubmitted, out rotX, out rotY, out rotZ);
            y += RowSpacing;
            BuildVectorRow("Scale", y, OnScaleSubmitted, out sclX, out sclY, out sclZ);
            y += RowSpacing + 4f;

            ISleekButton dropButton = Glazier.Get().CreateButton();
            dropButton.PositionOffset_X = 4f;
            dropButton.PositionOffset_Y = y;
            dropButton.SizeOffset_X = 130f;
            dropButton.SizeOffset_Y = RowHeight;
            dropButton.Text = "Drop to ground";
            dropButton.TooltipText = "Move each selected object down onto the surface below it";
            dropButton.OnClicked += (b) => { SelectionTools.DropToGround(); InvalidateCache(); };
            content.AddChild(dropButton);

            ISleekButton focusButton = Glazier.Get().CreateButton();
            focusButton.PositionOffset_X = 140f;
            focusButton.PositionOffset_Y = y;
            focusButton.SizeOffset_X = 130f;
            focusButton.SizeOffset_Y = RowHeight;
            focusButton.Text = "Focus camera";
            focusButton.TooltipText = "Fly the camera to the selection";
            focusButton.OnClicked += (b) => SelectionTools.FocusCameraOnSelection();
            content.AddChild(focusButton);
            y += RowSpacing + 4f;

            snapMoveField = BuildSnapRow("Snap move", y, MovePresets, OnSnapMoveChanged);
            y += RowSpacing + 26f;
            snapRotateField = BuildSnapRow("Snap rotate", y, RotatePresets, OnSnapRotateChanged);
        }

        private void BuildVectorRow(string label, float y, TypedSingle onSubmitted,
            out ISleekFloat32Field x, out ISleekFloat32Field yField, out ISleekFloat32Field z)
        {
            ISleekLabel rowLabel = Glazier.Get().CreateLabel();
            rowLabel.PositionOffset_X = 4f;
            rowLabel.PositionOffset_Y = y;
            rowLabel.SizeOffset_X = LabelWidth;
            rowLabel.SizeOffset_Y = RowHeight;
            rowLabel.TextAlignment = TextAnchor.MiddleLeft;
            rowLabel.Text = label;
            content.AddChild(rowLabel);

            // Sub-frame holding three equal columns in the remaining width.
            ISleekElement row = Glazier.Get().CreateFrame();
            row.PositionOffset_X = LabelWidth + 4f;
            row.PositionOffset_Y = y;
            row.SizeScale_X = 1f;
            row.SizeOffset_X = -(LabelWidth + 12f);
            row.SizeOffset_Y = RowHeight;
            content.AddChild(row);

            x = BuildAxisField(row, 0, onSubmitted);
            yField = BuildAxisField(row, 1, onSubmitted);
            z = BuildAxisField(row, 2, onSubmitted);
        }

        private static ISleekFloat32Field BuildAxisField(ISleekElement row, int column, TypedSingle onSubmitted)
        {
            ISleekFloat32Field field = Glazier.Get().CreateFloat32Field();
            field.PositionScale_X = column / 3f;
            field.PositionOffset_X = column > 0 ? 2f : 0f;
            field.SizeScale_X = 1f / 3f;
            field.SizeOffset_X = column > 0 ? -2f : 0f;
            field.SizeScale_Y = 1f;
            field.OnValueSubmitted += onSubmitted;
            row.AddChild(field);
            return field;
        }

        private ISleekFloat32Field BuildSnapRow(string label, float y, float[] presets, TypedSingle onChanged)
        {
            ISleekLabel rowLabel = Glazier.Get().CreateLabel();
            rowLabel.PositionOffset_X = 4f;
            rowLabel.PositionOffset_Y = y;
            rowLabel.SizeOffset_X = 90f;
            rowLabel.SizeOffset_Y = RowHeight;
            rowLabel.TextAlignment = TextAnchor.MiddleLeft;
            rowLabel.Text = label;
            content.AddChild(rowLabel);

            ISleekFloat32Field field = Glazier.Get().CreateFloat32Field();
            field.PositionOffset_X = 98f;
            field.PositionOffset_Y = y;
            field.SizeOffset_X = 70f;
            field.SizeOffset_Y = RowHeight;
            field.OnValueSubmitted += onChanged;
            content.AddChild(field);

            for (int i = 0; i < presets.Length; i++)
            {
                float preset = presets[i];
                ISleekButton presetButton = Glazier.Get().CreateButton();
                presetButton.PositionOffset_X = 4f + i * 46f;
                presetButton.PositionOffset_Y = y + RowHeight + 2f;
                presetButton.SizeOffset_X = 42f;
                presetButton.SizeOffset_Y = 20f;
                presetButton.Text = preset.ToString("0.##");
                presetButton.OnClicked += (b) =>
                {
                    field.Value = preset;
                    onChanged(field, preset);
                };
                content.AddChild(presetButton);
            }
            return field;
        }

        private static void OnSnapMoveChanged(ISleekFloat32Field field, float value)
        {
            EditorObjects.snapTransform = value;
            SyncVanillaSnapFields();
        }

        private static void OnSnapRotateChanged(ISleekFloat32Field field, float value)
        {
            EditorObjects.snapRotation = value;
            SyncVanillaSnapFields();
        }

        /// <summary>Keeps the vanilla objects-tab snap fields in sync with ours.</summary>
        private static void SyncVanillaSnapFields()
        {
            try
            {
                if (EditorLevelObjectsUI.snapTransformField != null)
                {
                    EditorLevelObjectsUI.snapTransformField.Value = EditorObjects.snapTransform;
                }
                if (EditorLevelObjectsUI.snapRotationField != null)
                {
                    EditorLevelObjectsUI.snapRotationField.Value = EditorObjects.snapRotation;
                }
            }
            catch
            {
                // Vanilla UI not built yet; values still apply via EditorObjects.
            }
        }

        private void InvalidateCache()
        {
            cachedPrimary = null;
        }

        private void RefreshFields(Transform primary, int count)
        {
            cachedPrimary = primary;
            cachedCount = count;
            if (primary == null)
            {
                statusLabel.Text = "Nothing selected";
                return;
            }
            cachedPosition = primary.position;
            cachedEuler = primary.rotation.eulerAngles;
            cachedScale = primary.localScale;

            posX.Value = cachedPosition.x;
            posY.Value = cachedPosition.y;
            posZ.Value = cachedPosition.z;
            rotX.Value = cachedEuler.x;
            rotY.Value = cachedEuler.y;
            rotZ.Value = cachedEuler.z;
            sclX.Value = cachedScale.x;
            sclY.Value = cachedScale.y;
            sclZ.Value = cachedScale.z;

            statusLabel.Text = count == 1
                ? "1 object selected"
                : $"{count} objects selected (values show the newest one)";
        }

        private void OnPositionSubmitted(ISleekFloat32Field field, float value)
        {
            Transform primary = SelectionTools.Primary;
            if (primary == null)
            {
                return;
            }
            Vector3 target = new Vector3(posX.Value, posY.Value, posZ.Value);
            Vector3 delta = target - primary.position;

            EditorObjects.pointSelection();
            foreach (EditorSelection s in SelectionTools.Selection)
            {
                if (s.transform != null)
                {
                    s.transform.position += delta;
                }
            }
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            InvalidateCache();
        }

        private void OnRotationSubmitted(ISleekFloat32Field field, float value)
        {
            if (SelectionTools.Count == 0)
            {
                return;
            }
            Quaternion rotation = Quaternion.Euler(rotX.Value, rotY.Value, rotZ.Value);

            EditorObjects.pointSelection();
            foreach (EditorSelection s in SelectionTools.Selection)
            {
                if (s.transform != null)
                {
                    s.transform.rotation = rotation;
                }
            }
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            InvalidateCache();
        }

        private void OnScaleSubmitted(ISleekFloat32Field field, float value)
        {
            if (SelectionTools.Count == 0)
            {
                return;
            }
            Vector3 scale = new Vector3(sclX.Value, sclY.Value, sclZ.Value);

            EditorObjects.pointSelection();
            foreach (EditorSelection s in SelectionTools.Selection)
            {
                if (s.transform != null)
                {
                    s.transform.localScale = scale;
                }
            }
            EditorObjects.applySelection();
            EditorObjects.calculateHandleOffsets();
            InvalidateCache();
        }

        protected override void OnTick()
        {
            // Keep snap fields in sync when something else changes them.
            if (!Mathf.Approximately(snapMoveField.Value, EditorObjects.snapTransform))
            {
                snapMoveField.Value = EditorObjects.snapTransform;
            }
            if (!Mathf.Approximately(snapRotateField.Value, EditorObjects.snapRotation))
            {
                snapRotateField.Value = EditorObjects.snapRotation;
            }

            Transform primary = SelectionTools.Primary;
            int count = SelectionTools.Count;

            if (!ReferenceEquals(primary, cachedPrimary) || count != cachedCount)
            {
                RefreshFields(primary, count);
                return;
            }
            if (primary == null)
            {
                return;
            }
            // Refresh when the object is moved externally (gizmo drag, undo, ...).
            if (primary.position != cachedPosition ||
                primary.rotation.eulerAngles != cachedEuler ||
                primary.localScale != cachedScale)
            {
                RefreshFields(primary, count);
            }
        }
    }
}
