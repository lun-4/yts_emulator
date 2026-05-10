using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.Udon;

namespace DMNware.AppGen.Editor
{
    /// Walks a Node tree and produces a UGUI prefab with the U# app behaviour wired up.
    /// One instance per app build.
    public class PrefabBuilder
    {
        private readonly DmnwareTheme _theme;
        private readonly DmnwareAppGenAssets _registry;
        private readonly DmnwareAppGenContext _context;
        private readonly Dictionary<string, GameObject> _idToGo = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, UnityEngine.Object> _idToFieldRef = new Dictionary<string, UnityEngine.Object>();
        private readonly List<string> _errors = new List<string>();
        private UdonBehaviour _appUdonBehaviour;

        public PrefabBuilder(DmnwareTheme theme, DmnwareAppGenAssets registry, DmnwareAppGenContext context)
        {
            _theme = theme;
            _registry = registry;
            _context = context;
        }

        public IReadOnlyList<string> Errors => _errors;

        // Matches UserArea's fixed RectTransform size in the scene. If the device's screen
        // resolution ever changes, update both sides.
        private const float DmnwareScreenWidth = 1080f;
        private const float DmnwareScreenHeight = 1670f;

        public bool Build(
            string className,
            Type appBehaviourType,
            DmnwareAppAttribute appAttr,
            Node root,
            string prefabPath)
        {
            if (root == null) { _errors.Add("Build() returned null."); return false; }
            if (appBehaviourType == null) { _errors.Add($"App behaviour type '{className}' not found."); return false; }
            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(appBehaviourType))
            { _errors.Add($"Type '{className}' must derive from UdonSharpBehaviour."); return false; }

            // ---- Ensure program assets exist for both U# behaviours we'll add ----
            EnsureProgramAsset(typeof(DmnwareApp));
            if (EnsureProgramAsset(appBehaviourType) == null)
            {
                _errors.Add($"Could not create U# program asset for '{appBehaviourType.Name}'. " +
                            $"Make sure '{appBehaviourType.Name}.cs' is in the project and compiles cleanly.");
                return false;
            }

            // ---- Build hierarchy in-memory ----
            var rootGo = new GameObject(className);
            try
            {
                // 1) DmnwareApp metadata on root
                var appMeta = rootGo.AddUdonSharpComponent<DmnwareApp>();
                appMeta.Name = appAttr.Name;
                appMeta.Icon = ResolveIconTexture(appAttr.IconKey);
                appMeta.Splash = ResolveIconTexture(appAttr.SplashKey);
                appMeta.HandheldOnly = appAttr.HandheldOnly;
                appMeta.VROnly = appAttr.VrOnly;
                appMeta.IsHiddenFromUser = appAttr.HiddenFromUser;

                // 2) Container child holds the UI + the user's app behaviour.
                // Must match the DMNware screen convention: fixed 1080x1670, anchored top-left,
                // pivot top-left. DmnwareDevice reparents this under UserArea and sets
                // localPosition=zero; UserArea is itself fixed-size top-left so a fixed-size
                // top-left-pivot container lines up exactly. A stretch-anchored container would
                // get its rect collapsed by the localPosition reset, since stretch geometry is
                // parent-relative and UserArea's pivot isn't centered.
                var containerGo = new GameObject("Container");
                containerGo.transform.SetParent(rootGo.transform, false);
                var containerRT = containerGo.AddComponent<RectTransform>();
                containerRT.anchorMin = new Vector2(0f, 1f);
                containerRT.anchorMax = new Vector2(0f, 1f);
                containerRT.pivot = new Vector2(0f, 1f);
                containerRT.anchoredPosition = Vector2.zero;
                containerRT.sizeDelta = new Vector2(DmnwareScreenWidth, DmnwareScreenHeight);

                // Clip every generated app to the user-area rect, so apps that draw outside the
                // flow layout (e.g. games positioning sprites by anchoredPosition) don't bleed into
                // the device's title bar or home-button strip.
                containerGo.AddComponent<RectMask2D>();

                var appProxy = (UdonSharpBehaviour)containerGo.AddUdonSharpComponent(appBehaviourType);
                appMeta.ContainerGameObject = containerGo;
                _appUdonBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(appProxy);

                // 3) Walk the layout tree and populate the container
                BuildNode(root, containerGo, isRoot: true);

                // 4) Bind generated nodes to the proxy's [SerializeField] fields by name
                BindFieldsToProxy(appProxy, appBehaviourType);

                if (_errors.Count > 0) return false;

                // 5) Save as prefab
                Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
                PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath, out var saveSuccess);
                if (!saveSuccess) { _errors.Add($"PrefabUtility.SaveAsPrefabAsset failed for {prefabPath}."); return false; }
                return true;
            }
            finally
            {
                if (rootGo != null) UnityEngine.Object.DestroyImmediate(rootGo);
            }
        }

        // ============================================================
        // Node walker
        // ============================================================

        /// Returns the GameObject created for this node.
        /// Caller is responsible for parenting.
        private GameObject BuildNode(Node node, GameObject parent, bool isRoot = false)
        {
            GameObject go = null;
            switch (node)
            {
                case VerticalNode v:    go = BuildContainer(v, parent, vertical: true, isRoot); break;
                case HorizontalNode h:  go = BuildContainer(h, parent, vertical: false, isRoot); break;
                case TextNode t:        go = BuildText(t, parent); break;
                case ImageNode i:       go = BuildImage(i, parent); break;
                case SpacerNode s:      go = BuildSpacer(s, parent); break;
                case DividerNode d:     go = BuildDivider(d, parent); break;
                case ButtonNode b:      go = BuildButton(b, parent); break;
                case ToggleNode tg:     go = BuildToggle(tg, parent); break;
                case SliderNode sl:     go = BuildSlider(sl, parent); break;
                case RawNode r:         go = BuildRaw(r, parent); break;
                default:
                    _errors.Add($"Unknown node type: {node.GetType().Name}");
                    return null;
            }

            if (go == null) return null;

            if (!string.IsNullOrEmpty(node.Id))
            {
                if (_idToGo.ContainsKey(node.Id))
                {
                    _errors.Add($"Duplicate id '{node.Id}'.");
                }
                else
                {
                    _idToGo[node.Id] = go;
                }
            }

            ApplyCommonModifiers(go, node);
            return go;
        }

        // ============================================================
        // Container
        // ============================================================

        private GameObject BuildContainer(ContainerNode node, GameObject parent, bool vertical, bool isRoot)
        {
            var go = NewUIChild(parent, vertical ? "Vertical" : "Horizontal");
            var rt = go.GetComponent<RectTransform>();
            if (isRoot) StretchToParent(rt);

            HorizontalOrVerticalLayoutGroup group = vertical
                ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>()
                : (HorizontalOrVerticalLayoutGroup)go.AddComponent<HorizontalLayoutGroup>();

            group.padding = new RectOffset(node.InnerPadding.L, node.InnerPadding.R, node.InnerPadding.T, node.InnerPadding.B);
            group.spacing = node.Spacing;
            group.childAlignment = MapChildAlignment(node.Align, node.Justify, vertical);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = vertical;          // V: stretch children horizontally
            group.childForceExpandHeight = !vertical;        // H: stretch children vertically

            // No ContentSizeFitter on non-root containers: their size is driven by the
            // ancestor LayoutGroup, which reads our child's preferred size through the
            // ILayoutElement that HorizontalOrVerticalLayoutGroup already implements.
            // Adding a CSF here causes a fight ("driven-by-LayoutGroup vs CSF") and Unity
            // logs a warning recommending removal.

            foreach (var child in node.Children)
                BuildNode(child, go);

            return go;
        }

        private static TextAnchor MapChildAlignment(Align a, Justify j, bool vertical)
        {
            // Unity TextAnchor (used by LayoutGroup.childAlignment) is row-major:
            // rows = Upper(0)/Middle(3)/Lower(6) (Y), cols = Left(0)/Center(1)/Right(2) (X).
            // For VerticalLayoutGroup the main axis is Y (Justify), cross is X (Align).
            // For HorizontalLayoutGroup the main axis is X (Justify), cross is Y (Align).
            int yRow, xCol;
            if (vertical)
            {
                yRow = j == Justify.End ? 2 : j == Justify.Center ? 1 : 0;
                xCol = a == Align.End ? 2 : a == Align.Center || a == Align.Stretch ? 1 : 0;
            }
            else
            {
                xCol = j == Justify.End ? 2 : j == Justify.Center ? 1 : 0;
                yRow = a == Align.End ? 2 : a == Align.Center || a == Align.Stretch ? 1 : 0;
            }
            return (TextAnchor)(yRow * 3 + xCol);
        }

        // ============================================================
        // Leaves
        // ============================================================

        private GameObject BuildText(TextNode node, GameObject parent)
        {
            var go = NewUIChild(parent, string.IsNullOrEmpty(node.Id) ? "Text" : node.Id);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = node.Text ?? "";
            ApplyTypography(tmp, node.Style);

            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = tmp;
            return go;
        }

        private GameObject BuildImage(ImageNode node, GameObject parent)
        {
            var go = NewUIChild(parent, string.IsNullOrEmpty(node.Id) ? "Image" : node.Id);
            var img = go.AddComponent<Image>();
            var sprite = ResolveIconSprite(node.AssetKey);
            if (sprite == null && !string.IsNullOrEmpty(node.AssetKey))
                _errors.Add($"Image asset key '{node.AssetKey}' not found in registry. Known keys: {KnownIconKeys()}");
            img.sprite = sprite;
            img.preserveAspect = node.Mode == FillMode.Contain || node.Mode == FillMode.Cover;

            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = img;
            return go;
        }

        private GameObject BuildSpacer(SpacerNode node, GameObject parent)
        {
            var go = NewUIChild(parent, "Spacer");
            var le = go.AddComponent<LayoutElement>();
            if (node.IsFlex)
            {
                le.flexibleWidth = node.Mods.FlexWeight;
                le.flexibleHeight = node.Mods.FlexWeight;
            }
            else
            {
                le.preferredWidth = node.FixedWidth;
                le.preferredHeight = node.FixedHeight;
                le.minWidth = node.FixedWidth;
                le.minHeight = node.FixedHeight;
            }
            return go;
        }

        private GameObject BuildDivider(DividerNode node, GameObject parent)
        {
            var go = NewUIChild(parent, "Divider");
            var img = go.AddComponent<Image>();
            img.color = _theme.Colors.TextMuted * new Color(1f, 1f, 1f, 0.4f);
            if (_theme.DividerSprite != null) img.sprite = _theme.DividerSprite;

            var le = go.AddComponent<LayoutElement>();
            if (node.Orientation == Orientation.Horizontal)
            {
                le.preferredHeight = _theme.DividerThickness;
                le.minHeight = _theme.DividerThickness;
                le.flexibleWidth = 1f;
            }
            else
            {
                le.preferredWidth = _theme.DividerThickness;
                le.minWidth = _theme.DividerThickness;
                le.flexibleHeight = 1f;
            }
            return go;
        }

        // ============================================================
        // Interactives
        // ============================================================

        private GameObject BuildButton(ButtonNode node, GameObject parent)
        {
            var go = NewUIChild(parent, node.Id ?? "Button");

            var bgImage = go.AddComponent<Image>();
            ApplyButtonStyle(bgImage, _theme.GetButtonStyle(node.Mods.Style));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bgImage;

            // Label child
            var labelGo = NewUIChild(go, "Label");
            var labelRT = labelGo.GetComponent<RectTransform>();
            StretchToParent(labelRT);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = node.Label ?? "";
            ApplyTypography(labelTmp, Typography.Body);
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = _theme.GetButtonStyle(node.Mods.Style).TextColor;

            // Sizing
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = _theme.ButtonHeight;
            le.preferredHeight = _theme.ButtonHeight;

            // Wire interactable + persistent listener
            WireInteractable(go, node.Id, node.OnClickMethod, attached: null, sliderTarget: null, scrollRectTarget: null);
            WireUdonPersistentListener(btn, "m_OnClick", node.OnClickMethod);

            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = btn;
            return go;
        }

        private GameObject BuildToggle(ToggleNode node, GameObject parent)
        {
            // Outer row: Label | Spacer | Toggle pill
            var row = NewUIChild(parent, node.Id ?? "Toggle");
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = Spacing.SM;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = _theme.ToggleHeight;

            // Label
            if (!string.IsNullOrEmpty(node.Label))
            {
                var labelGo = NewUIChild(row, "Label");
                var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
                labelTmp.text = node.Label;
                ApplyTypography(labelTmp, Typography.Body);
                labelTmp.alignment = TextAlignmentOptions.Left;

                var labelLE = labelGo.AddComponent<LayoutElement>();
                labelLE.flexibleWidth = 1f;
            }

            // Toggle pill
            var pillGo = NewUIChild(row, "Pill");
            var pillImg = pillGo.AddComponent<Image>();
            pillImg.color = _theme.Colors.SurfaceElevated;
            if (_theme.ToggleBg != null) pillImg.sprite = _theme.ToggleBg;
            var pillLE = pillGo.AddComponent<LayoutElement>();
            pillLE.minWidth = _theme.ToggleWidth;
            pillLE.preferredWidth = _theme.ToggleWidth;
            pillLE.minHeight = _theme.ToggleHeight;
            pillLE.preferredHeight = _theme.ToggleHeight;

            var toggle = pillGo.AddComponent<Toggle>();
            toggle.targetGraphic = pillImg;
            toggle.isOn = node.InitialValue;

            // Knob (the Toggle.graphic — visible when isOn)
            var knobGo = NewUIChild(pillGo, "Knob");
            var knobImg = knobGo.AddComponent<Image>();
            knobImg.color = _theme.Colors.Primary;
            if (_theme.ToggleKnob != null) knobImg.sprite = _theme.ToggleKnob;
            var knobRT = knobImg.rectTransform;
            knobRT.anchorMin = new Vector2(0.5f, 0.5f);
            knobRT.anchorMax = new Vector2(0.5f, 0.5f);
            knobRT.sizeDelta = new Vector2(_theme.ToggleHeight - 8f, _theme.ToggleHeight - 8f);
            knobRT.anchoredPosition = Vector2.zero;
            toggle.graphic = knobImg;

            // Wire interactable + persistent listener (on the pill)
            WireInteractable(pillGo, node.Id, node.OnChangeMethod, attached: toggle, sliderTarget: null, scrollRectTarget: null);
            WireUdonPersistentListener(toggle, "onValueChanged", node.OnChangeMethod);

            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = toggle;
            return row;
        }

        private GameObject BuildSlider(SliderNode node, GameObject parent)
        {
            // Column: Label / Slider
            var col = NewUIChild(parent, node.Id ?? "Slider");
            var colLayout = col.AddComponent<VerticalLayoutGroup>();
            colLayout.padding = new RectOffset(0, 0, 0, 0);
            colLayout.spacing = 4;
            colLayout.childAlignment = TextAnchor.UpperLeft;
            colLayout.childControlWidth = true;
            colLayout.childControlHeight = true;
            colLayout.childForceExpandWidth = true;
            colLayout.childForceExpandHeight = false;
            // Label
            if (!string.IsNullOrEmpty(node.Label))
            {
                var labelGo = NewUIChild(col, "Label");
                var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
                labelTmp.text = node.Label;
                ApplyTypography(labelTmp, Typography.Caption);
            }

            // Slider
            var sliderGo = NewUIChild(col, "Slider");
            var sliderLE = sliderGo.AddComponent<LayoutElement>();
            sliderLE.minHeight = _theme.SliderHeight;
            sliderLE.preferredHeight = _theme.SliderHeight;

            // Background
            var bgGo = NewUIChild(sliderGo, "Background");
            var bgRT = bgGo.GetComponent<RectTransform>();
            StretchToParent(bgRT, padX: 0f, padY: (_theme.SliderHeight - 8f) * 0.5f);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = _theme.Colors.SurfaceElevated;
            if (_theme.SliderTrack != null) bgImg.sprite = _theme.SliderTrack;

            // Fill area + Fill
            var fillAreaGo = NewUIChild(sliderGo, "Fill Area");
            var fillAreaRT = fillAreaGo.GetComponent<RectTransform>();
            StretchToParent(fillAreaRT, padX: _theme.SliderHandleSize * 0.5f, padY: (_theme.SliderHeight - 8f) * 0.5f);
            var fillGo = NewUIChild(fillAreaGo, "Fill");
            var fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.sizeDelta = new Vector2(_theme.SliderHandleSize, 0f);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = _theme.Colors.Primary;
            if (_theme.SliderFill != null) fillImg.sprite = _theme.SliderFill;

            // Handle area + Handle
            var handleAreaGo = NewUIChild(sliderGo, "Handle Slide Area");
            var handleAreaRT = handleAreaGo.GetComponent<RectTransform>();
            StretchToParent(handleAreaRT, padX: _theme.SliderHandleSize * 0.5f, padY: 0f);
            var handleGo = NewUIChild(handleAreaGo, "Handle");
            var handleRT = handleGo.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(_theme.SliderHandleSize, _theme.SliderHandleSize);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = _theme.Colors.Primary;
            if (_theme.SliderHandle != null) handleImg.sprite = _theme.SliderHandle;

            // Slider component
            var slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = node.Min;
            slider.maxValue = node.Max;
            slider.wholeNumbers = node.Integer;
            slider.value = node.InitialValue;

            // Wire interactable (drag-X mode) + persistent listener
            WireInteractable(sliderGo, node.Id, node.OnChangeMethod,
                attached: null, sliderTarget: slider, scrollRectTarget: null);
            WireUdonPersistentListener(slider, "m_OnValueChanged", node.OnChangeMethod);

            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = slider;
            return col;
        }

        private GameObject BuildRaw(RawNode node, GameObject parent)
        {
            var prefab = _registry != null ? _registry.GetPrefab(node.PrefabKey) : null;
            if (prefab == null)
            {
                _errors.Add($"Raw prefab key '{node.PrefabKey}' not found in registry.");
                // Stub so the build doesn't crash; caller will see errors.
                return NewUIChild(parent, node.Id ?? "RawMissing");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent.transform, false);
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();
            if (!string.IsNullOrEmpty(node.Id)) _idToFieldRef[node.Id] = go;
            return go;
        }

        // ============================================================
        // Modifiers / theme application
        // ============================================================

        private void ApplyCommonModifiers(GameObject go, Node node)
        {
            var m = node.Mods;

            // Hidden — set inactive at build time. U# code activates via .gameObject.SetActive(true).
            if (m.InitiallyHidden) go.SetActive(false);

            // Layout sizing via LayoutElement
            if (m.Width.Mode == SizeMode.Fixed || m.Height.Mode == SizeMode.Fixed
                || m.HasMinWidth || m.HasMinHeight || m.HasMaxWidth || m.HasMaxHeight
                || m.FlexWeight > 0f)
            {
                var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                if (m.Width.Mode == SizeMode.Fixed) { le.preferredWidth = m.Width.Value; le.minWidth = m.Width.Value; }
                if (m.Height.Mode == SizeMode.Fixed) { le.preferredHeight = m.Height.Value; le.minHeight = m.Height.Value; }
                if (m.HasMinWidth)  le.minWidth = m.MinWidth;
                if (m.HasMaxWidth)  le.preferredWidth = m.MaxWidth;     // approximation — UGUI has no max
                if (m.HasMinHeight) le.minHeight = m.MinHeight;
                if (m.HasMaxHeight) le.preferredHeight = m.MaxHeight;   // approximation
                if (m.FlexWeight > 0f)
                {
                    le.flexibleWidth = m.FlexWeight;
                    le.flexibleHeight = m.FlexWeight;
                }
            }

            // Margin: not supported in v1 (UGUI auto-layout has no per-child margin).
            // The DSL accepts it for forward compatibility but it's ignored.
        }

        private void ApplyTypography(TextMeshProUGUI tmp, Typography style)
        {
            var ts = _theme.GetTypeStyle(style);
            if (ts.Font != null) tmp.font = ts.Font;
            tmp.fontSize = ts.Size > 0 ? ts.Size : 18f;
            tmp.fontStyle = ts.Weight == FontWeight.Bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = ts.Color.a > 0 ? ts.Color : _theme.Colors.Text;
            tmp.enableWordWrapping = true;
        }

        private static void ApplyButtonStyle(Image bg, DmnwareTheme.ButtonStyle style)
        {
            bg.color = style.BackgroundTint;
            if (style.Background != null) bg.sprite = style.Background;
            bg.type = Image.Type.Sliced;
        }

        // ============================================================
        // Wiring
        // ============================================================

        /// Adds DmnwareUIInteractable and configures it for the appropriate control type.
        private void WireInteractable(
            GameObject go,
            string id,
            string methodName,
            Toggle attached,
            Slider sliderTarget,
            ScrollRect scrollRectTarget)
        {
            var interactable = go.AddUdonSharpComponent<DmnwareUIInteractable>();

            // NatLogger reference (DmnwareUIInteractable._DragSetDelta etc. log through it).
            if (_context != null && _context.NatLogger != null)
                interactable.NatLogger = _context.NatLogger;

            // Method invocation target: backing UdonBehaviour on the container.
            if (_appUdonBehaviour != null)
                SetSerialized(interactable, "TargetBehavior", _appUdonBehaviour);

            if (!string.IsNullOrEmpty(methodName))
                SetSerialized(interactable, "MethodName", methodName);

            if (_context != null && _context.ClickAudioSource != null)
                interactable.TargetAudioSource = _context.ClickAudioSource;

            if (attached != null)
                SetSerialized(interactable, "AttachedToggle", attached);

            if (sliderTarget != null)
            {
                SetSerialized(interactable, "SupportDraggingX", true);
                SetSerialized(interactable, "DraggingXSlider", sliderTarget);
            }

            if (scrollRectTarget != null)
            {
                SetSerialized(interactable, "SupportDragging", true);
                SetSerialized(interactable, "DraggingScrollRect", scrollRectTarget);
            }

            if (_context != null && _context.PickupCustomization != null)
                SetSerialized(interactable, "PickupCustomizationForForceDrop", _context.PickupCustomization);
        }

        /// Adds a persistent UnityEvent listener that invokes UdonBehaviour.SendCustomEvent(methodName).
        /// This is the same form U#'s EasyEventEditor produces when a user wires an event to a U# proxy method
        /// via the inspector — and it's what survives the VRChat build pipeline. Calling AddPersistentListener
        /// targeting the U# proxy directly does NOT work at runtime because the proxy's method body never
        /// executes in a Udon world; only the backing UdonBehaviour does, and only via SendCustomEvent.
        private void WireUdonPersistentListener(Component eventOwner, string eventFieldName, string methodName)
        {
            if (string.IsNullOrEmpty(methodName) || eventOwner == null) return;
            if (_appUdonBehaviour == null)
            {
                _errors.Add($"Wire failed: backing UdonBehaviour is null when wiring {methodName}.");
                return;
            }

            try
            {
                var so = new SerializedObject(eventOwner);
                var ev = so.FindProperty(eventFieldName);
                if (ev == null)
                {
                    _errors.Add($"Wire failed: no UnityEvent field '{eventFieldName}' on {eventOwner.GetType().Name}.");
                    return;
                }
                var calls = ev.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (calls == null)
                {
                    _errors.Add($"Wire failed: cannot find m_PersistentCalls.m_Calls on {eventFieldName}.");
                    return;
                }

                int idx = calls.arraySize;
                calls.InsertArrayElementAtIndex(idx);
                var call = calls.GetArrayElementAtIndex(idx);

                call.FindPropertyRelative("m_Target").objectReferenceValue = _appUdonBehaviour;
                var typeNameProp = call.FindPropertyRelative("m_TargetAssemblyTypeName");
                if (typeNameProp != null) typeNameProp.stringValue = typeof(UdonBehaviour).AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
                call.FindPropertyRelative("m_Mode").enumValueIndex = (int)PersistentListenerMode.String;
                call.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEventCallState.RuntimeOnly;

                var args = call.FindPropertyRelative("m_Arguments");
                args.FindPropertyRelative("m_StringArgument").stringValue = methodName;
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                var argTypeProp = args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
                if (argTypeProp != null) argTypeProp.stringValue = typeof(UnityEngine.Object).AssemblyQualifiedName;
                args.FindPropertyRelative("m_IntArgument").intValue = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_BoolArgument").boolValue = false;

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch (Exception e)
            {
                _errors.Add($"WireUdonPersistentListener {methodName} on {eventOwner.GetType().Name}.{eventFieldName}: {e.Message}");
            }
        }

        // ============================================================
        // Field binding
        // ============================================================

        private void BindFieldsToProxy(UdonSharpBehaviour proxy, Type behaviourType)
        {
            var fields = behaviourType.GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

            foreach (var kvp in _idToFieldRef)
            {
                var field = FindFieldByName(fields, kvp.Key);
                if (field == null)
                {
                    _errors.Add(
                        $"Layout exposes id '{kvp.Key}' but the U# behaviour '{behaviourType.Name}' has no matching field. " +
                        $"Did the .gen.cs file fail to recompile? (Re-run after Unity finishes compiling.)");
                    continue;
                }
                if (!field.FieldType.IsAssignableFrom(kvp.Value.GetType()))
                {
                    _errors.Add(
                        $"Field '{kvp.Key}' on {behaviourType.Name} is {field.FieldType.Name} but layout produced {kvp.Value.GetType().Name}.");
                    continue;
                }
                field.SetValue(proxy, kvp.Value);
            }
        }

        private static System.Reflection.FieldInfo FindFieldByName(System.Reflection.FieldInfo[] fields, string name)
        {
            foreach (var f in fields) if (f.Name == name) return f;
            return null;
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static GameObject NewUIChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void StretchToParent(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            // Anchors + offsets fully specify a stretch rect. Don't touch localPosition —
            // setting it after offsets puts the pivot at the parent's pivot, which
            // shifts the rect off-anchor when the parent's pivot isn't centered.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private Texture2D ResolveIconTexture(string key)
        {
            var sprite = ResolveIconSprite(key);
            return sprite != null ? sprite.texture : null;
        }

        private Sprite ResolveIconSprite(string key)
        {
            if (_registry == null) return null;
            return _registry.GetIcon(key);
        }

        private string KnownIconKeys()
        {
            if (_registry == null) return "<no registry>";
            var keys = new List<string>();
            foreach (var k in _registry.AllIconKeys()) keys.Add(k);
            return keys.Count == 0 ? "<empty>" : string.Join(", ", keys);
        }

        /// Make sure a UdonSharpProgramAsset exists for the given behaviour type. If not, create
        /// one next to the .cs file (mirroring what the U# right-click menu does) and force the
        /// U# program-asset cache to rebuild so subsequent lookups see it.
        private static UnityEngine.Object EnsureProgramAsset(Type behaviourType)
        {
            var existing = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviourType);
            if (existing != null) return existing;

            // Locate the .cs source file.
            var guids = AssetDatabase.FindAssets("t:MonoScript");
            MonoScript script = null;
            string csPath = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var s = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (s != null && s.GetClass() == behaviourType) { script = s; csPath = path; break; }
            }
            if (script == null || csPath == null) return null;

            var assetPath = Path.ChangeExtension(csPath, ".asset");
            if (!File.Exists(assetPath))
            {
                var newAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                newAsset.sourceCsScript = script;
                AssetDatabase.CreateAsset(newAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Force U#'s static program-asset cache to rebuild. The asset postprocessor *should*
            // clear the cache automatically, but on freshly-created assets the timing can be off
            // and subsequent GetUdonSharpProgramAsset lookups return stale null. Belt and suspenders.
            var clearMethod = typeof(UdonSharp.UdonSharpProgramAsset).GetMethod(
                "ClearProgramAssetCache",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            clearMethod?.Invoke(null, null);

            // Now the lookup should succeed.
            return UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviourType);
        }

        // SerializedObject path for assigning private [SerializeField] members on a Component.
        private static void SetSerialized(Component comp, string fieldName, UnityEngine.Object value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Component comp, string fieldName, bool value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Component comp, string fieldName, string value)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
