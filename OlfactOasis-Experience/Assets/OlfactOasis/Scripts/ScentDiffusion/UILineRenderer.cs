using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Minimal polyline graphic so a curve traced on a Canvas (e.g. by DesktopLineDrawingMethod) has live visual feedback.
[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : MaskableGraphic
{
    [SerializeField] float _thickness = 4f;

    readonly List<Vector2> _points = new();

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetPoints(IReadOnlyList<Vector3> points)
    {
        _points.Clear();
        if (points != null)
        {
            foreach (Vector3 point in points) _points.Add(point);
        }
        SetVerticesDirty();
    }

    public void Clear() => SetPoints(null);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_points.Count < 2) return;

        for (int pointIndex = 0; pointIndex < _points.Count - 1; pointIndex++)
        {
            AddSegment(vh, _points[pointIndex], _points[pointIndex + 1], pointIndex * 4);
        }
    }

    void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, int vertexOffset)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (_thickness * 0.5f);

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - normal;
        vh.AddVert(vertex);
        vertex.position = start + normal;
        vh.AddVert(vertex);
        vertex.position = end + normal;
        vh.AddVert(vertex);
        vertex.position = end - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(vertexOffset, vertexOffset + 1, vertexOffset + 2);
        vh.AddTriangle(vertexOffset + 2, vertexOffset + 3, vertexOffset);
    }
}
