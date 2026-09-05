using UnityEngine;

/// <summary>
/// A dart flying in a straight horizontal line. Spawned and configured by
/// <see cref="DartShooter"/>; despawns on its first solid hit or after
/// <see cref="lifetime"/> seconds so strays never pile up off-screen.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Dart : MonoBehaviour
{
    [Tooltip("Flight speed, in units per second.")]
    [SerializeField] private float speed = 12f;

    [Tooltip("Seconds before an airborne dart cleans itself up.")]
    [SerializeField] private float lifetime = 4f;

    private Rigidbody2D rb;
    private int direction = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Darts fly flat: no gravity, no spin, and they should pass through
        // triggers but stop on anything solid.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    /// <summary>Called by the shooter right after Instantiate. Direction is +1 (right) or -1 (left).</summary>
    public void Launch(int direction, float speed, float lifetime)
    {
        this.direction = direction >= 0 ? 1 : -1;
        this.speed = speed;
        this.lifetime = lifetime;

        // Face the way it flies (the sprite is authored pointing right).
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * this.direction,
            transform.localScale.y,
            transform.localScale.z);

        Destroy(gameObject, this.lifetime);
    }

    private void FixedUpdate()
    {
        // Velocity is re-asserted every step so a glancing hit cannot deflect
        // the dart into a diagonal.
        rb.linearVelocity = new Vector2(direction * speed, 0f);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }
}
