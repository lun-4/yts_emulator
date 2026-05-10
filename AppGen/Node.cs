#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;

namespace DMNware.AppGen
{
    // ---------- base ----------

    public abstract class Node
    {
        public string Id;
        public Modifiers Mods = Modifiers.Default;

        // Fluent modifiers — return Node so they chain on any subtype.
        public Node Width(Size s)         { Mods.Width = s; return this; }
        public Node Width(float v)        { Mods.Width = Size.Fixed(v); return this; }
        public Node Height(Size s)        { Mods.Height = s; return this; }
        public Node Height(float v)       { Mods.Height = Size.Fixed(v); return this; }
        public Node MinWidth(float v)     { Mods.MinWidth = v; Mods.HasMinWidth = true; return this; }
        public Node MaxWidth(float v)     { Mods.MaxWidth = v; Mods.HasMaxWidth = true; return this; }
        public Node MinHeight(float v)    { Mods.MinHeight = v; Mods.HasMinHeight = true; return this; }
        public Node MaxHeight(float v)    { Mods.MaxHeight = v; Mods.HasMaxHeight = true; return this; }
        public Node Padding(Pad p)        { Mods.Padding = p; return this; }
        public Node Padding(int v)        { Mods.Padding = Pad.All(v); return this; }
        public Node Margin(Pad p)         { Mods.Margin = p; return this; }
        public Node Margin(int v)         { Mods.Margin = Pad.All(v); return this; }
        public Node Flex(float weight = 1f) { Mods.FlexWeight = weight; return this; }
        public Node Style(Variant v)      { Mods.Style = v; return this; }
        public Node Hidden(bool initiallyHidden = true) { Mods.InitiallyHidden = initiallyHidden; return this; }
    }

    // ---------- containers ----------

    public abstract class ContainerNode : Node
    {
        public List<Node> Children = new List<Node>();
        public Pad InnerPadding;
        public int Spacing;
        public Align Align = Align.Stretch;
        public Justify Justify = Justify.Start;
    }

    public class VerticalNode : ContainerNode { }
    public class HorizontalNode : ContainerNode { }

    // ---------- leaves ----------

    public class TextNode : Node
    {
        public string Text;
        public Typography Style = Typography.Body;
    }

    public class ImageNode : Node
    {
        public string AssetKey;
        public FillMode Mode = FillMode.Contain;
    }

    public class SpacerNode : Node
    {
        public bool IsFlex;          // true => flex spacer, weight in Mods.FlexWeight
        public float FixedWidth, FixedHeight;
    }

    public class DividerNode : Node
    {
        public Orientation Orientation = Orientation.Horizontal;
    }

    // ---------- interactives ----------

    public class ButtonNode : Node
    {
        public string Label;
        public string OnClickMethod;
    }

    public class ToggleNode : Node
    {
        public string Label;
        public bool InitialValue;
        public string OnChangeMethod;
    }

    public class SliderNode : Node
    {
        public string Label;
        public float Min, Max, InitialValue;
        public bool Integer;
        public string OnChangeMethod;
    }

    // ---------- escape hatch ----------

    public class RawNode : Node
    {
        public string PrefabKey;
    }

    // ---------- spacer factories ----------
    // Lives outside AppLayout because `Spacer.Flex()` reads better than `SpacerFlex()`.

    public static class Spacer
    {
        public static SpacerNode Fixed(float width, float height) => new SpacerNode { FixedWidth = width, FixedHeight = height };
        public static SpacerNode Flex(float weight = 1f) => new SpacerNode
        {
            IsFlex = true,
            Mods = new Modifiers { Width = Size.Auto, Height = Size.Auto, FlexWeight = weight, Style = Variant.Default },
        };
    }
}
#endif
