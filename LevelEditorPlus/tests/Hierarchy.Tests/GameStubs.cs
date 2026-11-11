namespace UnityEngine
{
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => new(0, 0, 0);
        public static float Distance(Vector3 a, Vector3 b) =>
            MathF.Sqrt(MathF.Pow(a.x - b.x, 2) + MathF.Pow(a.y - b.y, 2) + MathF.Pow(a.z - b.z, 2));
    }
    public class Transform
    {
        public Vector3 position;
        public bool Destroyed;
        public static bool operator ==(Transform a, Transform b) =>
            (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed) ||
            ReferenceEquals(a, b);
        public static bool operator !=(Transform a, Transform b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Camera { public Transform transform = new(); }
    public static class Time { public static float realtimeSinceStartup; }
    public enum KeyCode { LeftControl, RightControl }
    public enum TextAnchor { MiddleLeft }
}

namespace SDG.Unturned
{
    using UnityEngine;
    public enum ESleekSide { RIGHT }
    public enum EEditorMessage { FOCUS }
    public class ISleekElement
    {
        public float PositionOffset_X, PositionOffset_Y, PositionScale_X, SizeScale_X, SizeScale_Y,
            SizeOffset_X, SizeOffset_Y;
        public readonly List<ISleekElement> Children = new();
        public void AddChild(ISleekElement child) => Children.Add(child);
    }
    public class ISleekLabel : ISleekElement
    {
        public string Text;
        public TextAnchor TextAlignment;
    }
    public class ISleekButton : ISleekLabel
    {
        public string TooltipText;
        public event Action<ISleekElement> OnClicked;
        public void Click() => OnClicked?.Invoke(this);
    }
    public class ISleekField : ISleekElement
    {
        public string PlaceholderText;
        public event Action<ISleekField, string> OnTextChanged;
        public void SetText(string text) => OnTextChanged?.Invoke(this, text);
    }
    public class ISleekToggle : ISleekElement
    {
        public string Label, TooltipText;
        public bool Value;
        public void AddLabel(string text, ESleekSide side) => Label = text;
        public event Action<ISleekToggle, bool> OnValueChanged;
        public void Set(bool value) { Value = value; OnValueChanged?.Invoke(this, value); }
    }
    public class ISleekFloat32Field : ISleekElement
    {
        public float Value;
        public string TooltipText;
        public void AddLabel(string text, ESleekSide side) { }
        public event Action<ISleekFloat32Field, float> OnValueChanged;
        public void Set(float value) { Value = value; OnValueChanged?.Invoke(this, value); }
    }
    public class Glazier
    {
        private static readonly Glazier instance = new();
        public static Glazier Get() => instance;
        public ISleekField CreateStringField() => new();
        public ISleekButton CreateButton() => new();
        public ISleekLabel CreateLabel() => new();
        public ISleekToggle CreateToggle() => new();
        public ISleekFloat32Field CreateFloat32Field() => new();
    }
    public class SleekList<T> : ISleekElement
    {
        public int itemHeight, itemPadding;
        public Func<T, ISleekElement> onCreateElement;
        private List<T> data;
        public void SetData(List<T> value) => data = value;
        public void ForceRebuildElements()
        {
            Children.Clear();
            foreach (T row in data) Children.Add(onCreateElement(row));
        }
    }
    public static class MainCamera { public static Camera instance = new(); }
    public static class InputEx { public static bool GetKey(KeyCode key) => false; }
    public class Asset { public string objectName, itemName; }
    public class LevelObject { public Transform transform; public Asset asset; public int id; }
    public class LevelBuildableObject { public Transform transform; public Asset asset; public int id; public Vector3 point; }
    public static class EditorUI
    {
        public static string LastHint;
        public static void hint(EEditorMessage message, string text) => LastHint = text;
    }
    public static class EditorObjects
    {
        public static bool isBuilding = true;
        public static void addSelection(Transform transform) =>
            LevelEditorPlus.Features.SelectionTools.Selected.Add(transform);
    }
}

namespace LevelEditorPlus.Features
{
    using SDG.Unturned;
    using UnityEngine;
    public static class SelectionTools
    {
        public const int MaxBulkSelect = 500;
        public static readonly List<LevelObject> Objects = new();
        public static readonly List<LevelBuildableObject> Buildables = new();
        public static readonly List<Transform> Selected = new();
        public static Vector3 Focused;
        public static IEnumerable<LevelObject> EnumerateLevelObjects() => Objects;
        public static IEnumerable<LevelBuildableObject> EnumerateBuildables() => Buildables;
        public static int TotalObjectCount() => Objects.Count + Buildables.Count;
        public static void SelectSameAsset() { }
        public static void ClearSelection() => Selected.Clear();
        public static void Select(Transform transform, bool additive) => Selected.Add(transform);
        public static void FocusCameraOn(Vector3 position) => Focused = position;
    }
}

namespace LevelEditorPlus.UI
{
    using SDG.Unturned;
    public class WindowManager { }
    public abstract class DraggableWindow
    {
        protected readonly ISleekElement content = new();
        public ISleekElement Content => content;
        protected DraggableWindow(WindowManager manager, string id, string title,
            float x, float y, float width, float height, bool defaultVisible) { }
        protected abstract void OnTick();
        public void Tick() => OnTick();
    }
}
