using System.Text.Json;
using LevelEditorPlus.Features;
using SDG.Framework.Rendering;
using SDG.Unturned;
using UnityEngine;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

string clipboardPath = Path.Combine(AppContext.BaseDirectory, "LevelEditorPlus.clipboard.json");
if (File.Exists(clipboardPath) || Directory.Exists(clipboardPath))
    throw new Exception("Test clipboard path already exists; refusing to overwrite it.");
try
{
    var first = new ObjectAsset { GUID = Guid.NewGuid() };
    var second = new ObjectAsset { GUID = Guid.NewGuid() };
    Assets.All.AddRange(new Asset[] { first, second });
    EditorObjects.copies.Clear();
    Time.realtimeSinceStartup = 0;
    var clipboard = new ClipboardManager();
    EditorObjects.copies.Add(new EditorCopy(new(1, 2, 3), new(0, 0, 0, 1), new(1, 1, 1), first, null));
    Time.realtimeSinceStartup = 1;
    clipboard.Update();
    string firstSnapshot = File.ReadAllText(clipboardPath);

    EditorObjects.copies[0].objectAsset = second;
    Time.realtimeSinceStartup = 2;
    clipboard.Update();
    string secondSnapshot = File.ReadAllText(clipboardPath);
    Check(firstSnapshot != secondSnapshot && secondSnapshot.Contains(second.GUID.ToString("N")),
        "Changing asset at the same position must persist");

    EditorObjects.copies[0].rotation = new Quaternion(0, 1, 0, 0);
    EditorObjects.copies[0].scale = new Vector3(2, 1, 1);
    Time.realtimeSinceStartup = 3;
    clipboard.Update();
    using (JsonDocument json = JsonDocument.Parse(File.ReadAllText(clipboardPath)))
    {
        JsonElement entry = json.RootElement[0];
        Check(entry.GetProperty("RY").GetSingle() == 1 && entry.GetProperty("SX").GetSingle() == 2,
            "Rotation and scale changes at the same position must persist");
    }

    File.Delete(clipboardPath);
    Directory.CreateDirectory(clipboardPath);
    EditorObjects.copies[0].scale = new Vector3(3, 1, 1);
    Time.realtimeSinceStartup = 4;
    clipboard.Update();
    Check(UnturnedLog.Exceptions == 1, "Save failures must be logged");
    Directory.Delete(clipboardPath);
    Time.realtimeSinceStartup = 5;
    clipboard.Update();
    Check(File.Exists(clipboardPath) && File.ReadAllText(clipboardPath).Contains("\"SX\":3"),
        "A failed save must be retried even when the clipboard is unchanged");

    EditorObjects.copies.Clear();
    _ = new ClipboardManager();
    Check(EditorObjects.copies.Count == 1 && EditorObjects.copies[0].objectAsset == second &&
        EditorObjects.copies[0].scale.x == 3 && EditorObjects.copies[0].rotation.y == 1,
        "The persisted clipboard restores asset and transform");
}
finally
{
    if (File.Exists(clipboardPath)) File.Delete(clipboardPath);
    if (Directory.Exists(clipboardPath)) Directory.Delete(clipboardPath);
}

void AddSlot(float rootX, float colliderX, bool custom = false)
{
    var root = new Transform { position = new Vector3(rootX, 0, 0) };
    var colliderTransform = new Transform { position = new Vector3(colliderX, 0, 0) };
    root.Colliders.Add(new BoxCollider { gameObject = new GameObject { name = "Door" }, transform = colliderTransform });
    SelectionTools.Objects.Add(new LevelObject
    {
        transform = root,
        asset = new ObjectAsset { GUID = custom ? Guid.Parse("87723a88b4064413bcaa587735e84b0d") : Guid.NewGuid() }
    });
}
void Refresh(float time)
{
    Time.realtimeSinceStartup = time;
    SlotVisualizer.Update();
    GL.Vertices.Clear();
    GLRenderer.Draw();
}

SelectionTools.Objects.Clear();
MainCamera.instance = new Camera();
AddSlot(0, 50, true);
AddSlot(0, 50.01f, true);
AddSlot(100, 10);
SlotVisualizer.Attach();
Refresh(10);
Check(GL.Vertices.Count == 60, "Only slot centers within 50 m, including the boundary, are outlined");
Check(GL.Vertices.Any(v => Math.Abs(v.x - 9.5f) < 0.001f),
    "Distance uses the collider center, even when its object's pivot is outside the radius");
MainCamera.instance.transform.position = new Vector3(100, 0, 0);
Refresh(12);
Check(GL.Vertices.Count == 60, "Camera movement updates nearby outlines on refresh");

SelectionTools.Objects.Clear();
MainCamera.instance.transform.position = Vector3.zero;
for (int i = 519; i >= 0; --i) AddSlot(i / 20f, i / 20f);
Refresh(14);
Check(GL.Vertices.Count == 512 * 30, "No more than 512 outlines are rendered");
Check(GL.Vertices.Where((v, index) => index % 30 == 0).All(v => v.x <= 511 / 20f - 0.5f + 0.001f) &&
    GL.Vertices.Any(v => Math.Abs(v.x + 0.5f) < 0.001f),
    "The nearest 512 are selected regardless of object enumeration order");
MainCamera.instance = null;
Refresh(16);
Check(GL.Vertices.Count == 0, "A missing camera clears outlines rather than using the world origin");
SlotVisualizer.Detach();

Console.WriteLine($"PASS: {checks} clipboard and slot-visualization checks (managed game stand-ins).");
