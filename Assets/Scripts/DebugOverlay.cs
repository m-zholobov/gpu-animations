using Crowd;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DebugOverlay : MonoBehaviour
{
    private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]DebugOverlay.Update");
        
    [SerializeField] private CrowdManager _crowdManager;
    [SerializeField] private bool _showFPS = true;
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private Color _textColor = Color.green;
    [SerializeField] private Vector2 _position = new Vector2(10, 10);
    [SerializeField] private int _targetFrameRate = 120;

    private float _displayFPS;
    private int _frameCount;
    private float _accumulatedTime;
    private const float UpdateInterval = 1.0f;

    private GUIStyle _guiStyle;
    private bool _guiStyleInitialized;

    private string _cachedOverlayText = string.Empty;
    private int _cachedFpsInt = int.MinValue;
    private int _cachedAgents = int.MinValue;
        
    private Vector2Int _appResolution;

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = _targetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        InitializeGUIStyle();
        CalcAppResolution();
    }

    private void Update()
    {
        using (UpdateMarker.Auto())
        {
            if (!_showFPS) return;

            _frameCount++;
            _accumulatedTime += Time.unscaledDeltaTime;

            if (_accumulatedTime >= UpdateInterval)
            {
                _displayFPS = _frameCount / _accumulatedTime;
                _frameCount = 0;
                _accumulatedTime = 0f;
            }

            RefreshOverlayCache();
        }
    }

    private void RefreshOverlayCache()
    {
        int fpsInt = Mathf.CeilToInt(_displayFPS);
        int agents = _crowdManager != null ? _crowdManager.ActiveAgentCount : -1;
        if (fpsInt == _cachedFpsInt && agents == _cachedAgents)
            return;

        _cachedFpsInt = fpsInt;
        _cachedAgents = agents;

        if (_crowdManager != null)
            _cachedOverlayText = "FPS: " + fpsInt + "\nAgents: " + agents + "\n" + _appResolution.x + "x" + _appResolution.y;
        else
            _cachedOverlayText = "FPS: " + fpsInt + "\n" + _appResolution.x + "x" + _appResolution.y;
    }

    private void OnGUI()
    {
        if (!_showFPS)
            return;

        if (!_guiStyleInitialized)
            InitializeGUIStyle();

        _guiStyle.normal.textColor = _cachedFpsInt < 30 ? Color.red : Color.green;

        GUI.Label(new Rect(_position.x, _position.y, 300, 150), _cachedOverlayText, _guiStyle);
    }

    private void InitializeGUIStyle()
    {
        _guiStyle = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = _fontSize,
            fontStyle = FontStyle.Bold
        };
        _guiStyle.normal.textColor = _textColor;
        _guiStyleInitialized = true;
    }

    private void CalcAppResolution()
    {
        var scale = UniversalRenderPipeline.asset.renderScale;
        var width = Mathf.RoundToInt(Screen.width * scale);
        var height = Mathf.RoundToInt(Screen.height * scale);
        _appResolution = new Vector2Int(width, height);
    }
}