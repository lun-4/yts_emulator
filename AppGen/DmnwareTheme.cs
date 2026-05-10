#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DMNware.AppGen
{
    [CreateAssetMenu(fileName = "DmnwareTheme", menuName = "DMNware/AppGen Theme", order = 0)]
    public class DmnwareTheme : ScriptableObject
    {
        [Serializable]
        public struct Palette
        {
            public Color Background;
            public Color Surface;
            public Color SurfaceElevated;
            public Color Primary;
            public Color OnPrimary;
            public Color Text;
            public Color TextMuted;
            public Color Accent;
            public Color Danger;
            public Color OnDanger;
        }

        [Serializable]
        public struct TypeStyle
        {
            public TMP_FontAsset Font;
            public float Size;
            public FontWeight Weight;
            public Color Color;
        }

        [Serializable]
        public struct ButtonStyle
        {
            public Sprite Background;
            public Color BackgroundTint;
            public Color TextColor;
        }

        [Header("Palette")]
        public Palette Colors;

        [Header("Typography")]
        public TypeStyle Header;
        public TypeStyle Body;
        public TypeStyle Caption;

        [Header("Sprites")]
        public Sprite PanelBg;
        public Sprite InputBg;
        public ButtonStyle ButtonDefault;
        public ButtonStyle ButtonPrimary;
        public ButtonStyle ButtonSecondary;
        public ButtonStyle ButtonGhost;
        public ButtonStyle ButtonDanger;
        public Sprite SliderTrack;
        public Sprite SliderFill;
        public Sprite SliderHandle;
        public Sprite ToggleBg;
        public Sprite ToggleKnob;
        public Sprite DividerSprite;

        [Header("Defaults")]
        public float ButtonHeight = 44f;
        public float SliderHeight = 32f;
        public float SliderHandleSize = 28f;
        public float ToggleWidth = 56f;
        public float ToggleHeight = 32f;
        public float DividerThickness = 1f;
        public float CornerRadiusPx = 8f;

        public ButtonStyle GetButtonStyle(Variant v)
        {
            switch (v)
            {
                case Variant.Primary: return ButtonPrimary;
                case Variant.Secondary: return ButtonSecondary;
                case Variant.Ghost: return ButtonGhost;
                case Variant.Danger: return ButtonDanger;
                default: return ButtonDefault;
            }
        }

        public TypeStyle GetTypeStyle(Typography t)
        {
            switch (t)
            {
                case Typography.Header: return Header;
                case Typography.Caption: return Caption;
                default: return Body;
            }
        }

        // ---- Singleton-ish accessor used by codegen ----

        private const string DefaultAssetPath = "Assets/nat/UdonScripts/DMNware/AppGen/DmnwareTheme.asset";

        public static DmnwareTheme LoadOrCreate()
        {
            var t = AssetDatabase.LoadAssetAtPath<DmnwareTheme>(DefaultAssetPath);
            if (t != null) return t;

            // Try a project-wide search before creating.
            var guids = AssetDatabase.FindAssets("t:DmnwareTheme");
            if (guids != null && guids.Length > 0)
            {
                t = AssetDatabase.LoadAssetAtPath<DmnwareTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (t != null) return t;
            }

            t = CreateInstance<DmnwareTheme>();
            t.ApplyDefaults();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DefaultAssetPath));
            AssetDatabase.CreateAsset(t, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return t;
        }

        public void ApplyDefaults()
        {
            Colors = new Palette
            {
                Background = new Color(0.07f, 0.08f, 0.10f, 1f),
                Surface = new Color(0.12f, 0.13f, 0.16f, 1f),
                SurfaceElevated = new Color(0.16f, 0.18f, 0.22f, 1f),
                Primary = new Color(0.25f, 0.55f, 1.00f, 1f),
                OnPrimary = Color.white,
                Text = new Color(0.95f, 0.96f, 0.98f, 1f),
                TextMuted = new Color(0.65f, 0.66f, 0.70f, 1f),
                Accent = new Color(0.45f, 0.85f, 1.00f, 1f),
                Danger = new Color(0.90f, 0.30f, 0.30f, 1f),
                OnDanger = Color.white,
            };

            Body = new TypeStyle { Size = 18f, Color = Colors.Text, Weight = FontWeight.Regular };
            Header = new TypeStyle { Size = 28f, Color = Colors.Text, Weight = FontWeight.Bold };
            Caption = new TypeStyle { Size = 14f, Color = Colors.TextMuted, Weight = FontWeight.Regular };

            ButtonDefault = new ButtonStyle { BackgroundTint = Colors.Surface, TextColor = Colors.Text };
            ButtonPrimary = new ButtonStyle { BackgroundTint = Colors.Primary, TextColor = Colors.OnPrimary };
            ButtonSecondary = new ButtonStyle { BackgroundTint = Colors.SurfaceElevated, TextColor = Colors.Accent };
            ButtonGhost = new ButtonStyle { BackgroundTint = new Color(0, 0, 0, 0), TextColor = Colors.Text };
            ButtonDanger = new ButtonStyle { BackgroundTint = Colors.Danger, TextColor = Colors.OnDanger };
        }
    }
}
#endif
