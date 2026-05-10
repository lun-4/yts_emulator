# DMNware AppGen — guide for Claude

How to author a new DMNware app from chat without bouncing through Unity twice. This system generates a tablet-app prefab from a layout DSL plus an UdonSharp behaviour.

## Mental model

Each app is **three files in a folder** under `Assets/nat/UdonScripts/DMNware/Apps/Gen/<AppName>/`:

| File | Authored by | Purpose |
|---|---|---|
| `<App>.cs` | you | `partial class <App> : UdonSharpBehaviour` — game logic, lifecycle hooks, click/change handlers. Compiled to Udon. |
| `<App>.layout.cs` | you | `class <App>Layout : AppLayout` with `[DmnwareApp(name: "...")]` — declarative widget tree. **Editor-only** (`#if !COMPILER_UDONSHARP && UNITY_EDITOR`). |
| `<App>.gen.cs` | codegen (or you, predicted) | `partial class <App>` adding `[SerializeField]` fields for every id'd node in the layout. **Auto-generated**, regenerated on every codegen pass. |

The codegen pass produces a `<App>.prefab` next to those files. The user drags the prefab onto the `AppSource` ("Apps") GameObject in the scene to install the app.

## The cold-start cycle

`<App>.cs` references generated fields (`bird.color`, `score.text`, etc.) that only exist in `<App>.gen.cs`. The generator reflects on `<App>Layout` to produce `<App>.gen.cs`, but reflection requires the assembly to compile, which requires `<App>.gen.cs` to exist. Loop on first creation.

**Two valid escapes:**

1. **Tell the user to run `DMNware/Create New App…` first.** Writes a stub `.cs` (no field refs) + minimal `.layout.cs` and queues codegen post-recompile via `SessionState`. Once it returns, the user has a compiling baseline. You then fill in the bodies and ask the user to re-run `DMNware/Generate App UI`.

2. **Write all three files yourself in one shot** — `.cs` (full), `.layout.cs` (full), AND `.gen.cs` (predicted `[SerializeField]` fields matching every id'd node in the layout). The project compiles immediately. User runs `DMNware/Generate App UI`, which idempotently overwrites `.gen.cs` with the canonical version (correcting any prediction mistakes) and produces the prefab. Faster but requires correct field prediction.

After bootstrap the cycle does not reappear — adding widgets incrementally is fine.

## Final user step

After the files compile, the user must:

1. **`DMNware/Generate App UI`** menu — produces `<App>.prefab` in the same folder. (Or right-click the `.layout.cs` file → "DMNware/Generate UI for this app".)
2. **Drag `<App>.prefab` onto the `AppSource` ("Apps") GameObject in the open scene** to install it.

You cannot do either of these from chat — say so explicitly.

## Layout DSL reference

`AppLayout` (in `AppGen/AppLayout.cs`) provides static factory methods. Subclasses inherit them, so you can call `Vertical(...)`, `Button(...)` etc. without a prefix.

### Containers
- `Vertical(padding, spacing, children…)` — adds `VerticalLayoutGroup`
- `Horizontal(padding, spacing, children…)` — adds `HorizontalLayoutGroup`
- Both also accept `(padding, spacing, Align, Justify, children…)` overloads.

### Leaves
- `Text(id, text)`, `Header(text)`, `Caption(text)` — TMP_Text. Only id'd Text nodes get a field.
- `Image(id, assetKey)` — `assetKey` looks up `DmnwareAppGenAssets.GetIcon`. **Pass `""` for a plain colored rect** (set color from code).
- `Divider()` — visual rule.
- `Spacer.Flex(weight)`, `Spacer.Fixed(w, h)`.

### Interactives (id is required, handler must exist on the behaviour)
- `Button(id, label, onClick: nameof(MyApp._OnFoo))`
- `Toggle(id, label, initial, onChange: nameof(MyApp._OnFoo))`
- `Slider(id, label, min, max, initial, onChange: nameof(...), integer: false)`

### Escape hatch
- `Raw(prefabKey, id)` — instantiates a registered prefab from `DmnwareAppGenAssets`. The field is `GameObject`.

### Modifiers (chain on any node)
`.Width(f)`, `.Height(f)`, `.MinWidth(f)`, `.MaxWidth(f)`, `.MinHeight(f)`, `.MaxHeight(f)`, `.Padding(p)`, `.Margin(p)`, `.Flex(weight)`, `.Style(Variant.Primary|Secondary|Ghost|Danger)`, `.Hidden()` (must have an id).

### Field type for `<App>.gen.cs`
The validator (in `AppGenerator.Validate`) emits one field per id'd node:

| Node | Field type |
|---|---|
| `TextNode` with id | `TMPro.TMP_Text` |
| `ImageNode` with id | `UnityEngine.UI.Image` |
| `ButtonNode` | `UnityEngine.UI.Button` |
| `ToggleNode` | `UnityEngine.UI.Toggle` |
| `SliderNode` | `UnityEngine.UI.Slider` |
| `RawNode` | `UnityEngine.GameObject` |

Containers/Spacers/Dividers without ids produce no field. **Reserved ids:** `Device`, `App`, `transform`, `gameObject`, `enabled`, `name`, `tag`, `layer`, anything starting with `_dmnware*`.

## Behaviour skeleton

Required fields and lifecycle hooks (every app behaviour MUST have these or the prefab build will misbehave):

```csharp
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;   // if you touch UI components
using TMPro;            // if you touch TMP_Text
using VRC.SDKBase;
using VRC.Udon;

public partial class MyApp : UdonSharpBehaviour
{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    void Start() { }

    public void _DmnwareAppInit()      { }   // once, after fields are bound
    public void _DmnwareAppLateInit()  { }
    public void _DmnwareAppOpen()      { }   // every time the user opens the app
    public void _DmnwareAppClose()     { }   // every time the user backs out

    // Click/change handlers — names must match nameof(...) refs in layout exactly.
    public void _OnFoo() { }
}
```

Underscore-prefixed handlers are convention; the validator just checks the method exists by name.

## Screen and masking

The user-visible "screen" inside the device is **1080 × 1670 (portrait)**. `PrefabBuilder` creates a `Container` GameObject of that fixed size, with `RectMask2D` clipping anything outside it (so games drawing sprites by `anchoredPosition` don't bleed into the title bar / home strip). The Container is anchored top-left, pivot top-left.

Your layout's root `Vertical`/`Horizontal` is parented to that Container with `StretchToParent` — so its rect is 1080×1670 with bottom-left at (0,0) when you anchor children to (0,0).

## UdonSharp / Udon whitelist gotchas (hit in practice)

These caused compile errors that aren't obvious from C# alone — **prefer the `gameObject.GetComponent<T>()` pattern** when in doubt:

| ❌ Doesn't work | ✅ Use instead |
|---|---|
| `tmpText.rectTransform` | `tmpText.gameObject.GetComponent<RectTransform>()` |
| `tmpText.transform` | `tmpText.gameObject.transform` (or get the RectTransform directly) |
| `button.rectTransform` | `button.gameObject.GetComponent<RectTransform>()` (Button isn't a Graphic) |
| `image.transform.parent` (sometimes) | `image.gameObject.transform.parent` |

`Image.rectTransform` and `Image.color` **do** work. `Behaviour.enabled` works on `VerticalLayoutGroup`, `HorizontalLayoutGroup`, etc. `RectTransform.anchoredPosition`, `sizeDelta`, `anchorMin/Max`, `pivot`, `localEulerAngles` all work. `Time.deltaTime`, `Mathf.Clamp`, `Random.Range`, `Color`, `Vector2`/`Vector3`, basic arrays, and `GameObject.SetActive` all work.

UdonSharp also lacks `??`, `?.`, and most LINQ. Use `if (x != null)` and explicit loops.

## Non-flow layouts (games, custom positioning)

The layout system imposes a `VerticalLayoutGroup`/`HorizontalLayoutGroup` on every container, which auto-positions children. For games like Flappy Bird that need free positioning:

1. Declare every sprite/element as a child of one root `Vertical` (with empty `assetKey: ""` for plain colored rects).
2. In `_DmnwareAppInit`, **disable the parent's `VerticalLayoutGroup`**:
   ```csharp
   var parent = anyField.gameObject.transform.parent;
   var vlg = parent.GetComponent<VerticalLayoutGroup>();
   if (vlg != null) vlg.enabled = false;
   ```
3. Anchor each element to bottom-left and drive `anchoredPosition` directly each frame:
   ```csharp
   var rt = field.gameObject.GetComponent<RectTransform>();
   rt.anchorMin = new Vector2(0f, 0f);
   rt.anchorMax = new Vector2(0f, 0f);
   rt.pivot = new Vector2(0.5f, 0.5f);
   rt.sizeDelta = new Vector2(w, h);
   rt.anchoredPosition = new Vector2(x, y);   // x, y in screen-space (0..1080, 0..1670)
   ```
4. Use a normal Unity `void Update()` for the per-frame loop. Guard it with an `_isOpen` flag set in `_DmnwareAppOpen`/`_DmnwareAppClose` so the game doesn't tick when the user has the app closed.

Worked example: `Apps/Gen/FlappyBird/`.

## Examples in the repo

Read these before authoring a new app — they cover most patterns:

- `Apps/Gen/Counter/` — minimal: text, slider, toggle, three buttons. Standard flow layout.
- `Apps/Gen/Calculator/` — many buttons in nested `Horizontal` rows, simple state machine.
- `Apps/Gen/FlappyBird/` — non-flow layout, `Update()` loop, manual `anchoredPosition`, `RectMask2D`-clipped game area, multi-state UI (idle / playing / game-over).

## Pipeline files (don't normally need to touch)

- `AppGen/AppLayout.cs` — DSL surface (factories).
- `AppGen/Node.cs`, `AppGen/AppGenTypes.cs` — node types and modifiers.
- `AppGen/Editor/AppGenerator.cs` — discovery, validation, partial-write, queue-and-recompile.
- `AppGen/Editor/PrefabBuilder.cs` — walks the node tree, builds the GameObject hierarchy, wires `Udon` listeners, saves the prefab.
- `AppGen/Editor/AppGenScaffold.cs` — `DMNware/Create New App…` wizard.
- `AppGen/Editor/AppGenMenu.cs` — menu items.
- `AppGen/DmnwareTheme.cs`, `DmnwareAppGenAssets.cs`, `DmnwareAppGenContext.cs` — theme colors, icon registry, scene-side context.
