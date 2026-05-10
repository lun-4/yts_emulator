using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public partial class Calculator : UdonSharpBehaviour
{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    // Op codes: 0 none, 1 +, 2 −, 3 ×, 4 ÷
    private float _accumulator;
    private int _pendingOp;
    private bool _hasPending;
    private bool _resetOnNextDigit;
    private string _entry;

    void Start() { }

    public void _DmnwareAppInit()
    {
        _Reset();
    }
    public void _DmnwareAppLateInit() { }
    public void _DmnwareAppOpen() { }
    public void _DmnwareAppClose() { }

    public void _OnClear() { _Reset(); }

    public void _OnDigit0() { _AppendDigit("0"); }
    public void _OnDigit1() { _AppendDigit("1"); }
    public void _OnDigit2() { _AppendDigit("2"); }
    public void _OnDigit3() { _AppendDigit("3"); }
    public void _OnDigit4() { _AppendDigit("4"); }
    public void _OnDigit5() { _AppendDigit("5"); }
    public void _OnDigit6() { _AppendDigit("6"); }
    public void _OnDigit7() { _AppendDigit("7"); }
    public void _OnDigit8() { _AppendDigit("8"); }
    public void _OnDigit9() { _AppendDigit("9"); }

    public void _OnDot()
    {
        if (_resetOnNextDigit)
        {
            _entry = "0.";
            _resetOnNextDigit = false;
        }
        else if (_entry.IndexOf(".") < 0)
        {
            _entry = _entry + ".";
        }
        _Refresh();
    }

    public void _OnAdd() { _ChainOp(1); }
    public void _OnSub() { _ChainOp(2); }
    public void _OnMul() { _ChainOp(3); }
    public void _OnDiv() { _ChainOp(4); }

    public void _OnEquals()
    {
        _ApplyPending();
        _pendingOp = 0;
        _hasPending = false;
        _entry = _Format(_accumulator);
        _resetOnNextDigit = true;
        _Refresh();
    }

    private void _Reset()
    {
        _accumulator = 0f;
        _pendingOp = 0;
        _hasPending = false;
        _resetOnNextDigit = false;
        _entry = "0";
        _Refresh();
    }

    private void _AppendDigit(string d)
    {
        if (_resetOnNextDigit || _entry == "0")
        {
            _entry = d;
            _resetOnNextDigit = false;
        }
        else if (_entry.Length < 16)
        {
            _entry = _entry + d;
        }
        _Refresh();
    }

    private void _ChainOp(int op)
    {
        _ApplyPending();
        _pendingOp = op;
        _hasPending = true;
        _resetOnNextDigit = true;
        _entry = _Format(_accumulator);
        _Refresh();
    }

    private void _ApplyPending()
    {
        float operand = 0f;
        if (!float.TryParse(_entry, out operand)) operand = 0f;

        if (!_hasPending || _pendingOp == 0)
        {
            _accumulator = operand;
            return;
        }
        if (_pendingOp == 1) _accumulator = _accumulator + operand;
        else if (_pendingOp == 2) _accumulator = _accumulator - operand;
        else if (_pendingOp == 3) _accumulator = _accumulator * operand;
        else if (_pendingOp == 4) _accumulator = operand == 0f ? 0f : _accumulator / operand;
    }

    private string _Format(float v)
    {
        if (v == (float)((int)v) && Mathf.Abs(v) < 1e9f) return ((int)v).ToString();
        return v.ToString();
    }

    private void _Refresh()
    {
        display.text = _entry;
    }
}
