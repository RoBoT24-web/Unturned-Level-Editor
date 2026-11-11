using System.Numerics;
using LevelEditorPlus.Features;

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
Console.WriteLine($"PASS: {checks} opening geometry checks.");
