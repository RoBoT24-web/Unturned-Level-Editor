using LevelEditorPlus.Core;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// Base class for floating tool windows: title bar dragging, resizing via a
    /// bottom-right grip, collapse and close buttons, and layout persistence.
    /// </summary>
    public abstract class DraggableWindow : SleekWrapper
    {
        protected const float TitleHeight = 26f;
        private const float GripSize = 18f;
        private const float MinWidth = 220f;
        private const float MinHeight = 140f;

        protected readonly WindowManager manager;
        public string Id { get; }

        private readonly ISleekBox titleBar;
        private readonly ISleekButton closeButton;
        private readonly ISleekButton collapseButton;
        private readonly ISleekBox gripBox;
        protected readonly ISleekElement content;

        private readonly WindowLayout layout;
        private bool dragging;
        private Vector2 dragGrabOffset;
        private bool resizing;
        private Vector2 resizeGrabOffset;
        private bool collapsed;

        protected DraggableWindow(WindowManager manager, string id, string title,
            float defaultX, float defaultY, float defaultWidth, float defaultHeight, bool defaultVisible)
        {
            this.manager = manager;
            Id = id;
            layout = ModuleSettings.GetOrCreateLayout(id, defaultX, defaultY, defaultWidth, defaultHeight, defaultVisible);

            SizeOffset_X = Mathf.Max(MinWidth, layout.Width);
            SizeOffset_Y = Mathf.Max(MinHeight, layout.Height);
            PositionOffset_X = layout.X;
            PositionOffset_Y = layout.Y;

            titleBar = Glazier.Get().CreateBox();
            titleBar.SizeScale_X = 1f;
            titleBar.SizeOffset_Y = TitleHeight;
            titleBar.Text = title;
            titleBar.FontSize = ESleekFontSize.Medium;
            AddChild(titleBar);

            closeButton = Glazier.Get().CreateButton();
            closeButton.PositionScale_X = 1f;
            closeButton.PositionOffset_X = -TitleHeight + 2f;
            closeButton.PositionOffset_Y = 2f;
            closeButton.SizeOffset_X = TitleHeight - 4f;
            closeButton.SizeOffset_Y = TitleHeight - 4f;
            closeButton.Text = "X";
            closeButton.OnClicked += OnClickedClose;
            titleBar.AddChild(closeButton);

            collapseButton = Glazier.Get().CreateButton();
            collapseButton.PositionScale_X = 1f;
            collapseButton.PositionOffset_X = -2f * TitleHeight + 2f;
            collapseButton.PositionOffset_Y = 2f;
            collapseButton.SizeOffset_X = TitleHeight - 4f;
            collapseButton.SizeOffset_Y = TitleHeight - 4f;
            collapseButton.Text = "-";
            collapseButton.OnClicked += OnClickedCollapse;
            titleBar.AddChild(collapseButton);

            content = Glazier.Get().CreateFrame();
            content.PositionOffset_Y = TitleHeight;
            content.SizeScale_X = 1f;
            content.SizeScale_Y = 1f;
            content.SizeOffset_Y = -TitleHeight;
            AddChild(content);

            gripBox = Glazier.Get().CreateBox();
            gripBox.PositionScale_X = 1f;
            gripBox.PositionScale_Y = 1f;
            gripBox.PositionOffset_X = -GripSize;
            gripBox.PositionOffset_Y = -GripSize;
            gripBox.SizeOffset_X = GripSize;
            gripBox.SizeOffset_Y = GripSize;
            gripBox.Text = "+";
            gripBox.TooltipText = "Drag to resize";
            AddChild(gripBox);

            if (layout.Collapsed)
            {
                SetCollapsed(true);
            }
            IsVisible = layout.Visible;
        }

        private void OnClickedClose(ISleekElement button)
        {
            SetWindowVisible(false);
        }

        private void OnClickedCollapse(ISleekElement button)
        {
            SetCollapsed(!collapsed);
        }

        public void SetWindowVisible(bool visible)
        {
            IsVisible = visible;
            layout.Visible = visible;
            ModuleSettings.MarkDirty();
            if (visible)
            {
                manager.BringToFront(this);
            }
        }

        public void ToggleVisible()
        {
            SetWindowVisible(!IsVisible);
        }

        private void SetCollapsed(bool value)
        {
            collapsed = value;
            content.IsVisible = !value;
            gripBox.IsVisible = !value;
            SizeOffset_Y = value ? TitleHeight : Mathf.Max(MinHeight, layout.Height);
            collapseButton.Text = value ? "+" : "-";
            layout.Collapsed = value;
            ModuleSettings.MarkDirty();
        }

        /// <summary>Sleek units to screen pixels conversion factor.</summary>
        private float PixelsPerUnit()
        {
            Vector2 abs = GetAbsoluteSize();
            if (abs.x > 0.5f && SizeOffset_X > 0.5f)
            {
                return abs.x / SizeOffset_X;
            }
            return 1f;
        }

        public static bool IsCursorInside(ISleekElement element)
        {
            Vector2 n = element.GetNormalizedCursorPosition();
            return n.x >= 0f && n.x <= 1f && n.y >= 0f && n.y <= 1f;
        }

        /// <summary>Cursor position in pixels with top-left origin.</summary>
        private static Vector2 CursorPx()
        {
            return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        }

        private Vector2 TopLeftPx(float scale)
        {
            return new Vector2(PositionOffset_X * scale, PositionOffset_Y * scale);
        }

        /// <summary>
        /// Called every frame by the window manager. <paramref name="receivedMouseDown"/>
        /// is true when this window is the top-most window under a fresh click.
        /// </summary>
        public void Tick(bool receivedMouseDown)
        {
            if (!IsVisible)
            {
                dragging = false;
                resizing = false;
                return;
            }

            float scale = PixelsPerUnit();
            Vector2 cursor = CursorPx();

            if (dragging)
            {
                if (!Input.GetMouseButton(0))
                {
                    dragging = false;
                    SavePosition();
                }
                else
                {
                    PositionOffset_X = (cursor.x - dragGrabOffset.x) / scale;
                    PositionOffset_Y = (cursor.y - dragGrabOffset.y) / scale;
                    ClampToParent(scale);
                }
            }
            else if (resizing)
            {
                if (!Input.GetMouseButton(0))
                {
                    resizing = false;
                    layout.Width = SizeOffset_X;
                    layout.Height = SizeOffset_Y;
                    ModuleSettings.MarkDirty();
                }
                else
                {
                    Vector2 topLeft = TopLeftPx(scale);
                    SizeOffset_X = Mathf.Max(MinWidth, (cursor.x - topLeft.x + resizeGrabOffset.x) / scale);
                    SizeOffset_Y = Mathf.Max(MinHeight, (cursor.y - topLeft.y + resizeGrabOffset.y) / scale);
                }
            }
            else if (receivedMouseDown)
            {
                manager.BringToFront(this);
                bool onButtons = IsCursorInside(closeButton) || IsCursorInside(collapseButton);
                if (!collapsed && IsCursorInside(gripBox))
                {
                    resizing = true;
                    Vector2 bottomRight = TopLeftPx(scale) + new Vector2(SizeOffset_X, SizeOffset_Y) * scale;
                    resizeGrabOffset = bottomRight - cursor;
                }
                else if (!onButtons && IsCursorInside(titleBar))
                {
                    dragging = true;
                    dragGrabOffset = cursor - TopLeftPx(scale);
                }
            }

            OnTick();
        }

        private void SavePosition()
        {
            layout.X = PositionOffset_X;
            layout.Y = PositionOffset_Y;
            ModuleSettings.MarkDirty();
        }

        private void ClampToParent(float scale)
        {
            ISleekElement parent = Parent;
            if (parent == null)
            {
                return;
            }
            Vector2 parentAbs = parent.GetAbsoluteSize();
            float parentW = parentAbs.x / scale;
            float parentH = parentAbs.y / scale;
            PositionOffset_X = Mathf.Clamp(PositionOffset_X, -(SizeOffset_X - 60f), Mathf.Max(0f, parentW - 60f));
            PositionOffset_Y = Mathf.Clamp(PositionOffset_Y, 0f, Mathf.Max(0f, parentH - TitleHeight));
        }

        /// <summary>Per-frame logic for subclasses; only called while visible.</summary>
        protected abstract void OnTick();
    }
}
