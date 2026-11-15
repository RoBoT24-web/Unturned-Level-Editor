using LevelEditorPlus.Features;
using LevelEditorPlus.Core;
using LevelEditorPlus.UI;
using SDG.Unturned;
using UnityEngine;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

var objectAsset = new ObjectAsset { FriendlyName = "House", type = EObjectType.LARGE };
var itemAsset = new ItemBarricadeAsset { FriendlyName = "Door" };
Assets.Objects.Add(objectAsset);
Assets.Items.Add(itemAsset);
ModuleSettings.Data.AssetPreviewSize = 60;
var browser = new AssetBrowserWindow(new WindowManager());
var browserContent = browser.Content.Children;
var slider = browserContent.OfType<ISleekSlider>().Single();
var browserList = browserContent.OfType<SleekList<Asset>>().Single();
Check(ModuleSettings.Data.AssetPreviewSize == 64 && browserList.itemHeight == 68,
    "Saved size is normalized and determines initial row height");
var objectPreview = browserList.Children.Single().Children.OfType<ISleekImage>().Single();
objectPreview.Click();
Check(EditorObjects.selectedObjectAsset == objectAsset && EditorLevelObjectsUI.selectedBox.Text == "House",
    "Clicking an object preview arms the object");
browserContent.OfType<ISleekToggle>().Single(t => t.Label == "B").Set(true);
var itemRow = browserList.Children.Single(r => r.Children.OfType<SleekItemIcon>().Any());
var itemIcon = itemRow.Children.OfType<SleekItemIcon>().Single();
Check(itemIcon.Width == 64 && itemIcon.Height == 64, "Item icon uses the selected size");
((ISleekImage)itemIcon.GetChildAtIndex(0)).Click();
Check(EditorObjects.selectedItemAsset == itemAsset && EditorObjects.selectedObjectAsset == null,
    "Clicking an item preview arms the item");
slider.Set(1f);
Check(ModuleSettings.Data.AssetPreviewSize == 128 && browserList.itemHeight == 68,
    "Preview size is saved immediately and refresh is debounced");
Time.realtimeSinceStartup = 0.2f;
browser.Tick();
Check(browserList.itemHeight == 132 &&
    browserList.Children.SelectMany(r => r.Children).OfType<SleekItemIcon>().Single().Width == 128,
    "Preview refresh rebuilds rows at the new size");
slider.Set(0f);
Time.realtimeSinceStartup = 0.4f;
browser.Tick();
Check(ModuleSettings.Data.AssetPreviewSize == 16 && browserList.itemHeight == 20,
    "Slider supports 16 px previews");
slider.Set(1f / 3f);
Time.realtimeSinceStartup = 0.6f;
browser.Tick();
Check(ModuleSettings.Data.AssetPreviewSize == 32 && browserList.itemHeight == 36,
    "Slider supports 32 px previews");
browserContent.OfType<ISleekField>().Single().SetText("house");
Time.realtimeSinceStartup = 1f;
browser.Tick();
Check(browserList.Children.Count == 1 && browserList.Children[0].Children.OfType<ISleekButton>()
    .Single().Text == "House", "Searching still filters preview rows");

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

SelectionTools.Objects.Clear();
SelectionTools.Buildables.Clear();
var assetGuid = Guid.NewGuid();
var otherGuid = Guid.NewGuid();
for (int i = 0; i < 501; i++)
{
    SelectionTools.Objects.Add(new LevelObject {
        id = 12, GUID = assetGuid, transform = new Transform(),
        asset = new Asset {
            objectName = "House, \"Blue\"", origin = new AssetOrigin { name = "Map\nAssets" }
        }
    });
}
SelectionTools.Objects.Add(new LevelObject {
    id = 12, GUID = otherGuid, asset = new Asset { objectName = "House, \"Blue\"" }
});
SelectionTools.Objects.Add(new LevelObject { id = 99, GUID = Guid.NewGuid() });
SelectionTools.Buildables.Add(new LevelBuildableObject {
    id = 12, asset = new ItemBarricadeAsset { itemName = "House, \"Blue\"", GUID = assetGuid }
});
SelectionTools.Buildables.Add(new LevelBuildableObject { id = 99 });
var entries = HierarchyExport.Collect();
Check(entries.Count == 505 && entries.Count(e => e.MissingAsset) == 2,
    "Export includes more than 500 placements, missing assets, and entries without transforms");
using (var detailed = new StringWriter())
{
    int missing = HierarchyExport.WriteDetailed(detailed, entries);
    string csv = detailed.ToString();
    Check(missing == 2 && csv.StartsWith("Name,Asset ID,Asset GUID,Category,Asset Source"),
        "Detailed has exact requested header and separate overall missing count");
    Check(csv.Split("\"House, \"\"Blue\"\"\"").Length - 1 == 503,
        "Detailed preserves repeated placements and correctly escapes commas/quotes");
    Check(csv.Contains("\"Map\nAssets\"") && csv.Contains(assetGuid.ToString("D")),
        "Detailed quotes multiline sources and includes GUIDs");
}
using (var summary = new StringWriter())
{
    HierarchyExport.WriteSummary(summary, entries);
    string[] lines = summary.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    Check(lines.Length == 6 && lines[0] == "Name,Asset ID,Category,Quantity",
        "Summary groups by asset identity, not shared name or numeric ID");
    Check(lines.Any(l => l.EndsWith(",\"Large\",501")) &&
        lines.Any(l => l.EndsWith(",\"Barricade\",1")),
        "Summary quantities distinguish object GUIDs and object/item families");
}
using (var empty = new StringWriter())
{
    Check(HierarchyExport.WriteDetailed(empty, new()) == 0 &&
        empty.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length == 1,
        "Empty map exports a header and zero missing placements");
}
using (var formula = new StringWriter())
{
    HierarchyExport.WriteDetailed(formula, new() {
        new HierarchyExport.Entry { Name = "=1+1", Category = "Object", Source = "@test" }
    });
    Check(formula.ToString().Contains("\"'=1+1\"") && formula.ToString().Contains("\"'@test\""),
        "Spreadsheet formula-like text is exported as text");
}
string temporaryMap = Path.Combine(Path.GetTempPath(), "HierarchyExportTests-" + Guid.NewGuid().ToString("N"));
try
{
    Level.info = new LevelInfo { path = temporaryMap };
    search.SetText("no matches");
    nearby.Set(true);
    MainCamera.instance = null;
    content.OfType<ISleekButton>().Single(b => b.Text == "Export detailed").Click();
    string directory = Path.Combine(temporaryMap, "HierarchyExports");
    string detailedPath = Directory.GetFiles(directory, "*.csv").Single();
    Check(File.ReadAllText(detailedPath).Split("\"House, \"\"Blue\"\"\"").Length - 1 == 503,
        "Detailed button exports entire map regardless of filters or camera");
    Check(File.ReadAllText(Directory.GetFiles(directory, "*.status.txt").Single()) ==
        $"Total placements: 505{Environment.NewLine}Missing asset placements: 2{Environment.NewLine}",
        "Detailed overall status is persisted separately, not attached to every row");
    Check(File.ReadAllBytes(detailedPath).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }),
        "Exports use UTF-8 BOM for spreadsheet compatibility");
    content.OfType<ISleekButton>().Single(b => b.Text == "Export summary").Click();
    Check(Directory.GetFiles(directory, "*summary*.csv").Length == 1 &&
        Directory.GetFiles(directory, "*.status.txt").Length == 1,
        "Summary button writes its own CSV without an extra status report");
    Level.info = null;
    content.OfType<ISleekButton>().Single(b => b.Text == "Export summary").Click();
    Check(EditorUI.LastHint.Contains("Open a map"), "No-map export has visible feedback");
    Level.info = new LevelInfo { path = detailedPath };
    content.OfType<ISleekButton>().Single(b => b.Text == "Export detailed").Click();
    Check(UnturnedLog.LastException != null && EditorUI.LastHint.Contains("failed"),
        "Filesystem export failures are logged and reported, not treated as success");
}
finally
{
    Level.info = null;
    if (Directory.Exists(temporaryMap))
    {
        string directory = Path.Combine(temporaryMap, "HierarchyExports");
        foreach (string file in Directory.GetFiles(directory))
            File.Delete(file);
        Directory.Delete(directory);
        Directory.Delete(temporaryMap);
    }
}
Console.WriteLine($"PASS: {checks} hierarchy checks (managed game/UI stand-ins).");
