using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manager for the ink-drawing mechanic. The player holds the mouse button (or
/// a finger) and drags to draw a stroke into the scene; on release the stroke
/// becomes solid 2D geometry the character can jump on.
///
/// Ink is a shared tank: it starts full, drawing spends it per unit of line
/// length, and it refills at <see cref="refillRate"/> over time. When the tank
/// runs dry mid-stroke the stroke simply ends where the ink ran out.
/// </summary>
public class InkDraw : MonoBehaviour
{
    [Header("Ink tank")]
    [Tooltip("Total ink, measured in world-units of line length. Starts full.")]
    [Min(0.1f)]
    [SerializeField] private float capacity = 20f;

    [Tooltip("Ink regained per second.")]
    [Min(0f)]
    [SerializeField] private float refillRate = 2f;

    [Tooltip("Also refill while the player is actively drawing. Off = the tank only recovers between strokes.")]
    [SerializeField] private bool refillWhileDrawing = false;

    [Tooltip("A new stroke cannot start unless at least this much ink is available, so taps with an " +
             "empty tank do not spam dot-strokes.")]
    [Min(0f)]
    [SerializeField] private float minInkToStart = 0.5f;

    [Tooltip("Ink spent per world-unit of drawn line. 1 = a 5-unit line costs 5 ink; 2 = the same line costs 10.")]
    [Min(0.01f)]
    [SerializeField] private float inkCostPerUnit = 1f;

    [Tooltip("Remaining ink, live. Shown for tuning/debugging; the game resets it to Capacity on Play.")]
    [SerializeField] private float currentInk;

    [Header("Drawing")]
    [Tooltip("Optional stroke prefab with an InkLine component. Leave empty to build strokes from scratch.")]
    [SerializeField] private InkLine linePrefab;

    [Tooltip("A new point is only added once the pointer has moved this far in world units. " +
             "Lower = smoother curves, more collider vertices.")]
    [Min(0.01f)]
    [SerializeField] private float minPointSpacing = 0.1f;

    [Tooltip("Stroke thickness in world units — both the visual and the collider.")]
    [Min(0.01f)]
    [SerializeField] private float lineWidth = 0.12f;

    [SerializeField] private Color lineColor = new Color(0.15f, 0.25f, 0.9f);

    [Tooltip("Material for the stroke. Leave empty to use the default sprite material.")]
    [SerializeField] private Material lineMaterial;

    [Tooltip("Optional physics material for the stroke surface (friction/bounce under the character).")]
    [SerializeField] private PhysicsMaterial2D linePhysicsMaterial;

    [Tooltip("Camera used to convert pointer position to world space. Empty = Camera.main.")]
    [SerializeField] private Camera drawCamera;

    [Header("Line lifetime")]
    [Tooltip("When on, finished strokes disappear on their own after Line Lifetime seconds.")]
    [SerializeField] private bool linesExpire = true;

    [Tooltip("Seconds a finished stroke stays in the scene before disappearing.")]
    [Min(0.1f)]
    [SerializeField] private float lineLifetime = 10f;

    [Tooltip("Seconds of fade-out at the end of the lifetime, so platforms do not vanish without warning. " +
             "0 = pop out instantly. Included in the lifetime, not added on top.")]
    [Min(0f)]
    [SerializeField] private float lineFadeDuration = 0.75f;

    [Header("Housekeeping")]
    [Tooltip("Maximum finished strokes kept in the scene; the oldest is erased (and NOT refunded) " +
             "when the limit is exceeded. 0 = unlimited.")]
    [Min(0)]
    [SerializeField] private int maxLines = 0;

    [Tooltip("Parent for spawned strokes, purely to keep the hierarchy tidy.")]
    [SerializeField] private Transform linesParent;

    private readonly System.Collections.Generic.List<InkLine> finishedLines =
        new System.Collections.Generic.List<InkLine>();

    private InkLine activeLine;
    private Material runtimeDefaultMaterial;

    /// <summary>Current ink, in world-units of drawable length.</summary>
    public float CurrentInk => currentInk;

    public float Capacity => capacity;

    /// <summary>0..1 fill amount, ready for a UI bar.</summary>
    public float NormalizedInk => capacity > 0f ? currentInk / capacity : 0f;

    public bool IsDrawing => activeLine != null;

    /// <summary>Raised whenever the ink amount changes; the float is <see cref="NormalizedInk"/>.</summary>
    public event Action<float> InkChanged;

    private void Awake()
    {
        currentInk = capacity; // complete at first, per design
        if (drawCamera == null)
            drawCamera = Camera.main;
    }

    private void Update()
    {
        Refill();

        // Pointer.current covers mouse AND touch through one API.
        var pointer = Pointer.current;
        if (pointer == null)
            return;

        if (pointer.press.wasPressedThisFrame)
            TryStartStroke(pointer);
        else if (pointer.press.isPressed && activeLine != null)
            ContinueStroke(pointer);
        else if (pointer.press.wasReleasedThisFrame && activeLine != null)
            FinishStroke();
    }

    private void Refill()
    {
        if (refillRate <= 0f || currentInk >= capacity)
            return;
        if (IsDrawing && !refillWhileDrawing)
            return;

        currentInk = Mathf.Min(capacity, currentInk + refillRate * Time.deltaTime);
        InkChanged?.Invoke(NormalizedInk);
    }

    private void TryStartStroke(Pointer pointer)
    {
        if (currentInk < minInkToStart || drawCamera == null)
            return;

        activeLine = linePrefab != null
            ? Instantiate(linePrefab, linesParent)
            : CreateLineFromScratch();

        activeLine.Begin(lineWidth, lineColor, ResolveMaterial(), linePhysicsMaterial);
        activeLine.AddPoint(PointerToWorld(pointer));
    }

    private void ContinueStroke(Pointer pointer)
    {
        Vector2 world = PointerToWorld(pointer);
        float distance = Vector2.Distance(activeLine.LastPoint, world);
        if (distance < minPointSpacing)
            return;

        // Out of ink mid-drag: clamp the segment to the length the remaining
        // ink can afford, so the stroke ends where the tank hits zero.
        float cost = distance * inkCostPerUnit;
        if (cost > currentInk)
        {
            float affordableLength = currentInk / inkCostPerUnit;
            if (affordableLength <= 0.001f)
            {
                FinishStroke();
                return;
            }
            world = activeLine.LastPoint + (world - activeLine.LastPoint).normalized * affordableLength;
            cost = currentInk;
        }

        activeLine.AddPoint(world);
        currentInk = Mathf.Max(0f, currentInk - cost);
        InkChanged?.Invoke(NormalizedInk);

        if (currentInk <= 0.001f)
            FinishStroke();
    }

    private void FinishStroke()
    {
        if (activeLine == null)
            return;

        bool valid = activeLine.Finish();
        if (!valid)
        {
            // A click without a drag: nothing was really drawn, refund nothing
            // because nothing was spent (single points cost 0 length).
            Destroy(activeLine.gameObject);
        }
        else
        {
            if (linesExpire)
                activeLine.SetLifetime(lineLifetime, lineFadeDuration);

            finishedLines.Add(activeLine);
            EnforceLineLimit();
        }

        activeLine = null;
    }

    private void EnforceLineLimit()
    {
        // Expired strokes destroy themselves; drop their stale entries first.
        finishedLines.RemoveAll(line => line == null);

        if (maxLines <= 0)
            return;

        while (finishedLines.Count > maxLines)
        {
            InkLine oldest = finishedLines[0];
            finishedLines.RemoveAt(0);
            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    /// <summary>Erases every finished stroke. Does not refund ink.</summary>
    public void ClearAllLines()
    {
        foreach (InkLine line in finishedLines)
        {
            if (line != null)
                Destroy(line.gameObject);
        }
        finishedLines.Clear();
    }

    private Vector2 PointerToWorld(Pointer pointer)
    {
        Vector3 screen = pointer.position.ReadValue();
        screen.z = -drawCamera.transform.position.z; // distance to the z=0 gameplay plane
        return drawCamera.ScreenToWorldPoint(screen);
    }

    private InkLine CreateLineFromScratch()
    {
        var go = new GameObject("InkLine");
        go.transform.SetParent(linesParent, worldPositionStays: true);
        go.AddComponent<LineRenderer>();
        go.AddComponent<EdgeCollider2D>();
        return go.AddComponent<InkLine>();
    }

    private Material ResolveMaterial()
    {
        if (lineMaterial != null)
            return lineMaterial;

        if (runtimeDefaultMaterial == null)
            runtimeDefaultMaterial = new Material(Shader.Find("Sprites/Default"));

        return runtimeDefaultMaterial;
    }
}
