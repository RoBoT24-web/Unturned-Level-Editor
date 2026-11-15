using System.Numerics;
using LevelEditorPlus.Features;
using LevelEditorPlus.Core;
using SDG.Unturned;
using UVector3 = UnityEngine.Vector3;

int checks = 0;
void Near(float actual, float expected, string message)
{
    checks++;
    if (Math.Abs(actual - expected) > 0.0001f)
        throw new Exception($"{message}: expected {expected}, got {actual}");
}
void VectorNear(Vector3 actual, Vector3 expected, string message)
{
    Near(Vector3.Distance(actual, expected), 0f, message);
}
void Reject(Vector3 a, Vector3 b, Vector3 c, float inset, float depth, string message)
{
    checks++;
    try { OpeningGeometry.Measure(a, b, c, inset, depth); }
    catch (ArgumentException) { return; }
    throw new Exception($"Expected rejection: {message}");
}

Vector3 a = new(10f, 3f, -5f);
Vector3 b = a + Vector3.UnitX * 2f;
Vector3 c = a + Vector3.UnitY * 4f;
OpeningGeometry opening = OpeningGeometry.Measure(a, b, c, 0f, 0f);
Near(opening.Width, 2f, "width");
Near(opening.Height, 4f, "height");
VectorNear(opening.Center, new(11f, 5f, -5f), "center, not bottom pivot");
VectorNear(opening.Right, Vector3.UnitX, "right");
VectorNear(opening.Up, Vector3.UnitY, "up");
VectorNear(opening.Normal, Vector3.UnitZ, "normal");

opening = OpeningGeometry.Measure(a, b, c, 0.005f, -0.15f);
Near(opening.Width, 1.99f, "5 mm inset on both sides");
Near(opening.Height, 3.99f, "5 mm inset on top and bottom");
VectorNear(opening.Center, new(11f, 5f, -5.15f), "wall depth offset without sideways drift");

// Rotated/tilted openings and hatches must not assume world-up or world XY.
Quaternion rotation = Quaternion.CreateFromYawPitchRoll(0.7f, 0.4f, -0.2f);
Vector3 offset = new(50, -20, 7);
Vector3 Transform(Vector3 p) => Vector3.Transform(p, rotation) + offset;
opening = OpeningGeometry.Measure(Transform(a), Transform(b), Transform(c), 0.005f, -0.15f);
Near(opening.Width, 1.99f, "rotated width");
Near(opening.Height, 3.99f, "rotated height");
VectorNear(opening.Center, Transform(new(11f, 5f, -5.15f)), "rotated centered position");
VectorNear(opening.Normal, Vector3.Transform(Vector3.UnitZ, rotation), "rotated normal");

opening = OpeningGeometry.Measure(Vector3.Zero, Vector3.UnitX * 2, Vector3.UnitZ * 3, 0, 0);
VectorNear(opening.Center, new(1, 0, 1.5f), "horizontal hatch center");
VectorNear(opening.Normal, -Vector3.UnitY, "horizontal hatch normal");

// Small picking error is orthogonalized, rather than producing a skewed box.
opening = OpeningGeometry.Measure(a, b, c + Vector3.UnitX * 0.01f, 0, 0);
VectorNear(opening.Center, new(11f, 5f, -5f), "small sideways picking error");
Near(Vector3.Dot(opening.Right, opening.Up), 0, "orthogonal axes");
Reject(a, a, c, 0, 0, "coincident bottom corners");
Reject(a, b, b, 0, 0, "collinear points");
Reject(a, b, c + Vector3.UnitX * 2, 0, 0, "top right instead of top left");
Reject(a, b, c, 1, 0, "inset removes opening");
Reject(a, b, c, -1, 0, "negative inset");
Reject(a, b, c, float.NaN, 0, "NaN inset");
Reject(a, b, c, 0, float.PositiveInfinity, "infinite depth");
Reject(new(float.NaN, 0, 0), b, c, 0, 0, "invalid corner");
Reject(a, a + Vector3.UnitX * 0.01f, c, 0, 0, "opening below minimum width");

// Verify the actual pose computation, including the offset prefab pivot,
// non-unit existing scale, and preservation of collider thickness.
foreach (int heightAxis in new[] { 1, 2 })
{
    opening = OpeningGeometry.Measure(Transform(a), Transform(b), Transform(c), 0.005f, -0.15f);
    Vector3 localHeight = heightAxis == 1 ? Vector3.UnitY : Vector3.UnitZ;
    Vector3 nativeSize = heightAxis == 1 ? new(1.1f, 2f, 0.15f) : new(1.1f, 0.15f, 2f);
    Vector3 localCenter = localHeight * 1.05f;
    var pose = opening.Place(localCenter, nativeSize, heightAxis, new(3f, 2f, 4f),
        Quaternion.Identity, true);
    Vector3 World(Vector3 p) => pose.Position + Vector3.Transform(p * pose.Scale, pose.Rotation);
    VectorNear(World(localCenter), opening.Center, $"v{heightAxis} collider centered, not root");
    Vector3 widthEdge = World(localCenter + Vector3.UnitX * 0.55f) - World(localCenter - Vector3.UnitX * 0.55f);
    Vector3 heightEdge = World(localCenter + localHeight) - World(localCenter - localHeight);
    Near(widthEdge.Length(), opening.Width, $"v{heightAxis} resulting collider width");
    Near(heightEdge.Length(), opening.Height, $"v{heightAxis} resulting collider height");
    VectorNear(Vector3.Normalize(widthEdge), opening.Right, $"v{heightAxis} width alignment");
    VectorNear(Vector3.Normalize(heightEdge), opening.Up, $"v{heightAxis} height alignment");
    Near(heightAxis == 1 ? pose.Scale.Z : pose.Scale.Y, heightAxis == 1 ? 4f : 2f, "depth scale unchanged");
}

opening = OpeningGeometry.Measure(a, b, c, 0, 0);
Vector3 oldScale = new(0.7f, 1.2f, 2.4f);
Vector3 centerOffset = new(0.1f, 0.3f, 1.05f);
var centered = opening.Place(centerOffset, new(1.1f, 0.15f, 2f), 2, oldScale, rotation, false);
VectorNear(centered.Position + Vector3.Transform(centerOffset * centered.Scale, centered.Rotation),
    opening.Center, "center-only accounts for rotated/scaled child offset");
VectorNear(centered.Scale, oldScale, "center-only preserves all scale axes");
Near(Math.Abs(Quaternion.Dot(centered.Rotation, rotation)), 1f, "center-only preserves rotation");
// Upright mode ignores bottom-edge tilt and depth differences in the picks.
opening = OpeningGeometry.Measure(new(0, 0.02f, 0), new(2, -0.02f, 0.08f),
    new(0.08f, 4, -0.05f), 0.005f, -0.1f, Vector3.UnitZ);
VectorNear(opening.Up, Vector3.UnitY, "upright mode vertical");
VectorNear(opening.Right, Vector3.UnitX, "upright mode level");
VectorNear(opening.Normal, Vector3.UnitZ, "upright mode wall aligned");
Near(opening.Width, 1.99f, "projected width excludes depth/height error");
Near(opening.Height, 3.99f, "upright height from averaged bottom");
VectorNear(opening.Center, new(1, 2, -0.1f), "upright center on first wall plane");
var lockedPose = opening.Place(new(0, 0, 1.05f), new(1.1f, 0.15f, 2),
    2, Vector3.One, Quaternion.Identity, true);
VectorNear(Vector3.Transform(Vector3.UnitZ, lockedPose.Rotation), Vector3.UnitY,
    "upright actual prefab height axis vertical");

Quaternion yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.63f);
Vector3 Yaw(Vector3 v) => Vector3.Transform(v, yaw);
opening = OpeningGeometry.Measure(Yaw(new(0, 0.02f, 0)), Yaw(new(2, -0.02f, 0.08f)),
    Yaw(new(0.08f, 4, -0.05f)), 0, 0, Yaw(Vector3.UnitZ));
VectorNear(opening.Right, Yaw(Vector3.UnitX), "non-cardinal wall yaw preserved");
VectorNear(opening.Up, Vector3.UnitY, "yaw wall stays upright");
VectorNear(opening.Center, Yaw(new(1, 2, 0)), "yaw wall centered");
opening = OpeningGeometry.Measure(a, b, c, 0, 0, -Vector3.UnitZ);
VectorNear(opening.Right, Vector3.UnitX, "reverse surface normal preserves pick order");

checks++;
try
{
    OpeningGeometry.Measure(a, b, c, 0, 0, Vector3.UnitY);
    throw new Exception("Upright fitting should reject floor surface normal");
}
catch (ArgumentException) { }
checks++;
try
{
    OpeningGeometry.Measure(a, b, c, 0, 0, new(float.NaN, 0, 0));
    throw new Exception("Upright fitting should reject invalid normal");
}
catch (ArgumentException) { }
// Regression: the first pick hits the INNER jamb, whose normal runs across
// the doorway, rather than the front wall. It must not turn the slot 90 deg.
foreach (float angle in new[] { 0f, 0.63f, 1.57f, -2.1f })
{
    Quaternion wallYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
    Vector3 Rotate(Vector3 v) => Vector3.Transform(v, wallYaw);
    opening = OpeningGeometry.Measure(Rotate(new(0, 0.02f, 0)),
        Rotate(new(2, -0.02f, 0)), Rotate(new(0, 4, 0)),
        0.005f, -0.1f, Rotate(Vector3.UnitX));
    VectorNear(opening.Right, Rotate(Vector3.UnitX), "jamb hit still spans opening");
    VectorNear(opening.Normal, Rotate(Vector3.UnitZ), "jamb hit does not turn slot sideways");
    VectorNear(opening.Up, Vector3.UnitY, "jamb fallback stays vertical");
    Near(opening.Width, 1.99f, "jamb hit retains full opening width");
    VectorNear(opening.Center, Rotate(new(1, 2, -0.1f)), "jamb fallback center/depth");
    checks++;
    if (!opening.IgnoredSurfaceDirection) throw new Exception("Jamb fallback must be reported");
    var pose = opening.Place(new(0, 0, 1.05f), new(1.1f, 0.15f, 2),
        2, Vector3.One, Quaternion.Identity, true);
    VectorNear(Vector3.Transform(Vector3.UnitX, pose.Rotation), Rotate(Vector3.UnitX),
        "actual slot width spans doorway after jamb hit");
    VectorNear(pose.Position + Vector3.Transform(new Vector3(0, 0, 1.05f) * pose.Scale,
        pose.Rotation), opening.Center, "actual slot remains centered after jamb hit");
}
opening = OpeningGeometry.Measure(a, b, c, 0, 0, Vector3.UnitZ);
checks++;
if (opening.IgnoredSurfaceDirection) throw new Exception("Good front-face normal must remain authoritative");

// Probe real rectangular boundaries rather than assuming the seed is centered.
foreach (Quaternion orientation in new[] { Quaternion.Identity, yaw, rotation,
    Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2) })
{
    Vector3 origin = new(10, 20, -30);
    Vector3 ToWorld(Vector3 p) => origin + Vector3.Transform(p, orientation);
    Vector3 ToLocal(Vector3 p) => Vector3.Transform(p - origin, Quaternion.Inverse(orientation));
    bool Frame(Vector3 p)
    {
        Vector3 local = ToLocal(p);
        return local.X <= 0 || local.X >= 2.137f || local.Y <= 0 || local.Y >= 3.053f;
    }
    foreach (Vector3 seed in new[] { new Vector3(0.3f, 0.4f, 0), new Vector3(1.8f, 2.7f, 0) })
    {
        opening = OpeningDetector.Detect(ToWorld(seed),
            Vector3.Transform(Vector3.UnitX, orientation), Vector3.Transform(Vector3.UnitY, orientation),
            Frame, 0.005f, -0.1f);
        checks++;
        if (Math.Abs(opening.Width - 2.127f) > 0.002f ||
            Math.Abs(opening.Height - 3.043f) > 0.002f ||
            Vector3.Distance(opening.Center, ToWorld(new(1.0685f, 1.5265f, -0.1f))) > 0.002f)
            throw new Exception("Auto-fit must measure off-center, rotated and horizontal openings within 2 mm.");
        VectorNear(opening.Normal, Vector3.Transform(Vector3.UnitZ, orientation), "auto-fit facing");
    }
}

void RejectDetection(Func<Vector3, bool> frame, string message, float inset = 0)
{
    checks++;
    try { OpeningDetector.Detect(new(1, 1, 0), Vector3.UnitX, Vector3.UnitY, frame, inset, 0); }
    catch (ArgumentException) { return; }
    throw new Exception($"Expected detection rejection: {message}");
}
bool Rectangle(Vector3 p) => p.X <= 0 || p.X >= 2 || p.Y <= 0 || p.Y >= 3;
RejectDetection(_ => true, "closed opening / pick on frame");
RejectDetection(_ => false, "no enclosing frame");
RejectDetection(p => p.X <= 0 || p.X >= 2 || p.Y >= 3, "missing bottom edge");
RejectDetection(p => Rectangle(p) && !(p.X < 0 && p.Y > 1.4f && p.Y < 1.8f),
    "broken jamb away from seed scan");
RejectDetection(p => Rectangle(p) || (p.X > 1.45f && p.X < 1.85f && p.Y > 1.7f && p.Y < 2.1f),
    "obstruction away from seed scans");
RejectDetection(p => p.X <= 0 || p.X >= 2 || p.Y <= 0 ||
    p.Y >= 3 - 0.5f * p.X, "sloped / non-rectangular top edge");
RejectDetection(Rectangle, "inset removes opening", 1);
checks++;
try
{
    OpeningDetector.Detect(new(1, 1, 0), Vector3.UnitX, Vector3.UnitX, Rectangle, 0, 0);
    throw new Exception("Auto-fit must reject parallel probe axes.");
}
catch (ArgumentException) { }

UnityEngine.Transform target = new()
{
    position = new(30, 40, 50),
    localScale = new(1.2f, 1.5f, 2.4f)
};
UnityEngine.BoxCollider collider = new()
{
    transform = target, center = new(0, 1, 0), size = new(1.1f, 2, 0.15f)
};
target.Colliders = new[] { collider };
SelectionTools.Primary = target;
UnityEngine.Transform frame = new();
UnityEngine.RaycastHit solidHit = new() { transform = frame, normal = UVector3.forward };
UnityEngine.RaycastHit[] SolidProbe(UVector3 p, UVector3 direction) =>
    Rectangle(new(p.x, p.y, 0)) ? new[] { solidHit } : Array.Empty<UnityEngine.RaycastHit>();

void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
void Pick(OpeningFitter fitter, UVector3 point, LevelEditorPlus.UI.OpeningFitWindow window = null,
    bool cursorOverTools = false)
{
    UnityEngine.Physics.Hits = new[]
    {
        new UnityEngine.RaycastHit { transform = frame, point = point, normal = UVector3.forward, distance = 5 }
    };
    MainCamera.instance.Ray = new() { origin = point + UVector3.forward * 5, direction = -UVector3.forward };
    Keybinds.PressedAction = ShortcutAction.PickCorner;
    if (window == null)
        fitter.Update(cursorOverTools);
    else
        window.UpdateFitting(cursorOverTools);
    Keybinds.PressedAction = null;
}
void AutoPreview(OpeningFitter fitter)
{
    UnityEngine.Physics.Probe = SolidProbe;
    fitter.BeginAuto(0.005f, -0.1f);
    Pick(fitter, new(0, 1.5f, 0));
    Pick(fitter, new(0.7f, 1.2f, 0));
    Check(fitter.HasPreview, "Two auto-fit picks must produce a confirmation preview.");
}

using (OpeningFitter fitter = new())
{
    AutoPreview(fitter);
    VectorNear(target.position.Numeric, new(30, 40, 50), "preview does not move slot");
    VectorNear(target.localScale.Numeric, new(1.2f, 1.5f, 2.4f), "preview does not resize slot");
    Check(EditorObjects.UndoRecords == 0 && EditorObjects.Applied == 0, "Preview must not record or apply an edit.");
    SDG.Framework.Rendering.GLRenderer.Draw();
    Check(UnityEngine.GL.Vertices.Count == 10, "Preview must draw four rectangle edges and a facing line.");
    Check(Math.Abs((UnityEngine.GL.Vertices[1] - UnityEngine.GL.Vertices[0]).magnitude - 1.99f) < 0.002f,
        "Preview width must match inset-adjusted detected opening.");
    fitter.Cancel();
    fitter.ConfirmPreview();
    Check(!fitter.Active && !fitter.HasPreview && EditorObjects.Applied == 0,
        "Cancellation and confirmation without a preview must leave the slot untouched.");
    SDG.Framework.Rendering.GLRenderer.Draw();
    Check(UnityEngine.GL.Vertices.Count == 10, "Cancelled fitter must unsubscribe from rendering.");

    AutoPreview(fitter);
    fitter.ConfirmPreview();
    Check(!fitter.Active && !fitter.HasPreview && EditorObjects.UndoRecords == 1 && EditorObjects.Applied == 1,
        "Confirmation must apply exactly one undoable transform.");
    Check(Vector3.Distance(collider.transform.TransformPoint(collider.center).Numeric, new(1, 1.5f, -0.1f)) < 0.002f,
        "Confirmed collider center must match the opening including depth, not the prefab pivot.");
    Check(Math.Abs(target.localScale.x * collider.size.x - 1.99f) < 0.002f &&
        Math.Abs(target.localScale.y * collider.size.y - 2.99f) < 0.002f,
        "Confirmed collider dimensions must match preview dimensions.");
    Near(target.localScale.z, 2.4f, "auto-fit preserves existing thickness scale");

    AutoPreview(fitter);
    UnityEngine.Transform replacementSlot = new();
    replacementSlot.Colliders = new[]
    {
        new UnityEngine.BoxCollider { transform = replacementSlot, size = new(1.1f, 2, 0.15f) }
    };
    SelectionTools.Primary = replacementSlot;
    fitter.ConfirmPreview();
    Check(!fitter.Active && EditorObjects.Applied == 1 && fitter.Status.Contains("Selection or slot changed"),
        "Changed selection must invalidate confirmation and report the reason.");
    SelectionTools.Primary = target;

    UnityEngine.Physics.Probe = (_, _) => Array.Empty<UnityEngine.RaycastHit>();
    fitter.BeginAuto(0, 0);
    Pick(fitter, new(0, 1.5f, 0));
    Pick(fitter, new(1, 1, 0));
    Check(fitter.Active && !fitter.HasPreview && EditorObjects.Applied == 1 &&
        fitter.Status.Contains("No enclosing frame"), "Detection failure must be explicit and leave the slot unchanged.");

    UnityEngine.Physics.Probe = (_, _) => Enumerable.Repeat(solidHit, 64).ToArray();
    Pick(fitter, new(1, 1, 0));
    Check(!fitter.HasPreview && fitter.Status.Contains("Too many overlapping colliders") && EditorObjects.Applied == 1,
        "Full raycast buffer must reject detection instead of silently trusting truncated results.");

    // The existing three-corner workflow remains available after auto-fit failure.
    fitter.Begin(true, 0, 0);
    Pick(fitter, new(0, 0, 0));
    Pick(fitter, new(2, 0, 0));
    Pick(fitter, new(0, 3, 0));
    Check(!fitter.Active && EditorObjects.Applied == 2 && EditorObjects.UndoRecords == 2,
        "Manual fitting must still apply after three corner picks.");
    VectorNear(collider.transform.TransformPoint(collider.center).Numeric, new(1, 1.5f, 0), "manual fit center preserved");

    AutoPreview(fitter);
    Pick(fitter, new(1, 1, 0));
    Check(!fitter.Active && EditorObjects.Applied == 3 && EditorObjects.UndoRecords == 3,
        "Pick shortcut must confirm a preview with exactly one undoable edit.");
    AutoPreview(fitter);
    Keybinds.PressedAction = ShortcutAction.CancelFit;
    fitter.Update(false);
    Keybinds.PressedAction = null;
    Check(!fitter.Active && !fitter.HasPreview && EditorObjects.Applied == 3,
        "Cancel shortcut must discard a preview without applying it.");
}
using (OpeningFitter fitter = new())
{
    LevelEditorPlus.UI.OpeningFitWindow window = new(new(), fitter);
    ISleekFloat32Field[] fields = window.Content.Children.OfType<ISleekFloat32Field>().ToArray();
    fields[0].Value = 0.01f;
    fields[1].Value = -0.08f;
    UnityEngine.Physics.Probe = SolidProbe;
    Pick(fitter, new(0, 1.5f, 0), window);
    Check(window.IsVisible && fitter.Active && !fitter.HasPreview,
        "First pick must start auto-fit and pick the frame even when the window was closed.");
    Pick(fitter, new(1, 1, 0), window);
    Check(window.IsVisible && fitter.HasPreview && EditorObjects.Applied == 3,
        "Always-on auto-fit must produce a preview after two picks without a start button or shortcut.");
    SDG.Framework.Rendering.GLRenderer.Draw();
    Check(Math.Abs((UnityEngine.GL.Vertices[1] - UnityEngine.GL.Vertices[0]).magnitude - 1.98f) < 0.002f &&
        Math.Abs(UnityEngine.GL.Vertices[0].z + 0.08f) < 0.002f,
        "Default auto-fit must use the current inset and depth fields.");

    window.Content.Children.OfType<ISleekButton>().Single(b => b.Text == "Manual Fit + Center").Click();
    Check(!ModuleSettings.Data.AutoFitSlots && ModuleSettings.Dirty,
        "Choosing manual fit must disable automatic starting and mark the preference for saving.");
    Pick(fitter, new(0, 0, 0));
    Pick(fitter, new(2, 0, 0));
    Check(fitter.Active && !fitter.HasPreview && EditorObjects.Applied == 3,
        "Manual button must retain three-corner fitting rather than auto-fit.");
    Pick(fitter, new(0, 3, 0));
    Check(!fitter.Active && EditorObjects.Applied == 4, "Manual button must still complete a fit.");

    window.StartFit();
    Pick(fitter, new(0, 1.5f, 0));
    Pick(fitter, new(1, 1, 0));
    Check(fitter.Active && !fitter.HasPreview && EditorObjects.Applied == 4,
        "Explicit start must respect the chosen manual mode rather than overriding it.");
    fitter.Cancel();
    window.SetWindowVisible(false);
    Pick(fitter, new(0, 1.5f, 0), window);
    Check(!fitter.Active && !window.IsVisible, "F10 must not auto-start after choosing manual mode.");
    LevelEditorPlus.UI.OpeningFitWindow reopened = new(new(), fitter);
    Pick(fitter, new(0, 1.5f, 0), reopened);
    Check(!fitter.Active && !reopened.IsVisible, "Recreating the window must retain the chosen manual preference.");

    window.Content.Children.OfType<ISleekToggle>().Last().Change(true);
    Pick(fitter, new(0, 1.5f, 0), window);
    Pick(fitter, new(1, 1, 0), window);
    Pick(fitter, new(1, 1, 0), window);
    Check(!fitter.Active && EditorObjects.Applied == 5 && ModuleSettings.Data.AutoFitSlots,
        "Re-enabling always auto-fit must allow three F10 presses to apply exactly one fit.");

    UnityEngine.Transform nextSlot = new();
    nextSlot.Colliders = new[]
    {
        new UnityEngine.BoxCollider { transform = nextSlot, size = new(1.1f, 2, 0.15f) }
    };
    SelectionTools.Primary = nextSlot;
    Pick(fitter, new(0, 1.5f, 0), window);
    Pick(fitter, new(1, 1, 0), window);
    Check(fitter.HasPreview && EditorObjects.Applied == 5,
        "Selecting another slot must allow immediate auto-fit picks without restarting the tool.");
    Keybinds.PressedAction = ShortcutAction.CancelFit;
    window.UpdateFitting(false);
    Keybinds.PressedAction = null;
    Check(!fitter.Active && ModuleSettings.Data.AutoFitSlots,
        "Cancelling a fit must leave always auto-fit enabled for the next opening.");

    window.SetWindowVisible(false);
    Pick(fitter, new(0, 1.5f, 0), window, true);
    Check(!fitter.Active && !window.IsVisible, "Auto-start must not run while the cursor is over another tool.");
    EditorInteract.isFlying = true;
    Pick(fitter, new(0, 1.5f, 0), window);
    EditorInteract.isFlying = false;
    Check(!fitter.Active, "Auto-start must not run while flying.");
    Glazier.Get().ShouldGameProcessInput = false;
    Pick(fitter, new(0, 1.5f, 0), window);
    Glazier.Get().ShouldGameProcessInput = true;
    Check(!fitter.Active, "Auto-start must respect the game input gate.");

    SelectionTools.Primary = frame;
    LevelObjects.Overrides[frame] = new();
    int warnings = UnturnedLog.Warnings.Count;
    Pick(fitter, new(0, 1.5f, 0), window);
    Check(!fitter.Active && !window.IsVisible && UnturnedLog.Warnings.Count == warnings,
        "Always auto-fit must ignore picks when the selected object is not a custom slot.");
    SelectionTools.Primary = target;
}
Console.WriteLine($"PASS: {checks} opening geometry checks.");
