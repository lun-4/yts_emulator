// The single Avalonia window. Top toolbar (orientation/handheld/pickup + home), a scaled-down
// 1080x1670 screen below it, and a notification overlay layered on top.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using YtsEmulator.Layout;

namespace YtsEmulator.Device;

public class MainWindow : Window
{
    private const double ScreenScale = 0.42;       // 1080 * 0.42 ≈ 454, 1670 * 0.42 ≈ 700
    private readonly Canvas _screen;               // native-resolution canvas (1080x1670)
    private readonly StackPanel _notifStack;
    private readonly Button _homeButton;
    private readonly EmulatorDevice _device;
    private DispatcherTimer _frameTimer;
    private DateTime _lastTick = DateTime.UtcNow;

    public MainWindow()
    {
        Title = "YTS Emulator";
        Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0B, 0x10));

        // Native screen surface. A second wrapper applies the visual scale.
        _screen = new Canvas
        {
            Width = LayoutPass.NativeW,
            Height = LayoutPass.NativeH,
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x06, 0x09)),
            ClipToBounds = true,
        };
        var screenWrap = new Border
        {
            Width = LayoutPass.NativeW * ScreenScale,
            Height = LayoutPass.NativeH * ScreenScale,
            CornerRadius = new CornerRadius(48),
            ClipToBounds = true,
            Background = Brushes.Black,
            Child = new LayoutTransformControl
            {
                LayoutTransform = new ScaleTransform(ScreenScale, ScreenScale),
                Child = _screen,
            },
        };

        _notifStack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 0, 0),
            IsHitTestVisible = false,
        };

        var screenLayer = new Panel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { screenWrap, _notifStack },
        };

        _homeButton = new Button
        {
            Content = "← Home",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x20, 0x22, 0x2A)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 12),
            IsVisible = false,
        };

        // Construct the device and wire chrome callbacks.
        _device = new EmulatorDevice(
            _screen,
            onOpenChanged: () => _homeButton.IsVisible = _device.OpenAppIdx != -1,
            notify: ShowNotification);

        _homeButton.Click += (_, _) => _device.ShowHome();

        var handheldToggle = new ToggleSwitch { OnContent = "Handheld", OffContent = "Handheld" };
        handheldToggle.IsCheckedChanged += (_, _) => _device.SetHandheld(handheldToggle.IsChecked == true);
        var landscapeToggle = new ToggleSwitch { OnContent = "Landscape", OffContent = "Landscape" };
        landscapeToggle.IsCheckedChanged += (_, _) => _device.SetLandscape(landscapeToggle.IsChecked == true);
        var pickupBtn = new Button
        {
            Content = "Pickup",
            Padding = new Thickness(16, 6),
            Margin = new Thickness(8, 0, 0, 0),
        };
        pickupBtn.Click += (_, _) => _device.Pickup();

        var toolbar = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(16, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { handheldToggle, landscapeToggle, pickupBtn },
        };

        var rootStack = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { toolbar, screenLayer, _homeButton },
        };
        Content = new ScrollViewer { Content = rootStack };
        Width = LayoutPass.NativeW * ScreenScale + 80;
        Height = LayoutPass.NativeH * ScreenScale + 200;

        Opened += OnOpened;
    }

    private void OnOpened(object sender, EventArgs e)
    {
        _device.Boot();
        _device.DoInitialLayoutPasses();

        _frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _frameTimer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            var dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;
            _device.Tick(dt);
        };
        _lastTick = DateTime.UtcNow;
        _frameTimer.Start();
    }

    private void ShowNotification(string source, string message, float timeoutSec)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x20, 0x22, 0x2A)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 10),
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Children =
                {
                    new TextBlock { Text = source, Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 14 },
                    new TextBlock { Text = message, Foreground = Brushes.LightGray, FontSize = 13 },
                },
            },
        };
        _notifStack.Children.Insert(0, border);
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timeoutSec) };
        t.Tick += (_, _) =>
        {
            _notifStack.Children.Remove(border);
            t.Stop();
        };
        t.Start();
    }
}
