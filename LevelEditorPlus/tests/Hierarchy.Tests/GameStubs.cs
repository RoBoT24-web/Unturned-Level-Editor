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
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static int RoundToInt(float value) => (int)MathF.Round(value);
    }
    public enum KeyCode { LeftControl, RightControl }
    public enum TextAnchor { MiddleLeft }
}

namespace SDG.Unturned
{
    using UnityEngine;
    public enum ESleekSide { RIGHT }
    public enum ESleekOrientation { HORIZONTAL }
    public enum EObjectType { LARGE, MEDIUM, SMALL, DECAL, NPC }
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
    public class ISleekBox : ISleekLabel { public string TooltipText; }
    public class ISleekButton : ISleekLabel
    {
        public string TooltipText;
        public event Action<ISleekElement> OnClicked;
        public void Click() => OnClicked?.Invoke(this);
    }
    public class ISleekField : ISleekElement
    {
        public string PlaceholderText;
        public string Text = "";
        public event Action<ISleekField, string> OnTextChanged;
        public void SetText(string text) { Text = text; OnTextChanged?.Invoke(this, text); }
    }
    public class ISleekSlider : ISleekElement
    {
        public ESleekOrientation Orientation;
        public float Value;
        public event Action<ISleekSlider, float> OnValueChanged;
        public void Set(float value) { Value = value; OnValueChanged?.Invoke(this, value); }
    }
    public class ISleekImage : ISleekElement
    {
        public event Action OnClicked;
        public void Click() => OnClicked?.Invoke();
    }
    public class SleekWrapper : ISleekElement { }
    public class SleekItemIcon : SleekWrapper
    {
        public ItemAsset Asset;
        public int Width, Height;
        public SleekItemIcon() => AddChild(new ISleekImage());
        public void Refresh(ItemAsset asset, int width, int height) { Asset = asset; Width = width; Height = height; }
        public ISleekElement GetChildAtIndex(int index) => Children[index];
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
        public ISleekBox CreateBox() => new();
        public ISleekSlider CreateSlider() => new();
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
    public class AssetOrigin { public string name; }
    public class Asset
    {
        public string objectName, itemName;
        public string FriendlyName;
        public ushort id;
        public Guid GUID;
        public string type = "Large";
        public AssetOrigin origin;
    }
    public class ObjectAsset : Asset { public new EObjectType type; }
    public class ItemAsset : Asset { }
    public class ItemBarricadeAsset : ItemAsset { }
    public class ItemStructureAsset : ItemAsset { }
    public static class Assets
    {
        public static readonly List<ObjectAsset> Objects = new();
        public static readonly List<ItemAsset> Items = new();
        public static void find(List<ObjectAsset> results) => results.AddRange(Objects);
        public static void find(List<ItemAsset> results) => results.AddRange(Items);
    }
    public class EditorObjectSearchFilter
    {
        private readonly string search;
        private EditorObjectSearchFilter(string search) => this.search = search;
        public static EditorObjectSearchFilter parse(string search) => new(search);
        public bool ignores(Asset asset) => !asset.FriendlyName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
    public static class EditorLevelObjectsUI { public static ISleekBox selectedBox = new(); }
    public class LevelObject { public Transform transform; public Asset asset; public ushort id; public Guid GUID; }
    public class LevelBuildableObject { public Transform transform; public Asset asset; public ushort id; public Vector3 point; }
    public class LevelInfo { public string path; }
    public static class Level { public static LevelInfo info; }
    public static class UnturnedLog
    {
        public static Exception LastException;
        public static string LastInfo;
        public static void info(string text) => LastInfo = text;
        public static void exception(Exception e, string text) => LastException = e;
    }
    public static class EditorUI
    {
        public static string LastHint;
        public static void hint(EEditorMessage message, string text) => LastHint = text;
    }
    public static class EditorObjects
    {
        public static bool isBuilding = true;
        public static ObjectAsset selectedObjectAsset;
        public static ItemAsset selectedItemAsset;
        public static void addSelection(Transform transform) =>
            LevelEditorPlus.Features.SelectionTools.Selected.Add(transform);
    }
}

namespace LevelEditorPlus
{
    public static class Nexus { public const string Name = "LevelEditorPlus"; }
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

namespace LevelEditorPlus.Core
{
    public static class ModuleSettings
    {
        public class SettingsData { public int AssetPreviewSize = 32; }
        public static SettingsData Data = new();
        public static int DirtyCount;
        public static void MarkDirty() => DirtyCount++;
    }
}

namespace LevelEditorPlus.UI
{
    using SDG.Unturned;
    public class WindowManager { }
    public static class ObjectAssetPreview
    {
        public static ISleekElement Create(ObjectAsset asset, int size, System.Action onClicked)
        {
            var image = new ISleekImage();
            image.OnClicked += onClicked;
            return image;
        }
    }
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
