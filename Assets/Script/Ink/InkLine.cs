using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One drawn ink stroke: a LineRenderer for the visual and an EdgeCollider2D
/// for physics, so the character can stand and jump on it. Built point by
/// point by <see cref="InkDraw"/> while the player drags, then solidified
/// with <see cref="Finish"/> on release.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class InkLine : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private EdgeCollider2D edgeCollider;
    private readonly List<Vector2> points = new List<Vector2>();
    private float lifetime = -1f; // <0 = never expires
    private float fadeDuration;
    private float age;
    private Color baseColor;

    /// <summary>World-space length of the stroke so far — what ink cost is based on.</summary>
    public float TotalLength { get; private set; }

    public bool IsFinished { get; private set; }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        edgeCollider = GetComponent<EdgeCollider2D>();

        // The collider stays disabled while drawing so a half-drawn stroke
        // cannot catch the character mid-air; Finish() turns it on.
        edgeCollider.enabled = false;
    }

    /// <summary>Called once by the manager right after Instantiate.</summary>
    public void Begin(float width, Color color, Material material, PhysicsMaterial2D physicsMaterial)
    {
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        if (material != null)
            lineRenderer.material = material;

        baseColor = color;

        // Give the edge the same thickness as the visual so the character
        // stands on the drawn surface, not on an invisible hairline.
        edgeCollider.edgeRadius = width * 0.5f;
        if (physicsMaterial != null)
            edgeCollider.sharedMaterial = physicsMaterial;
    }

    /// <summary>Appends a point and returns the world-space distance added (0 for the first point).</summary>
    public float AddPoint(Vector2 worldPoint)
    {
        float added = points.Count > 0 ? Vector2.Distance(points[points.Count - 1], worldPoint) : 0f;

        points.Add(worldPoint);
        TotalLength += added;

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, worldPoint);

        return added;
    }

    /// <summary>Last point of the stroke, used by the manager for spacing checks.</summary>
    public Vector2 LastPoint => points.Count > 0 ? points[points.Count - 1] : Vector2.zero;

    public int PointCount => points.Count;

    /// <summary>
    /// Turns the stroke solid. Returns false for degenerate strokes (a click
    /// with no drag), which the manager destroys and refunds.
    /// </summary>
    public bool Finish()
    {
        IsFinished = true;

        if (points.Count < 2)
            return false;

        edgeCollider.SetPoints(points);
        edgeCollider.enabled = true;
        return true;
    }

    /// <summary>
    /// Arms the self-destruct timer; called by the manager on finished strokes
    /// when line expiry is enabled. The clock only runs once the stroke is
    /// finished, so time spent drawing does not count against it.
    /// </summary>
    public void SetLifetime(float seconds, float fadeSeconds)
    {
        lifetime = seconds;
        fadeDuration = Mathf.Min(fadeSeconds, seconds);
    }

    private void Update()
    {
        if (!IsFinished || lifetime < 0f)
            return;

        age += Time.deltaTime;

        // Fade the tail end of the lifetime as a "this is about to go" warning.
        if (fadeDuration > 0f)
        {
            float remaining = lifetime - age;
            if (remaining < fadeDuration)
            {
                Color faded = baseColor;
                faded.a = baseColor.a * Mathf.Clamp01(remaining / fadeDuration);
                lineRenderer.startColor = faded;
                lineRenderer.endColor = faded;
            }
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }
}
