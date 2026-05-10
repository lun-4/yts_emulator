// One node in the materialized tree: source DSL Node + Avalonia control + mock GameObject.
// Both LayoutEngine (build) and LayoutPass (per-frame layout) operate on this.

using System.Collections.Generic;
using Avalonia.Controls;
using DMNware.AppGen;
using UnityEngine;
using UnityEngine.UI;

namespace YtsEmulator.Layout;

public class Materialized
{
    public Node Source;
    public GameObject GO;
    public Control Ctl;            // The Avalonia control sitting in the parent Canvas.
    public Materialized Parent;
    public List<Materialized> Children = new();

    // Container-only: which layout-group mock sits on GO. Null for leaves.
    // When this is non-null and `LayoutGroup.enabled` is true, LayoutPass positions the
    // children each frame; when disabled (FlappyBird trick), RectTransform writes win.
    public Behaviour LayoutGroup;
    public bool IsVertical;

    // Cached preferred size, computed by LayoutEngine; LayoutPass reads.
    public double PreferredW;
    public double PreferredH;

    public Modifiers Mods => Source.Mods;
}
