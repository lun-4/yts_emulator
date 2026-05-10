#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DMNware.AppGen.Editor
{
    /// "Create New App" wizard. Bootstraps a brand-new app, breaking the
    /// chicken-and-egg cycle: the user's MyApp.cs would normally reference
    /// generated fields (count, step, etc.) that only exist after codegen, but
    /// codegen needs MyApp to compile so it can reflect on MyAppLayout. We
    /// sidestep by writing a *stub* MyApp.cs that references no generated
    /// fields, then queueing GenerateOne to run after the recompile triggered
    /// by AssetDatabase.Refresh().
    public class CreateAppWizard : ScriptableWizard
    {
        public string AppName = "MyApp";
        public string TargetFolder = DefaultFolder;

        private const string DefaultFolder = "Assets/nat/UdonScripts/DMNware/Apps/Gen";
        private const string PendingScaffoldKey = "DMNware.AppGen.PendingScaffolds";

        [MenuItem("DMNware/Create New App...", priority = 50)]
        public static void Open()
        {
            DisplayWizard<CreateAppWizard>("Create DMNware App", "Create");
        }

        protected override bool DrawWizardGUI()
        {
            bool changed = base.DrawWizardGUI();
            errorString = ValidationError(AppName, TargetFolder);
            isValid = string.IsNullOrEmpty(errorString);
            return changed;
        }

        private void OnWizardCreate()
        {
            var error = ValidationError(AppName, TargetFolder);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[AppGen] Cannot create app: {error}");
                return;
            }

            var folder = Path.Combine(TargetFolder, AppName).Replace('\\', '/');
            Directory.CreateDirectory(folder);

            File.WriteAllText($"{folder}/{AppName}.cs",        BehaviourStub(AppName));
            File.WriteAllText($"{folder}/{AppName}.layout.cs", LayoutStub(AppName));

            var existing = SessionState.GetString(PendingScaffoldKey, "");
            var queue = string.IsNullOrEmpty(existing) ? AppName : existing + "|" + AppName;
            SessionState.SetString(PendingScaffoldKey, queue);

            Debug.Log($"[AppGen] Scaffolded {AppName} at {folder}. Recompiling, then generating prefab...");
            AssetDatabase.Refresh();
        }

        // Runs after Unity recompiles. Resolves each queued name to its Layout
        // type (which only exists post-recompile) and runs codegen on it.
        // AppGenerator has its own DidReloadScripts that drains a *different*
        // queue (assembly-qualified type names from the partial-write phase) —
        // ours stores plain class names because at scaffold time the type
        // doesn't exist yet, so an AQN can't be captured.
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            var queue = SessionState.GetString(PendingScaffoldKey, "");
            if (string.IsNullOrEmpty(queue)) return;
            SessionState.EraseString(PendingScaffoldKey);

            foreach (var name in queue.Split('|').Where(s => !string.IsNullOrEmpty(s)))
            {
                var layoutTypeName = name + "Layout";
                var t = FindType(layoutTypeName);
                if (t == null)
                {
                    Debug.LogError($"[AppGen] Scaffolded {name} but {layoutTypeName} not found post-recompile. " +
                        "Check the console for compile errors in the new files.");
                    continue;
                }
                AppGenerator.GenerateOne(t);
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static string ValidationError(string appName, string folder)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return "App name is required.";
            if (!IsValidIdentifier(appName))
                return $"'{appName}' is not a valid C# identifier.";
            if (appName.EndsWith("Layout"))
                return "App name must not end with 'Layout' (the layout class is named that automatically).";
            if (string.IsNullOrWhiteSpace(folder))
                return "Target folder is required.";
            var normalizedFolder = folder.Replace('\\', '/');
            if (!normalizedFolder.StartsWith("Assets/"))
                return "Target folder must be inside Assets/.";
            var fullFolder = Path.Combine(normalizedFolder, appName).Replace('\\', '/');
            if (Directory.Exists(fullFolder))
                return $"Folder '{fullFolder}' already exists.";
            if (FindType(appName) != null)
                return $"A class named '{appName}' already exists in the project.";
            if (FindType(appName + "Layout") != null)
                return $"A class named '{appName}Layout' already exists.";
            return null;
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!char.IsLetter(s[0]) && s[0] != '_') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
            return true;
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                    if (t != null && t.Name == name) return t;
            }
            return null;
        }

        // ============================================================
        // Stubs
        // ============================================================

        private static string BehaviourStub(string name) =>
$@"using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public partial class {name} : UdonSharpBehaviour
{{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    void Start() {{ }}

    public void _DmnwareAppInit() {{ }}
    public void _DmnwareAppLateInit() {{ }}
    public void _DmnwareAppOpen() {{ }}
    public void _DmnwareAppClose() {{ }}
}}
";

        private static string LayoutStub(string name) =>
$@"#if !COMPILER_UDONSHARP && UNITY_EDITOR
using DMNware.AppGen;

[DmnwareApp(name: ""{name}"")]
public class {name}Layout : AppLayout
{{
    public override Node Build() => Vertical(
        padding: Spacing.LG,
        spacing: Spacing.MD,
        Header(""{name}"")
    );
}}
#endif
";
    }
}
#endif
