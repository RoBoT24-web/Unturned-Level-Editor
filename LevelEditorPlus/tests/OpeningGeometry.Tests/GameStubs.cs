#nullable disable
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal NVector3 Numeric => new(x, y, z);
        internal static Vector3 From(NVector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 forward => new(0, 0, 1);
        public float sqrMagnitude => Numeric.LengthSquared();
        public float magnitude => Numeric.Length();
        public Vector3 normalized => sqrMagnitude > 0 ? From(NVector3.Normalize(Numeric)) : default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Numeric + b.Numeric);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Numeric - b.Numeric);
        public static Vector3 operator -(Vector3 v) => From(-v.Numeric);
        public static Vector3 operator *(Vector3 v, float s) => From(v.Numeric * s);
        public static float Dot(Vector3 a, Vector3 b) => NVector3.Dot(a.Numeric, b.Numeric);
        public static Vector3 Cross(Vector3 a, Vector3 b) => From(NVector3.Cross(a.Numeric, b.Numeric));
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - n * (Dot(v, n) / n.sqrMagnitude);
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        internal NQuaternion Numeric => new(x, y, z, w);
        internal static Quaternion From(NQuaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static float Angle(Quaternion a, Quaternion b) =>
            2 * MathF.Acos(Math.Clamp(Math.Abs(NQuaternion.Dot(a.Numeric, b.Numeric)), 0, 1)) * 180 / MathF.PI;
    }
    public class Transform
    {
        public Transform parent;
        public Vector3 position;
        public Vector3 localScale = Vector3.one;
        public Quaternion rotation = new(0, 0, 0, 1);
        public BoxCollider[] Colliders = Array.Empty<BoxCollider>();
        public Vector3 lossyScale => parent == null ? localScale :
            Vector3.From(localScale.Numeric * parent.lossyScale.Numeric);
        public Vector3 up => Vector3.From(NVector3.Transform(NVector3.UnitY, rotation.Numeric));
        public Vector3 forward => Vector3.From(NVector3.Transform(NVector3.UnitZ, rotation.Numeric));
        public bool IsChildOf(Transform other) => this == other || (parent != null && parent.IsChildOf(other));
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class => Colliders.OfType<T>().ToArray();
        public Vector3 TransformVector(Vector3 v) => Vector3.From(NVector3.Transform(v.Numeric * lossyScale.Numeric, rotation.Numeric));
        public Vector3 InverseTransformVector(Vector3 v) =>
            Vector3.From(NVector3.Transform(v.Numeric, NQuaternion.Inverse(rotation.Numeric)) / lossyScale.Numeric);
        public Vector3 TransformPoint(Vector3 v) => position + TransformVector(v);
        public Vector3 InverseTransformPoint(Vector3 v) => InverseTransformVector(v - position);
        public void RotateAround(Vector3 center, Vector3 axis, float angle)
        {
            NQuaternion q = NQuaternion.CreateFromAxisAngle(axis.Numeric, angle * MathF.PI / 180);
            position = center + Vector3.From(NVector3.Transform((position - center).Numeric, q));
            rotation = Quaternion.From(q * rotation.Numeric);
        }
    }
    public class BoxCollider
    {
        public string name = "Door";
        public Transform transform;
        public Vector3 center;
        public Vector3 size;
        public bool CompareTag(string tag) => tag == "Logic";
    }
    public struct Ray
    {
        public Vector3 origin, direction;
        public Vector3 GetPoint(float distance) => origin + direction * distance;
    }
    public struct RaycastHit
    {
        public Transform transform;
        public Vector3 point, normal;
        public float distance;
    }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public static RaycastHit[] Hits = Array.Empty<RaycastHit>();
        public static Func<Vector3, Vector3, RaycastHit[]> Probe = (_, _) => Array.Empty<RaycastHit>();
        public static RaycastHit[] RaycastAll(Ray ray, float distance, int mask, QueryTriggerInteraction triggers) => Hits;
        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] buffer,
            float distance, int mask, QueryTriggerInteraction triggers)
        {
            RaycastHit[] hits = Probe(origin, direction);
            int count = Math.Min(buffer.Length, hits.Length);
            Array.Copy(hits, buffer, count);
            return count;
        }
    }
    public class Camera
    {
        public Ray Ray;
        public Ray ScreenPointToRay(Vector3 point) => Ray;
    }
    public static class Input { public static Vector3 mousePosition; }
    public static class Mathf { public static float Abs(float v) => Math.Abs(v); }
    public struct Color { public static Color cyan => default; }
    public enum TextAnchor { UpperLeft, MiddleLeft }
    public static class GL
    {
        public const int LINES = 1;
        public static readonly List<Vector3> Vertices = new();
        public static void Begin(int mode) => Vertices.Clear();
        public static void End() { }
        public static void Color(Color c) { }
        public static void Vertex(Vector3 v) => Vertices.Add(v);
    }
}
namespace SDG.Framework.Rendering
{
    public static class GLRenderer
    {
        public static event Action render;
        public static void Draw() => render?.Invoke();
    }
    public class Material { public void SetPass(int pass) { } }
    public static class GLUtility { public static readonly Material LINE_FLAT_COLOR = new(); }
}
namespace SDG.Unturned
{
    public class ObjectAsset { public Guid GUID = Guid.NewGuid(); }
    public static class LevelObjects
    {
        public static readonly ObjectAsset Asset = new();
        public static readonly Dictionary<UnityEngine.Transform, ObjectAsset> Overrides = new();
        public static void getAssetEditor(UnityEngine.Transform root, out ObjectAsset asset, out object item)
        { asset = Overrides.TryGetValue(root, out ObjectAsset replacement) ? replacement : Asset; item = null; }
    }
    public static class EditorObjects
    {
        public static bool isBuilding = true;
        public static int UndoRecords, Applied;
        public static void pointSelection() => UndoRecords++;
        public static void applySelection() => Applied++;
        public static void calculateHandleOffsets() { }
    }
    public static class EditorInteract { public static bool isFlying; }
    public class SleekWindow { public bool isEnabled = true; }
    public static class EditorUI { public static SleekWindow window = new(); }
    public enum ESleekSide { RIGHT }
    public class ISleekElement
    {
        public float PositionOffset_X, PositionOffset_Y, SizeOffset_X, SizeOffset_Y, SizeScale_X;
        public string TooltipText;
        public readonly List<ISleekElement> Children = new();
        public void AddChild(ISleekElement element) => Children.Add(element);
    }
    public class ISleekLabel : ISleekElement
    {
        public string Text;
        public UnityEngine.TextAnchor TextAlignment;
    }
    public class ISleekFloat32Field : ISleekElement { public float Value; }
    public class ISleekToggle : ISleekElement
    {
        public bool Value;
        public Action<ISleekToggle, bool> OnValueChanged;
        public void AddLabel(string text, ESleekSide side) { }
        public void Change(bool value) { Value = value; OnValueChanged?.Invoke(this, value); }
    }
    public class ISleekButton : ISleekElement
    {
        public string Text;
        public Action<ISleekElement> OnClicked;
        public void Click() => OnClicked?.Invoke(this);
    }
    public class Glazier
    {
        private static readonly Glazier instance = new();
        public bool ShouldGameProcessInput = true;
        public static Glazier Get() => instance;
        public ISleekToggle CreateToggle() => new();
        public ISleekLabel CreateLabel() => new();
        public ISleekFloat32Field CreateFloat32Field() => new();
        public ISleekButton CreateButton() => new();
    }
    public static class MainCamera { public static UnityEngine.Camera instance = new(); }
    public static class RayMasks { public const int EDITOR_WORLD = 1; }
    public static class UnturnedLog
    {
        public static readonly List<string> Warnings = new();
        public static void warn(string message) => Warnings.Add(message);
        public static void info(string message) { }
    }
}
namespace LevelEditorPlus
{
    public static class Nexus { public const string Name = "Test"; }
}
namespace LevelEditorPlus.Core
{
    public class SettingsData
    {
        public bool UprightSlotFitting = true;
        public bool AutoFitSlots = true;
    }
    public static class ModuleSettings
    {
        public static SettingsData Data = new();
        public static bool Dirty;
        public static void MarkDirty() => Dirty = true;
    }
    public enum ShortcutAction { PickCorner, CancelFit }
    public static class Keybinds
    {
        public static ShortcutAction? PressedAction;
        public static bool Pressed(ShortcutAction action) => PressedAction == action;
        public static string Label(ShortcutAction action) => action == ShortcutAction.PickCorner ? "F10" : "F11";
    }
}
namespace LevelEditorPlus.Features
{
    public static class SelectionTools
    {
        public static int Count = 1;
        public static UnityEngine.Transform Primary;
    }
    public static class SlotVisualizer
    {
        public static bool IsCustomSlot(Guid guid) => guid == SDG.Unturned.LevelObjects.Asset.GUID;
    }
}
namespace LevelEditorPlus.UI
{
    public class WindowManager { }
    public abstract class DraggableWindow
    {
        protected readonly SDG.Unturned.ISleekElement content = new();
        public SDG.Unturned.ISleekElement Content => content;
        public bool IsVisible;
        protected DraggableWindow(WindowManager manager, string id, string title,
            float x, float y, float width, float height, bool visible) => IsVisible = visible;
        public void SetWindowVisible(bool visible) => IsVisible = visible;
        protected abstract void OnTick();
    }
}
