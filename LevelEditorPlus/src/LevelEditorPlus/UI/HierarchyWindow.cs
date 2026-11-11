using System;
using System.Collections.Generic;
using LevelEditorPlus.Features;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// Searchable list of every object placed on the map. Click to select,
    /// Ctrl+click for multi-select, double-click to fly the camera there.
    /// </summary>
    public class HierarchyWindow : DraggableWindow
    {
        private class Row
        {
            public Transform Transform;
            public string Name;
            public Vector3 Position;
            public float Distance;
            public int Order;
        }

        private readonly ISleekField searchField;
        private readonly ISleekLabel countLabel;
        private readonly SleekList<Row> list;

        private readonly List<Row> allRows = new List<Row>();
        private readonly List<Row> rows = new List<Row>();
        private string filter = string.Empty;
        private float lastAutoRefresh;
        private int lastTotalCount = -1;
        private bool nearbyOnly;
        private bool nearestFirst = true;
        private float radius = 25f;
        private float lastLocationRefresh;
        private bool hasCamera;

        private Row lastClickedRow;
        private float lastClickTime;

        public HierarchyWindow(WindowManager manager)
            : base(manager, "hierarchy", "Hierarchy", 420f, 60f, 340f, 420f, defaultVisible: false)
        {
            searchField = Glazier.Get().CreateStringField();
            searchField.PositionOffset_X = 4f;
            searchField.PositionOffset_Y = 4f;
            searchField.SizeScale_X = 1f;
            searchField.SizeOffset_X = -88f;
            searchField.SizeOffset_Y = 24f;
            searchField.PlaceholderText = "Search objects...";
            searchField.OnTextChanged += OnSearchChanged;
            content.AddChild(searchField);

            ISleekButton refreshButton = Glazier.Get().CreateButton();
            refreshButton.PositionScale_X = 1f;
            refreshButton.PositionOffset_X = -80f;
            refreshButton.PositionOffset_Y = 4f;
            refreshButton.SizeOffset_X = 76f;
            refreshButton.SizeOffset_Y = 24f;
            refreshButton.Text = "Refresh";
            refreshButton.OnClicked += (b) => Rebuild();
            content.AddChild(refreshButton);

            ISleekButton selectResultsButton = Glazier.Get().CreateButton();
            selectResultsButton.PositionOffset_X = 4f;
            selectResultsButton.PositionOffset_Y = 32f;
            selectResultsButton.SizeOffset_X = 100f;
            selectResultsButton.SizeOffset_Y = 24f;
            selectResultsButton.Text = "Select results";
            selectResultsButton.TooltipText = $"Select every listed object (max {SelectionTools.MaxBulkSelect})";
            selectResultsButton.OnClicked += OnClickedSelectResults;
            content.AddChild(selectResultsButton);

            ISleekButton selectSameButton = Glazier.Get().CreateButton();
            selectSameButton.PositionOffset_X = 108f;
            selectSameButton.PositionOffset_Y = 32f;
            selectSameButton.SizeOffset_X = 100f;
            selectSameButton.SizeOffset_Y = 24f;
            selectSameButton.Text = "Select same";
            selectSameButton.TooltipText = "Select all objects that use the same asset as the current selection";
            selectSameButton.OnClicked += (b) => SelectionTools.SelectSameAsset();
            content.AddChild(selectSameButton);

            ISleekButton deselectButton = Glazier.Get().CreateButton();
            deselectButton.PositionOffset_X = 212f;
            deselectButton.PositionOffset_Y = 32f;
            deselectButton.SizeOffset_X = 80f;
            deselectButton.SizeOffset_Y = 24f;
            deselectButton.Text = "Deselect";
            deselectButton.OnClicked += (b) => SelectionTools.ClearSelection();
            content.AddChild(deselectButton);

            ISleekToggle nearby = Glazier.Get().CreateToggle();
            nearby.PositionOffset_X = 4f;
            nearby.PositionOffset_Y = 60f;
            nearby.SizeOffset_X = 24f;
            nearby.SizeOffset_Y = 24f;
            nearby.AddLabel("Nearby only", ESleekSide.RIGHT);
            nearby.TooltipText = "Limit search results to a 3D radius around the editor camera.";
            nearby.OnValueChanged += (t, value) =>
            {
                nearbyOnly = value;
                ApplyFilter();
            };
            content.AddChild(nearby);

            ISleekFloat32Field radiusField = Glazier.Get().CreateFloat32Field();
            radiusField.PositionOffset_X = 164f;
            radiusField.PositionOffset_Y = 60f;
            radiusField.SizeOffset_X = 64f;
            radiusField.SizeOffset_Y = 24f;
            radiusField.Value = radius;
            radiusField.AddLabel("meters", ESleekSide.RIGHT);
            radiusField.TooltipText = "Nearby radius in meters. Must be positive.";
            radiusField.OnValueChanged += (field, value) =>
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                {
                    field.Value = radius;
                    EditorUI.hint(EEditorMessage.FOCUS, "Nearby radius must be a positive, finite number.");
                    return;
                }
                radius = value;
                ApplyFilter();
            };
            content.AddChild(radiusField);

            ISleekToggle nearest = Glazier.Get().CreateToggle();
            nearest.PositionOffset_X = 4f;
            nearest.PositionOffset_Y = 88f;
            nearest.SizeOffset_X = 24f;
            nearest.SizeOffset_Y = 24f;
            nearest.Value = nearestFirst;
            nearest.AddLabel("Nearest first", ESleekSide.RIGHT);
            nearest.TooltipText = "Sort by distance from the editor camera. Off: alphabetical.";
            nearest.OnValueChanged += (t, value) =>
            {
                nearestFirst = value;
                ApplyFilter();
            };
            content.AddChild(nearest);

            countLabel = Glazier.Get().CreateLabel();
            countLabel.PositionOffset_X = 4f;
            countLabel.PositionOffset_Y = 116f;
            countLabel.SizeScale_X = 1f;
            countLabel.SizeOffset_X = -8f;
            countLabel.SizeOffset_Y = 20f;
            countLabel.TextAlignment = TextAnchor.MiddleLeft;
            countLabel.Text = string.Empty;
            content.AddChild(countLabel);

            list = new SleekList<Row>();
            list.PositionOffset_X = 4f;
            list.PositionOffset_Y = 140f;
            list.SizeScale_X = 1f;
            list.SizeScale_Y = 1f;
            list.SizeOffset_X = -8f;
            list.SizeOffset_Y = -144f;
            list.itemHeight = 24;
            list.itemPadding = 1;
            list.onCreateElement = CreateRow;
            list.SetData(rows);
            content.AddChild(list);
        }

        private ISleekElement CreateRow(Row row)
        {
            ISleekButton button = Glazier.Get().CreateButton();
            button.SizeScale_X = 1f;
            button.Text = hasCamera ? $"{row.Distance:0.#} m | {row.Name}" : row.Name;
            button.TextAlignment = TextAnchor.MiddleLeft;
            button.TooltipText = $"{row.Position.x:0.#}, {row.Position.y:0.#}, {row.Position.z:0.#}\nClick: select | Ctrl+click: multi-select | Double-click: fly to";
            button.OnClicked += (b) => OnClickedRow(row);
            return button;
        }

        private void OnClickedRow(Row row)
        {
            if (row.Transform == null)
            {
                Rebuild();
                return;
            }
            bool isDoubleClick = lastClickedRow == row && Time.realtimeSinceStartup - lastClickTime < 0.4f;
            lastClickedRow = row;
            lastClickTime = Time.realtimeSinceStartup;

            if (isDoubleClick)
            {
                SelectionTools.FocusCameraOn(row.Transform.position);
                return;
            }

            if (!EditorObjects.isBuilding)
            {
                EditorUI.hint(EEditorMessage.FOCUS, "Open the Objects tool to select");
                return;
            }
            bool additive = InputEx.GetKey(KeyCode.LeftControl) || InputEx.GetKey(KeyCode.RightControl);
            SelectionTools.Select(row.Transform, additive);
        }

        private void OnClickedSelectResults(ISleekElement button)
        {
            if (!EditorObjects.isBuilding)
            {
                return;
            }
            SelectionTools.ClearSelection();
            int added = 0;
            foreach (Row row in rows)
            {
                if (added >= SelectionTools.MaxBulkSelect)
                {
                    break;
                }
                if (row.Transform != null)
                {
                    EditorObjects.addSelection(row.Transform);
                    added++;
                }
            }
        }

        private void OnSearchChanged(ISleekField field, string text)
        {
            filter = text ?? string.Empty;
            ApplyFilter();
        }

        private void Rebuild()
        {
            allRows.Clear();
            foreach (LevelObject obj in SelectionTools.EnumerateLevelObjects())
            {
                allRows.Add(new Row
                {
                    Transform = obj.transform,
                    Name = obj.asset != null ? obj.asset.objectName : $"<missing asset> ({obj.id})",
                    Position = obj.transform != null ? obj.transform.position : Vector3.zero,
                });
            }
            foreach (LevelBuildableObject b in SelectionTools.EnumerateBuildables())
            {
                allRows.Add(new Row
                {
                    Transform = b.transform,
                    Name = b.asset != null ? b.asset.itemName : $"<missing item> ({b.id})",
                    Position = b.point,
                });
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            rows.Clear();
            Camera camera = MainCamera.instance;
            hasCamera = camera != null;
            if (!hasCamera && (nearbyOnly || nearestFirst))
            {
                list.ForceRebuildElements();
                countLabel.Text = "Editor camera unavailable";
                return;
            }
            Vector3 cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            int order = 0;
            foreach (Row row in allRows)
            {
                row.Order = order++;
                if (row.Transform == null)
                {
                    continue;
                }
                row.Position = row.Transform.position;
                row.Distance = hasCamera ? Vector3.Distance(cameraPosition, row.Position) : 0f;
                if ((!nearbyOnly || row.Distance <= radius) &&
                    (string.IsNullOrEmpty(filter) ||
                    row.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    rows.Add(row);
                }
            }
            rows.Sort((a, b) =>
            {
                int comparison = nearestFirst ? a.Distance.CompareTo(b.Distance) : 0;
                if (comparison == 0)
                    comparison = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
            });
            list.ForceRebuildElements();
            countLabel.Text = $"{rows.Count} of {allRows.Count} objects" +
                (nearbyOnly ? $" within {radius:0.#} m" : string.Empty);
        }

        protected override void OnTick()
        {
            if (Time.realtimeSinceStartup - lastLocationRefresh >= 0.5f)
            {
                lastLocationRefresh = Time.realtimeSinceStartup;
                ApplyFilter();
            }
            // Auto-refresh when objects were added or removed.
            if (Time.realtimeSinceStartup - lastAutoRefresh < 2f)
            {
                return;
            }
            lastAutoRefresh = Time.realtimeSinceStartup;
            int total = SelectionTools.TotalObjectCount();
            if (total != lastTotalCount)
            {
                lastTotalCount = total;
                Rebuild();
            }
        }
    }
}
