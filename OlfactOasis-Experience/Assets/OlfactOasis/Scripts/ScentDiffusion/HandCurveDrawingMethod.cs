using EditorAttributes;
using UnityEngine;

// VR curve drawing method: while a pinch (Player.cs) is held, the pinching hand's fingertip traces a
// line in the air, drawn live via a LineRenderer. Releasing and pinching again continues the same line.
// The drawing is validated (EndDraw) by pressing _validateButton, and wiped by pressing _clearButton.
//
// Relies on Player's PinchReleased gesture to know when the pinch stops : the pinch ActiveStateUnityEventWrapper's
// _whenDeactivated must call Player.OnLeftPinchReleased / OnRightPinchReleased.
public class HandCurveDrawingMethod : AbstractCurveDrawingMethod, IPlayerGesturesListener
{
    // PushButton listeners don't say which button fired, so each button gets its own relay.
    class ButtonRelay : IButtonListener
    {
        readonly System.Action _onButtonDown;
        public ButtonRelay(System.Action onButtonDown) => _onButtonDown = onButtonDown;
        public void OnButtonDown() => _onButtonDown?.Invoke();
        public void OnButtonUp() { }
    }

    [Header("Dependencies")]
    [SerializeField] Player _player;
    [SerializeField] PushButton _validateButton;
    [SerializeField] PushButton _clearButton;

    [Header("Visual feedback")]
    [SerializeField] LineRenderer _lineRenderer;

    [Header("Recording")]
    [Tooltip("Points are recorded in this transform's local space. Defaults to this object's own transform if left empty.")]
    [SerializeField] Transform _drawingSpace;
    [Tooltip("Minimum world-space distance (meters) between two recorded points.")]
    [SerializeField] float _minPointDistance = 0.005f;

    [Header("Debug")]
    [SerializeField] bool _debug;

    ButtonRelay _validateRelay;
    ButtonRelay _clearRelay;

    bool _isDrawing;
    bool _isTracingStroke;
    ESide _strokeSide;
    Vector3 _lastWorldPoint;

    void Awake()
    {
        if (_drawingSpace == null) _drawingSpace = transform;

        _validateRelay = new ButtonRelay(OnValidatePressed);
        _clearRelay = new ButtonRelay(OnClearPressed);

        if (_lineRenderer != null)
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 0;
            _lineRenderer.enabled = false;
        }
    }

    // Listeners are registered for this object's whole lifetime and gated by _isDrawing, rather than
    // added/removed in OnStartDraw/OnEndDraw : EndDraw runs from inside PushButton's listener loop.
    void Start()
    {
        if (_player == null) _player = Player.Instance;

        if (_player == null) LLogger.E("HandCurveDrawingMethod: no Player found.");
        else _player.AddListener(this);

        if (_validateButton == null) LLogger.E("HandCurveDrawingMethod: no validate PushButton assigned.");
        else _validateButton.AddListener(_validateRelay);

        if (_clearButton == null) LLogger.W("HandCurveDrawingMethod: no clear PushButton assigned.");
        else _clearButton.AddListener(_clearRelay);
    }

    void OnDestroy()
    {
        if (_player != null) _player.RemoveListener(this);
        if (_validateButton != null) _validateButton.RemoveListener(_validateRelay);
        if (_clearButton != null) _clearButton.RemoveListener(_clearRelay);
    }

    [Button("Debug : Start Draw")]
    void DebugStartDraw() => StartDraw(() => Log($"draw validated with {GetPoints().Count} points."));

    protected override void OnStartDraw()
    {
        Log("draw started, waiting for a pinch.");
        _isDrawing = true;
        _isTracingStroke = false;
        ClearLine();
        if (_lineRenderer != null) _lineRenderer.enabled = true;
    }

    protected override void OnEndDraw()
    {
        Log("draw ended.");
        _isDrawing = false;
        _isTracingStroke = false;
        if (_lineRenderer != null) _lineRenderer.enabled = false;
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = default)
    {
        if (gesture != EPlayerGesture.Pinch && gesture != EPlayerGesture.PinchReleased) return;

        if (!_isDrawing)
        {
            Log($"{gesture} ({side}) ignored : StartDraw() hasn't been called.");
            return;
        }

        if (gesture == EPlayerGesture.Pinch && !_isTracingStroke)
        {
            if (!TryGetFingerTip(side, out Transform fingerTip))
            {
                LLogger.W($"HandCurveDrawingMethod: no finger tip on Player for side {side}, pinch ignored.");
                return;
            }

            _isTracingStroke = true;
            _strokeSide = side;
            RecordPoint(fingerTip.position);
            Log($"stroke started ({side}) at {fingerTip.position}.");
        }
        else if (gesture == EPlayerGesture.PinchReleased && _isTracingStroke && side == _strokeSide)
        {
            _isTracingStroke = false;
            Log($"stroke ended ({side}), {PointCount} points so far.");
        }
    }

    void Update()
    {
        if (!_isDrawing || !_isTracingStroke) return;
        if (!TryGetFingerTip(_strokeSide, out Transform fingerTip)) return;

        Vector3 worldPoint = fingerTip.position;
        if (Vector3.Distance(worldPoint, _lastWorldPoint) >= _minPointDistance)
        {
            RecordPoint(worldPoint);
        }
    }

    void OnValidatePressed()
    {
        if (!_isDrawing) return;

        // Nothing traced yet (or a single stray point) : ignore the press, let the participant draw first.
        if (PointCount < 2)
        {
            LLogger.W("HandCurveDrawingMethod: validate pressed before any line was drawn, ignored.");
            return;
        }

        EndDraw();
    }

    void OnClearPressed()
    {
        if (!_isDrawing) return;

        Log("line cleared.");

        _isTracingStroke = false;
        ClearLine();
    }

    void RecordPoint(Vector3 worldPoint)
    {
        _lastWorldPoint = worldPoint;
        AddPoint(_drawingSpace.InverseTransformPoint(worldPoint));

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount++;
            _lineRenderer.SetPosition(_lineRenderer.positionCount - 1, worldPoint);
        }
    }

    void ClearLine()
    {
        ClearPoints();
        if (_lineRenderer != null) _lineRenderer.positionCount = 0;
    }

    void Log(string message)
    {
        if (_debug) LLogger.L($"HandCurveDrawingMethod: {message}");
    }

    bool TryGetFingerTip(ESide side, out Transform fingerTip)
    {
        fingerTip = null;
        if (_player == null) return false;

        GameObject tip = side switch
        {
            ESide.Left => _player.LeftFingerTip,
            ESide.Right => _player.RightFingerTip,
            _ => null
        };

        if (tip == null) return false;
        fingerTip = tip.transform;
        return true;
    }
}
