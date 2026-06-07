using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;

public partial class Minesweeper : UdonSharpBehaviour
{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    private const float ScreenW = 1080f;
    private const float ScreenH = 1670f;
    private const float GridSize = 1080f;
    private const int   GridN = 8;
    private const float CellPitch = 135f;
    private const float CellSize  = 130f;
    private const int   MinesTotal = 10;

    private const int StateIdle = 0;
    private const int StatePlay = 1;
    private const int StateWin  = 2;
    private const int StateLose = 3;

    private int _state;
    private bool _isOpen;
    private bool _flagMode;
    private int _flagsPlaced;
    private int _revealedCount;
    private float _startTime;
    private int _displayedSecs;

    private bool[] _isMine;
    private bool[] _isRevealed;
    private bool[] _isFlagged;
    private int[]  _adjacent;

    private Button[]   _cellBtns;
    private TMP_Text[] _cellLabels;

    void Start() { }

    public void _DmnwareAppInit()
    {
        var parent = bg.gameObject.transform.parent;
        var vlg = parent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;

        _SetupRect(bg.rectTransform, ScreenW, ScreenH, ScreenW * 0.5f, ScreenH * 0.5f);
        bg.color = new Color(0.13f, 0.16f, 0.20f, 1f);

        // HUD strip (y in [1080, 1670], height 590).
        _SetupRect(minesText.gameObject.GetComponent<RectTransform>(),
                   460f, 100f, 260f, 1590f);
        minesText.fontSize = 64f;
        minesText.alignment = TextAlignmentOptions.Left;
        minesText.color = Color.white;

        _SetupRect(timerText.gameObject.GetComponent<RectTransform>(),
                   460f, 100f, 820f, 1590f);
        timerText.fontSize = 64f;
        timerText.alignment = TextAlignmentOptions.Right;
        timerText.color = Color.white;

        _SetupRect(statusText.gameObject.GetComponent<RectTransform>(),
                   ScreenW, 140f, ScreenW * 0.5f, 1450f);
        statusText.fontSize = 96f;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = Color.white;

        _SetupRect(flagToggle.gameObject.GetComponent<RectTransform>(),
                   540f, 140f, 290f, 1200f);

        _SetupRect(newGame.gameObject.GetComponent<RectTransform>(),
                   460f, 140f, 820f, 1200f);

        // Allocate state arrays + position the 64 cells.
        _isMine     = new bool[GridN * GridN];
        _isRevealed = new bool[GridN * GridN];
        _isFlagged  = new bool[GridN * GridN];
        _adjacent   = new int[GridN * GridN];

        _cellBtns   = new Button[GridN * GridN];
        _cellLabels = new TMP_Text[GridN * GridN];

        _cellBtns[0]  = cell00; _cellBtns[1]  = cell01; _cellBtns[2]  = cell02; _cellBtns[3]  = cell03;
        _cellBtns[4]  = cell04; _cellBtns[5]  = cell05; _cellBtns[6]  = cell06; _cellBtns[7]  = cell07;
        _cellBtns[8]  = cell10; _cellBtns[9]  = cell11; _cellBtns[10] = cell12; _cellBtns[11] = cell13;
        _cellBtns[12] = cell14; _cellBtns[13] = cell15; _cellBtns[14] = cell16; _cellBtns[15] = cell17;
        _cellBtns[16] = cell20; _cellBtns[17] = cell21; _cellBtns[18] = cell22; _cellBtns[19] = cell23;
        _cellBtns[20] = cell24; _cellBtns[21] = cell25; _cellBtns[22] = cell26; _cellBtns[23] = cell27;
        _cellBtns[24] = cell30; _cellBtns[25] = cell31; _cellBtns[26] = cell32; _cellBtns[27] = cell33;
        _cellBtns[28] = cell34; _cellBtns[29] = cell35; _cellBtns[30] = cell36; _cellBtns[31] = cell37;
        _cellBtns[32] = cell40; _cellBtns[33] = cell41; _cellBtns[34] = cell42; _cellBtns[35] = cell43;
        _cellBtns[36] = cell44; _cellBtns[37] = cell45; _cellBtns[38] = cell46; _cellBtns[39] = cell47;
        _cellBtns[40] = cell50; _cellBtns[41] = cell51; _cellBtns[42] = cell52; _cellBtns[43] = cell53;
        _cellBtns[44] = cell54; _cellBtns[45] = cell55; _cellBtns[46] = cell56; _cellBtns[47] = cell57;
        _cellBtns[48] = cell60; _cellBtns[49] = cell61; _cellBtns[50] = cell62; _cellBtns[51] = cell63;
        _cellBtns[52] = cell64; _cellBtns[53] = cell65; _cellBtns[54] = cell66; _cellBtns[55] = cell67;
        _cellBtns[56] = cell70; _cellBtns[57] = cell71; _cellBtns[58] = cell72; _cellBtns[59] = cell73;
        _cellBtns[60] = cell74; _cellBtns[61] = cell75; _cellBtns[62] = cell76; _cellBtns[63] = cell77;

        for (int r = 0; r < GridN; r++)
        {
            for (int c = 0; c < GridN; c++)
            {
                int idx = r * GridN + c;
                float cx = CellPitch * 0.5f + c * CellPitch;
                float cy = GridSize - (CellPitch * 0.5f + r * CellPitch);
                _SetupRect(_cellBtns[idx].gameObject.GetComponent<RectTransform>(),
                           CellSize, CellSize, cx, cy);
                // PrefabBuilder creates a single "Label" child holding the TextMeshProUGUI
                // for each Button (see AppGen/Editor/PrefabBuilder.cs BuildButton).
                Transform labelT = _cellBtns[idx].gameObject.transform.GetChild(0);
                _cellLabels[idx] = labelT.gameObject.GetComponent<TMP_Text>();
                if (_cellLabels[idx] != null)
                {
                    _cellLabels[idx].fontSize = 78f;
                    _cellLabels[idx].alignment = TextAlignmentOptions.Center;
                }
            }
        }

        _ResetGame();
    }

    public void _DmnwareAppLateInit() { }
    public void _DmnwareAppOpen()  { _isOpen = true; }
    public void _DmnwareAppClose() { _isOpen = false; }

    void Update()
    {
        if (!_isOpen) return;
        if (_state != StatePlay) return;
        int secs = (int)(Time.time - _startTime);
        if (secs > 999) secs = 999;
        if (secs != _displayedSecs)
        {
            _displayedSecs = secs;
            timerText.text = secs.ToString();
        }
    }

    public void _OnFlagToggle()
    {
        _flagMode = flagToggle.isOn;
    }

    public void _OnNewGame()
    {
        _ResetGame();
    }

    // 64 cell dispatchers — Udon click handlers are parameterless, so each cell needs its
    // own entry point that forwards (row, col) to the shared core.
    public void _OnCell00() { _OnCell(0, 0); }
    public void _OnCell01() { _OnCell(0, 1); }
    public void _OnCell02() { _OnCell(0, 2); }
    public void _OnCell03() { _OnCell(0, 3); }
    public void _OnCell04() { _OnCell(0, 4); }
    public void _OnCell05() { _OnCell(0, 5); }
    public void _OnCell06() { _OnCell(0, 6); }
    public void _OnCell07() { _OnCell(0, 7); }
    public void _OnCell10() { _OnCell(1, 0); }
    public void _OnCell11() { _OnCell(1, 1); }
    public void _OnCell12() { _OnCell(1, 2); }
    public void _OnCell13() { _OnCell(1, 3); }
    public void _OnCell14() { _OnCell(1, 4); }
    public void _OnCell15() { _OnCell(1, 5); }
    public void _OnCell16() { _OnCell(1, 6); }
    public void _OnCell17() { _OnCell(1, 7); }
    public void _OnCell20() { _OnCell(2, 0); }
    public void _OnCell21() { _OnCell(2, 1); }
    public void _OnCell22() { _OnCell(2, 2); }
    public void _OnCell23() { _OnCell(2, 3); }
    public void _OnCell24() { _OnCell(2, 4); }
    public void _OnCell25() { _OnCell(2, 5); }
    public void _OnCell26() { _OnCell(2, 6); }
    public void _OnCell27() { _OnCell(2, 7); }
    public void _OnCell30() { _OnCell(3, 0); }
    public void _OnCell31() { _OnCell(3, 1); }
    public void _OnCell32() { _OnCell(3, 2); }
    public void _OnCell33() { _OnCell(3, 3); }
    public void _OnCell34() { _OnCell(3, 4); }
    public void _OnCell35() { _OnCell(3, 5); }
    public void _OnCell36() { _OnCell(3, 6); }
    public void _OnCell37() { _OnCell(3, 7); }
    public void _OnCell40() { _OnCell(4, 0); }
    public void _OnCell41() { _OnCell(4, 1); }
    public void _OnCell42() { _OnCell(4, 2); }
    public void _OnCell43() { _OnCell(4, 3); }
    public void _OnCell44() { _OnCell(4, 4); }
    public void _OnCell45() { _OnCell(4, 5); }
    public void _OnCell46() { _OnCell(4, 6); }
    public void _OnCell47() { _OnCell(4, 7); }
    public void _OnCell50() { _OnCell(5, 0); }
    public void _OnCell51() { _OnCell(5, 1); }
    public void _OnCell52() { _OnCell(5, 2); }
    public void _OnCell53() { _OnCell(5, 3); }
    public void _OnCell54() { _OnCell(5, 4); }
    public void _OnCell55() { _OnCell(5, 5); }
    public void _OnCell56() { _OnCell(5, 6); }
    public void _OnCell57() { _OnCell(5, 7); }
    public void _OnCell60() { _OnCell(6, 0); }
    public void _OnCell61() { _OnCell(6, 1); }
    public void _OnCell62() { _OnCell(6, 2); }
    public void _OnCell63() { _OnCell(6, 3); }
    public void _OnCell64() { _OnCell(6, 4); }
    public void _OnCell65() { _OnCell(6, 5); }
    public void _OnCell66() { _OnCell(6, 6); }
    public void _OnCell67() { _OnCell(6, 7); }
    public void _OnCell70() { _OnCell(7, 0); }
    public void _OnCell71() { _OnCell(7, 1); }
    public void _OnCell72() { _OnCell(7, 2); }
    public void _OnCell73() { _OnCell(7, 3); }
    public void _OnCell74() { _OnCell(7, 4); }
    public void _OnCell75() { _OnCell(7, 5); }
    public void _OnCell76() { _OnCell(7, 6); }
    public void _OnCell77() { _OnCell(7, 7); }

    private void _OnCell(int r, int c)
    {
        if (_state == StateWin || _state == StateLose) return;
        int idx = r * GridN + c;

        if (_flagMode)
        {
            if (_isRevealed[idx]) return;
            if (_isFlagged[idx])
            {
                _isFlagged[idx] = false;
                _flagsPlaced--;
            }
            else
            {
                _isFlagged[idx] = true;
                _flagsPlaced++;
            }
            _UpdateMinesCounter();
            _RefreshCell(idx);
            return;
        }

        if (_isFlagged[idx]) return;
        if (_isRevealed[idx]) return;

        if (_state == StateIdle)
        {
            _PlaceMines(r, c);
            _state = StatePlay;
            _startTime = Time.time;
            _displayedSecs = 0;
            timerText.text = "0";
        }

        if (_isMine[idx])
        {
            _isRevealed[idx] = true;
            _state = StateLose;
            statusText.text = "BOOM!";
            statusText.color = new Color(1f, 0.4f, 0.35f, 1f);
            _RefreshAll();
            return;
        }

        _Flood(r, c);
        _RefreshAll();

        if (_revealedCount >= GridN * GridN - MinesTotal)
        {
            _state = StateWin;
            statusText.text = "WIN!";
            statusText.color = new Color(0.4f, 1f, 0.5f, 1f);
        }
    }

    private void _PlaceMines(int safeR, int safeC)
    {
        int total = GridN * GridN;
        int safeIdx = safeR * GridN + safeC;
        int placed = 0;
        // Bounded loop: pick random cells until MinesTotal placed (always converges since
        // MinesTotal << total).
        while (placed < MinesTotal)
        {
            int idx = Random.Range(0, total);
            if (idx == safeIdx) continue;
            if (_isMine[idx]) continue;
            _isMine[idx] = true;
            placed++;
        }
        for (int r = 0; r < GridN; r++)
        {
            for (int c = 0; c < GridN; c++)
            {
                int idx = r * GridN + c;
                if (_isMine[idx]) { _adjacent[idx] = 0; continue; }
                int n = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = r + dr;
                        int nc = c + dc;
                        if (nr < 0 || nr >= GridN) continue;
                        if (nc < 0 || nc >= GridN) continue;
                        if (_isMine[nr * GridN + nc]) n++;
                    }
                }
                _adjacent[idx] = n;
            }
        }
    }

    // Iterative flood fill — reveal the start cell, and if its adjacency count is 0,
    // reveal all reachable non-mine cells. `queued[]` ensures each cell enters the queue
    // at most once, so the queue is bounded at GridN*GridN.
    private void _Flood(int startR, int startC)
    {
        int total = GridN * GridN;
        bool[] queued = new bool[total];
        int[] queueR = new int[total];
        int[] queueC = new int[total];
        int head = 0;
        int tail = 0;

        queueR[tail] = startR;
        queueC[tail] = startC;
        queued[startR * GridN + startC] = true;
        tail++;

        while (head < tail)
        {
            int r = queueR[head];
            int c = queueC[head];
            head++;
            int idx = r * GridN + c;

            if (_isRevealed[idx]) continue;
            if (_isFlagged[idx]) continue;
            if (_isMine[idx]) continue;

            _isRevealed[idx] = true;
            _revealedCount++;

            if (_adjacent[idx] != 0) continue;

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = r + dr;
                    int nc = c + dc;
                    if (nr < 0 || nr >= GridN) continue;
                    if (nc < 0 || nc >= GridN) continue;
                    int nidx = nr * GridN + nc;
                    if (queued[nidx]) continue;
                    if (_isRevealed[nidx]) continue;
                    if (_isFlagged[nidx]) continue;
                    if (_isMine[nidx]) continue;
                    queueR[tail] = nr;
                    queueC[tail] = nc;
                    queued[nidx] = true;
                    tail++;
                }
            }
        }
    }

    private void _ResetGame()
    {
        _state = StateIdle;
        _flagsPlaced = 0;
        _revealedCount = 0;
        _displayedSecs = 0;
        int total = GridN * GridN;
        for (int i = 0; i < total; i++)
        {
            _isMine[i] = false;
            _isRevealed[i] = false;
            _isFlagged[i] = false;
            _adjacent[i] = 0;
        }
        timerText.text = "0";
        timerText.color = Color.white;
        statusText.text = "";
        _UpdateMinesCounter();
        _RefreshAll();
    }

    private void _RefreshAll()
    {
        int total = GridN * GridN;
        for (int i = 0; i < total; i++) _RefreshCell(i);
    }

    private void _RefreshCell(int idx)
    {
        Button btn = _cellBtns[idx];
        TMP_Text label = _cellLabels[idx];

        // On loss, expose every un-flagged mine.
        if (_state == StateLose && _isMine[idx] && !_isFlagged[idx])
        {
            btn.image.color = new Color(0.65f, 0.12f, 0.12f, 1f);
            if (label != null) { label.text = "*"; label.color = Color.white; }
            return;
        }
        if (_isFlagged[idx])
        {
            btn.image.color = new Color(1f, 0.6f, 0.15f, 1f);
            if (label != null) { label.text = "F"; label.color = Color.white; }
            return;
        }
        if (!_isRevealed[idx])
        {
            btn.image.color = new Color(0.45f, 0.50f, 0.58f, 1f);
            if (label != null) { label.text = ""; }
            return;
        }
        // Revealed.
        if (_isMine[idx])
        {
            btn.image.color = new Color(0.65f, 0.12f, 0.12f, 1f);
            if (label != null) { label.text = "*"; label.color = Color.white; }
            return;
        }
        btn.image.color = new Color(0.82f, 0.84f, 0.86f, 1f);
        int n = _adjacent[idx];
        if (label != null)
        {
            if (n == 0) { label.text = ""; }
            else { label.text = n.ToString(); label.color = _NumberColor(n); }
        }
    }

    private Color _NumberColor(int n)
    {
        if (n == 1) return new Color(0.10f, 0.30f, 0.95f, 1f);
        if (n == 2) return new Color(0.10f, 0.55f, 0.20f, 1f);
        if (n == 3) return new Color(0.90f, 0.15f, 0.15f, 1f);
        if (n == 4) return new Color(0.05f, 0.10f, 0.50f, 1f);
        if (n == 5) return new Color(0.50f, 0.05f, 0.10f, 1f);
        if (n == 6) return new Color(0.10f, 0.50f, 0.55f, 1f);
        if (n == 7) return Color.black;
        return new Color(0.35f, 0.35f, 0.35f, 1f);
    }

    private void _UpdateMinesCounter()
    {
        int remaining = MinesTotal - _flagsPlaced;
        minesText.text = "Mines: " + remaining.ToString();
    }

    private void _SetupRect(RectTransform rt, float w, float h, float anchoredX, float anchoredY)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(anchoredX, anchoredY);
    }
}
