using MagicPigGames;
using UnityEngine;

/// <summary>
/// A horizontal Magic Pig Games progress bar that drains from the LEFT side
/// instead of the right. Same overlay mechanism as the stock bar; the only
/// change is which edge the overlay is anchored to, mirroring how
/// <see cref="VerticalProgressBar"/> re-anchors for its orientation.
/// Swap this in for HorizontalProgressBar on the bar object to flip it.
/// </summary>
public class HorizontalProgressBarFlipped : ProgressBar
{
    // Pin the overlay to the LEFT edge so it grows rightward as it covers the
    // fill: the visible liquid then shrinks left-to-right, the mirror of stock.
    protected override void CheckOverlayBarRectTransform()
    {
        if (overlayBar == null)
            return;

        if (overlayBar.anchorMin == new Vector2(0, 0)
            && overlayBar.anchorMax == new Vector2(0, 1)
            && Mathf.Approximately(overlayBar.pivot.x, 0f))
            return;

        overlayBar.anchorMin = new Vector2(0, 0); // anchor to the left edge
        overlayBar.anchorMax = new Vector2(0, 1); // stretch vertically
        overlayBar.pivot = new Vector2(0f, 0.5f); // grow away from the left edge
        overlayBar.anchoredPosition = new Vector2(0f, overlayBar.anchoredPosition.y);
    }
}
