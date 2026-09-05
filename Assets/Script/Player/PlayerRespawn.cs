using System.Collections;
using UnityEngine;

/// <summary>
/// Death-and-respawn flow: when <see cref="PlayerController.Died"/> fires, the
/// character fades out where it fell, teleports back to its spawn point, fades
/// back in and comes alive again. The spawn point defaults to wherever the
/// character started the scene.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Where the character reappears. Empty = the position it had when the scene started.")]
    [SerializeField] private Transform respawnPoint;

    [Tooltip("Seconds the corpse takes to fade out after dying.")]
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Tooltip("Extra invisible pause between fade-out and reappearing, so death registers as a beat.")]
    [Min(0f)]
    [SerializeField] private float respawnDelay = 0.4f;

    [Tooltip("Seconds to fade back in at the spawn point.")]
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.3f;

    private PlayerController controller;
    private Rigidbody2D rb;
    private SpriteRenderer[] renderers;
    private Vector3 initialPosition;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
        initialPosition = transform.position;
    }

    private void OnEnable()
    {
        controller.Died += OnDied;
    }

    private void OnDisable()
    {
        controller.Died -= OnDied;
    }

    private void OnDied()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // Fade out in place — the corpse keeps ragdolling under physics while
        // it disappears, which reads better than freezing mid-air.
        yield return Fade(1f, 0f, fadeOutDuration);

        // Invisible: stop physics so nothing bumps the ghost around, move home.
        rb.simulated = false;
        rb.linearVelocity = Vector2.zero;

        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        transform.position = respawnPoint != null ? respawnPoint.position : initialPosition;

        rb.simulated = true;
        yield return Fade(0f, 1f, fadeInDuration);

        controller.Revive();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
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
