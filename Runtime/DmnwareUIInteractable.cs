
using UdonSharp;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public class DmnwareUIInteractable : NatLoggerUser
{
    private RectTransform MyRT;
    private Transform MyTransform;
    [SerializeField]
    private UdonBehaviour TargetBehavior = null;
    [SerializeField]
    private string MethodName;
    [SerializeField]
    public bool SupportDragging = false;
    [SerializeField]
    private ScrollRect DraggingScrollRect = null;
    [SerializeField]
    private Slider DraggingSlider = null;
    private RectTransform DraggingContentRT = null;
    private bool Dragging = false;
    private float DraggingStartPosition = 0f;
    private bool StuffCached = false;
    [SerializeField]
    private VRCUrlInputField AttachedUrlInputField = null;
    [SerializeField]
    private InputField AttachedInputField = null;
    [SerializeField]
    private Toggle AttachedToggle = null;
    private RectTransform DraggingScrollRectRT = null;
    private Transform DraggingScrollRectParentTransform = null;
    [SerializeField]
    private DmnwareDevicePickupCustomization PickupCustomizationForForceDrop = null;
    [SerializeField]
    private bool SupportDraggingX = false;
    [SerializeField]
    private Slider DraggingXSlider = null;
    private RectTransform DraggingXSliderRT = null;
    private int DraggingType = -1;
    //0 scroll rect y
    //1 slider x
    [SerializeField]
    public bool DraggingNoDeadZone = false;
    [SerializeField]
    public bool ForceNoClickSound = false;
    public AudioSource TargetAudioSource = null;
    [SerializeField]
    private DmnwareUISliderToggle AttachedSliderToggle = null;
    [SerializeField]
    private DmnwareUISliderHelper AttachedSliderHelper = null;

    // Inertia variables
    private float InertiaVelocityY = 0f;
    private float InertiaVelocityX = 0f;
    private float LastDeltaY = 0f;
    private float LastDeltaX = 0f;
    private float LastDragTime = 0f;
    private bool InertiaActive = false;

    [SerializeField]
    private float InertiaDecayRate = 0.92f; // How quickly inertia slows down (0-1, higher = slower decay) - For ScrollRects
    [SerializeField]
    private float InertiaMinVelocity = 0.0005f; // Minimum velocity before stopping
    [SerializeField]
    private float InertiaVelocityMultiplier = 1.5f; // Multiplier for the initial velocity - For ScrollRects
    [SerializeField]
    private float SliderInertiaDecayRate = 0.70f; // Much higher damping for sliders to reduce VR jitter
    [SerializeField]
    private float SliderInertiaVelocityMultiplier = 0.3f; // Lower multiplier for sliders
    private void EnsureStuffCached()
    {
        if (StuffCached) return;
        StuffCached = true;
        MyRT = this.GetComponent<RectTransform>();
        MyTransform = this.transform;
        if (SupportDragging)
        {
            if (DraggingScrollRect != null)
            {
                DraggingContentRT = DraggingScrollRect.content;
                DraggingScrollRectRT = DraggingScrollRect.GetComponent<RectTransform>();
                DraggingScrollRectParentTransform = DraggingScrollRect.transform;
                DraggingType = 0;
            }
        }
        if (SupportDraggingX)
        {
            if (DraggingXSlider != null)
            {
                DraggingXSliderRT = DraggingXSlider/*.transform.Find("Handle Slide Area")*/.GetComponent<RectTransform>();
                DraggingType = 1;
            }
        }
    }

    public void _StartDragging()
    {
        EnsureStuffCached();
        Dragging = true;
        InertiaActive = false; // Stop any ongoing inertia
        InertiaVelocityY = 0f;
        InertiaVelocityX = 0f;
        LastDeltaY = 0f;
        LastDeltaX = 0f;
        LastDragTime = Time.time;

        if (DraggingType == 0)
        {
            DraggingStartPosition = DraggingScrollRect.verticalNormalizedPosition;
        } else if (DraggingType == 1)
        {
            if (!ForceNoClickSound) TargetAudioSource.Play();
        }
    }

    public void _DragSetDelta(float deltaX, float deltaY, Vector3 world_space_cur_pos)
    {
        EnsureStuffCached();

        // Calculate time delta for velocity tracking
        float currentTime = Time.time;
        float timeDelta = currentTime - LastDragTime;
        if (timeDelta > 0f)
        {
            if (DraggingType == 0)
            {
                float dragarea01 = DraggingContentRT.sizeDelta.y - DraggingScrollRectRT.sizeDelta.y;
                if (dragarea01 <= 0f) return;

                float normalizedDeltaY = deltaY / dragarea01;
                DraggingScrollRect.verticalNormalizedPosition = DraggingStartPosition - normalizedDeltaY;

                // Track velocity (change in normalized position per second)
                float currentDeltaY = normalizedDeltaY - LastDeltaY;
                InertiaVelocityY = currentDeltaY / timeDelta;
                LastDeltaY = normalizedDeltaY;
            }
            else if (DraggingType == 1)
            {
                Vector3 posonobj = DraggingXSliderRT.InverseTransformPoint(world_space_cur_pos);
                float x = posonobj.x + (DraggingXSliderRT.pivot.x * DraggingXSliderRT.sizeDelta.x);
                x /= DraggingXSliderRT.sizeDelta.x;
                float max = DraggingXSlider.maxValue - DraggingXSlider.minValue;
                float newval = x * max;
                newval += DraggingXSlider.minValue;

                // Track velocity for slider
                float valueDelta = newval - DraggingXSlider.value;
                InertiaVelocityX = valueDelta / timeDelta;

                DraggingXSlider.value = newval;
            }

            LastDragTime = currentTime;
        }
    }
    public void _StopDragging()
    {
        Dragging = false;

        // Start inertia if there's enough velocity
        if (DraggingType == 0)
        {
            if (Mathf.Abs(InertiaVelocityY) > InertiaMinVelocity)
            {
                InertiaVelocityY *= InertiaVelocityMultiplier;
                InertiaActive = true;
                DLog($"[YTS] DmnwareUIInteractable: start scroll rect y inertia with vel {InertiaVelocityY}");
            }
        }
        else if (DraggingType == 1)
        {
            if (Mathf.Abs(InertiaVelocityX) > InertiaMinVelocity)
            {
                InertiaVelocityX *= SliderInertiaVelocityMultiplier; // Use lower multiplier for sliders
                InertiaActive = true;
                DLog($"[YTS] DmnwareUIInteractable: start slider inertia with vel {InertiaVelocityX}");
            }
        }
    }

    void Update()
    {
        if (!InertiaActive) return;

        if (DraggingType == 0)
        {
            // Apply inertia to ScrollRect
            float newPosition = DraggingScrollRect.verticalNormalizedPosition - (InertiaVelocityY * Time.deltaTime);
            newPosition = Mathf.Clamp01(newPosition);
            DraggingScrollRect.verticalNormalizedPosition = newPosition;

            // Apply decay
            InertiaVelocityY *= InertiaDecayRate;

            // Stop if velocity is too low or hit boundaries
            if (Mathf.Abs(InertiaVelocityY) < InertiaMinVelocity ||
                (newPosition <= 0f && InertiaVelocityY > 0f) ||
                (newPosition >= 1f && InertiaVelocityY < 0f))
            {
                InertiaActive = false;
                InertiaVelocityY = 0f;
                DLog("[YTS] DmnwareUIInteractable: stop inertia");
            }
        }
        else if (DraggingType == 1)
        {
            // Apply inertia to Slider with heavy damping to reduce VR jitter
            float newValue = DraggingXSlider.value + (InertiaVelocityX * Time.deltaTime);
            newValue = Mathf.Clamp(newValue, DraggingXSlider.minValue, DraggingXSlider.maxValue);
            DraggingXSlider.value = newValue;

            // Apply higher decay for sliders
            InertiaVelocityX *= SliderInertiaDecayRate;

            // Stop if velocity is too low or hit boundaries
            if (Mathf.Abs(InertiaVelocityX) < InertiaMinVelocity ||
                (newValue <= DraggingXSlider.minValue && InertiaVelocityX < 0f) ||
                (newValue >= DraggingXSlider.maxValue && InertiaVelocityX > 0f))
            {
                InertiaActive = false;
                InertiaVelocityX = 0f;
                DLog("[YTS] DmnwareUIInteractable: stop inertia");
            }
        }
    }

    public bool _TestPoint(Vector3 point)
    {
        EnsureStuffCached();
        if (DraggingScrollRectRT != null && !DraggingScrollRectRT.rect.Contains(DraggingScrollRectParentTransform.InverseTransformPoint(point))) return false;
        if (AttachedUrlInputField != null && !AttachedUrlInputField.enabled) return false; //TODO implement for other types!!!
        return MyRT.rect.Contains(MyTransform.InverseTransformPoint(point));
    }

    public void _MyInvoke()
    {
        Debug.Log("[YTS] DmnwareUIInteractable: invoke");
        if (AttachedToggle != null)
        {
            AttachedToggle.SetIsOnWithoutNotify(!AttachedToggle.isOn);
        }
        else if (AttachedSliderToggle != null)
        {
            AttachedSliderToggle._OnClick();
        }
        else if (AttachedSliderHelper != null)
        {
            AttachedSliderHelper._OnClick();
        }
        else if (AttachedUrlInputField != null)
        {
            Debug.Log("[YTS] DmnwareUIInteractable: invoke on URLIF");
            AttachedUrlInputField.Select();
            AttachedUrlInputField.ActivateInputField();
            SendCustomEventDelayedFrames("_WeirdDoubleInvoke", 1);
        }
        else if (AttachedInputField != null)
        {
            AttachedInputField.Select();
            AttachedInputField.ActivateInputField();
            SendCustomEventDelayedFrames("_WeirdDoubleInvoke", 1);
        }
        if (!ForceNoClickSound && TargetAudioSource != null) TargetAudioSource.Play();
        if (TargetBehavior == null) return;
        TargetBehavior.SendCustomEvent(MethodName);
    }

    public void _WeirdDoubleInvoke()
    {
        Debug.Log("[YTS] DmnwareUIInteractable: WDI");
        if (PickupCustomizationForForceDrop != null) PickupCustomizationForForceDrop._ForceDrop();
        if (AttachedUrlInputField != null)
        {
            AttachedUrlInputField.Select();
            AttachedUrlInputField.ActivateInputField();
        }
        else if (AttachedInputField != null)
        {
            AttachedInputField.Select();
            AttachedInputField.ActivateInputField();
        }
    }
}
