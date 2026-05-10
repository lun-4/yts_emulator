#if !COMPILER_UDONSHARP && UNITY_EDITOR
using DMNware.AppGen;

[DmnwareApp(name: "Flappy")]
public class FlappyBirdLayout : AppLayout
{
    // The U# code disables this Vertical's auto-layout in _DmnwareAppInit and positions every
    // child via anchoredPosition. We just need (a) a stable parent for all the sprites and
    // (b) every interactive element to have a registered handler.
    public override Node Build() => Vertical(
        padding: 0,
        spacing: 0,
        Image("bg", ""),
        Image("ground", ""),
        Image("pipeTop1", ""),
        Image("pipeBot1", ""),
        Image("pipeTop2", ""),
        Image("pipeBot2", ""),
        Image("pipeTop3", ""),
        Image("pipeBot3", ""),
        Image("bird", ""),
        Text("title", "FLAPPY"),
        Text("hint", "Tap anywhere to flap!"),
        Text("score", "0"),
        Text("gameOver", "GAME OVER"),
        Text("bestText", "Best: 0"),
        Button("flap", "", onClick: nameof(FlappyBird._OnFlap)),
        Button("restart", "Restart", onClick: nameof(FlappyBird._OnRestart)).Style(Variant.Primary)
    );
}
#endif
