using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DMNware.AppGen.Editor
{
    /// Top-level pipeline for generating DMNware apps from layout source files.
    ///
    /// Two-phase build:
    ///   Phase 1: validate, write all .gen.cs partials, queue prefab builds in SessionState,
    ///            trigger AssetDatabase.Refresh() to recompile.
    ///   Phase 2: after recompile (DidReloadScripts), drain the queue and build prefabs.
    ///
    /// If no partials need to change, phase 1 also builds prefabs immediately and skips the queue.
    public static class AppGenerator
    {
        private const string SessionQueueKey = "DMNware.AppGen.PendingApps";

        // ============================================================
        // Public entry points (called by AppGenMenu)
        // ============================================================

        public static void GenerateAll()
        {
            // Force Unity to import any unimported scripts before reflecting.
            AssetDatabase.Refresh();

            var infos = DiscoverLayouts();
            if (infos.Count == 0)
            {
                // Diagnose why nothing was found.
                int appLayoutSubclasses = 0;
                int withAttribute = 0;
                var sampleNames = new List<string>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        if (!typeof(AppLayout).IsAssignableFrom(t)) continue;
                        if (t == typeof(AppLayout) || t.IsAbstract) continue;
                        appLayoutSubclasses++;
                        if (sampleNames.Count < 5) sampleNames.Add(t.FullName);
                        if (t.GetCustomAttribute<DmnwareAppAttribute>() != null) withAttribute++;
                    }
                }

                if (appLayoutSubclasses == 0)
                {
                    Debug.Log("[AppGen] No [DmnwareApp] layouts found. " +
                        "Reflection found ZERO classes inheriting AppLayout. " +
                        "Check: (1) your .layout.cs file is visible in the Project window, " +
                        "(2) the class extends DMNware.AppGen.AppLayout, " +
                        "(3) Unity has compiled with no errors (check the console). " +
                        "If you just saved the file, wait for compile to finish then try again.");
                }
                else
                {
                    Debug.Log($"[AppGen] No [DmnwareApp] layouts found. " +
                        $"Found {appLayoutSubclasses} AppLayout subclass(es) ({string.Join(", ", sampleNames)}) but {withAttribute} have the [DmnwareApp] attribute. " +
                        "Add `[DmnwareApp(name: \"…\")]` above the class declaration.");
                }
                return;
            }
            RunPhase1(infos);
        }

        public static void GenerateOne(Type layoutType)
        {
            var info = TryReflectLayout(layoutType);
            if (info == null) return;
            RunPhase1(new List<LayoutInfo> { info });
        }

        // ============================================================
        // Phase 1 — validate, write partials, queue/build
        // ============================================================

        private static void RunPhase1(List<LayoutInfo> infos)
        {
            var theme = DmnwareTheme.LoadOrCreate();
            var registry = DmnwareAppGenAssets.LoadOrCreate();
            var context = DmnwareAppGenContext.FindInScene();
            if (context == null)
                Debug.LogWarning("[AppGen] No DmnwareAppGenContext found in the open scene. " +
                    "Add the component to a scene GameObject (e.g. on the tablet) and populate ClickAudioSource / PickupCustomization / NatLogger. " +
                    "Generation will continue but interactives may misbehave (no click sound, slider drags log NRE, input fields don't force-drop).");

            int written = 0;
            int errored = 0;
            var pendingPrefabs = new List<string>(); // class names

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var info in infos)
                {
                    var (errors, fields) = Validate(info);
                    if (errors.Count > 0)
                    {
                        errored++;
                        foreach (var e in errors) Debug.LogError($"[AppGen] {info.LayoutType.Name}: {e}");
                        continue;
                    }

                    // Render and write partial.
                    var content = PartialWriter.Render(info.AppClassName, Path.GetFileName(info.AppFolder + "/" + info.AppClassName + ".layout.cs"), fields);
                    var partialPath = Path.Combine(info.AppFolder, info.AppClassName + ".gen.cs");
                    bool changed = PartialWriter.WriteIfChanged(partialPath, content);
                    if (changed) { written++; AssetDatabase.ImportAsset(partialPath); }

                    pendingPrefabs.Add(info.LayoutType.AssemblyQualifiedName);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (errored > 0)
                Debug.LogError($"[AppGen] {errored} app(s) errored. Successful apps will still build.");

            if (pendingPrefabs.Count == 0) return;

            if (written > 0)
            {
                // Queue and refresh — phase 2 runs after recompile.
                SessionState.SetString(SessionQueueKey, string.Join("|", pendingPrefabs));
                Debug.Log($"[AppGen] Wrote {written} partial(s). Recompiling, then building prefabs...");
                AssetDatabase.Refresh();
                return;
            }

            // No partials changed — build prefabs immediately.
            BuildPrefabsForTypes(pendingPrefabs, theme, registry, context);
        }

        // ============================================================
        // Phase 2 — runs after Unity recompiles
        // ============================================================

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            var queue = SessionState.GetString(SessionQueueKey, "");
            if (string.IsNullOrEmpty(queue)) return;
            SessionState.EraseString(SessionQueueKey);

            // Force U# to finish rebaking program assets and run its scene fixup
            // before we touch the scene / SaveAssets. Without this, our reload
            // callback can race U#'s, leaving every UdonBehaviour in the scene
            // momentarily showing a missing program-asset reference.
            try { UdonSharpCompilerV1.CompileSync(); }
            catch (Exception ex) { Debug.LogWarning($"[AppGen] U# CompileSync threw, continuing anyway: {ex.Message}"); }

            var theme = DmnwareTheme.LoadOrCreate();
            var registry = DmnwareAppGenAssets.LoadOrCreate();
            var context = DmnwareAppGenContext.FindInScene();
            if (context == null)
                Debug.LogWarning("[AppGen] No DmnwareAppGenContext found in the open scene. " +
                    "Add the component to a scene GameObject (e.g. on the tablet) and populate ClickAudioSource / PickupCustomization / NatLogger. " +
                    "Generation will continue but interactives may misbehave (no click sound, slider drags log NRE, input fields don't force-drop).");

            var typeNames = queue.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
            BuildPrefabsForTypes(typeNames, theme, registry, context);
        }

        private static void BuildPrefabsForTypes(
            List<string> assemblyQualifiedTypeNames,
            DmnwareTheme theme,
            DmnwareAppGenAssets registry,
            DmnwareAppGenContext context)
        {
            int built = 0, errored = 0;
            foreach (var aqn in assemblyQualifiedTypeNames)
            {
                try
                {
                    var t = Type.GetType(aqn);
                    if (t == null)
                    {
                        Debug.LogError($"[AppGen] Type '{aqn}' not found post-recompile.");
                        errored++;
                        continue;
                    }
                    var info = TryReflectLayout(t);
                    if (info == null) { errored++; continue; }

                    var builder = new PrefabBuilder(theme, registry, context);
                    var prefabPath = Path.Combine(info.AppFolder, info.AppClassName + ".prefab");
                    bool ok = builder.Build(info.AppClassName, info.AppBehaviourType, info.Attribute, info.Root, prefabPath);
                    if (!ok)
                    {
                        errored++;
                        foreach (var e in builder.Errors) Debug.LogError($"[AppGen] {info.LayoutType.Name}: {e}");
                    }
                    else
                    {
                        built++;
                        Debug.Log($"[AppGen] generated: {prefabPath}");
                    }
                }
                catch (Exception ex)
                {
                    errored++;
                    Debug.LogError($"[AppGen] exception on {aqn}: {ex}");
                }
            }
            Debug.Log($"[AppGen] Done. {built} built, {errored} errored.");
            AssetDatabase.SaveAssets();
        }

        // ============================================================
        // Discovery & reflection
        // ============================================================

        public class LayoutInfo
        {
            public Type LayoutType;
            public Type AppBehaviourType;
            public string AppClassName;          // e.g. "Counter"
            public string AppFolder;             // absolute-Asset-path folder
            public DmnwareAppAttribute Attribute;
            public Node Root;
        }

        private static List<LayoutInfo> DiscoverLayouts()
        {
            var results = new List<LayoutInfo>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(AppLayout).IsAssignableFrom(t)) continue;
                    if (t.IsAbstract) continue;
                    if (t.GetCustomAttribute<DmnwareAppAttribute>() == null) continue;

                    var info = TryReflectLayout(t);
                    if (info != null) results.Add(info);
                }
            }
            return results;
        }

        private static LayoutInfo TryReflectLayout(Type layoutType)
        {
            var attr = layoutType.GetCustomAttribute<DmnwareAppAttribute>();
            if (attr == null)
            {
                Debug.LogError($"[AppGen] {layoutType.Name} is missing [DmnwareApp] attribute.");
                return null;
            }
            if (!layoutType.Name.EndsWith("Layout"))
            {
                Debug.LogError($"[AppGen] Layout class {layoutType.Name} must end in 'Layout' (e.g. CounterLayout for Counter).");
                return null;
            }
            var appClassName = layoutType.Name.Substring(0, layoutType.Name.Length - "Layout".Length);
            if (string.IsNullOrEmpty(appClassName))
            {
                Debug.LogError($"[AppGen] Layout class {layoutType.Name} has no name before 'Layout'.");
                return null;
            }

            var appBehaviourType = FindUdonSharpBehaviourType(appClassName);
            if (appBehaviourType == null)
            {
                Debug.LogError(
                    $"[AppGen] {layoutType.Name}: no UdonSharpBehaviour named '{appClassName}' found in any assembly. " +
                    $"Create '{appClassName}.cs' with `public partial class {appClassName} : UdonSharpBehaviour {{ ... }}'.");
                return null;
            }

            // Locate the source folder for the layout via MonoScript lookup.
            var folder = FindLayoutSourceFolder(layoutType);
            if (folder == null)
            {
                Debug.LogError($"[AppGen] {layoutType.Name}: cannot find .layout.cs source file in project (looked for MonoScript named {layoutType.Name}).");
                return null;
            }

            // Instantiate and call Build()
            AppLayout instance;
            Node root;
            try
            {
                instance = (AppLayout)Activator.CreateInstance(layoutType);
                root = instance.Build();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppGen] {layoutType.Name}.Build() threw: {ex}");
                return null;
            }
            if (root == null)
            {
                Debug.LogError($"[AppGen] {layoutType.Name}.Build() returned null.");
                return null;
            }

            return new LayoutInfo
            {
                LayoutType = layoutType,
                AppBehaviourType = appBehaviourType,
                AppClassName = appClassName,
                AppFolder = folder,
                Attribute = attr,
                Root = root,
            };
        }

        private static Type FindUdonSharpBehaviourType(string className)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.Name != className) continue;
                    if (!typeof(UdonSharpBehaviour).IsAssignableFrom(t)) continue;
                    return t;
                }
            }
            return null;
        }

        private static string FindLayoutSourceFolder(Type layoutType)
        {
            // Asset names don't always match class names (e.g. "Counter.layout.cs" → asset name
            // "Counter.layout"), so we can't use FindAssets's name filter. Iterate every MonoScript
            // and match by its declared class.
            var guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == layoutType)
                    return Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return null;
        }

        // ============================================================
        // Validation
        // ============================================================

        private static (List<string> errors, List<PartialWriter.FieldDecl> fields) Validate(LayoutInfo info)
        {
            var errors = new List<string>();
            var fields = new List<PartialWriter.FieldDecl>();
            var idsSeen = new HashSet<string>();
            var methodsOnBehaviour = new HashSet<string>();

            foreach (var m in info.AppBehaviourType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                methodsOnBehaviour.Add(m.Name);

            void Walk(Node n)
            {
                if (n == null) return;

                if (!string.IsNullOrEmpty(n.Id))
                {
                    if (!IsValidIdentifier(n.Id))
                        errors.Add($"id '{n.Id}' is not a valid C# identifier.");
                    if (IsReservedId(n.Id))
                        errors.Add($"id '{n.Id}' is reserved.");
                    if (!idsSeen.Add(n.Id))
                        errors.Add($"duplicate id '{n.Id}'.");
                }

                string handler = null;
                string typeName = null;
                switch (n)
                {
                    case ButtonNode b:
                        handler = b.OnClickMethod;
                        typeName = "UnityEngine.UI.Button";
                        if (string.IsNullOrEmpty(b.Id)) errors.Add($"Button label='{b.Label}' is missing required id.");
                        break;
                    case ToggleNode t:
                        handler = t.OnChangeMethod;
                        typeName = "UnityEngine.UI.Toggle";
                        if (string.IsNullOrEmpty(t.Id)) errors.Add($"Toggle label='{t.Label}' is missing required id.");
                        break;
                    case SliderNode s:
                        handler = s.OnChangeMethod;
                        typeName = "UnityEngine.UI.Slider";
                        if (string.IsNullOrEmpty(s.Id)) errors.Add($"Slider label='{s.Label}' is missing required id.");
                        break;
                    case TextNode tx when !string.IsNullOrEmpty(tx.Id):
                        typeName = "TMPro.TMP_Text";
                        break;
                    case ImageNode im when !string.IsNullOrEmpty(im.Id):
                        typeName = "UnityEngine.UI.Image";
                        break;
                    case RawNode rn:
                        typeName = "UnityEngine.GameObject";
                        if (string.IsNullOrEmpty(rn.Id)) errors.Add("Raw node is missing required id.");
                        break;
                    case ContainerNode c:
                        foreach (var ch in c.Children) Walk(ch);
                        break;
                }

                if (handler != null)
                {
                    if (!methodsOnBehaviour.Contains(handler))
                        errors.Add(
                            $"handler '{handler}' is referenced in layout but does not exist on {info.AppBehaviourType.Name}. " +
                            $"Add `public void {handler}() {{ }}` to {info.AppBehaviourType.Name}.cs.");
                }

                if (!string.IsNullOrEmpty(n.Id) && typeName != null)
                {
                    fields.Add(new PartialWriter.FieldDecl { TypeName = typeName, FieldName = n.Id });
                }

                if (n.Mods.InitiallyHidden && string.IsNullOrEmpty(n.Id))
                    errors.Add("a node with Hidden() must also have an id so the U# code can show it.");
            }

            Walk(info.Root);
            return (errors, fields);
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!char.IsLetter(s[0]) && s[0] != '_') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
            return true;
        }

        private static readonly HashSet<string> Reserved = new HashSet<string>
        {
            "Device", "App", "transform", "gameObject", "enabled", "name", "tag", "layer",
            "rigidbody", "rigidbody2D", "audio", "camera", "light", "collider", "collider2D",
            "renderer", "animation", "constantForce", "hingeJoint", "particleEmitter", "particleSystem",
        };

        private static bool IsReservedId(string s)
        {
            if (Reserved.Contains(s)) return true;
            if (s.StartsWith("_dmnware", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
