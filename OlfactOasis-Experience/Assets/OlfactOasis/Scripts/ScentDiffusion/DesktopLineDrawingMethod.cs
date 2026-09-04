using UnityEngine;
using UnityEngine.InputSystem;

// Desktop stand-in for the VR curve drawing method: click and drag inside a RectTransform on a Canvas
// to trace a continuous line with the mouse, bounded to that area, drawn live via a UILineRenderer.
public class DesktopLineDrawingMethod : AbstractCurveDrawingMethod
{
    [Header("Canvas")]
    [SerializeField] Canvas _canvas;

    [Header("Drawing area")]
    [Tooltip("Bounds within which the line is drawn. Defaults to this object's own RectTransform if left empty.")]
    [SerializeField] RectTransform _drawingArea;
    [Tooltip("Leave empty for a Screen Space - Overlay canvas.")]
    [SerializeField] Camera _uiCamera;

    [Header("Visual feedback")]
    [SerializeField] UILineRenderer _lineRenderer;

    [Header("Sampling")]
    [SerializeField] float _minPointDistance = 5f;

    [Header("Input")]
    [SerializeField] InputAction _pressAction;

    bool _isDrawing;
    bool _isTracingStroke;
    Vector2 _lastLocalPoint;

    // A Screen Space - Overlay canvas ignores cameras entirely; a stray _uiCamera assignment there
    // makes RectTransformUtility project through real 3D space instead of the canvas's screen-space shortcut.
    Camera EffectiveCamera => _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _uiCamera;

    void Awake()
    {
        if (_drawingArea == null) _drawingArea = transform as RectTransform;

        _canvas.enabled = false;
    }

    void OnEnable()
    {
        _pressAction.Enable();
    }

    void OnDisable()
    {
        _pressAction.Disable();
    }

    protected override void OnStartDraw()
    {
        _canvas.enabled = true;
        _isDrawing = true;
        _isTracingStroke = false;
        if (_lineRenderer != null) _lineRenderer.Clear();
    }

    protected override void OnEndDraw()
    {
        _canvas.enabled = false;
        _isDrawing = false;
        _isTracingStroke = false;
    }

    void Update()
    {
        if (!_isDrawing) return;

        Vector2 pointerPosition = Pointer.current.position.ReadValue();

        if (!_isTracingStroke)
        {
            if (_pressAction.WasPressedThisFrame() && IsPointerInsideDrawingArea(pointerPosition) && TryGetLocalPoint(pointerPosition, out Vector2 startPoint))
            {
                _isTracingStroke = true;
                RecordPoint(startPoint);
            }
            return;
        }

        if (_pressAction.WasReleasedThisFrame())
        {
            _isTracingStroke = false;
            EndDraw();
            return;
        }

        if (TryGetLocalPoint(pointerPosition, out Vector2 localPoint) &&
            Vector2.Distance(localPoint, _lastLocalPoint) >= _minPointDistance)
        {
            RecordPoint(localPoint);
        }
    }

    void RecordPoint(Vector2 localPoint)
    {
        _lastLocalPoint = localPoint;
        AddPoint(new Vector3(localPoint.x, localPoint.y, 0f));
        if (_lineRenderer != null) _lineRenderer.SetPoints(GetPoints());
    }

    bool IsPointerInsideDrawingArea(Vector2 screenPoint)
    {
        return _drawingArea != null && RectTransformUtility.RectangleContainsScreenPoint(_drawingArea, screenPoint, EffectiveCamera);
    }

    bool TryGetLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (_drawingArea == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_drawingArea, screenPoint, EffectiveCamera, out localPoint)) return false;

        Rect rect = _drawingArea.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
        localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax);
        return true;
    }
}
