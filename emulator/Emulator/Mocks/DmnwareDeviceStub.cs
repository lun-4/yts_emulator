// Stand-in for the real Runtime/DmnwareDevice.cs (which is too tangled with VRC SDK +
// NatLoggerUser to compile here). Exposes the same public surface apps see, and forwards
// to the emulator's EmulatorDevice singleton via callbacks set at construction time.

using System;
using UdonSharp;
using UnityEngine;

public class DmnwareDevice : UdonSharpBehaviour
{
    public bool IsCurrentlyHandheld;
    public bool IsCurrentlyLandscape;
    public bool IsCurrentlyLandscapeU;
    public bool VibrationEnabled = true;
    public bool Ready;
    public int OpenAppIdx = -1;
    public DmnwareApp[] Apps;

    public Action<DmnwareApp> ForwardOpenApp;
    public Action<int> ForwardOpenAppByIdx;
    public Action<string, string, Texture2D, float> ForwardLaunchNotification;
    public Action<DmnwareApp, string, float, string, Texture> ForwardLaunchNotificationFromApp;

    public void _OpenApp(DmnwareApp app) => ForwardOpenApp?.Invoke(app);
    public void _OpenAppByIdx(int idx) => ForwardOpenAppByIdx?.Invoke(idx);

    public void _LaunchNotification(string source, string message, Texture2D icon, float timeout = 2.5f)
        => ForwardLaunchNotification?.Invoke(source, message, icon, timeout);

    public void _LaunchNotificationFromApp(DmnwareApp app, string message, float timeout = 2.5f,
        string custom_sender = "", Texture custom_icon = null)
        => ForwardLaunchNotificationFromApp?.Invoke(app, message, timeout, custom_sender, custom_icon);
}
