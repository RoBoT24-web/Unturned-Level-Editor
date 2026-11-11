using LevelEditorPlus.Features;
using LevelEditorPlus.UI;
using SDG.Unturned;
using UnityEngine;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
LevelObject Add(string name, float x, float y = 0)
{
    var obj = new LevelObject { asset = new Asset { objectName = name },
        transform = new Transform { position = new Vector3(x, y, 0) } };
    SelectionTools.Objects.Add(obj);
    return obj;
}
var far = Add("Barricade Slot Far", 40);
var near = Add("Barricade Slot Near", 5);
var edge = Add("Barricade Slot Edge", 25);
Add("Other", 2);
Add("Barricade Slot Upstairs", 0, 26);
var window = new HierarchyWindow(new WindowManager());
var content = window.Content.Children;
var search = content.OfType<ISleekField>().Single();
var nearby = content.OfType<ISleekToggle>().Single(t => t.Label == "Nearby only");
var nearest = content.OfType<ISleekToggle>().Single(t => t.Label == "Nearest first");
var radius = content.OfType<ISleekFloat32Field>().Single();
var list = content.Single(e => e.GetType().IsGenericType);
ISleekButton[] Rows() => list.Children.Cast<ISleekButton>().ToArray();
void Tick(float time) { Time.realtimeSinceStartup = time; window.Tick(); }
Tick(2);
Check(Rows().Length == 5, "Nearby must default off");
Check(Rows()[0].Text == "2 m | Other", "Default sort must be nearest first with distance");
search.SetText("barricade slot");
Check(Rows().Length == 4, "Name search is case insensitive");
nearby.Set(true);
Check(Rows().Length == 2, "25 m includes boundary, excludes distant and upstairs objects");
Check(Rows()[1].Text.Contains("Edge"), "Radius boundary must be inclusive");
radius.Set(5);
Check(Rows().Length == 1 && Rows()[0].Text.Contains("Near"), "Radius edits apply immediately");
radius.Set(float.NaN);
Check(radius.Value == 5 && EditorUI.LastHint != null, "Reject NaN with visible feedback");
radius.Set(0);
Check(radius.Value == 5, "Reject zero");
radius.Set(float.PositiveInfinity);
Check(radius.Value == 5, "Reject infinity");
radius.Set(-10);
Check(radius.Value == 5, "Reject negative radius");
radius.Set(25);
MainCamera.instance.transform.position = new Vector3(40, 0, 0);
Tick(2.5f);
Check(Rows().Length == 2 && Rows()[0].Text == "0 m | Barricade Slot Far",
    "Camera movement must update radius and sort without object count changes");
near.transform.position = new Vector3(41, 0, 0);
Tick(3);
Check(Rows().Length == 3 && Rows()[1].Text == "1 m | Barricade Slot Near",
    "Object movement must update positions and sort");
nearest.Set(false);
Check(Rows()[0].Text.Contains("Edge"), "Nearest off restores alphabetical order");
content.OfType<ISleekButton>().Single(b => b.Text == "Select results").Click();
Check(SelectionTools.Selected.Count == 3 && SelectionTools.Selected.Contains(far.transform),
    "Bulk select uses only current filtered results");
Rows().Single(b => b.Text.Contains("Near")).Click();
Time.realtimeSinceStartup = 3.1f;
Rows().Single(b => b.Text.Contains("Near")).Click();
Check(SelectionTools.Focused.x == 41, "Double click focuses current object position");
near.transform.Destroyed = true;
Tick(3.5f);
Check(Rows().Length == 2, "Destroyed objects must disappear");
nearby.Set(false);
search.SetText("");
Check(Rows().Length == 4, "Disabling filters restores remaining objects");
nearest.Set(true);
MainCamera.instance = null;
Tick(4);
Check(Rows().Length == 0 && content.OfType<ISleekLabel>()
    .Single(e => e.GetType() == typeof(ISleekLabel)).Text.Contains("unavailable"),
    "Missing camera explicitly reported");
MainCamera.instance = new Camera();
Tick(4.5f);
Check(Rows().Length == 4, "Camera recovery restores results");
SelectionTools.Buildables.Add(new LevelBuildableObject {
    asset = new Asset { itemName = "Buildable" },
    transform = new Transform { position = new Vector3(1, 0, 0) },
    point = new Vector3(100, 0, 0) });
Tick(6);
Check(Rows()[0].Text == "1 m | Buildable", "Buildables use live transforms rather than saved points");
Console.WriteLine($"PASS: {checks} hierarchy checks (managed game/UI stand-ins).");
