// Per-frame V/H layout. Runs each tick on every container whose layout-group component is
// enabled. When disabled (the FlappyBird trick), child positions are left to RectTransform
// writes from app code via RectTransformBinding.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DMNware.AppGen;
using UnityEngine;
using UnityUI = UnityEngine.UI;

namespace YtsEmulator.Layout;

public static class LayoutPass
{
    // The screen is a fixed 1080x1670 native canvas — every container starts with that
    // available size for the root, and recurses with each child's allocated rect.
    public const double NativeW = 1080;
    public const double NativeH = 1670;

    public static void Run(Materialized root, double width, double height)
    {
        ComputePreferred(root);
        Layout(root, 0, 0, width, height);
    }

    private static void ComputePreferred(Materialized m)
    {
        foreach (var c in m.Children) ComputePreferred(c);

        var mods = m.Mods;
        switch (m.Source)
        {
            case TextNode t:
                m.PreferredW = double.NaN;
                m.PreferredH = Theme.TypographySize(t.Style) * 1.4;
                break;
            case ButtonNode:
                m.PreferredW = double.NaN;
                m.PreferredH = Theme.ButtonHeight;
                break;
            case SliderNode:
                m.PreferredW = double.NaN;
                m.PreferredH = Theme.SliderHeight + 28;   // label above
                break;
            case ToggleNode:
                m.PreferredW = Theme.ToggleWidth + 220;
                m.PreferredH = Theme.ToggleHeight + 8;
                break;
            case DividerNode:
                m.PreferredW = double.NaN;
                m.PreferredH = Theme.DividerThickness;
                break;
            case SpacerNode sp:
                if (sp.IsFlex)
                {
                    m.PreferredW = 0; m.PreferredH = 0;
                }
                else
                {
                    m.PreferredW = sp.FixedWidth;
                    m.PreferredH = sp.FixedHeight;
                }
                break;
            case ImageNode:
                m.PreferredW = double.NaN;
                m.PreferredH = double.NaN;
                break;
            case ContainerNode cn:
                // Sum children for preferred size.
                bool vert = m.IsVertical;
                double main = 0, cross = 0;
                int n = m.Children.Count;
                for (int i = 0; i < n; i++)
                {
                    var ch = m.Children[i];
                    var pmW = isFinite(ch.PreferredW) ? ch.PreferredW : 0;
                    var pmH = isFinite(ch.PreferredH) ? ch.PreferredH : 0;
                    if (vert) { main += pmH; cross = Math.Max(cross, pmW); }
                    else { main += pmW; cross = Math.Max(cross, pmH); }
                }
                if (n > 1) main += (n - 1) * cn.Spacing;
                main += vert ? (cn.InnerPadding.T + cn.InnerPadding.B) : (cn.InnerPadding.L + cn.InnerPadding.R);
                cross += vert ? (cn.InnerPadding.L + cn.InnerPadding.R) : (cn.InnerPadding.T + cn.InnerPadding.B);
                if (vert) { m.PreferredW = cross; m.PreferredH = main; }
                else { m.PreferredW = main; m.PreferredH = cross; }
                break;
        }

        // Modifier overrides take precedence.
        if (mods.Width.Mode == SizeMode.Fixed) m.PreferredW = mods.Width.Value;
        if (mods.Height.Mode == SizeMode.Fixed) m.PreferredH = mods.Height.Value;
    }

    private static bool isFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    // x/y/w/h are the rect allocated to `m` inside its parent's Canvas (Avalonia coords:
    // top-left origin, y grows down).
    private static void Layout(Materialized m, double x, double y, double w, double h)
    {
        Canvas.SetLeft(m.Ctl, x);
        Canvas.SetTop(m.Ctl, y);
        m.Ctl.Width = w;
        m.Ctl.Height = h;

        // Mirror the allocated rect onto the mock RectTransform so app code that reads
        // anchoredPosition/sizeDelta sees something consistent before it writes.
        var rt = (RectTransform)m.GO.transform;
        // Quiet write to avoid retriggering an OnChanged loop.
        SetRectQuiet(rt, w, h, x + w * 0.5, h + 0); // size only; we don't compute Unity-coord anchor here

        if (m.LayoutGroup is { enabled: true } && m.Children.Count > 0)
            LayoutContainer(m, w, h);
    }

    private static void SetRectQuiet(RectTransform rt, double w, double h, double cx, double cy)
    {
        // Apps that never disable the layout group don't read RectTransform; we write sizeDelta
        // for completeness but don't trigger OnChanged loops. (Use the field-backing path —
        // accessed via reflection to avoid the public setter's OnChanged.)
        var f = typeof(RectTransform).GetField("_sizeDelta",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null) f.SetValue(rt, new Vector2((float)w, (float)h));
    }

    private static void LayoutContainer(Materialized m, double w, double h)
    {
        var cn = (ContainerNode)m.Source;
        bool vert = m.IsVertical;
        var pad = cn.InnerPadding;

        double inX = pad.L;
        double inY = pad.T;
        double inW = Math.Max(0, w - pad.L - pad.R);
        double inH = Math.Max(0, h - pad.T - pad.B);

        double mainAvail = vert ? inH : inW;
        double crossAvail = vert ? inW : inH;

        // 1) Total fixed main-axis size + flex weight.
        double fixedMain = 0;
        double totalFlexWeight = 0;
        var n = m.Children.Count;
        for (int i = 0; i < n; i++)
        {
            var ch = m.Children[i];
            double weight = ch.Mods.FlexWeight;
            // SpacerNode with IsFlex carries weight on Mods.FlexWeight — already covered.
            if (weight > 0)
            {
                totalFlexWeight += weight;
            }
            else
            {
                double pm = vert ? ch.PreferredH : ch.PreferredW;
                if (!isFinite(pm)) pm = 0;
                fixedMain += pm;
            }
        }
        if (n > 1) fixedMain += (n - 1) * cn.Spacing;
        double remaining = Math.Max(0, mainAvail - fixedMain);

        // 2) Place children sequentially along main axis; cross axis follows Align.
        double cursor = vert ? inY : inX;
        for (int i = 0; i < n; i++)
        {
            var ch = m.Children[i];
            double weight = ch.Mods.FlexWeight;
            double mainSize;
            if (weight > 0 && totalFlexWeight > 0)
            {
                mainSize = remaining * (weight / totalFlexWeight);
            }
            else
            {
                mainSize = vert ? ch.PreferredH : ch.PreferredW;
                if (!isFinite(mainSize)) mainSize = 0;
            }

            double crossSize = vert ? ch.PreferredW : ch.PreferredH;
            // Stretch fills the cross axis; otherwise honour preferred (or full when NaN).
            if (cn.Align == DMNware.AppGen.Align.Stretch || !isFinite(crossSize))
                crossSize = crossAvail;
            else if (crossSize > crossAvail) crossSize = crossAvail;

            double crossOffset = cn.Align switch
            {
                DMNware.AppGen.Align.Center => (crossAvail - crossSize) * 0.5,
                DMNware.AppGen.Align.End => crossAvail - crossSize,
                _ => 0,
            };

            double childX, childY, childW, childH;
            if (vert)
            {
                childX = inX + crossOffset;
                childY = cursor;
                childW = crossSize;
                childH = mainSize;
            }
            else
            {
                childX = cursor;
                childY = inY + crossOffset;
                childW = mainSize;
                childH = crossSize;
            }

            Layout(ch, childX, childY, childW, childH);
            cursor += mainSize + cn.Spacing;
        }
    }
}
