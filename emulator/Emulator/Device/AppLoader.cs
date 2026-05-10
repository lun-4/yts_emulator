// Reflection-driven discovery: find every type with [DmnwareApp], pair it with its
// matching <Name>Layout type, instantiate both, and produce a slot the EmulatorDevice
// can boot.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DMNware.AppGen;

namespace YtsEmulator.Device;

public class AppSlot
{
    public string Name;
    public bool HandheldOnly;
    public bool VrOnly;
    public bool HiddenFromUser;
    public Type LayoutType;
    public Type BehaviourType;
    public DmnwareApp Meta = new();      // populated at boot
    public object Behaviour;             // <App> instance
    public Layout.LayoutEngine Engine;
    public Layout.Materialized Root;
}

public static class AppLoader
{
    public static List<AppSlot> Discover()
    {
        var slots = new List<AppSlot>();
        foreach (var t in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes))
        {
            if (!typeof(AppLayout).IsAssignableFrom(t)) continue;
            if (t.IsAbstract) continue;
            var attr = t.GetCustomAttribute<DmnwareAppAttribute>();
            if (attr == null) continue;

            // Layout class name convention: <App>Layout. Strip the suffix to find the behaviour.
            string baseName = t.Name.EndsWith("Layout") ? t.Name[..^"Layout".Length] : t.Name;
            var behaviourType = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(x => x.Name == baseName && !x.IsAbstract);
            if (behaviourType == null)
            {
                Console.WriteLine($"[loader] layout '{t.Name}' has no matching behaviour '{baseName}', skipping");
                continue;
            }

            slots.Add(new AppSlot
            {
                Name = attr.Name,
                HandheldOnly = attr.HandheldOnly,
                VrOnly = attr.VrOnly,
                HiddenFromUser = attr.HiddenFromUser,
                LayoutType = t,
                BehaviourType = behaviourType,
            });
        }
        return slots;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
    }
}
