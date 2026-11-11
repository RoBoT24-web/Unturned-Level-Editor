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
        private readonly ISleekField searchField;
        private readonly ISleekBox currentBox;
        private readonly ISleekLabel countLabel;
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

        private string pendingSearch;
        private float pendingSearchTime;

        public AssetBrowserWindow(WindowManager manager)
            : base(manager, "assets", "Asset Browser", 780f, 60f, 360f, 460f, defaultVisible: false)
        {
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

            currentBox = Glazier.Get().CreateBox();
            currentBox.PositionOffset_X = 4f;
            currentBox.PositionOffset_Y = 60f;
            currentBox.SizeScale_X = 1f;
            currentBox.SizeOffset_X = -8f;
            currentBox.SizeOffset_Y = 24f;
            currentBox.Text = "Nothing armed";
            currentBox.TooltipText = "The asset that will be placed with the place key (E)";
            content.AddChild(currentBox);

            countLabel = Glazier.Get().CreateLabel();
            countLabel.PositionOffset_X = 4f;
            countLabel.PositionOffset_Y = 86f;
            countLabel.SizeScale_X = 1f;
            countLabel.SizeOffset_X = -8f;
            countLabel.SizeOffset_Y = 20f;
            countLabel.TextAlignment = TextAnchor.MiddleLeft;
            content.AddChild(countLabel);

            list = new SleekList<Asset>();
            list.PositionOffset_X = 4f;
            list.PositionOffset_Y = 108f;
            list.SizeScale_X = 1f;
            list.SizeScale_Y = 1f;
            list.SizeOffset_X = -8f;
            list.SizeOffset_Y = -112f;
            list.itemHeight = 24;
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

        private ISleekElement CreateRow(Asset asset)
        {
            ISleekButton button = Glazier.Get().CreateButton();
            button.SizeScale_X = 1f;
            button.Text = asset.FriendlyName;
            button.TextAlignment = TextAnchor.MiddleLeft;
            button.TooltipText = $"{asset.FriendlyName}\nID: {asset.id} | From: {asset.origin?.name ?? "unknown"}";
            button.OnClicked += (b) => OnClickedAsset(asset);
            return button;
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
            if (pendingSearch != null && Time.realtimeSinceStartup >= pendingSearchTime)
            {
                pendingSearch = null;
                Rebuild();
            }
        }
    }
}
