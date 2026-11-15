using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// Searchable asset browser. Clicking a result arms it for placement with
    /// the editor's normal place key (E), exactly like the vanilla list.
    /// </summary>
    public class AssetBrowserWindow : DraggableWindow
    {
        private static readonly int[] PreviewSizes = { 16, 32, 64, 128 };

        private readonly ISleekField searchField;
        private readonly ISleekBox currentBox;
        private readonly ISleekLabel countLabel;
        private readonly ISleekSlider previewSizeSlider;
        private readonly ISleekLabel previewSizeLabel;
        private readonly SleekList<Asset> list;

        private readonly ISleekToggle largeToggle;
        private readonly ISleekToggle mediumToggle;
        private readonly ISleekToggle smallToggle;
        private readonly ISleekToggle barricadesToggle;
        private readonly ISleekToggle structuresToggle;
        private readonly ISleekToggle npcsToggle;

        private readonly List<Asset> rows = new List<Asset>();
        private readonly List<ObjectAsset> tempObjects = new List<ObjectAsset>();
        private readonly List<ItemAsset> tempItems = new List<ItemAsset>();

        private int previewSize;
        private string pendingSearch;
        private float pendingSearchTime;
        private bool pendingPreviewRefresh;
        private float pendingPreviewRefreshTime;

        public AssetBrowserWindow(WindowManager manager)
            : base(manager, "assets", "Asset Browser", 780f, 60f, 360f, 460f, defaultVisible: false)
        {
            previewSize = NormalizePreviewSize(Core.ModuleSettings.Data.AssetPreviewSize);
            if (Core.ModuleSettings.Data.AssetPreviewSize != previewSize)
            {
                Core.ModuleSettings.Data.AssetPreviewSize = previewSize;
                Core.ModuleSettings.MarkDirty();
            }

            searchField = Glazier.Get().CreateStringField();
            searchField.PositionOffset_X = 4f;
            searchField.PositionOffset_Y = 4f;
            searchField.SizeScale_X = 1f;
            searchField.SizeOffset_X = -8f;
            searchField.SizeOffset_Y = 24f;
            searchField.PlaceholderText = "Search assets... (supports vanilla filters)";
            searchField.OnTextChanged += OnSearchChanged;
            content.AddChild(searchField);

            largeToggle = BuildToggle(4f, "L", "Large objects", defaultValue: true);
            mediumToggle = BuildToggle(56f, "M", "Medium objects", defaultValue: true);
            smallToggle = BuildToggle(108f, "S", "Small objects + decals", defaultValue: true);
            barricadesToggle = BuildToggle(160f, "B", "Barricades", defaultValue: false);
            structuresToggle = BuildToggle(212f, "St", "Structures", defaultValue: false);
            npcsToggle = BuildToggle(264f, "N", "NPCs", defaultValue: false);

            ISleekLabel previewSizeTitle = Glazier.Get().CreateLabel();
            previewSizeTitle.PositionOffset_X = 4f;
            previewSizeTitle.PositionOffset_Y = 60f;
            previewSizeTitle.SizeOffset_X = 64f;
            previewSizeTitle.SizeOffset_Y = 24f;
            previewSizeTitle.Text = "Preview";
            previewSizeTitle.TextAlignment = TextAnchor.MiddleLeft;
            content.AddChild(previewSizeTitle);

            previewSizeSlider = Glazier.Get().CreateSlider();
            previewSizeSlider.PositionOffset_X = 68f;
            previewSizeSlider.PositionOffset_Y = 62f;
            previewSizeSlider.SizeScale_X = 1f;
            previewSizeSlider.SizeOffset_X = -130f;
            previewSizeSlider.SizeOffset_Y = 20f;
            previewSizeSlider.Orientation = ESleekOrientation.HORIZONTAL;
            previewSizeSlider.Value = PreviewSizeToSliderValue(previewSize);
            previewSizeSlider.OnValueChanged += OnPreviewSizeChanged;
            content.AddChild(previewSizeSlider);

            previewSizeLabel = Glazier.Get().CreateLabel();
            previewSizeLabel.PositionScale_X = 1f;
            previewSizeLabel.PositionOffset_X = -58f;
            previewSizeLabel.PositionOffset_Y = 60f;
            previewSizeLabel.SizeOffset_X = 54f;
            previewSizeLabel.SizeOffset_Y = 24f;
            previewSizeLabel.TextAlignment = TextAnchor.MiddleLeft;
            previewSizeLabel.Text = $"{previewSize}px";
            content.AddChild(previewSizeLabel);

            currentBox = Glazier.Get().CreateBox();
            currentBox.PositionOffset_X = 4f;
            currentBox.PositionOffset_Y = 86f;
            currentBox.SizeScale_X = 1f;
            currentBox.SizeOffset_X = -8f;
            currentBox.SizeOffset_Y = 24f;
            currentBox.Text = "Nothing armed";
            currentBox.TooltipText = "The asset that will be placed with the place key (E)";
            content.AddChild(currentBox);

            countLabel = Glazier.Get().CreateLabel();
            countLabel.PositionOffset_X = 4f;
            countLabel.PositionOffset_Y = 112f;
            countLabel.SizeScale_X = 1f;
            countLabel.SizeOffset_X = -8f;
            countLabel.SizeOffset_Y = 20f;
            countLabel.TextAlignment = TextAnchor.MiddleLeft;
            content.AddChild(countLabel);

            list = new SleekList<Asset>();
            list.PositionOffset_X = 4f;
            list.PositionOffset_Y = 134f;
            list.SizeScale_X = 1f;
            list.SizeScale_Y = 1f;
            list.SizeOffset_X = -8f;
            list.SizeOffset_Y = -138f;
            list.itemHeight = previewSize + 4;
            list.itemPadding = 1;
            list.onCreateElement = CreateRow;
            list.SetData(rows);
            content.AddChild(list);

            Rebuild();
        }

        private ISleekToggle BuildToggle(float x, string label, string tooltip, bool defaultValue)
        {
            ISleekToggle toggle = Glazier.Get().CreateToggle();
            toggle.PositionOffset_X = x;
            toggle.PositionOffset_Y = 32f;
            toggle.SizeOffset_X = 24f;
            toggle.SizeOffset_Y = 24f;
            toggle.Value = defaultValue;
            toggle.TooltipText = tooltip;
            toggle.AddLabel(label, ESleekSide.RIGHT);
            toggle.OnValueChanged += (t, value) => Rebuild();
            content.AddChild(toggle);
            return toggle;
        }

        private static int NormalizePreviewSize(int size)
        {
            int closestSize = PreviewSizes[0];
            long smallestDifference = Math.Abs((long)size - closestSize);
            for (int index = 1; index < PreviewSizes.Length; ++index)
            {
                long difference = Math.Abs((long)size - PreviewSizes[index]);
                if (difference < smallestDifference)
                {
                    closestSize = PreviewSizes[index];
                    smallestDifference = difference;
                }
            }
            return closestSize;
        }

        private static float PreviewSizeToSliderValue(int size)
        {
            return Array.IndexOf(PreviewSizes, size) / (float)(PreviewSizes.Length - 1);
        }

        private void OnPreviewSizeChanged(ISleekSlider slider, float value)
        {
            int index = Mathf.RoundToInt(Mathf.Clamp01(value) * (PreviewSizes.Length - 1));
            int newSize = PreviewSizes[index];
            if (newSize == previewSize)
            {
                return;
            }

            previewSize = newSize;
            previewSizeSlider.Value = PreviewSizeToSliderValue(previewSize);
            previewSizeLabel.Text = $"{previewSize}px";
            Core.ModuleSettings.Data.AssetPreviewSize = previewSize;
            Core.ModuleSettings.MarkDirty();
            pendingPreviewRefresh = true;
            pendingPreviewRefreshTime = Time.realtimeSinceStartup + 0.15f;
        }

        private ISleekElement CreateRow(Asset asset)
        {
            SleekWrapper row = new SleekWrapper();
            ISleekElement preview = null;
            if (asset is ItemAsset itemAsset)
            {
                SleekItemIcon icon = new SleekItemIcon();
                icon.PositionOffset_X = 2f;
                icon.PositionOffset_Y = 2f;
                icon.SizeOffset_X = previewSize;
                icon.SizeOffset_Y = previewSize;
                icon.Refresh(itemAsset, previewSize, previewSize);
                if (icon.GetChildAtIndex(0) is ISleekImage image)
                {
                    image.OnClicked += () => OnClickedAsset(asset);
                }
                preview = icon;
            }
            else if (asset is ObjectAsset objectAsset)
            {
                preview = ObjectAssetPreview.Create(objectAsset, previewSize, () => OnClickedAsset(asset));
                if (preview != null)
                {
                    preview.PositionOffset_X = 2f;
                    preview.PositionOffset_Y = 2f;
                    preview.SizeOffset_X = previewSize;
                    preview.SizeOffset_Y = previewSize;
                }
            }

            float previewWidth = preview != null ? previewSize + 6f : 0f;
            ISleekButton button = Glazier.Get().CreateButton();
            button.PositionOffset_X = previewWidth;
            button.SizeScale_X = 1f;
            button.SizeScale_Y = 1f;
            button.SizeOffset_X = -previewWidth;
            button.Text = asset.FriendlyName;
            button.TextAlignment = TextAnchor.MiddleLeft;
            button.TooltipText = $"{asset.FriendlyName}\nID: {asset.id} | From: {asset.origin?.name ?? "unknown"}";
            button.OnClicked += (b) => OnClickedAsset(asset);
            row.AddChild(button);
            if (preview != null)
            {
                row.AddChild(preview);
            }
            return row;
        }

        private void OnClickedAsset(Asset asset)
        {
            EditorObjects.selectedObjectAsset = asset as ObjectAsset;
            EditorObjects.selectedItemAsset = asset as ItemAsset;
            currentBox.Text = asset.FriendlyName;

            // Keep the vanilla objects tab's "selected" box in sync.
            try
            {
                if (EditorLevelObjectsUI.selectedBox != null)
                {
                    EditorLevelObjectsUI.selectedBox.Text = asset.FriendlyName;
                }
            }
            catch
            {
                // Vanilla UI not available; arming still works.
            }
        }

        private void OnSearchChanged(ISleekField field, string text)
        {
            // Debounce so we don't rescan all assets on every keystroke.
            pendingSearch = text ?? string.Empty;
            pendingSearchTime = Time.realtimeSinceStartup + 0.3f;
        }

        private void Rebuild()
        {
            rows.Clear();
            string search = searchField.Text;
            EditorObjectSearchFilter searchFilter = EditorObjectSearchFilter.parse(search);

            bool large = largeToggle.Value;
            bool medium = mediumToggle.Value;
            bool small = smallToggle.Value;
            bool npcs = npcsToggle.Value;

            if (large || medium || small || npcs)
            {
                tempObjects.Clear();
                Assets.find(tempObjects);
                foreach (ObjectAsset asset in tempObjects)
                {
                    if ((large || asset.type != EObjectType.LARGE) &&
                        (medium || asset.type != EObjectType.MEDIUM) &&
                        (small || (asset.type != EObjectType.SMALL && asset.type != EObjectType.DECAL)) &&
                        (npcs || asset.type != EObjectType.NPC) &&
                        (searchFilter == null || !searchFilter.ignores(asset)))
                    {
                        rows.Add(asset);
                    }
                }
            }

            if (barricadesToggle.Value || structuresToggle.Value)
            {
                tempItems.Clear();
                Assets.find(tempItems);
                foreach (ItemAsset item in tempItems)
                {
                    if (item is ItemBarricadeAsset)
                    {
                        if (!barricadesToggle.Value)
                        {
                            continue;
                        }
                    }
                    else if (!(item is ItemStructureAsset) || !structuresToggle.Value)
                    {
                        continue;
                    }
                    if (searchFilter == null || !searchFilter.ignores(item))
                    {
                        rows.Add(item);
                    }
                }
            }

            rows.Sort((a, b) => string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase));
            list.ForceRebuildElements();
            countLabel.Text = $"{rows.Count} assets";
        }

        protected override void OnTick()
        {
            if (pendingPreviewRefresh && Time.realtimeSinceStartup >= pendingPreviewRefreshTime)
            {
                pendingPreviewRefresh = false;
                list.itemHeight = previewSize + 4;
                list.ForceRebuildElements();
            }

            if (pendingSearch != null && Time.realtimeSinceStartup >= pendingSearchTime)
            {
                pendingSearch = null;
                Rebuild();
            }
        }
    }
}
