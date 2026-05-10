#if !COMPILER_UDONSHARP && UNITY_EDITOR
using DMNware.AppGen;

[DmnwareApp(name: "Counter")]
public class CounterLayout : AppLayout
{
    public override Node Build() => Vertical(
        padding: Spacing.LG,
        spacing: Spacing.MD,
        Header("Counter"),
        Text("count", "0"),
        Spacer.Flex(),
        Slider("step", "Step size", min: 1f, max: 10f, initial: 1f, onChange: nameof(Counter._OnStepChanged), integer: true),
        Toggle("autoFive", "Add 5 instead of step", initial: false, onChange: nameof(Counter._OnAutoFiveChanged)),
        Horizontal(0, Spacing.SM,
            Button("dec",   "−",     onClick: nameof(Counter._OnDec)).Flex(1f),
            Button("inc",   "+",     onClick: nameof(Counter._OnInc)).Flex(1f),
            Button("reset", "Reset", onClick: nameof(Counter._OnReset)).Style(Variant.Danger).Flex(1f)
        )
    );
}
#endif
