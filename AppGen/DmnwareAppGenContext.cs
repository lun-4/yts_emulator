using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DMNware.AppGen
{
    /// Scene-resident pointer object. Holds references the codegen needs that
    /// must be scene objects (AudioSource, DmnwareDevicePickupCustomization, NatLogger).
    ///
    /// Add this component to one GameObject in your tablet scene (e.g. on the tablet itself
    /// or under AppSource), populate the fields once, and the codegen will find it
    /// automatically via FindObjectOfType.
    ///
    /// This is intentionally a plain MonoBehaviour, not a UdonSharpBehaviour — it carries
    /// no runtime logic and never executes inside Udon.
    public class DmnwareAppGenContext : MonoBehaviour
    {
        [Tooltip("AudioSource played by DmnwareUIInteractable on tap. Usually the tablet's shared click sound.")]
        public AudioSource ClickAudioSource;

        [Tooltip("DmnwareDevicePickupCustomization — used to force-drop the tablet when an input field is focused.")]
        public DmnwareDevicePickupCustomization PickupCustomization;

        [Tooltip("NatLogger — DmnwareUIInteractable logs through this during drag operations.")]
        public NatLogger NatLogger;

        [Tooltip("Folder where new generated app prefabs/scripts are placed.")]
        public string AppsRoot = "Assets/nat/UdonScripts/DMNware/Apps/Gen";

#if UNITY_EDITOR
        public static DmnwareAppGenContext FindInScene()
        {
            // Searches the loaded scenes for the context. Includes inactive objects.
            // Returns null if not found.
            var all = Resources.FindObjectsOfTypeAll<DmnwareAppGenContext>();
            foreach (var c in all)
            {
                if (c == null) continue;
                // Skip prefabs in the project; we only want a scene instance.
                if (EditorUtility.IsPersistent(c)) continue;
                if ((c.hideFlags & HideFlags.NotEditable) != 0 || (c.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
                return c;
            }
            return null;
        }
#endif
    }
}
