#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using DMNware.AppGen;

// The U# code disables this Vertical's auto-layout in _DmnwareAppInit and positions every
// child via anchoredPosition. The grid is 8x8 = 64 button cells with ids cell00..cell77,
// each wired to its own _OnCellRC dispatcher (Udon click handlers are parameterless, so
// every cell needs its own entry point).
[DmnwareApp(name: "Mines")]
public class MinesweeperLayout : AppLayout
{
    public override Node Build()
    {
        var children = new List<Node>();
        children.Add(Image("bg", ""));
        children.Add(Text("minesText", "Mines: 10"));
        children.Add(Text("timerText", "0"));
        children.Add(Text("statusText", ""));
        children.Add(Toggle("flagToggle", "Flag Mode", false,
                            onChange: nameof(Minesweeper._OnFlagToggle)));
        children.Add(Button("newGame", "New Game",
                            onClick: nameof(Minesweeper._OnNewGame))
                     .Style(Variant.Primary));

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                children.Add(Button("cell" + r + c, "", onClick: "_OnCell" + r + c));

        return Vertical(padding: 0, spacing: 0, children.ToArray());
    }
}
#endif
