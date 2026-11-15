namespace Newtonsoft.Json
{
    public static class JsonConvert
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string SerializeObject(object value) => System.Text.Json.JsonSerializer.Serialize(value, Options);
        public static T DeserializeObject<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, Options);
    }
}

namespace UnityEngine
{
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 up => new(0, 1, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public void Normalize()
        {
            float length = MathF.Sqrt(sqrMagnitude);
            x /= length; y /= length; z /= length;
        }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x / b, a.y / b, a.z / b);
    }
    public struct Quaternion(float x, float y, float z, float w)
    {
        public float x = x, y = y, z = z, w = w;
        public static Quaternion Euler(float x, float y, float z) => new(x, y, z, 1);
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
    }
    public class Transform
    {
        public Vector3 position;
        public Vector3 right = new(1, 0, 0), up = new(0, 1, 0), forward = new(0, 0, 1);
        public List<BoxCollider> Colliders = new();
        public Vector3 TransformPoint(Vector3 point) => position + point;
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : class
        {
            results.Clear();
            results.AddRange(Colliders.OfType<T>());
        }
    }
    public class GameObject { public string name; }
    public class BoxCollider
    {
        public GameObject gameObject = new();
        public Transform transform;
        public Vector3 center, size = new(1, 1, 1);
        public bool Logic = true;
        public bool CompareTag(string tag) => Logic && tag == "Logic";
    }
    public class Camera { public Transform transform = new(); }
    public struct Color
    {
        public float r, g, b;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
    }
    public static class Mathf { public static float Abs(float value) => MathF.Abs(value); }
    public static class Time { public static float realtimeSinceStartup; }
    public static class GL
    {
        public const int LINES = 1;
        public static readonly List<Vector3> Vertices = new();
        public static void Begin(int mode) => Vertices.Clear();
        public static void End() { }
        public static void Color(Color color) { }
        public static void Vertex(Vector3 vertex) => Vertices.Add(vertex);
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
    using UnityEngine;
    public class Asset { public Guid GUID; }
    public class ObjectAsset : Asset { }
    public class ItemAsset : Asset { }
    public class EditorCopy(Vector3 position, Quaternion rotation, Vector3 scale, ObjectAsset objectAsset, ItemAsset itemAsset)
    {
        public Vector3 position = position, scale = scale;
        public Quaternion rotation = rotation;
        public ObjectAsset objectAsset = objectAsset;
        public ItemAsset itemAsset = itemAsset;
    }
    public class LevelObject { public Transform transform; public ObjectAsset asset; }
    public static class Assets
    {
        public static readonly List<Asset> All = new();
        public static T find<T>(Guid guid) where T : Asset => All.OfType<T>().FirstOrDefault(a => a.GUID == guid);
    }
    public static class EditorObjects
    {
        public static bool isBuilding;
        public static List<EditorCopy> copies = new();
        public static void clearSelection() { }
        public static void addSelection(Transform transform) { }
    }
    public static class EditorInteract
    {
        public static bool isFlying;
        public static (Transform transform, Vector3 point) worldHit;
    }
    public class Glazier
    {
        public bool ShouldGameProcessInput = true;
        public static Glazier Get() => new();
    }
    public static class LevelObjects
    {
        public static int step;
        public static Transform registerAddObject(Vector3 position, Quaternion rotation, Vector3 scale,
            ObjectAsset objectAsset, ItemAsset itemAsset) => new();
    }
    public static class MainCamera { public static Camera instance = new(); }
    public static class UnturnedLog
    {
        public static int Exceptions;
        public static void exception(Exception e, string message) => Exceptions++;
        public static void info(string message) { }
    }
}

namespace LevelEditorPlus
{
    public static class Nexus { public const string Name = "LevelEditorPlus"; }
}
namespace LevelEditorPlus.Core
{
    public class SettingsData { public bool ShowSlotOutlines = true; }
    public static class ModuleSettings
    {
        public static SettingsData Data = new();
        public static void MarkDirty() { }
    }
    public enum ShortcutAction { Duplicate, PasteAtCursor }
    public static class Keybinds
    {
        public static bool Pressed(ShortcutAction action) => false;
        public static string Label(ShortcutAction action) => action.ToString();
    }
}
namespace LevelEditorPlus.Features
{
    using SDG.Unturned;
    public static class SelectionTools
    {
        public static readonly List<LevelObject> Objects = new();
        public static IEnumerable<LevelObject> EnumerateLevelObjects() => Objects;
        public static int DuplicateSelection() => 0;
    }
}
