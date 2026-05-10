using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;

public partial class FlappyBird : UdonSharpBehaviour
{
    [HideInInspector] public DmnwareDevice Device;
    [HideInInspector] public DmnwareApp App;

    private const float ScreenW = 1080f;
    private const float ScreenH = 1670f;

    private const float Gravity = -2400f;
    private const float FlapVel = 900f;
    private const float PipeSpeed = 360f;
    private const float PipeGap = 380f;
    private const float PipeWidth = 160f;
    private const float PipeHeight = 1000f;
    private const float PipeSpacing = 540f;
    private const float BirdSize = 90f;
    private const float BirdX = 280f;
    private const float GroundH = 200f;

    private const int StateIdle = 0;
    private const int StatePlay = 1;
    private const int StateOver = 2;

    private int _state;
    private float _birdY;
    private float _birdVy;
    private int _score;
    private int _best;
    private bool _isOpen;

    private float[] _pipeX;
    private float[] _pipeGapY;
    private bool[] _pipePassed;

    private RectTransform _birdRT;
    private RectTransform[] _pipeTopRTs;
    private RectTransform[] _pipeBotRTs;

    void Start() { }

    public void _DmnwareAppInit()
    {
        // Disable the auto-layout group on our parent so we can position elements absolutely.
        var parent = bird.gameObject.transform.parent;
        var vlg = parent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;

        _pipeX = new float[3];
        _pipeGapY = new float[3];
        _pipePassed = new bool[3];

        _birdRT = bird.rectTransform;
        _pipeTopRTs = new RectTransform[3];
        _pipeBotRTs = new RectTransform[3];
        _pipeTopRTs[0] = pipeTop1.rectTransform;
        _pipeTopRTs[1] = pipeTop2.rectTransform;
        _pipeTopRTs[2] = pipeTop3.rectTransform;
        _pipeBotRTs[0] = pipeBot1.rectTransform;
        _pipeBotRTs[1] = pipeBot2.rectTransform;
        _pipeBotRTs[2] = pipeBot3.rectTransform;

        // Background fills the full screen. Anchor every element to bottom-left (point anchor)
        // so anchoredPosition is the rect's center in screen-space.
        _SetupRect(bg.rectTransform, ScreenW, ScreenH, ScreenW * 0.5f, ScreenH * 0.5f);
        bg.color = new Color(0.45f, 0.78f, 0.95f, 1f);

        _SetupRect(ground.rectTransform, ScreenW, GroundH, ScreenW * 0.5f, GroundH * 0.5f);
        ground.color = new Color(0.85f, 0.7f, 0.4f, 1f);

        _SetupRect(_birdRT, BirdSize, BirdSize, BirdX, ScreenH * 0.55f);
        bird.color = new Color(1f, 0.85f, 0.2f, 1f);

        Color pipeColor = new Color(0.30f, 0.78f, 0.32f, 1f);
        pipeTop1.color = pipeColor; pipeBot1.color = pipeColor;
        pipeTop2.color = pipeColor; pipeBot2.color = pipeColor;
        pipeTop3.color = pipeColor; pipeBot3.color = pipeColor;

        for (int i = 0; i < 3; i++)
        {
            _SetupRect(_pipeTopRTs[i], PipeWidth, PipeHeight, -2000f, 0f);
            _SetupRect(_pipeBotRTs[i], PipeWidth, PipeHeight, -2000f, 0f);
        }

        // Score text — top of the screen.
        _SetupRect(score.gameObject.GetComponent<RectTransform>(), 600f, 160f, ScreenW * 0.5f, ScreenH - 180f);
        score.fontSize = 120f;
        score.alignment = TextAlignmentOptions.Center;
        score.color = Color.white;

        // Title — visible while idle.
        _SetupRect(title.gameObject.GetComponent<RectTransform>(), ScreenW, 220f, ScreenW * 0.5f, ScreenH * 0.78f);
        title.fontSize = 160f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        // Hint — under the title.
        _SetupRect(hint.gameObject.GetComponent<RectTransform>(), ScreenW, 100f, ScreenW * 0.5f, ScreenH * 0.40f);
        hint.fontSize = 56f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = Color.white;

        // Game-over banner.
        _SetupRect(gameOver.gameObject.GetComponent<RectTransform>(), ScreenW, 220f, ScreenW * 0.5f, ScreenH * 0.65f);
        gameOver.fontSize = 150f;
        gameOver.alignment = TextAlignmentOptions.Center;
        gameOver.color = Color.white;
        gameOver.gameObject.SetActive(false);

        // Best-score line under game over.
        _SetupRect(bestText.gameObject.GetComponent<RectTransform>(), ScreenW, 100f, ScreenW * 0.5f, ScreenH * 0.55f);
        bestText.fontSize = 60f;
        bestText.alignment = TextAlignmentOptions.Center;
        bestText.color = Color.white;
        bestText.gameObject.SetActive(false);

        // Big invisible flap button covers everything above the ground.
        _SetupRect(flap.gameObject.GetComponent<RectTransform>(), ScreenW, ScreenH - GroundH, ScreenW * 0.5f, GroundH + (ScreenH - GroundH) * 0.5f);
        flap.image.color = new Color(0f, 0f, 0f, 0f);

        // Restart button at the bottom — hidden until game over.
        _SetupRect(restart.gameObject.GetComponent<RectTransform>(), 520f, 140f, ScreenW * 0.5f, ScreenH * 0.30f);
        restart.gameObject.SetActive(false);

        _ResetGame();
    }

    public void _DmnwareAppLateInit() { }
    public void _DmnwareAppOpen() { _isOpen = true; }
    public void _DmnwareAppClose() { _isOpen = false; }

    void Update()
    {
        if (!_isOpen) return;

        // Idle bob — gentle sine motion so the bird looks alive while waiting.
        if (_state == StateIdle)
        {
            float bobY = ScreenH * 0.55f + Mathf.Sin(Time.time * 4f) * 30f;
            _birdRT.anchoredPosition = new Vector2(BirdX, bobY);
            _birdRT.localEulerAngles = Vector3.zero;
            return;
        }

        if (_state != StatePlay) return;

        float dt = Time.deltaTime;
        if (dt > 0.05f) dt = 0.05f;

        _birdVy += Gravity * dt;
        _birdY += _birdVy * dt;
        _birdRT.anchoredPosition = new Vector2(BirdX, _birdY);

        float rotZ = Mathf.Clamp(_birdVy * 0.05f, -75f, 35f);
        _birdRT.localEulerAngles = new Vector3(0f, 0f, rotZ);

        for (int i = 0; i < 3; i++)
        {
            _pipeX[i] -= PipeSpeed * dt;

            // Recycle pipes that have left the screen.
            if (_pipeX[i] + PipeWidth * 0.5f < 0f)
            {
                float maxX = _pipeX[0];
                for (int j = 1; j < 3; j++) if (_pipeX[j] > maxX) maxX = _pipeX[j];
                _pipeX[i] = maxX + PipeSpacing;
                _pipeGapY[i] = Random.Range(GroundH + 280f, ScreenH - 350f);
                _pipePassed[i] = false;
            }

            float topY = _pipeGapY[i] + PipeGap * 0.5f + PipeHeight * 0.5f;
            float botY = _pipeGapY[i] - PipeGap * 0.5f - PipeHeight * 0.5f;
            _pipeTopRTs[i].anchoredPosition = new Vector2(_pipeX[i], topY);
            _pipeBotRTs[i].anchoredPosition = new Vector2(_pipeX[i], botY);

            if (!_pipePassed[i] && _pipeX[i] + PipeWidth * 0.5f < BirdX - BirdSize * 0.5f)
            {
                _pipePassed[i] = true;
                _score++;
                score.text = _score.ToString();
            }

            if (_PipeHits(i))
            {
                _GameOver();
                return;
            }
        }

        if (_birdY - BirdSize * 0.5f <= GroundH || _birdY + BirdSize * 0.5f >= ScreenH)
        {
            _GameOver();
        }
    }

    public void _OnFlap()
    {
        if (_state == StateIdle)
        {
            _state = StatePlay;
            title.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
            _birdY = ScreenH * 0.55f;
            _birdVy = FlapVel;
        }
        else if (_state == StatePlay)
        {
            _birdVy = FlapVel;
        }
    }

    public void _OnRestart()
    {
        _ResetGame();
    }

    private void _ResetGame()
    {
        _state = StateIdle;
        _score = 0;
        score.text = "0";
        _birdY = ScreenH * 0.55f;
        _birdVy = 0f;
        _birdRT.anchoredPosition = new Vector2(BirdX, _birdY);
        _birdRT.localEulerAngles = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            _pipeX[i] = ScreenW + 220f + i * PipeSpacing;
            _pipeGapY[i] = Random.Range(GroundH + 280f, ScreenH - 350f);
            _pipePassed[i] = false;
            float topY = _pipeGapY[i] + PipeGap * 0.5f + PipeHeight * 0.5f;
            float botY = _pipeGapY[i] - PipeGap * 0.5f - PipeHeight * 0.5f;
            _pipeTopRTs[i].anchoredPosition = new Vector2(_pipeX[i], topY);
            _pipeBotRTs[i].anchoredPosition = new Vector2(_pipeX[i], botY);
        }

        title.gameObject.SetActive(true);
        hint.gameObject.SetActive(true);
        gameOver.gameObject.SetActive(false);
        bestText.gameObject.SetActive(false);
        restart.gameObject.SetActive(false);
    }

    private void _GameOver()
    {
        _state = StateOver;
        if (_score > _best) _best = _score;
        bestText.text = "Best: " + _best.ToString();
        gameOver.gameObject.SetActive(true);
        bestText.gameObject.SetActive(true);
        restart.gameObject.SetActive(true);
        hint.gameObject.SetActive(false);
        title.gameObject.SetActive(false);
    }

    private bool _PipeHits(int i)
    {
        // Slight collision forgiveness (40% radius rather than 50%).
        float halfBird = BirdSize * 0.4f;
        float pxL = _pipeX[i] - PipeWidth * 0.5f;
        float pxR = _pipeX[i] + PipeWidth * 0.5f;
        if (BirdX + halfBird < pxL) return false;
        if (BirdX - halfBird > pxR) return false;

        float gapTop = _pipeGapY[i] + PipeGap * 0.5f;
        float gapBot = _pipeGapY[i] - PipeGap * 0.5f;
        if (_birdY + halfBird > gapTop) return true;
        if (_birdY - halfBird < gapBot) return true;
        return false;
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
