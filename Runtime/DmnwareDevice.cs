
using UdonSharp;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase;
using VRC.Udon;

public class DmnwareDevice : NatLoggerUser
{
    [SerializeField]
    private bool DebugMode = false;
    [SerializeField]
    private GameObject AppSource;
    [HideInInspector]
    public DmnwareApp[] Apps;
    public int OpenAppIdx = -1;
    [SerializeField]
    private GameObject NotifProto;
    public bool IsCurrentlyHandheld = false;
    public bool IsCurrentlyLandscape = false;
    public bool IsCurrentlyLandscapeU = false;
    [SerializeField]
    private GameObject HomeButton;
    [SerializeField]
    public bool VibrationEnabled = true;
    [SerializeField] private Transform DmnwareDumpster;
    public bool Ready = false;

    void Start()
    {
        _Log("DMNware Device \"" + this.gameObject.name + "\"");
        _Log("Booting OS...");

        _Log("enumerating apps");
        Apps = AppSource.GetComponentsInChildren<DmnwareApp>();

        DmnwareApp tut = null;
        foreach (DmnwareApp app in Apps)
        {
            _Log($"preparing app {app.Name}");
            app.Validated = false;

            if (app.ContainerGameObject == null)
            {
                _LogWarn("app has no ContainerGameObject set!!! it will be marked as non-validated");
                continue;
            }

            app.Validated = true;
            app.ContainerGameObject.GetComponent<UdonBehaviour>().SetProgramVariable("Device", this);
            app.ContainerGameObject.GetComponent<UdonBehaviour>().SetProgramVariable("App", app);
            app.ContainerGameObject.transform.SetParent(DmnwareDumpster);
            app.ContainerGameObject.SetActive(true);
            SendDmnwareEventToApp(app, "AppInit");
            if (app.Name == "Tutorial") tut = app;
        }

        foreach (DmnwareApp app in Apps)
        {
            _Log($"initializing app {app.Name}");
            SendDmnwareEventToApp(app, "AppLateInit");
        }

        OpenAppIdx = -1;

        NotifProto.SetActive(false);

        if (tut)
        {
            _Log("opening tut");
            _OpenApp(tut);
        } else
        {
            _Log("opening home screen");
            _OpenAppByIdx(0); //HACK FIX ME THIS SHOULD LOOKUP HOME SCREEN
        }

        _Log("setting handheld mode false");
        _SetHandheldMode(false);
        Ready = true;
    }
    public void _SetHandheldMode(bool en)
    {
        _Log($"setting handheld mode {en} and sending evt");
        IsCurrentlyHandheld = en;
        if (OpenAppIdx != -1) SendDmnwareEventToApp(Apps[OpenAppIdx], "AppHandheldModeChange");
    }
    public void _Log(string message, DmnwareApp app = null)
    {
        if (app != null)
            DLog($"[DMNware] [{app.Name}] I: {message}");
        else
            DLog($"[DMNware] [unknown] I: {message}");
    }

    public void _LogError(string message, DmnwareApp app = null)
    {
        if (app != null)
            DLogError($"[DMNware] [{app.Name}] E: {message}");
        else
            DLogError($"[DMNware] [unknown] E: {message}");
    }
    public void _LogWarn(string message, DmnwareApp app = null)
    {
        if (app != null)
            DLogWarning($"[DMNware] [{app.Name}] W: {message}");
        else
            DLogWarning($"[DMNware] [unknown] W: {message}");
    }

    private void SendDmnwareEventToApp(DmnwareApp app, string evt)
    {
        if (DebugMode) _Log("sending evt _Dmnware" + evt + " to app " + app.Name);
        if (!app.Validated)
        {
            _LogWarn("can't send evt to non-validated app!!!");
            return;
        }
        app.ContainerGameObject.GetComponent<UdonBehaviour>().SendCustomEvent("_Dmnware"+evt);
    }
    public void _OpenApp(DmnwareApp app)
    {
        for (int i=0;i<Apps.Length;i++)
        {
            if (Apps[i] == app)
            {
                _OpenAppByIdx(i);
                break;
            }
        }
    }
    public void _OpenAppByIdx(int idx)
    {
        _Log($"attempting to open app {Apps[idx].Name}");
        if (!Apps[idx].Validated)
        {
            _LogWarn("user trying to launch non-validated app!!!");
            _LaunchNotificationFromApp(Apps[idx], "This app is not available right now");
            return;
        }

        if (Apps[idx].HandheldOnly && !IsCurrentlyHandheld)
        {
            _LogWarn("app can't be launched, requires handheld mode");
            _LaunchNotificationFromApp(Apps[idx], "App can only be used in handheld mode");
            return;
        }

        if (OpenAppIdx != -1)
        {
            _Log("closing existing app");
            SendDmnwareEventToApp(Apps[OpenAppIdx], "AppClose");
            Apps[OpenAppIdx].ContainerGameObject.transform.SetParent(DmnwareDumpster);
        }

        _Log("opening new app");
        OpenAppIdx = idx;
        HomeButton.SetActive(idx != 0);
        Apps[OpenAppIdx].ContainerGameObject.transform.SetParent(transform.Find("Display/BlueBG/UserArea"));
        Apps[OpenAppIdx].ContainerGameObject.transform.localPosition = Vector3.zero;
        Apps[OpenAppIdx].ContainerGameObject.transform.localRotation = Quaternion.identity;
        Apps[OpenAppIdx].ContainerGameObject.transform.localScale = Vector3.one;
        SendDmnwareEventToApp(Apps[OpenAppIdx], "AppOpen");
    }

    public void _LaunchNotification(string source_name, string message, Texture2D icon, float timeout = 2.5f, UdonBehaviour tgt_behavior = null, string tgt_method = "")
    {
        _Log($"launching notification from {source_name}");
        GameObject notif = Instantiate(NotifProto, NotifProto.transform.parent);
        notif.GetComponent<DmnwareNotification>()._LaunchNotification(source_name, message, icon, VibrationEnabled, timeout, tgt_behavior, tgt_method);
    }
    public void _LaunchNotificationFromApp(DmnwareApp app, string message, float timeout = 2.5f, string custom_sender = "", UdonBehaviour tgt_behavior = null, string tgt_method = "", Texture custom_icon = null)
    {
        _Log($"launching notification from {app.Name}");
        GameObject notif = Instantiate(NotifProto, NotifProto.transform.parent);
        notif.SetActive(true);
        notif.GetComponent<DmnwareNotification>()._LaunchNotification((custom_sender!="")?custom_sender:app.Name, message, (custom_icon==null)?app.Icon:custom_icon, VibrationEnabled, timeout, tgt_behavior, tgt_method);
    }

    public void _PickupTrigger()
    {
        if (OpenAppIdx != -1) SendDmnwareEventToApp(Apps[OpenAppIdx], "PickupTrigger");
    }

    public void _UpdateOrientation(bool landscape, bool u)
    {
        IsCurrentlyLandscape = landscape;
        IsCurrentlyLandscapeU = u;
        if (OpenAppIdx != -1) SendDmnwareEventToApp(Apps[OpenAppIdx], "OrientationChange");
    }

    public void _DirtyApps()
    {
        if (OpenAppIdx != -1) SendDmnwareEventToApp(Apps[OpenAppIdx], "DirtyApps");
    }
}
