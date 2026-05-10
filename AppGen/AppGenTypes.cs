#if !COMPILER_UDONSHARP && UNITY_EDITOR
namespace DMNware.AppGen
{
    public enum Variant { Default, Primary, Secondary, Ghost, Danger }
    public enum Align { Start, Center, End, Stretch }
    public enum Justify { Start, Center, End, SpaceBetween, SpaceAround }
    public enum Typography { Body, Header, Caption }
    public enum Orientation { Horizontal, Vertical }
    public enum FillMode { Contain, Cover, Stretch, Native }
    public enum ScrollDirection { Vertical, Horizontal }

    public static class Spacing
    {
        public const int XS = 4;
        public const int SM = 8;
        public const int MD = 12;
        public const int LG = 20;
        public const int XL = 32;
    }

    public struct Pad
    {
        public int L, T, R, B;

        public static Pad All(int v) => new Pad { L = v, T = v, R = v, B = v };
        public static Pad Symmetric(int h, int v) => new Pad { L = h, T = v, R = h, B = v };
        public static Pad Of(int l, int t, int r, int b) => new Pad { L = l, T = t, R = r, B = b };
        public static implicit operator Pad(int v) => All(v);

        public bool IsZero => L == 0 && T == 0 && R == 0 && B == 0;
    }

    public enum SizeMode { Auto, Fixed, Flex }

    public struct Size
    {
        public SizeMode Mode;
        public float Value;

        public static readonly Size Auto = new Size { Mode = SizeMode.Auto };
        public static Size Fixed(float v) => new Size { Mode = SizeMode.Fixed, Value = v };
        public static Size Flex(float weight = 1f) => new Size { Mode = SizeMode.Flex, Value = weight };

        public static implicit operator Size(float v) => Fixed(v);
    }

    public struct Modifiers
    {
        public Size Width;
        public Size Height;
        public float MinWidth, MinHeight, MaxWidth, MaxHeight;
        public Pad Padding;
        public Pad Margin;
        public float FlexWeight;        // 0 = unset
        public Variant Style;
        public bool InitiallyHidden;
        public bool HasMinWidth, HasMinHeight, HasMaxWidth, HasMaxHeight;

        public static Modifiers Default => new Modifiers
        {
            Width = Size.Auto,
            Height = Size.Auto,
            Style = Variant.Default,
        };
    }
}
#endif
