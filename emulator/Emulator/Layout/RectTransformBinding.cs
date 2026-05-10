// Plumbs writes to the mock RectTransform (anchoredPosition, sizeDelta, anchors, pivot,
// localEulerAngles) onto the backing Avalonia control's Canvas.Left/Top/Width/Height +
// RenderTransform. This is the ONLY thing that makes FlappyBird's per-frame manual
// positioning visible on screen.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using UnityEngine;

namespace YtsEmulator.Layout;

public static class RectTransformBinding
{
    // Bind every materialized node: when its RectTransform mutates, recompute the Avalonia
    // rect using the parent container's current size.
    public static void BindAll(Materialized root)
    {
        Walk(root);
    }

    private static void Walk(Materialized m)
    {
        if (m.Parent != null) Bind(m);
        foreach (var c in m.Children) Walk(c);
    }

    private static void Bind(Materialized m)
    {
        var rt = (RectTransform)m.GO.transform;
        // Capture parent so the closure can read its current width/height each tick.
        var parent = m.Parent;
        var ctl = m.Ctl;

        rt.OnChanged = () =>
        {
            // If the parent's layout group is enabled, the LayoutPass owns positioning —
            // app writes here are noise (matches Unity behaviour). If disabled, we propagate.
            if (parent.LayoutGroup is { enabled: true }) return;

            double parentW = parent.Ctl.Bounds.Width;
            double parentH = parent.Ctl.Bounds.Height;
            // First-tick fallback if Bounds aren't measured yet.
            if (parentW <= 0) parentW = parent.Ctl.Width;
            if (parentH <= 0) parentH = parent.Ctl.Height;
            if (parentW <= 0) parentW = LayoutPass.NativeW;
            if (parentH <= 0) parentH = LayoutPass.NativeH;

            // Anchor position in parent (Avalonia coords).
            double anchorX = parentW * rt.anchorMin.x;
            double anchorY = parentH * (1 - rt.anchorMin.y);   // Unity y=0 is parent bottom

            // Pivot position in parent (Avalonia coords): apply anchored offset (Unity y up).
            double pivotX = anchorX + rt.anchoredPosition.x;
            double pivotY = anchorY - rt.anchoredPosition.y;

            double w = rt.sizeDelta.x;
            double h = rt.sizeDelta.y;

            double left = pivotX - w * rt.pivot.x;
            // pivot.y = 0 means pivot at top of rect in Unity = bottom of rect in Avalonia.
            double top = pivotY - h * (1 - rt.pivot.y);

            Canvas.SetLeft(ctl, left);
            Canvas.SetTop(ctl, top);
            ctl.Width = w;
            ctl.Height = h;

            // Z rotation: Unity is CCW-positive, Avalonia RotateTransform is CW-positive.
            float rz = rt.localEulerAngles.z;
            if (rz != 0f)
            {
                ctl.RenderTransform = new RotateTransform(-rz);
                ctl.RenderTransformOrigin = new RelativePoint(rt.pivot.x, 1 - rt.pivot.y, RelativeUnit.Relative);
            }
            else if (ctl.RenderTransform != null)
            {
                ctl.RenderTransform = null;
            }
        };
    }
}
