using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public partial class Counter : UdonSharpBehaviour
{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    private int _value;

    void Start() { }

    public void _DmnwareAppInit()
    {
        _value = 0;
        _Refresh();
    }
    public void _DmnwareAppLateInit() { }
    public void _DmnwareAppOpen() { }
    public void _DmnwareAppClose() { }

    public void _OnInc()
    {
        _value += _CurrentStep();
        _Refresh();
    }

    public void _OnDec()
    {
        _value -= _CurrentStep();
        _Refresh();
    }

    public void _OnReset()
    {
        _value = 0;
        _Refresh();
    }

    public void _OnStepChanged() { }
    public void _OnAutoFiveChanged() { }

    private int _CurrentStep()
    {
        return autoFive.isOn ? 5 : (int)step.value;
    }

    private void _Refresh()
    {
        count.text = _value.ToString();
    }
}
