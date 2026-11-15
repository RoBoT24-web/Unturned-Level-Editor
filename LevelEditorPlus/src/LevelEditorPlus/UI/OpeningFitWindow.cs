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
        private readonly ISleekToggle autoFit;

        public OpeningFitWindow(WindowManager manager, OpeningFitter fitter)
            : base(manager, "opening-fit", "Fit / Center Slot", 420f, 90f, 440f, 350f, false)
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
            autoFit = Glazier.Get().CreateToggle();
            autoFit.PositionOffset_X = 260f;
            autoFit.PositionOffset_Y = 4f;
            autoFit.SizeOffset_X = 24f;
            autoFit.SizeOffset_Y = 24f;
            autoFit.Value = Core.ModuleSettings.Data.AutoFitSlots;
            autoFit.AddLabel("Always auto-fit", ESleekSide.RIGHT);
            autoFit.TooltipText = "On by default: pick a frame face with the pick shortcut to start auto-fit immediately. Off: start manual fitting explicitly. This preference is saved.";
            autoFit.OnValueChanged += (t, value) => SetAutoFit(value);
            content.AddChild(autoFit);
            pickHelp = Label("", 28f);

            Label("Inset (m)", 58f);
            inset = Field(106f, 58f, 0.005f);
            inset.TooltipText = "Inset on each edge. 0.005 = 5 mm. Used for auto-fit and manual Fit + Center.";
            Label("Depth (m)", 88f);
            depth = Field(106f, 88f, 0f);
            depth.TooltipText = "Offset from picked face: positive toward you, negative into wall. Use -half wall thickness for its midpoint.";

            Button("Auto-fit preview", 4f, 122f, () =>
            {
                SetAutoFit(true);
                StartFit();
            });
            Button("Center only", 160f, 122f, () =>
            {
                SetAutoFit(false);
                fitter.Begin(false, 0f, depth.Value);
            });
            Button("Manual Fit + Center", 4f, 152f, () =>
            {
                SetAutoFit(false);
                StartFit();
            });
            Button("Confirm preview", 160f, 152f, fitter.ConfirmPreview);
            Button("Flip facing", 4f, 182f, fitter.FlipFacing);
            Button("Cancel", 160f, 182f, fitter.Cancel);

            status = Glazier.Get().CreateLabel();
            status.PositionOffset_X = 4f;
            status.PositionOffset_Y = 216f;
            status.SizeScale_X = 1f;
            status.SizeOffset_X = -12f;
            status.SizeOffset_Y = 80f;
            status.TextAlignment = TextAnchor.UpperLeft;
            content.AddChild(status);
        }

        public void StartFit()
        {
            SetWindowVisible(true);
            if (Core.ModuleSettings.Data.AutoFitSlots)
                fitter.BeginAuto(inset.Value, depth.Value);
            else
                fitter.Begin(true, inset.Value, depth.Value);
        }

        private void SetAutoFit(bool enabled)
        {
            fitter.Cancel();
            Core.ModuleSettings.Data.AutoFitSlots = enabled;
            if (autoFit.Value != enabled)
                autoFit.Value = enabled;
            Core.ModuleSettings.MarkDirty();
        }

        public void UpdateFitting(bool cursorOverTools)
        {
            if (!IsVisible)
                fitter.Cancel();
            if (!fitter.Active && Core.ModuleSettings.Data.AutoFitSlots && !cursorOverTools &&
                !EditorInteract.isFlying && Glazier.Get().ShouldGameProcessInput &&
                EditorUI.window != null && EditorUI.window.isEnabled &&
                Core.Keybinds.Pressed(Core.ShortcutAction.PickCorner) && OpeningFitter.IsSlotSelected())
                StartFit();
            fitter.Update(cursorOverTools);
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
            pickHelp.Text = $"{Core.Keybinds.Label(Core.ShortcutAction.PickCorner)}: pick / confirm; {Core.Keybinds.Label(Core.ShortcutAction.CancelFit)}: cancel. Follow prompts below.";
            status.Text = fitter.Status;
        }
    }
}
