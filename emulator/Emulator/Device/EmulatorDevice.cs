// The runtime "phone": discovers apps, builds their UI trees, runs lifecycle events,
// owns the screen swap on app open/close. Mirrors the surface of Runtime/DmnwareDevice.cs
// that apps depend on — IsCurrentlyHandheld, _OpenApp, etc. — without dragging in VRC SDK.

using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media;
using DMNware.AppGen;
using UnityEngine;
using YtsEmulator.Layout;

namespace YtsEmulator.Device;

public class EmulatorDevice
{
    public List<AppSlot> Apps { get; } = new();
    public int OpenAppIdx { get; private set; } = -1;

    public bool IsCurrentlyHandheld;
    public bool IsCurrentlyLandscape;
    public bool IsCurrentlyLandscapeU;

    public DmnwareDevice DeviceMock { get; private set; }

    // Single root canvas the chrome embeds; we swap children to switch screens.
    public Canvas ScreenCanvas { get; }

    private Control _homeContent;
    private Action _onOpenChanged;
    private Action<string, string, float> _notify;

    public EmulatorDevice(Canvas screenCanvas, Action onOpenChanged, Action<string, string, float> notify)
    {
        ScreenCanvas = screenCanvas;
        _onOpenChanged = onOpenChanged;
        _notify = notify;

        DeviceMock = new DmnwareDevice
        {
            ForwardOpenApp = a => _OpenApp(a),
            ForwardOpenAppByIdx = i => _OpenAppByIdx(i),
            ForwardLaunchNotification = (src, msg, _, t) => _notify(src, msg, t),
            ForwardLaunchNotificationFromApp = (app, msg, t, sender, _) =>
                _notify(string.IsNullOrEmpty(sender) ? app.Name : sender, msg, t),
        };
    }

    public void Boot()
    {
        var slots = AppLoader.Discover();
        Console.WriteLine("[YTS] enumerating apps");
        foreach (var s in slots)
        {
            Console.WriteLine($"[YTS] preparing app {s.Name}");
            s.Behaviour = Activator.CreateInstance(s.BehaviourType);
            s.Meta = new DmnwareApp { Name = s.Name, Validated = true, HandheldOnly = s.HandheldOnly, VROnly = s.VrOnly };

            // Inject Device + App on the behaviour (matches DmnwareDevice.SetProgramVariable wiring).
            InjectField(s.Behaviour, "Device", DeviceMock);
            InjectField(s.Behaviour, "App", s.Meta);

            // Instantiate the layout, build the node tree.
            var layout = (AppLayout)Activator.CreateInstance(s.LayoutType);
            var tree = layout.Build();
            if (tree == null) throw new InvalidOperationException($"[loader] {s.Name}.Build() returned null");

            // Materialize.
            s.Engine = new LayoutEngine(s.Behaviour);
            s.Root = s.Engine.Build(tree);
            // Root container fills the entire native screen.
            s.Root.Ctl.Width = LayoutPass.NativeW;
            s.Root.Ctl.Height = LayoutPass.NativeH;
            Canvas.SetLeft(s.Root.Ctl, 0);
            Canvas.SetTop(s.Root.Ctl, 0);
            // Bind RectTransform writes once the tree exists and parent sizes are known.
            RectTransformBinding.BindAll(s.Root);

            Apps.Add(s);
            DispatchLifecycle(s, "_DmnwareAppInit");
        }
        DeviceMock.Apps = new DmnwareApp[Apps.Count];
        for (int i = 0; i < Apps.Count; i++) DeviceMock.Apps[i] = Apps[i].Meta;

        foreach (var s in Apps)
        {
            Console.WriteLine($"[YTS] late-init {s.Name}");
            DispatchLifecycle(s, "_DmnwareAppLateInit");
        }

        // Build home screen content as a Canvas full of app tiles.
        _homeContent = HomeScreen.Build(Apps, _OpenAppByIdx);
        Canvas.SetLeft(_homeContent, 0);
        Canvas.SetTop(_homeContent, 0);
        if (_homeContent is Avalonia.Layout.Layoutable l) { l.Width = LayoutPass.NativeW; l.Height = LayoutPass.NativeH; }
        ShowHome();
    }

    private static void InjectField(object instance, string name, object value)
    {
        var f = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        f?.SetValue(instance, value);
    }

    public void ShowHome()
    {
        if (OpenAppIdx != -1)
        {
            DispatchLifecycle(Apps[OpenAppIdx], "_DmnwareAppClose");
            ScreenCanvas.Children.Remove(Apps[OpenAppIdx].Root.Ctl);
        }
        ScreenCanvas.Children.Clear();
        ScreenCanvas.Children.Add(_homeContent);
        OpenAppIdx = -1;
        DeviceMock.OpenAppIdx = -1;
        _onOpenChanged?.Invoke();
    }

    public void _OpenApp(DmnwareApp app)
    {
        for (int i = 0; i < Apps.Count; i++) if (Apps[i].Meta == app) { _OpenAppByIdx(i); return; }
    }

    public void _OpenAppByIdx(int idx)
    {
        if (idx < 0 || idx >= Apps.Count) return;
        var s = Apps[idx];
        Console.WriteLine($"[YTS] opening {s.Name}");
        if (s.HandheldOnly && !IsCurrentlyHandheld)
        {
            _notify(s.Name, "App can only be used in handheld mode", 2.5f);
            return;
        }

        if (OpenAppIdx != -1)
        {
            DispatchLifecycle(Apps[OpenAppIdx], "_DmnwareAppClose");
            ScreenCanvas.Children.Remove(Apps[OpenAppIdx].Root.Ctl);
        }
        ScreenCanvas.Children.Clear();
        ScreenCanvas.Children.Add(s.Root.Ctl);
        OpenAppIdx = idx;
        DeviceMock.OpenAppIdx = idx;
        DispatchLifecycle(s, "_DmnwareAppOpen");
        _onOpenChanged?.Invoke();
    }

    public void SetHandheld(bool v)
    {
        IsCurrentlyHandheld = v;
        DeviceMock.IsCurrentlyHandheld = v;
        if (OpenAppIdx != -1) DispatchLifecycle(Apps[OpenAppIdx], "_DmnwareAppHandheldModeChange");
    }

    public void SetLandscape(bool v)
    {
        IsCurrentlyLandscape = v;
        DeviceMock.IsCurrentlyLandscape = v;
        if (OpenAppIdx != -1) DispatchLifecycle(Apps[OpenAppIdx], "_DmnwareAppOrientationChange");
    }

    public void Pickup()
    {
        if (OpenAppIdx != -1) DispatchLifecycle(Apps[OpenAppIdx], "_DmnwareAppPickupTrigger");
    }

    public void Tick(double dtSeconds)
    {
        UnityEngine.Time.deltaTime = (float)dtSeconds;
        UnityEngine.Time.time += (float)dtSeconds;
        UnityEngine.Time.frameCount++;

        // Run Update() on the open app's behaviour (FlappyBird needs this).
        if (OpenAppIdx != -1)
        {
            var s = Apps[OpenAppIdx];
            var m = s.BehaviourType.GetMethod("Update",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            try { m?.Invoke(s.Behaviour, null); }
            catch (TargetInvocationException ex) { Console.WriteLine($"[update {s.Name}] {ex.InnerException}"); }

            // Layout pass for the open app — runs each frame so V/H groups (when enabled) reflow.
            try { LayoutPass.Run(s.Root, LayoutPass.NativeW, LayoutPass.NativeH); }
            catch (Exception ex) { Console.WriteLine($"[layoutpass {s.Name}] {ex}"); }
        }

        UdonSharp.EmulatorTick.Drain();
    }

    public void DoInitialLayoutPasses()
    {
        // Run a one-shot layout pass per app so its preferred-size + initial flow is computed
        // BEFORE _DmnwareAppInit fires (Counter etc. read the slider/toggle initial value at init).
        // Already handled: LayoutPass.Run is called in Tick; we additionally run it once after build.
        foreach (var s in Apps)
            try { LayoutPass.Run(s.Root, LayoutPass.NativeW, LayoutPass.NativeH); }
            catch (Exception ex) { Console.WriteLine($"[initial-layoutpass {s.Name}] {ex}"); }
    }

    private void DispatchLifecycle(AppSlot s, string method)
    {
        var m = s.BehaviourType.GetMethod(method,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        try { m?.Invoke(s.Behaviour, null); }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine($"[lifecycle {s.Name}.{method}] {ex.InnerException}");
        }
    }
}
