using System.IO;
using UnityEditor;
using UnityEngine;

namespace DMNware.AppGen.Editor
{
    public static class AppGenMenu
    {
        [MenuItem("DMNware/Generate App UI", priority = 100)]
        public static void GenerateAll()
        {
            AppGenerator.GenerateAll();
        }

        [MenuItem("DMNware/Regenerate All (theme changed)", priority = 101)]
        public static void RegenerateAll()
        {
            // Same as GenerateAll — separate menu item is just for clarity in intent.
            AppGenerator.GenerateAll();
        }

        [MenuItem("DMNware/Open Theme Asset", priority = 200)]
        public static void OpenTheme()
        {
            var t = DmnwareTheme.LoadOrCreate();
            Selection.activeObject = t;
            EditorGUIUtility.PingObject(t);
        }

        [MenuItem("DMNware/Open Asset Registry", priority = 201)]
        public static void OpenRegistry()
        {
            var a = DmnwareAppGenAssets.LoadOrCreate();
            Selection.activeObject = a;
            EditorGUIUtility.PingObject(a);
        }

        [MenuItem("DMNware/Find AppGen Context In Scene", priority = 202)]
        public static void FindContext()
        {
            var c = DmnwareAppGenContext.FindInScene();
            if (c == null)
            {
                Debug.LogWarning("[AppGen] No DmnwareAppGenContext found. " +
                    "Add the DmnwareAppGenContext component to any GameObject in your scene (e.g. the tablet), " +
                    "then populate the ClickAudioSource / PickupCustomization / NatLogger fields.");
                return;
            }
            Selection.activeObject = c.gameObject;
            EditorGUIUtility.PingObject(c.gameObject);
        }

        // ----------------------------------------------------------------
        // Right-click on a *.layout.cs file in Project ▸ Generate UI for this app
        // ----------------------------------------------------------------

        [MenuItem("Assets/DMNware/Generate UI for this app", true)]
        private static bool ValidateGenerateForSelected()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".layout.cs");
        }

        [MenuItem("Assets/DMNware/Generate UI for this app", priority = 50)]
        private static void GenerateForSelected()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".layout.cs"))
            {
                Debug.LogError("[AppGen] Selected asset is not a .layout.cs file.");
                return;
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var t = script != null ? script.GetClass() : null;
            if (t == null)
            {
                Debug.LogError($"[AppGen] Could not resolve a class from {path}.");
                return;
            }
            AppGenerator.GenerateOne(t);
        }
    }
}
