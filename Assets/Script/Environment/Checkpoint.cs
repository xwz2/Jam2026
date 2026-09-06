using UnityEngine;

/// <summary>
/// A checkpoint: a trigger region plus a respawn transform. When the player
/// walks into the region, this becomes their active checkpoint — dying
/// afterwards respawns them at <see cref="respawnPoint"/> instead of the
/// scene-start position. The latest checkpoint entered always wins.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Where the player reappears after dying with this checkpoint active. " +
             "Empty = this object's own position.")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("Only objects with this tag activate the checkpoint.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Optional: a SpriteRenderer to tint when the checkpoint becomes active (e.g. a flag).")]
    [SerializeField] private SpriteRenderer activationRenderer;

    [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.5f);

    /// <summary>True once the player has touched this checkpoint.</summary>
    public bool IsActivated { get; private set; }

    /// <summary>World position the player respawns at.</summary>
    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null)
            return;

        respawn.SetCheckpoint(this);

        if (!IsActivated)
        {
            IsActivated = true;
            if (activationRenderer != null)
                activationRenderer.color = activeColor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // The trigger region.
        var box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        }

        // The respawn spot, and the link from region to spot.
        Gizmos.color = new Color(0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(RespawnPosition, 0.25f);
        Gizmos.DrawLine(transform.position, RespawnPosition);
    }
}
