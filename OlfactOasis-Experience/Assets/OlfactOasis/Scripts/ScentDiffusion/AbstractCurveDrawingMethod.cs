using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractCurveDrawingMethod : MonoBehaviour
{
    List<Vector3> points = new List<Vector3>();
    System.Action _onDrawEnd;

    public void StartDraw(System.Action onDrawEnd)
    {
        _onDrawEnd = onDrawEnd;
        points.Clear();
        OnStartDraw();
    }

    public void EndDraw()
    {
        OnEndDraw();

        System.Action onDrawEnd = _onDrawEnd;
        _onDrawEnd = null;
        onDrawEnd?.Invoke();

        points.Clear();
    }

    protected abstract void OnEndDraw();

    protected abstract void OnStartDraw();

    protected void AddPoint(Vector3 point) => points.Add(point);

    public List<Vector3> GetPoints()
    {
        if(points == null || points.Count == 0)
        {
            LLogger.W("No points have been drawn yet. Please call StartDraw() and EndDraw() before calling GetPoints.");
            return new List<Vector3>();
        }
        return points;
    }
}