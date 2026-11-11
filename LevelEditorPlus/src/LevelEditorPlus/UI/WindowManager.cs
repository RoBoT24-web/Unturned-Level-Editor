using System.Collections.Generic;
using LevelEditorPlus.Features;
using LevelEditorPlus.Core;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// Owns the overlay container, the toolbar, and all tool windows for one
    /// editor session. Recreated every time the editor loads.
    /// </summary>
    public class WindowManager : System.IDisposable
    {
        private readonly ISleekElement overlay;
        private ISleekElement toolbar;
        private readonly List<DraggableWindow> zOrder = new List<DraggableWindow>();
        private readonly ClipboardManager clipboard;
        private readonly OpeningFitter openingFitter = new OpeningFitter();

        public ConsoleWindow Console { get; }
        public TransformWindow Transform { get; }
        public HierarchyWindow Hierarchy { get; }
        public AssetBrowserWindow AssetBrowser { get; }
        public OpeningFitWindow OpeningFit { get; }
        public KeybindsWindow Keybindings { get; }

        public WindowManager(SleekWindow root)
        {
            // All our UI lives in one fullscreen frame appended after the
            // vanilla UI so it renders (and receives clicks) on top.
            overlay = Glazier.Get().CreateFrame();
            overlay.SizeScale_X = 1f;
            overlay.SizeScale_Y = 1f;
            root.AddChild(overlay);

            Console = new ConsoleWindow(this);
            Transform = new TransformWindow(this);
            Hierarchy = new HierarchyWindow(this);
            AssetBrowser = new AssetBrowserWindow(this);
            OpeningFit = new OpeningFitWindow(this, openingFitter);
            Keybindings = new KeybindsWindow(this);

            Register(Console);
            Register(Transform);
            Register(Hierarchy);
            Register(AssetBrowser);
            Register(OpeningFit);
            Register(Keybindings);

            BuildToolbar();

            clipboard = new ClipboardManager();
        }

        private void Register(DraggableWindow window)
        {
            overlay.AddChild(window);
            zOrder.Add(window);
        }

        /// <summary>
        /// Reorders our windows so the given one renders on top. Children later
        /// in the list render above earlier ones, so we rebuild the order with
        /// SetAsFirstSibling from front to back.
        /// </summary>
        public void BringToFront(DraggableWindow window)
        {
            if (zOrder.Count == 0 || zOrder[zOrder.Count - 1] == window)
            {
                return;
            }
            if (!zOrder.Remove(window))
            {
                return;
            }
            zOrder.Add(window);
            for (int i = zOrder.Count - 1; i >= 0; i--)
            {
                zOrder[i].SetAsFirstSibling();
            }
        }

        private void BuildToolbar()
        {
            const float buttonWidth = 80f;
            const float buttonHeight = 24f;
            string[] labels = { "Console", "Transform", "Hierarchy", "Assets", "Fit slot", "Keybinds" };
            DraggableWindow[] windows = { Console, Transform, Hierarchy, AssetBrowser, OpeningFit, Keybindings };
            int buttonCount = labels.Length + 1; // +1 for the Slots toggle

            toolbar = Glazier.Get().CreateFrame();
            toolbar.PositionScale_X = 1f;
            toolbar.PositionOffset_X = -(buttonCount * (buttonWidth + 4f)) - 10f;
            toolbar.PositionOffset_Y = 4f;
            toolbar.SizeOffset_X = buttonCount * (buttonWidth + 4f);
            toolbar.SizeOffset_Y = buttonHeight;
            overlay.AddChild(toolbar);

            for (int i = 0; i < labels.Length; i++)
            {
                DraggableWindow target = windows[i];
                ISleekButton button = Glazier.Get().CreateButton();
                button.PositionOffset_X = i * (buttonWidth + 4f);
                button.SizeOffset_X = buttonWidth;
                button.SizeOffset_Y = buttonHeight;
                button.Text = labels[i];
                button.TooltipText = $"Toggle the {labels[i]} window";
                button.OnClicked += (b) => target.ToggleVisible();
                toolbar.AddChild(button);
            }

            ISleekButton slotsButton = Glazier.Get().CreateButton();
            slotsButton.PositionOffset_X = labels.Length * (buttonWidth + 4f);
            slotsButton.SizeOffset_X = buttonWidth;
            slotsButton.SizeOffset_Y = buttonHeight;
            slotsButton.Text = SlotVisualizer.Enabled ? "Slots: On" : "Slots: Off";
            slotsButton.TooltipText = "Toggle wireframe outlines for barricade slot colliders (doors, gates, windows, hatches)";
            slotsButton.OnClicked += (b) =>
            {
                SlotVisualizer.Enabled = !SlotVisualizer.Enabled;
                slotsButton.Text = SlotVisualizer.Enabled ? "Slots: On" : "Slots: Off";
            };
            toolbar.AddChild(slotsButton);
        }

        public void Update()
        {
            if (!Keybindings.IsVisible) Keybinds.CancelCapture();
            if (Keybinds.Capturing.HasValue) openingFitter.Cancel();
            Keybinds.UpdateCapture();
            // Route a fresh click to the top-most window under the cursor so
            // overlapping windows don't both react.
            DraggableWindow clickTarget = null;
            if (Input.GetMouseButtonDown(0))
            {
                for (int i = zOrder.Count - 1; i >= 0; i--)
                {
                    DraggableWindow w = zOrder[i];
                    if (w.IsVisible && DraggableWindow.IsCursorInside(w))
                    {
                        clickTarget = w;
                        break;
                    }
                }
            }

            // Tick a snapshot since BringToFront can reorder the list mid-loop.
            DraggableWindow[] windows = zOrder.ToArray();
            foreach (DraggableWindow w in windows)
            {
                w.Tick(w == clickTarget);
            }

            clipboard.Update();
            SlotVisualizer.Update();
            bool cursorOverTools = DraggableWindow.IsCursorInside(toolbar);
            foreach (DraggableWindow w in zOrder)
            {
                cursorOverTools |= w.IsVisible && DraggableWindow.IsCursorInside(w);
            }
            if (!OpeningFit.IsVisible)
            {
                openingFitter.Cancel();
            }
            openingFitter.Update(cursorOverTools);
            if (EditorObjects.isBuilding && !EditorInteract.isFlying)
            {
                if (Keybinds.Pressed(ShortcutAction.FitAndCenter)) OpeningFit.StartFit();
                if (Keybinds.Pressed(ShortcutAction.FlipFacing)) openingFitter.FlipFacing();
            }
        }

        public void Dispose()
        {
            Keybinds.CancelCapture();
            openingFitter.Dispose();
        }
    }
}
