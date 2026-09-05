using MagicPigGames;
using UnityEngine;

/// <summary>
/// Bridges <see cref="InkDraw"/> to the Magic Pig Games progress bar template:
/// listens to the ink manager's InkChanged event and forwards the 0..1 value
/// to <see cref="ProgressBar.SetProgress"/>. Works with both the Vertical and
/// Horizontal Progress Bar prefabs, since both derive from ProgressBar.
///
/// Setup: drop the "Vertical Progress Bar" prefab under your Canvas, add this
/// component next to (or anywhere near) it, and assign the two references.
/// </summary>
public class InkBarUI : MonoBehaviour
{
    [Tooltip("The ink manager to display. If empty, the first InkDraw in the scene is found on Start.")]
    [SerializeField] private InkDraw inkDraw;

    [Tooltip("The progress bar from the template (e.g. the Vertical Progress Bar prefab instance).")]
    [SerializeField] private ProgressBar progressBar;

    [Tooltip("Skip updates smaller than this, so the bar's transition coroutine is not restarted " +
             "every frame while ink refills. 0 = forward every change.")]
    [Range(0f, 0.05f)]
    [SerializeField] private float minChangeToUpdate = 0.003f;

    private float lastSent = -1f;

    private void Start()
    {
        if (inkDraw == null)
            inkDraw = FindFirstObjectByType<InkDraw>();

        if (progressBar == null)
            progressBar = GetComponentInChildren<ProgressBar>();

        if (inkDraw == null || progressBar == null)
        {
            Debug.LogWarning($"{nameof(InkBarUI)}: missing {(inkDraw == null ? "InkDraw" : "ProgressBar")} " +
                             "reference; the ink bar will not update.", this);
            enabled = false;
            return;
        }

        inkDraw.InkChanged += OnInkChanged;
        Push(inkDraw.NormalizedInk); // bar starts where the tank starts (full)
    }

    private void OnDestroy()
    {
        if (inkDraw != null)
            inkDraw.InkChanged -= OnInkChanged;
    }

    private void OnInkChanged(float normalized)
    {
        // Always let the exact endpoints through so the bar visibly reaches
        // truly full and truly empty, throttle everything in between.
        bool endpoint = normalized <= 0f || normalized >= 1f;
        if (!endpoint && Mathf.Abs(normalized - lastSent) < minChangeToUpdate)
            return;

        Push(normalized);
    }

    private void Push(float normalized)
    {
        lastSent = normalized;
        progressBar.SetProgress(Mathf.Clamp01(normalized));
    }
}
