using System.Collections.Generic;
using LevelEditorPlus.Core;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    public sealed class KeybindsWindow : DraggableWindow
    {
        private readonly Dictionary<ShortcutAction, ISleekButton> buttons = new Dictionary<ShortcutAction, ISleekButton>();
        private readonly ISleekLabel status;

        public KeybindsWindow(WindowManager manager)
            : base(manager, "keybinds", "Keybinds", 440f, 100f, 480f, 370f, false)
        {
            float y = 4;
            foreach (ShortcutAction action in Keybinds.Actions)
            {
                ShortcutAction target = action;
                ISleekLabel label = Glazier.Get().CreateLabel();
                label.PositionOffset_X = 4;
                label.PositionOffset_Y = y;
                label.SizeOffset_X = 170;
                label.SizeOffset_Y = 26;
                label.TextAlignment = TextAnchor.MiddleLeft;
                label.Text = Keybinds.Name(action);
                content.AddChild(label);
                ISleekButton button = Glazier.Get().CreateButton();
                button.PositionOffset_X = 178;
                button.PositionOffset_Y = y;
                button.SizeOffset_X = 220;
                button.SizeOffset_Y = 26;
                button.OnClicked += b => Keybinds.BeginCapture(target);
                content.AddChild(button);
                buttons.Add(action, button);
                y += 30;
            }
            ISleekButton reset = Glazier.Get().CreateButton();
            reset.PositionOffset_X = 4;
            reset.PositionOffset_Y = y;
            reset.SizeOffset_X = 180;
            reset.SizeOffset_Y = 26;
            reset.Text = "Restore defaults";
            reset.OnClicked += b => Keybinds.Reset();
            content.AddChild(reset);
            status = Glazier.Get().CreateLabel();
            status.PositionOffset_X = 4;
            status.PositionOffset_Y = y + 30;
            status.SizeScale_X = 1;
            status.SizeOffset_X = -12;
            status.SizeOffset_Y = 90;
            status.TextAlignment = TextAnchor.UpperLeft;
            content.AddChild(status);
        }

        protected override void OnTick()
        {
            foreach (var pair in buttons)
                pair.Value.Text = Keybinds.Capturing == pair.Key ? "Press keys..." : Keybinds.Label(pair.Key);
            status.Text = Keybinds.Status + "\nKeyboard only; Ctrl/Shift/Alt supported.\nAvoid keys used by Unturned's own controls.";
        }
    }
}
