// Walks a DSL Node tree, materializes one Avalonia control + mock GameObject per node,
// wires Unity-side mock setters to update the Avalonia control, registers id'd nodes for
// reflection-based field assignment, and binds Button/Slider/Toggle handlers to behaviour
// methods.

using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DMNware.AppGen;
using TMPro;
using UnityEngine;
using U = UnityEngine.UI;
using AvCtl = Avalonia.Controls;
using AvMedia = Avalonia.Media;

namespace YtsEmulator.Layout;

public class LayoutEngine
{
    public readonly Dictionary<string, Materialized> ById = new();
    public Materialized Root { get; private set; }

    private readonly object _behaviour;
    private readonly Type _behaviourType;

    public LayoutEngine(object behaviour)
    {
        _behaviour = behaviour;
        _behaviourType = behaviour.GetType();
    }

    public Materialized Build(Node tree)
    {
        Root = Materialize(tree, parent: null);
        AssignSerializeFields();
        return Root;
    }

    private Materialized Materialize(Node node, Materialized parent)
    {
        var m = new Materialized { Source = node, Parent = parent };

        switch (node)
        {
            case ContainerNode c: BuildContainer(c, m); break;
            case TextNode t: BuildText(t, m); break;
            case ImageNode i: BuildImage(i, m); break;
            case ButtonNode b: BuildButton(b, m); break;
            case ToggleNode t: BuildToggle(t, m); break;
            case SliderNode s: BuildSlider(s, m); break;
            case DividerNode d: BuildDivider(d, m); break;
            case SpacerNode sp: BuildSpacer(sp, m); break;
            case RawNode: BuildRaw(m); break;
            default: throw new InvalidOperationException($"unknown node {node.GetType().Name}");
        }

        // Mods that apply to every node.
        ApplyCommonMods(m);

        // Register id for SerializeField assignment.
        if (!string.IsNullOrEmpty(node.Id))
        {
            if (ById.ContainsKey(node.Id))
                throw new InvalidOperationException($"duplicate id '{node.Id}' in layout");
            ById[node.Id] = m;
        }

        // Initial visibility from .Hidden(true).
        if (node.Mods.InitiallyHidden) m.GO.SetActive(false);

        return m;
    }

    // ---------- containers ----------

    private void BuildContainer(ContainerNode c, Materialized m)
    {
        var go = new GameObject(c.GetType().Name);
        var canvas = new Canvas
        {
            ClipToBounds = false,
            Background = Brushes.Transparent,
        };
        m.GO = go;
        m.Ctl = canvas;
        m.IsVertical = c is VerticalNode;

        if (m.IsVertical) m.LayoutGroup = go.AddComponent<U.VerticalLayoutGroup>();
        else m.LayoutGroup = go.AddComponent<U.HorizontalLayoutGroup>();

        foreach (var child in c.Children)
        {
            var cm = Materialize(child, m);
            m.Children.Add(cm);
            cm.GO.transform.SetParent(go.transform);
            canvas.Children.Add(cm.Ctl);
            // SetActive mirroring.
            cm.GO.OnActiveChanged = v => cm.Ctl.IsVisible = v;
            // Trigger once for nodes that started Hidden.
            cm.Ctl.IsVisible = cm.GO.activeSelf;
        }
    }

    // ---------- text ----------

    private void BuildText(TextNode t, Materialized m)
    {
        var go = new GameObject("Text");
        var tmp = new TMP_Text();
        go.AddComponent(tmp);
        var tb = new TextBlock
        {
            Text = t.Text ?? "",
            FontSize = Theme.TypographySize(t.Style),
            FontWeight = Theme.TypographyWeight(t.Style),
            Foreground = new SolidColorBrush(Theme.TypographyColor(t.Style)),
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        tmp.text = t.Text ?? "";
        tmp.fontSize = (float)Theme.TypographySize(t.Style);
        tmp.OnTextChanged = s => tb.Text = s;
        tmp.OnFontSizeChanged = f => tb.FontSize = f;
        tmp.OnAlignmentChanged = a => tb.TextAlignment = MapTextAlign(a);
        tmp.OnColorChanged = col =>
        {
            // Unity Color (0..1 floats); skip writes that would render fully transparent text
            // by accident — but FlappyBird does set color to clear, so respect it.
            tb.Foreground = Theme.BrushFromUnity(col);
        };
        m.GO = go; m.Ctl = tb;
        // Initial colour through Graphic.color = white default doesn't fire unless app sets it.
        // We've already set Foreground above from theme.
    }

    private static TextAlignment MapTextAlign(TextAlignmentOptions a) => a switch
    {
        TextAlignmentOptions.Center or TextAlignmentOptions.Top
            or TextAlignmentOptions.Bottom => TextAlignment.Center,
        TextAlignmentOptions.Right or TextAlignmentOptions.TopRight
            or TextAlignmentOptions.BottomRight => TextAlignment.Right,
        _ => TextAlignment.Left,
    };

    // ---------- image ----------

    private void BuildImage(ImageNode i, Materialized m)
    {
        var go = new GameObject("Image");
        var img = new U.Image();
        go.AddComponent(img);

        // Render Image as a colored rectangle by default. assetKey support is a v2.
        var rect = new Rectangle { Fill = Brushes.Transparent };
        img.OnColorChanged = c => rect.Fill = Theme.BrushFromUnity(c);

        m.GO = go; m.Ctl = rect;
    }

    // ---------- button ----------

    private void BuildButton(ButtonNode b, Materialized m)
    {
        var go = new GameObject("Button");
        var btn = new U.Button();
        var imgChild = new U.Image();
        go.AddComponent(btn);
        // Button.image is a child Image (covers the button background) — FlappyBird touches it.
        // We give it its own backing GameObject so .gameObject etc. would work, but we don't put
        // a separate Avalonia control for it; instead the button's own background tracks it.
        var imgGo = new GameObject("Button.Image");
        imgGo.AddComponent(imgChild);
        btn.image = imgChild;

        var (bgCol, fgCol) = Theme.ButtonColors(b.Mods.Style);
        var av = new AvCtl.Button
        {
            Content = new TextBlock
            {
                Text = b.Label ?? "",
                Foreground = new SolidColorBrush(fgCol),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = Theme.BodyFontSize,
            },
            Background = new SolidColorBrush(bgCol),
            Foreground = new SolidColorBrush(fgCol),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Theme.CornerRadius),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        // FlappyBird sets flap.image.color = clear to make the button invisible. Mirror it.
        imgChild.OnColorChanged = c =>
        {
            av.Background = Theme.BrushFromUnity(c);
        };

        // Avalonia click → mock UnityEvent → app handler.
        av.Click += (_, _) =>
        {
            try { btn.onClick.Invoke(); }
            catch (Exception ex) { Console.WriteLine($"[btn:{b.Id}] click handler threw: {ex}"); }
        };

        // Wire the layout-declared handler (nameof(MyApp._OnFoo)) to the behaviour method.
        if (!string.IsNullOrEmpty(b.OnClickMethod))
            BindHandler(b.OnClickMethod, () => btn.onClick.AddListener(() => InvokeHandler(b.OnClickMethod)));

        m.GO = go; m.Ctl = av;
    }

    // ---------- toggle ----------

    private void BuildToggle(ToggleNode t, Materialized m)
    {
        var go = new GameObject("Toggle");
        var tg = new U.Toggle();
        go.AddComponent(tg);

        var av = new ToggleSwitch
        {
            IsChecked = t.InitialValue,
            OnContent = t.Label ?? "",
            OffContent = t.Label ?? "",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = new SolidColorBrush(Theme.Text),
        };
        tg.SetIsOnWithoutNotify(t.InitialValue);

        bool reentrancy = false;
        av.IsCheckedChanged += (_, _) =>
        {
            if (reentrancy) return;
            reentrancy = true; tg.isOn = av.IsChecked == true; reentrancy = false;
        };
        tg.OnExternalSet = v =>
        {
            if (reentrancy) return;
            reentrancy = true; av.IsChecked = v; reentrancy = false;
        };

        if (!string.IsNullOrEmpty(t.OnChangeMethod))
            BindHandler(t.OnChangeMethod,
                () => tg.onValueChanged.AddListener(_ => InvokeHandler(t.OnChangeMethod)));

        m.GO = go; m.Ctl = av;
    }

    // ---------- slider ----------

    private void BuildSlider(SliderNode s, Materialized m)
    {
        var go = new GameObject("Slider");
        var sl = new U.Slider
        {
            minValue = s.Min,
            maxValue = s.Max,
            wholeNumbers = s.Integer,
        };
        go.AddComponent(sl);
        sl.SetValueQuiet(s.InitialValue);

        // Wrap in a vertical stack: label on top, slider below. Apply a fixed total preferred
        // height so V layout pass treats this as a single block.
        var label = new TextBlock
        {
            Text = s.Label ?? "",
            FontSize = Theme.CaptionFontSize,
            Foreground = new SolidColorBrush(Theme.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var av = new AvCtl.Slider
        {
            Minimum = s.Min,
            Maximum = s.Max,
            Value = s.InitialValue,
            IsSnapToTickEnabled = s.Integer,
            TickFrequency = s.Integer ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = new SolidColorBrush(Theme.Primary),
        };
        var stack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 4,
            Children = { label, av },
        };

        bool reentrancy = false;
        av.PropertyChanged += (_, e) =>
        {
            if (e.Property != AvCtl.Slider.ValueProperty) return;
            if (reentrancy) return;
            reentrancy = true;
            try { sl.value = (float)av.Value; }
            finally { reentrancy = false; }
        };
        sl.OnExternalSet = v =>
        {
            if (reentrancy) return;
            reentrancy = true;
            try { av.Value = v; }
            finally { reentrancy = false; }
        };

        if (!string.IsNullOrEmpty(s.OnChangeMethod))
            BindHandler(s.OnChangeMethod,
                () => sl.onValueChanged.AddListener(_ => InvokeHandler(s.OnChangeMethod)));

        m.GO = go; m.Ctl = stack;
    }

    // ---------- divider, spacer, raw ----------

    private void BuildDivider(DividerNode d, Materialized m)
    {
        var go = new GameObject("Divider");
        var rect = new Rectangle
        {
            Fill = new SolidColorBrush(Theme.SurfaceElevated),
            Height = Theme.DividerThickness,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        m.GO = go; m.Ctl = rect;
    }

    private void BuildSpacer(SpacerNode sp, Materialized m)
    {
        var go = new GameObject("Spacer");
        // An empty placeholder; LayoutPass uses Mods.FlexWeight or FixedW/FixedH for sizing.
        var ctl = new Panel { Background = Brushes.Transparent };
        m.GO = go; m.Ctl = ctl;
    }

    private void BuildRaw(Materialized m)
    {
        var go = new GameObject("Raw");
        // No registered prefab support yet — render a placeholder.
        var rect = new Rectangle { Fill = new AvMedia.SolidColorBrush(AvMedia.Color.FromRgb(0xFF, 0x00, 0xFF)) };
        m.GO = go; m.Ctl = rect;
    }

    // ---------- modifiers ----------

    private void ApplyCommonMods(Materialized m)
    {
        var mods = m.Mods;
        if (mods.Width.Mode == SizeMode.Fixed) m.Ctl.Width = mods.Width.Value;
        if (mods.Height.Mode == SizeMode.Fixed) m.Ctl.Height = mods.Height.Value;
        // Mins/maxes — set on Avalonia Layoutable.
        if (mods.HasMinWidth) m.Ctl.MinWidth = mods.MinWidth;
        if (mods.HasMaxWidth) m.Ctl.MaxWidth = mods.MaxWidth;
        if (mods.HasMinHeight) m.Ctl.MinHeight = mods.MinHeight;
        if (mods.HasMaxHeight) m.Ctl.MaxHeight = mods.MaxHeight;
        if (!mods.Margin.IsZero)
            m.Ctl.Margin = new Thickness(mods.Margin.L, mods.Margin.T, mods.Margin.R, mods.Margin.B);
    }

    // ---------- field assignment + handler binding ----------

    private void AssignSerializeFields()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var f in _behaviourType.GetFields(flags))
        {
            if (!ById.TryGetValue(f.Name, out var m)) continue;
            // Pull out the mock component matching the field type. Multiple components can sit on
            // one GameObject (Button + Image) — pick the one whose type the field is asking for.
            var ftype = f.FieldType;
            object value = ResolveFieldValue(m.GO, ftype);
            if (value == null) continue;
            f.SetValue(_behaviour, value);
        }
    }

    private static object ResolveFieldValue(GameObject go, Type fieldType)
    {
        // Special: GameObject field → the GO itself.
        if (fieldType == typeof(GameObject)) return go;
        // Use reflection on GameObject.GetComponent<T> via the dictionary path: just iterate
        // the non-public _components dict.
        var comps = typeof(GameObject)
            .GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(go) as System.Collections.IDictionary;
        if (comps == null) return null;
        foreach (System.Collections.DictionaryEntry entry in comps)
        {
            if (fieldType.IsAssignableFrom(entry.Value.GetType())) return entry.Value;
        }
        return null;
    }

    private void BindHandler(string methodName, Action wire)
    {
        // Validate at build time: surface a clear error rather than letting the click silently no-op.
        var m = _behaviourType.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (m == null)
            throw new InvalidOperationException(
                $"[layout] handler '{methodName}' not found on {_behaviourType.Name}");
        wire();
    }

    private void InvokeHandler(string methodName)
    {
        var m = _behaviourType.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        try { m?.Invoke(_behaviour, null); }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine($"[handler {methodName}] {ex.InnerException}");
        }
    }
}
