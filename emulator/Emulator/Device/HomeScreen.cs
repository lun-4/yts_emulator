// Renders the app launcher grid. Plain Avalonia controls — no DSL involvement.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using YtsEmulator.Layout;

namespace YtsEmulator.Device;

public static class HomeScreen
{
    public static Control Build(IReadOnlyList<AppSlot> apps, Action<int> onOpen)
    {
        var grid = new UniformGrid
        {
            Columns = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 60, 0, 0),
        };

        for (int i = 0; i < apps.Count; i++)
        {
            int idx = i;
            var slot = apps[i];
            if (slot.HiddenFromUser) continue;

            var tile = new Button
            {
                Width = 240,
                Height = 240,
                Margin = new Thickness(20),
                Background = new SolidColorBrush(Theme.Surface),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(36),
                Padding = new Thickness(0),
                Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Vertical,
                    Spacing = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new Border
                        {
                            Width = 110, Height = 110,
                            CornerRadius = new CornerRadius(28),
                            Background = new SolidColorBrush(Theme.Primary),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = IconGlyph(slot.Name),
                                Foreground = new SolidColorBrush(Theme.OnPrimary),
                                FontSize = 60,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                            },
                        },
                        new TextBlock
                        {
                            Text = slot.Name,
                            Foreground = new SolidColorBrush(Theme.Text),
                            FontSize = 32,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
            };
            tile.Click += (_, _) => onOpen(idx);
            grid.Children.Add(tile);
        }

        var bg = new Border
        {
            Background = new SolidColorBrush(Theme.Background),
            Width = LayoutPass.NativeW,
            Height = LayoutPass.NativeH,
            Child = grid,
        };
        return bg;
    }

    private static string IconGlyph(string appName) => appName switch
    {
        "Counter" => "#",
        "Calculator" => "=",
        "Flappy" => "~",
        _ => appName.Length > 0 ? appName[0].ToString() : "?",
    };
}
