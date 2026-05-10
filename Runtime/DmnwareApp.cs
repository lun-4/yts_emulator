
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class DmnwareApp : UdonSharpBehaviour
{
    public bool Dummy_DEPRECATED;
    public string Name;
    public Texture2D Icon;
    public Texture2D Splash;
    public GameObject ContainerGameObject;
    public bool Validated = false;
    public bool VROnly;
    public bool HandheldOnly;
    public bool PendingNotif = false;
    public bool IsHiddenFromUser = false;
    public bool IsLabAssistantsOnly = false;
    public bool IsVrcPlusOnly = false;
    public bool SupportsGhosting = false;
    public string OutboundCustomData = "";
}
