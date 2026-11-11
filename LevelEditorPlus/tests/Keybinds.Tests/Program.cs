using LevelEditorPlus.Core;
using SDG.Unturned;
using UnityEngine;

int checks = 0;
void Check(bool value, string message)
{
    checks++;
    if (!value) throw new Exception(message);
}
void Frame(params KeyCode[] down)
{
    Time.frameCount++;
    Input.Down.Clear();
    Input.Held.Clear();
    foreach (KeyCode key in down) Input.Down.Add(key);
}

Check(Keybinds.Actions.Length == 6, "All six actions exposed");
for (int i = 0; i < Keybinds.Actions.Length; i++)
    for (int j = i + 1; j < Keybinds.Actions.Length; j++)
        Check(!Keybinds.Get(Keybinds.Actions[i]).SameAs(Keybinds.Get(Keybinds.Actions[j])), "Defaults must be distinct");
Frame(KeyCode.F10);
Check(Keybinds.Pressed(ShortcutAction.PickCorner), "Default corner pick");
Frame(KeyCode.F10, KeyCode.LeftControl);
Check(!Keybinds.Pressed(ShortcutAction.PickCorner), "Ctrl+F10 must not also pick a corner");
Check(Keybinds.Pressed(ShortcutAction.FitAndCenter), "Default fit shortcut");
Frame(KeyCode.G, KeyCode.RightControl, KeyCode.RightShift);
Check(Keybinds.Pressed(ShortcutAction.FlipFacing), "Right Ctrl supported");
Check(!Keybinds.Pressed(ShortcutAction.CancelFit), "Flip must not also cancel");
Check(!Keybinds.Pressed(ShortcutAction.PasteAtCursor), "Flip must not also paste");
Frame(KeyCode.D, KeyCode.LeftControl, KeyCode.LeftShift);
Check(!Keybinds.Pressed(ShortcutAction.Duplicate), "Exact modifiers avoid overlap");
Frame(KeyCode.D, KeyCode.LeftControl);
InputEx.Typing = true;
Check(!Keybinds.Pressed(ShortcutAction.Duplicate), "Typing blocks shortcuts");
InputEx.Typing = false;
EditorUI.window.isEnabled = false;
Check(!Keybinds.Pressed(ShortcutAction.Duplicate), "Hidden HUD blocks shortcuts");
EditorUI.window.isEnabled = true;

Keybinds.BeginCapture(ShortcutAction.Duplicate);
Keybinds.UpdateCapture();
Check(Keybinds.Capturing.HasValue, "Capture does not read activation frame");
Check(Keybinds.SuppressGameInput, "Capture blocks game input");
Frame(KeyCode.F10);
Keybinds.UpdateCapture();
Check(Keybinds.Capturing.HasValue, "Conflict keeps capture open");
Check(Keybinds.Get(ShortcutAction.Duplicate).Key == KeyCode.D, "Conflict preserves old binding");
Frame(KeyCode.K, KeyCode.LeftAlt, KeyCode.LeftShift);
Keybinds.UpdateCapture();
Check(!Keybinds.Capturing.HasValue, "New chord captured");
Check(Keybinds.Label(ShortcutAction.Duplicate) == "Shift+Alt+K", "Readable modifier label");
Check(ModuleSettings.Dirty, "Settings marked for saving");
Check(!Keybinds.Pressed(ShortcutAction.Duplicate), "Capture key cannot fire action in same frame");
Frame(KeyCode.K, KeyCode.RightAlt, KeyCode.RightShift);
Check(Keybinds.Pressed(ShortcutAction.Duplicate), "New chord works immediately");

var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
string json = System.Text.Json.JsonSerializer.Serialize(ModuleSettings.Data, options);
ModuleSettings.Data = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json, options);
Check(Keybinds.Label(ShortcutAction.Duplicate) == "Shift+Alt+K", "Binding survives settings roundtrip");
Keybinds.BeginCapture(ShortcutAction.Duplicate);
Frame(KeyCode.Escape);
Keybinds.UpdateCapture();
Check(!Keybinds.Capturing.HasValue, "Escape cancels rebinding");
Check(Keybinds.Label(ShortcutAction.Duplicate) == "Shift+Alt+K", "Cancellation preserves binding");
Keybinds.BeginCapture(ShortcutAction.Duplicate);
Frame(KeyCode.Delete);
Keybinds.UpdateCapture();
Check(Keybinds.Label(ShortcutAction.Duplicate) == "Unbound", "Delete clears binding");
Frame(KeyCode.None);
Check(!Keybinds.Pressed(ShortcutAction.Duplicate), "Unbound action cannot run");
Keybinds.Reset();
Check(Keybinds.Label(ShortcutAction.Duplicate) == "Ctrl+D", "Defaults restored");
Frame();
Check(!Keybinds.SuppressGameInput, "Game input restored next frame");
Console.WriteLine($"PASS: {checks} keybinding checks (managed input stand-ins).");
