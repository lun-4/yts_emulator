#if !COMPILER_UDONSHARP && UNITY_EDITOR
using DMNware.AppGen;

[DmnwareApp(name: "Calculator")]
public class CalculatorLayout : AppLayout
{
    public override Node Build() => Vertical(
        padding: Spacing.LG,
        spacing: Spacing.MD,
        Header("Calculator"),
        Text("display", "0"),
        Spacer.Flex(),
        Horizontal(0, Spacing.SM,
            Button("d7",  "7", onClick: nameof(Calculator._OnDigit7)).Flex(1f),
            Button("d8",  "8", onClick: nameof(Calculator._OnDigit8)).Flex(1f),
            Button("d9",  "9", onClick: nameof(Calculator._OnDigit9)).Flex(1f),
            Button("div", "÷", onClick: nameof(Calculator._OnDiv)).Style(Variant.Secondary).Flex(1f)
        ),
        Horizontal(0, Spacing.SM,
            Button("d4",  "4", onClick: nameof(Calculator._OnDigit4)).Flex(1f),
            Button("d5",  "5", onClick: nameof(Calculator._OnDigit5)).Flex(1f),
            Button("d6",  "6", onClick: nameof(Calculator._OnDigit6)).Flex(1f),
            Button("mul", "×", onClick: nameof(Calculator._OnMul)).Style(Variant.Secondary).Flex(1f)
        ),
        Horizontal(0, Spacing.SM,
            Button("d1",  "1", onClick: nameof(Calculator._OnDigit1)).Flex(1f),
            Button("d2",  "2", onClick: nameof(Calculator._OnDigit2)).Flex(1f),
            Button("d3",  "3", onClick: nameof(Calculator._OnDigit3)).Flex(1f),
            Button("sub", "−", onClick: nameof(Calculator._OnSub)).Style(Variant.Secondary).Flex(1f)
        ),
        Horizontal(0, Spacing.SM,
            Button("clear", "C", onClick: nameof(Calculator._OnClear)).Style(Variant.Danger).Flex(1f),
            Button("d0",    "0", onClick: nameof(Calculator._OnDigit0)).Flex(1f),
            Button("dot",   ".", onClick: nameof(Calculator._OnDot)).Flex(1f),
            Button("add",   "+", onClick: nameof(Calculator._OnAdd)).Style(Variant.Secondary).Flex(1f)
        ),
        Horizontal(0, Spacing.SM,
            Button("eq", "=", onClick: nameof(Calculator._OnEquals)).Style(Variant.Primary).Flex(1f)
        )
    );
}
#endif
