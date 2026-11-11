using LevelEditorPlus.Features;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    public sealed class OpeningFitWindow : DraggableWindow
    {
        private readonly OpeningFitter fitter;
        private readonly ISleekLabel status;
        private readonly ISleekLabel pickHelp;
        private readonly ISleekFloat32Field inset;
        private readonly ISleekFloat32Field depth;

        public OpeningFitWindow(WindowManager manager, OpeningFitter fitter)
            : base(manager, "opening-fit", "Fit / Center Slot", 420f, 90f, 440f, 320f, false)
        {
            this.fitter = fitter;
            ISleekToggle alignment = Glazier.Get().CreateToggle();
            alignment.PositionOffset_X = 4f;
            alignment.PositionOffset_Y = 4f;
            alignment.SizeOffset_X = 24f;
            alignment.SizeOffset_Y = 24f;
            alignment.Value = Core.ModuleSettings.Data.UprightSlotFitting;
            alignment.AddLabel("Upright / wall-aligned", ESleekSide.RIGHT);
            alignment.TooltipText = "On: level edges and vertical sides. Uses wall direction only if it agrees with your left-to-right picks; ignores side-face normals. Off: free-angle fitting (hatches/slopes).";
            alignment.OnValueChanged += (t, value) =>
            {
                fitter.Cancel();
                Core.ModuleSettings.Data.UprightSlotFitting = value;
                Core.ModuleSettings.MarkDirty();
            };
            content.AddChild(alignment);
            pickHelp = Label("", 28f);

            Label("Inset (m)", 58f);
            inset = Field(106f, 58f, 0.005f);
            inset.TooltipText = "Inset on each edge. 0.005 = 5 mm. Only used for Fit + Center.";
            Label("Depth (m)", 88f);
            depth = Field(106f, 88f, 0f);
            depth.TooltipText = "Offset from picked face: positive toward you, negative into wall. Use -half wall thickness for its midpoint.";

            Button("Fit + Center", 4f, 122f, StartFit);
            Button("Center only", 160f, 122f, () => fitter.Begin(false, 0f, depth.Value));
            Button("Flip facing", 4f, 152f, fitter.FlipFacing);
            Button("Cancel", 160f, 152f, fitter.Cancel);

            status = Glazier.Get().CreateLabel();
            status.PositionOffset_X = 4f;
            status.PositionOffset_Y = 186f;
            status.SizeScale_X = 1f;
            status.SizeOffset_X = -12f;
            status.SizeOffset_Y = 80f;
            status.TextAlignment = TextAnchor.UpperLeft;
            content.AddChild(status);
        }

        public void StartFit()
        {
            SetWindowVisible(true);
            fitter.Begin(true, inset.Value, depth.Value);
        }

        private ISleekLabel Label(string text, float y)
        {
            ISleekLabel label = Glazier.Get().CreateLabel();
            label.PositionOffset_X = 4f;
            label.PositionOffset_Y = y;
            label.SizeScale_X = 1f;
            label.SizeOffset_X = -8f;
            label.SizeOffset_Y = 24f;
            label.Text = text;
            label.TextAlignment = TextAnchor.MiddleLeft;
            content.AddChild(label);
            return label;
        }

        private ISleekFloat32Field Field(float x, float y, float initial)
        {
            ISleekFloat32Field field = Glazier.Get().CreateFloat32Field();
            field.PositionOffset_X = x;
            field.PositionOffset_Y = y;
            field.SizeOffset_X = 90f;
            field.SizeOffset_Y = 24f;
            field.Value = initial;
            content.AddChild(field);
            return field;
        }

        private void Button(string text, float x, float y, System.Action action)
        {
            ISleekButton button = Glazier.Get().CreateButton();
            button.PositionOffset_X = x;
            button.PositionOffset_Y = y;
            button.SizeOffset_X = 150f;
            button.SizeOffset_Y = 26f;
            button.Text = text;
            button.OnClicked += b => action();
            content.AddChild(button);
        }

        protected override void OnTick()
        {
            pickHelp.Text = $"{Core.Keybinds.Label(Core.ShortcutAction.PickCorner)}: bottom left, bottom right, top left.";
            status.Text = fitter.Status;
        }
    }
}
