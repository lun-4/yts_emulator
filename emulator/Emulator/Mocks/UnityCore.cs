// Unity Engine surface stubs. Sized exactly to what DmnwareApp.cs + Counter/Calculator/FlappyBird
// actually touch — not a faithful Unity reimplementation, just enough that the in-repo .cs files
// compile and run inside the emulator process.

using System;

namespace UnityEngine
{
    // ---- Attributes ----
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class HideInInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName, menuName;
        public int order;
    }

    // ---- Vectors / colors / math ----
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public override string ToString() => $"({x:0.##},{y:0.##})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new Quaternion { w = 1f };
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color magenta => new Color(1, 0, 1, 1);
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Abs(float v) => v < 0 ? -v : v;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Sin(float r) => (float)Math.Sin(r);
        public static float Cos(float r) => (float)Math.Cos(r);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }

    public static class Time
    {
        // Set every frame by the emulator's FrameLoop.
        public static float deltaTime;
        public static float time;
        public static float unscaledDeltaTime => deltaTime;
        public static float unscaledTime => time;
        public static int frameCount;
    }

    public static class Random
    {
        private static readonly System.Random _rng = new System.Random();
        public static float Range(float min, float max)
            => (float)(min + _rng.NextDouble() * (max - min));
        public static int Range(int minInclusive, int maxExclusive)
            => _rng.Next(minInclusive, maxExclusive);
        public static float value => (float)_rng.NextDouble();
    }

    public static class Debug
    {
        public static void Log(object o) => Console.WriteLine($"[unity] {o}");
        public static void LogWarning(object o) => Console.WriteLine($"[unity:warn] {o}");
        public static void LogError(object o) => Console.WriteLine($"[unity:error] {o}");
    }

    // ---- Texture / sprite stubs (apps don't draw with these; we just need the type to exist) ----
    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;
    }

    [Flags]
    public enum HideFlags { None = 0, NotEditable = 8, HideAndDontSave = 61 }

    public class Texture : Object { }
    public class Texture2D : Texture { }
    public class Sprite : Object { }
    public class AudioSource : Object { public void Play() { } }
    public class AudioClip : Object { }

    public enum FontWeight
    {
        Thin = 100, ExtraLight = 200, Light = 300, Regular = 400,
        Medium = 500, SemiBold = 600, Bold = 700, ExtraBold = 800, Black = 900,
    }

    // ---- Component / GameObject scene graph ----
    public class Component : Object
    {
        // Set by GameObject.AddComponent. Never null after construction.
        public GameObject gameObject;
        public Transform transform => gameObject?.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
    }

    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class Transform : Component
    {
        // Public for the emulator's own use; apps never poke into _children directly.
        public Transform parent;
        public readonly System.Collections.Generic.List<Transform> _children = new();
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Vector3 localEulerAngles;

        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];

        public void SetParent(Transform newParent)
        {
            if (parent != null) parent._children.Remove(this);
            parent = newParent;
            if (newParent != null) newParent._children.Add(this);
        }
    }

    public class RectTransform : Transform
    {
        // The emulator's LayoutEngine reads these on each frame; setters are reactive
        // via the OnChanged callback (set by LayoutEngine when the RectTransform is
        // created so that writes propagate to the Avalonia control).
        private Vector2 _anchoredPosition;
        private Vector2 _sizeDelta;
        private Vector2 _anchorMin;
        private Vector2 _anchorMax;
        private Vector2 _pivot = new Vector2(0.5f, 0.5f);

        public Action OnChanged;

        public Vector2 anchoredPosition { get => _anchoredPosition; set { _anchoredPosition = value; OnChanged?.Invoke(); } }
        public Vector2 sizeDelta { get => _sizeDelta; set { _sizeDelta = value; OnChanged?.Invoke(); } }
        public Vector2 anchorMin { get => _anchorMin; set { _anchorMin = value; OnChanged?.Invoke(); } }
        public Vector2 anchorMax { get => _anchorMax; set { _anchorMax = value; OnChanged?.Invoke(); } }
        public Vector2 pivot { get => _pivot; set { _pivot = value; OnChanged?.Invoke(); } }

        // localEulerAngles inherits from Transform but FlappyBird writes through this typed reference,
        // and it expects the rotation to actually apply visually. Mirror the trick.
        public new Vector3 localEulerAngles
        {
            get => base.localEulerAngles;
            set { base.localEulerAngles = value; OnChanged?.Invoke(); }
        }
    }

    public class GameObject : Object
    {
        private readonly System.Collections.Generic.Dictionary<Type, Component> _components = new();
        private bool _activeSelf = true;

        // Avalonia backing — assigned by the LayoutEngine when materializing nodes.
        // Apps never touch this; the emulator uses it to mirror SetActive into IsVisible.
        public Action<bool> OnActiveChanged;

        public GameObject() : this("GameObject") { }
        public GameObject(string n) { name = n; AddComponent<RectTransform>(); }

        public Transform transform => (Transform)_components[typeof(RectTransform)];

        public T GetComponent<T>() where T : Component
        {
            // Return exact match first, then any subclass match (so GetComponent<Transform>() works
            // for objects that have a RectTransform, and GetComponent<Graphic>() works for Image).
            if (_components.TryGetValue(typeof(T), out var exact)) return (T)exact;
            foreach (var c in _components.Values)
                if (c is T t) return t;
            return null;
        }

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this };
            _components[typeof(T)] = c;
            return c;
        }

        public void AddComponent(Component c)
        {
            c.gameObject = this;
            _components[c.GetType()] = c;
        }

        public bool activeSelf => _activeSelf;
        public bool activeInHierarchy => _activeSelf;
        public void SetActive(bool v)
        {
            if (_activeSelf == v) return;
            _activeSelf = v;
            OnActiveChanged?.Invoke(v);
        }
    }
}
