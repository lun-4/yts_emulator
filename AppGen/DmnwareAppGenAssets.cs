#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DMNware.AppGen
{
    [CreateAssetMenu(fileName = "DmnwareAppGenAssets", menuName = "DMNware/AppGen Asset Registry", order = 1)]
    public class DmnwareAppGenAssets : ScriptableObject
    {
        [Serializable]
        public struct IconEntry
        {
            public string Key;
            public Sprite Sprite;
        }

        [Serializable]
        public struct PrefabEntry
        {
            public string Key;
            public GameObject Prefab;
        }

        public List<IconEntry> Icons = new List<IconEntry>();
        public List<PrefabEntry> Prefabs = new List<PrefabEntry>();

        public Sprite GetIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in Icons) if (e.Key == key) return e.Sprite;
            return null;
        }

        public GameObject GetPrefab(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in Prefabs) if (e.Key == key) return e.Prefab;
            return null;
        }

        public IEnumerable<string> AllIconKeys()
        {
            foreach (var e in Icons) if (!string.IsNullOrEmpty(e.Key)) yield return e.Key;
        }

        public IEnumerable<string> AllPrefabKeys()
        {
            foreach (var e in Prefabs) if (!string.IsNullOrEmpty(e.Key)) yield return e.Key;
        }

        private const string DefaultAssetPath = "Assets/nat/UdonScripts/DMNware/AppGen/DmnwareAppGenAssets.asset";

        public static DmnwareAppGenAssets LoadOrCreate()
        {
            var a = AssetDatabase.LoadAssetAtPath<DmnwareAppGenAssets>(DefaultAssetPath);
            if (a != null) return a;

            var guids = AssetDatabase.FindAssets("t:DmnwareAppGenAssets");
            if (guids != null && guids.Length > 0)
            {
                a = AssetDatabase.LoadAssetAtPath<DmnwareAppGenAssets>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (a != null) return a;
            }

            a = CreateInstance<DmnwareAppGenAssets>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DefaultAssetPath));
            AssetDatabase.CreateAsset(a, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return a;
        }
    }
}
#endif
