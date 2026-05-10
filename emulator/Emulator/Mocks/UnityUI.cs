// UnityEngine.UI / TMPro / UdonSharp / VRC stubs. Same principle as UnityCore.cs:
// only the surface the in-repo .cs files actually touch.

using System;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    // The Unity inheritance chain Maskable Graphic : Graphic : UIBehaviour. We collapse it.
    public class Graphic : Behaviour
    {
        private Color _color = Color.white;
        public Action<Color> OnColorChanged;
        public Color color { get => _color; set { _color = value; OnColorChanged?.Invoke(value); } }
        public RectTransform rectTransform => (RectTransform)gameObject.transform;
    }

    public class Image : Graphic
    {
        private Sprite _sprite;
        public Action<Sprite> OnSpriteChanged;
        public Sprite sprite { get => _sprite; set { _sprite = value; OnSpriteChanged?.Invoke(value); } }
    }

    public class Text : Graphic
    {
        public string text;
        public int fontSize;
    }

    // Stand-in for selectable widgets; we only need the public surface, not the input plumbing.
    public class Selectable : Behaviour { }

    public class UnityEvent
    {
        private readonly List<Action> _listeners = new();
        public void AddListener(Action a) => _listeners.Add(a);
        public void RemoveListener(Action a) => _listeners.Remove(a);
        public void Invoke()
        {
            // Snapshot in case a listener mutates the list (shouldn't happen here).
            foreach (var l in _listeners.ToArray()) l();
        }
    }

    public class UnityEventFloat
    {
        private readonly List<Action<float>> _listeners = new();
        public void AddListener(Action<float> a) => _listeners.Add(a);
        public void Invoke(float v) { foreach (var l in _listeners.ToArray()) l(v); }
    }

    public class UnityEventBool
    {
        private readonly List<Action<bool>> _listeners = new();
        public void AddListener(Action<bool> a) => _listeners.Add(a);
        public void Invoke(bool v) { foreach (var l in _listeners.ToArray()) l(v); }
    }

    public class Button : Selectable
    {
        public Image image;                       // FlappyBird touches flap.image.color
        public UnityEvent onClick = new();
    }

    public class Slider : Selectable
    {
        private float _value;
        public float minValue = 0f;
        public float maxValue = 1f;
        public Action<float> OnExternalSet;       // emulator writes here when the Avalonia slider moves
        public UnityEventFloat onValueChanged = new();
        public bool wholeNumbers;

        public float value
        {
            get => _value;
            set
            {
                var clamped = value < minValue ? minValue : (value > maxValue ? maxValue : value);
                if (wholeNumbers) clamped = (float)Math.Round(clamped);
                if (clamped == _value) return;
                _value = clamped;
                OnExternalSet?.Invoke(clamped);
                onValueChanged.Invoke(clamped);
            }
        }

        // Used by the emulator to push slider movement back without re-triggering OnExternalSet.
        public void SetValueQuiet(float v) { _value = v; onValueChanged.Invoke(v); }
    }

    public class Toggle : Selectable
    {
        private bool _isOn;
        public Action<bool> OnExternalSet;
        public UnityEventBool onValueChanged = new();
        public bool isOn
        {
            get => _isOn;
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                OnExternalSet?.Invoke(value);
                onValueChanged.Invoke(value);
            }
        }
        public void SetIsOnWithoutNotify(bool v) { _isOn = v; }
    }

    public class VerticalLayoutGroup : Behaviour { }
    public class HorizontalLayoutGroup : Behaviour { }
    public class ContentSizeFitter : Behaviour { }
    public class RectMask2D : Behaviour { }
    public class ScrollRect : Behaviour
    {
        public RectTransform content;
        public float verticalNormalizedPosition;
    }

    public class InputField : Selectable
    {
        public void Select() { }
        public void ActivateInputField() { }
    }
}

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        TopLeft, Top, TopRight,
        Left, Center, Right,
        BottomLeft, Bottom, BottomRight,
    }

    public class TMP_FontAsset : UnityEngine.Object { }

    public class TMP_Text : UnityEngine.UI.Graphic
    {
        private string _text = "";
        private float _fontSize = 18f;
        private TextAlignmentOptions _alignment = TextAlignmentOptions.Left;

        public Action<string> OnTextChanged;
        public Action<float> OnFontSizeChanged;
        public Action<TextAlignmentOptions> OnAlignmentChanged;

        public string text { get => _text; set { _text = value ?? ""; OnTextChanged?.Invoke(_text); } }
        public float fontSize { get => _fontSize; set { _fontSize = value; OnFontSizeChanged?.Invoke(value); } }
        public TextAlignmentOptions alignment { get => _alignment; set { _alignment = value; OnAlignmentChanged?.Invoke(value); } }
    }
}

namespace UdonSharp
{
    // A no-op base. Lifecycle methods are dispatched by the EmulatorDevice via reflection.
    public class UdonSharpBehaviour : UnityEngine.MonoBehaviour
    {
        public void SendCustomEvent(string name)
        {
            var m = GetType().GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            m?.Invoke(this, null);
        }

        public void SendCustomEventDelayedFrames(string name, int frames)
        {
            // Approximate: fire next tick. None of the three reference apps rely on this.
            EmulatorTick.Schedule(() => SendCustomEvent(name));
        }

        public void SendCustomEventDelayedSeconds(string name, float seconds)
        {
            EmulatorTick.Schedule(() => SendCustomEvent(name), seconds);
        }
    }

    // Tiny scheduler the SendCustomEvent* helpers post into. Drained by FrameLoop each tick.
    public static class EmulatorTick
    {
        private static readonly System.Collections.Generic.List<(float at, Action a)> _q = new();
        public static void Schedule(Action a, float delay = 0f) { _q.Add((UnityEngine.Time.time + delay, a)); }
        public static void Drain()
        {
            for (int i = _q.Count - 1; i >= 0; i--)
                if (UnityEngine.Time.time >= _q[i].at)
                {
                    var a = _q[i].a;
                    _q.RemoveAt(i);
                    try { a(); } catch (Exception ex) { UnityEngine.Debug.LogError(ex); }
                }
        }
    }
}

// VRC SDK / Udon shims. Apps just `using` these — they don't actually touch the types.
namespace VRC.SDKBase { public class VRCPlayerApi { } }
namespace VRC.Udon { public class UdonBehaviour : UnityEngine.MonoBehaviour { } }
namespace VRC.Core { }
