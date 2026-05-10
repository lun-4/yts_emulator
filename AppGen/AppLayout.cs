#if !COMPILER_UDONSHARP && UNITY_EDITOR
namespace DMNware.AppGen
{
    /// Base class for app layouts. Subclasses implement Build() and decorate the class with
    /// [DmnwareApp]. The static factory methods below are inherited so subclasses can call
    /// `Vertical(...)`, `Button(...)`, etc. without any prefix or using-static directive.
    public abstract class AppLayout
    {
        public abstract Node Build();

        // ---- Containers ----
        public static ContainerNode Vertical(params Node[] children) => Vertical(0, 0, Align.Stretch, Justify.Start, children);
        public static ContainerNode Vertical(int padding, int spacing, params Node[] children) => Vertical(padding, spacing, Align.Stretch, Justify.Start, children);
        public static ContainerNode Vertical(int padding, int spacing, Align align, Justify justify, params Node[] children)
        {
            var n = new VerticalNode { InnerPadding = Pad.All(padding), Spacing = spacing, Align = align, Justify = justify };
            n.Children.AddRange(children);
            return n;
        }
        public static ContainerNode Vertical(Pad padding, int spacing, Align align, Justify justify, params Node[] children)
        {
            var n = new VerticalNode { InnerPadding = padding, Spacing = spacing, Align = align, Justify = justify };
            n.Children.AddRange(children);
            return n;
        }

        public static ContainerNode Horizontal(params Node[] children) => Horizontal(0, 0, Align.Stretch, Justify.Start, children);
        public static ContainerNode Horizontal(int padding, int spacing, params Node[] children) => Horizontal(padding, spacing, Align.Stretch, Justify.Start, children);
        public static ContainerNode Horizontal(int padding, int spacing, Align align, Justify justify, params Node[] children)
        {
            var n = new HorizontalNode { InnerPadding = Pad.All(padding), Spacing = spacing, Align = align, Justify = justify };
            n.Children.AddRange(children);
            return n;
        }
        public static ContainerNode Horizontal(Pad padding, int spacing, Align align, Justify justify, params Node[] children)
        {
            var n = new HorizontalNode { InnerPadding = padding, Spacing = spacing, Align = align, Justify = justify };
            n.Children.AddRange(children);
            return n;
        }

        // ---- Leaves ----
        public static TextNode Text(string text) => new TextNode { Text = text };
        public static TextNode Text(string id, string text) => new TextNode { Id = id, Text = text };
        public static TextNode Text(string id, string text, Typography style) => new TextNode { Id = id, Text = text, Style = style };
        public static TextNode Header(string text) => new TextNode { Text = text, Style = Typography.Header };
        public static TextNode Header(string id, string text) => new TextNode { Id = id, Text = text, Style = Typography.Header };
        public static TextNode Caption(string text) => new TextNode { Text = text, Style = Typography.Caption };

        public static ImageNode Image(string assetKey) => new ImageNode { AssetKey = assetKey };
        public static ImageNode Image(string id, string assetKey) => new ImageNode { Id = id, AssetKey = assetKey };
        public static ImageNode Image(string id, string assetKey, FillMode mode) => new ImageNode { Id = id, AssetKey = assetKey, Mode = mode };

        public static DividerNode Divider() => new DividerNode();
        public static DividerNode Divider(Orientation o) => new DividerNode { Orientation = o };

        // ---- Interactives ----
        public static ButtonNode Button(string id, string label, string onClick)
            => new ButtonNode { Id = id, Label = label, OnClickMethod = onClick };

        public static ToggleNode Toggle(string id, string label, bool initial, string onChange)
            => new ToggleNode { Id = id, Label = label, InitialValue = initial, OnChangeMethod = onChange };

        public static SliderNode Slider(string id, string label, float min, float max, float initial, string onChange, bool integer = false)
            => new SliderNode { Id = id, Label = label, Min = min, Max = max, InitialValue = initial, OnChangeMethod = onChange, Integer = integer };

        // ---- Escape hatch ----
        public static RawNode Raw(string prefabKey, string id) => new RawNode { Id = id, PrefabKey = prefabKey };
    }
}
#endif
