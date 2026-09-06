using UnityEngine;

/// <summary>
/// The gameplay half of the fan: pushes the player (by tag) along this
/// object's UP direction while they are inside the fan area. Pairs with
/// <see cref="FanParticles"/> on the same object — the trigger auto-sizes to
/// the particle area, so the push region always matches what the player sees.
/// Rotate the object and the particles and the push rotate together.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class FanForce : MonoBehaviour
{
    [Tooltip("Only objects with this tag are pushed.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Continuous push force, in newtons, along this object's up direction. " +
             "Must beat gravity on the player's Rigidbody2D to lift them.")]
    [Min(0f)]
    [SerializeField] private float pushForce = 25f;

    [Tooltip("Stop pushing once the player already moves this fast in the push direction. " +
             "Keeps the fan from accelerating them forever. 0 = no limit.")]
    [Min(0f)]
    [SerializeField] private float maxPushSpeed = 6f;

    [Tooltip("Match the trigger size to the FanParticles area on this object automatically.")]
    [SerializeField] private bool syncSizeToParticles = true;

    private BoxCollider2D box;

    private void OnEnable()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        SyncSize();
    }

    private void SyncSize()
    {
        if (!syncSizeToParticles)
            return;

        FanParticles particles = GetComponent<FanParticles>();
        if (particles != null)
        {
            box.size = particles.AreaSize;
            box.offset = Vector2.zero;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        Rigidbody2D rb = other.attachedRigidbody;
        if (rb == null)
            return;

        Vector2 pushDirection = transform.up;

        // Already at wind speed: let them ride instead of accelerating forever.
        if (maxPushSpeed > 0f && Vector2.Dot(rb.linearVelocity, pushDirection) >= maxPushSpeed)
            return;

        rb.AddForce(pushDirection * pushForce);
    }

    private void OnValidate()
    {
        if (box == null)
            box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            SyncSize();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Push direction arrow from the center.
        Gizmos.color = new Color(0.4f, 1f, 0.6f);
        Vector3 tip = transform.position + transform.up * 1.5f;
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawLine(tip, tip - transform.up * 0.3f + transform.right * 0.2f);
        Gizmos.DrawLine(tip, tip - transform.up * 0.3f - transform.right * 0.2f);
    }
}
