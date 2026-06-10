# YTS Emulator

A desktop harness for developing **DMNware** apps — the app platform that runs on the
YTS in-world handheld/VR "phone" (VRChat + UdonSharp) — **without** Unity or VRChat.

The real device is a `UdonSharpBehaviour` tangled into the VRC SDK, so you normally need a
full Unity project to see an app render. This emulator instead provides small mock
implementations of the Unity / TMPro / UdonSharp / VRC types the apps actually touch
(`emulator/Emulator/Mocks/`) and renders the UI with [Avalonia](https://avaloniaui.net/).
The DSL and the app sources are compiled straight from this repo — the same `.cs` files that
ship in the Unity project — so what you see in the emulator is what the device builds.

## What's in the repo

| Path | What it is |
| --- | --- |
| `AppGen/` | The layout DSL (`Node`, `AppLayout`, `[DmnwareApp]`) and its Unity-editor codegen |
| `Runtime/` | Runtime types apps depend on (`DmnwareApp`, `DmnwareDevice`, …) |
| `Counter/`, `Calculator/`, `FlappyBird/`, `Minesweeper/` | Sample apps (behaviour + `.layout.cs` + generated `.gen.cs`) |
| `emulator/` | This harness — the Avalonia app that mocks Unity and runs the apps |
| `DMNware-AppGen-Authoring-Guide.md` | How to author an app with the DSL |

The harness compiles the DSL, the runtime, and the sample apps into itself — see the
`<Compile Include="..\..\…">` entries in `emulator/Emulator/Emulator.csproj`.

## Requirements

- A **.NET 10 SDK** (the project targets `net10.0`).
- A graphical display (X11 or Wayland). Avalonia's desktop backend needs a live graphics
  connection; with none it will fail to open a window rather than run headless.

You do **not** need Unity, the VRChat SDK, or any of their packages.

## Install the .NET SDK

If you already have a .NET 10 SDK on your `PATH`, skip this.

Otherwise use the vendored Microsoft installer to drop one in your home directory (no root,
no system change):

```sh
./emulator/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"      # add to your shell profile to make it stick
```

Verify:

```sh
dotnet --version    # should print a 10.x version
```

> fish shell: `set -gx PATH $HOME/.dotnet $PATH`

## Run the emulator

From the repo root:

```sh
dotnet run --project emulator/Emulator
```

First run restores NuGet packages (Avalonia) and compiles the DSL + apps, so it takes a
moment. A window titled **"YTS Emulator"** opens showing the home screen with one tile per
discovered app.

### Using the window

- **App tiles** — click one to open that app; the **← Home** button returns to the launcher.
- **Handheld** toggle — simulates the device being held in handheld mode. Apps marked
  handheld-only refuse to open (and notify) unless this is on.
- **Landscape** toggle — flips orientation; apps get an orientation-change lifecycle call.
- **Pickup** button — fires the device "pickup" trigger on the open app.
- Notifications raised by apps appear as toasts over the top of the screen.

The screen is the device's native 1080×1670 surface, scaled down to fit the window.

## How app discovery works

On boot the harness reflects over every loaded type, finds each `AppLayout` subclass
carrying a `[DmnwareApp]` attribute, and pairs it with its behaviour class by name
convention: a layout named `FooLayout` is matched to a behaviour named `Foo`. Both are
instantiated, the layout's node tree is materialized into Avalonia controls, fields marked
`[SerializeField]` are wired to the matching nodes by id, and the standard lifecycle
(`_DmnwareAppInit`, `_DmnwareAppLateInit`, `_DmnwareAppOpen`, …) is dispatched.

### Adding another app to the harness

Add its sources to the compile list in `emulator/Emulator/Emulator.csproj` alongside the
existing apps:

```xml
<Compile Include="..\..\MyApp\MyApp.cs"        Link="Apps\MyApp\MyApp.cs" />
<Compile Include="..\..\MyApp\MyApp.gen.cs"    Link="Apps\MyApp\MyApp.gen.cs" />
<Compile Include="..\..\MyApp\MyApp.layout.cs" Link="Apps\MyApp\MyApp.layout.cs" />
```

It will show up on the home screen automatically (unless its `[DmnwareApp]` marks it
`HiddenFromUser`).

## Known limitations

The mock layer covers only what the sample apps exercise — it is not a faithful Unity
reimplementation. In particular:

- `Image` renders as a solid color rectangle; sprite/asset-key support is not implemented.
- A `Raw` node renders as a magenta placeholder.
- The mocked Unity surface (`UnityCore.cs`, `UnityUI.cs`) only includes types and members the
  in-repo apps touch; new apps using other Unity APIs may need the mocks extended.
