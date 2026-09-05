using UnityEngine;

/// <summary>
/// A fireball that flies straight along its own facing — spawn it rotated and
/// it follows that heading forever. It explodes on touching anything solid
/// (killing the character on a direct hit) or quietly fades out when its
/// lifetime runs dry. Spawned and configured by <see cref="GameManagerTetris"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Fireball : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Forward speed, in units per second, along the fireball's own right axis.")]
    [SerializeField] private float speed = 3f;

    [Tooltip("Degrees added to the facing if the sprite is not authored pointing right (e.g. 90 if it points up).")]
    [SerializeField] private float spriteAngleOffset = 0f;

    [Header("Lifetime")]
    [Tooltip("Seconds before the fireball fades out on its own. Randomized by the spawner (10-15 by default).")]
    [SerializeField] private float lifetime = 12f;

    [Tooltip("Seconds of alpha fade at the end of the lifetime. Included in the lifetime, not added on top.")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Visual & explosion")]
    [Tooltip("Child holding the fireball's sprite. Faded out quickly on impact so the explosion takes over.")]
    [SerializeField] private GameObject visual;

    [Tooltip("Inactive child holding the explosion animation. Activated on impact, played exactly once, " +
             "never repeated; the whole fireball is destroyed on the animation's last frame.")]
    [SerializeField] private GameObject explosion;

    [Tooltip("Seconds to fade the fireball sprite out after impact. Keep short so the explosion reads as the hit.")]
    [Min(0f)]
    [SerializeField] private float impactFadeDuration = 0.12f;

    [Tooltip("How long the explosion lasts if its object has no Animator (fallback only).")]
    [Min(0.05f)]
    [SerializeField] private float explosionFallbackDuration = 0.6f;

    private Rigidbody2D rb;
    private SpriteRenderer[] renderers;
    private float age;
    private bool exploded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // The explosion must be dormant until impact — enforce it even if the
        // prefab was saved with it active, and do it BEFORE collecting the
        // renderers so the explosion's sprite is never part of the fireball fade.
        if (explosion != null)
            explosion.SetActive(false);

        renderers = visual != null
            ? visual.GetComponentsInChildren<SpriteRenderer>()
            : GetComponentsInChildren<SpriteRenderer>();

        // Fireballs fly on rails: no gravity, no spin from physics, and a
        // trigger collider so they detect hits without shoving blocks around.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    /// <summary>Called by the spawner right after Instantiate.</summary>
    public void Init(float speed, float lifetime)
    {
        this.speed = speed;
        this.lifetime = lifetime;
    }

    private void FixedUpdate()
    {
        if (exploded)
            return;

        // Heading derives from the spawn rotation every step, so the path is
        // always a straight line along wherever the fireball points.
        Vector2 forward = Quaternion.Euler(0f, 0f, spriteAngleOffset) * transform.right;
        rb.linearVelocity = forward * speed;
    }

    private void Update()
    {
        if (exploded)
            return;

        age += Time.deltaTime;

        float remaining = lifetime - age;
        if (fadeDuration > 0f && remaining < fadeDuration)
            SetVisualAlpha(Mathf.Clamp01(remaining / fadeDuration));

        // Timeout is a quiet fade-out, not an explosion.
        if (age >= lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Fireballs pass through each other; everything else detonates them.
        if (other.GetComponentInParent<Fireball>() != null)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
            player.Kill();

        Explode();
    }

    private void Explode()
    {
        if (exploded)
            return;
        exploded = true;

        // Freeze in place and go inert: no more flying, no more hits.
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        StartCoroutine(ExplodeRoutine());
    }

    private System.Collections.IEnumerator ExplodeRoutine()
    {
        if (explosion != null)
        {
            // The explosion should not inherit the flight rotation.
            explosion.transform.rotation = Quaternion.identity;
            explosion.SetActive(true);
        }

        // Fast fade of the fireball sprite while the explosion takes over.
        float t = 0f;
        while (t < impactFadeDuration)
        {
            t += Time.deltaTime;
            SetVisualAlpha(1f - t / impactFadeDuration);
            yield return null;
        }
        SetVisualAlpha(0f);

        Animator animator = explosion != null ? explosion.GetComponentInChildren<Animator>() : null;
        if (animator != null)
        {
            // Let the Animator enter its default state, then watch that one
            // play-through. Destroying (and freezing) at normalizedTime 1 means
            // the animation ends on its last frame and can never loop back to
            // frame 0 — even if the clip itself has Loop Time ticked.
            yield return null;
            while (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
                yield return null;

            if (animator != null)
                animator.speed = 0f; // pin the last frame for the destroy frame
        }
        else
        {
            yield return new WaitForSeconds(explosionFallbackDuration);
        }

        Destroy(gameObject);
    }

    private void SetVisualAlpha(float alpha)
    {
        foreach (SpriteRenderer r in renderers)
        {
            if (r == null)
                continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}
