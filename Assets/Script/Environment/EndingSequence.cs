using System.Collections;
using UnityEngine;

/// <summary>
/// The finale. A door trigger: when the player walks in —
///   1. control is taken away and the character fades out,
///   2. the rocket's animation starts and the ship vibrates on the pad,
///   3. it lifts off, accelerating into the sky, with the camera following,
///   4. after a few seconds the camera halts and the ship departs alone.
///
/// Setup: put this on a door object with a trigger region, drag the scene's
/// RocketSpaceShip instance into <see cref="rocket"/>. The rocket's Animator
/// is held paused until ignition.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EndingSequence : MonoBehaviour
{
    [Header("Actors")]
    [Tooltip("The RocketSpaceShip instance in the scene.")]
    [SerializeField] private Transform rocket;

    [Tooltip("Only this tag starts the ending.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Optional framing target: while the ship launches, the camera moves to and holds on THIS " +
             "transform instead of chasing the ship. Place it to compose the launch shot (e.g. framing the " +
             "whole pad and sky). Empty = camera follows the ship itself.")]
    [SerializeField] private Transform cameraFocusTarget;

    [Header("Camera intro")]
    [Tooltip("Seconds the camera takes to reach the focus point and the widened framing. Small = fast, punchy.")]
    [Min(0.05f)]
    [SerializeField] private float introCameraDuration = 0.8f;

    [Header("Ignition")]
    [Tooltip("Seconds of on-pad vibration (with the animation running) before liftoff.")]
    [Min(0f)]
    [SerializeField] private float vibrationDuration = 2.5f;

    [Tooltip("How hard the ship shakes on the pad, in world units. Ramps up over the vibration time.")]
    [SerializeField] private float vibrationIntensity = 0.06f;

    [Header("Liftoff")]
    [Tooltip("Upward acceleration of the ship, in units per second squared.")]
    [SerializeField] private float liftoffAcceleration = 3f;

    [Tooltip("Top ascent speed, in units per second.")]
    [SerializeField] private float maxAscendSpeed = 10f;

    [Tooltip("Seconds the camera follows the climbing ship before it halts and lets the ship go.")]
    [Min(0f)]
    [SerializeField] private float cameraFollowDuration = 8f;

    [Tooltip("THE wideness control: the view eases to this multiple of its gameplay size. " +
             "1 = no zoom, 1.5 = 50 percent wider, 2 = double. This is exact unless Auto Fit Ship is on.")]
    [Min(1f)]
    [SerializeField] private float cameraWidenFactor = 1.5f;

    [Tooltip("When on, the view is widened FURTHER (beyond the factor above) if needed to keep the whole " +
             "ship in frame. Can zoom out a lot if the focus target sits far from the ship - off = full manual control.")]
    [SerializeField] private bool autoFitShip = false;

    [Tooltip("Extra margin around the ship when auto-fitting the view, as a fraction. 0.2 = 20% breathing room.")]
    [Range(0f, 1f)]
    [SerializeField] private float fitMargin = 0.25f;

    [Tooltip("Camera position relative to the ship while following.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.5f, -10f);

    [Tooltip("How quickly the camera catches up to the ship, in seconds of lag.")]
    [Min(0f)]
    [SerializeField] private float cameraDamping = 0.35f;

    [Tooltip("The ship is destroyed this many seconds after liftoff, once far off-screen. 0 = keep forever.")]
    [Min(0f)]
    [SerializeField] private float destroyRocketAfter = 12f;

    private Animator rocketAnimator;
    private bool started;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        if (rocket != null)
        {
            // Hold the launch animation until the character actually arrives.
            rocketAnimator = rocket.GetComponentInChildren<Animator>();
            if (rocketAnimator != null)
                rocketAnimator.speed = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (started || !other.CompareTag(playerTag))
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        started = true;
        StartCoroutine(RunEnding(player));
    }

    private IEnumerator RunEnding(PlayerController player)
    {
        // --- 1. the character steps through the door: gone, instantly ---
        player.gameObject.SetActive(false);

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Camera camComponent = cam != null ? cam.GetComponent<Camera>() : null;

        // --- 2. camera lerps to the focus point and widens ---
        Vector3 focus = (cameraFocusTarget != null ? cameraFocusTarget.position
                        : rocket != null ? rocket.position : transform.position) + cameraOffset;

        float startOrtho = camComponent != null && camComponent.orthographic ? camComponent.orthographicSize : 0f;
        float targetOrtho = startOrtho > 0f ? ComputeShipFitOrthoSize(startOrtho, focus, camComponent.aspect) : 0f;

        Vector3 camStart = cam != null ? cam.position : Vector3.zero;
        float t = 0f;
        while (t < introCameraDuration)
        {
            t += Time.deltaTime;
            if (cam != null)
            {
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / introCameraDuration));
                cam.position = Vector3.Lerp(camStart, focus, eased);
                if (targetOrtho > 0f)
                    camComponent.orthographicSize = Mathf.Lerp(startOrtho, targetOrtho, eased);
            }
            yield return null;
        }

        if (rocket == null)
            yield break;

        Vector3 padPosition = rocket.position;

        // --- 4. ignition: animation on, vibration ramping up ---
        if (rocketAnimator != null)
        {
            rocketAnimator.speed = 1f;
            StartCoroutine(HoldAnimatorLastFrame());
        }

        t = 0f;
        while (t < vibrationDuration)
        {
            t += Time.deltaTime;
            float ramp = t / vibrationDuration; // shake grows as the engines spool up
            rocket.position = padPosition + (Vector3)(Random.insideUnitCircle * vibrationIntensity * ramp);
            yield return null;
        }
        rocket.position = padPosition;

        // --- 5. liftoff: camera already framed and wide; follow (or hold), then let go ---
        float speed = 0f;
        float flightTime = 0f;
        Vector3 cameraVelocity = Vector3.zero;

        while (destroyRocketAfter <= 0f || flightTime < destroyRocketAfter)
        {
            float dt = Time.deltaTime;
            flightTime += dt;

            speed = Mathf.Min(maxAscendSpeed, speed + liftoffAcceleration * dt);
            rocket.position += Vector3.up * speed * dt;

            // A faint residual shudder during ascent sells the thrust.
            Vector3 shudder = (Vector3)(Random.insideUnitCircle * vibrationIntensity * 0.3f);
            rocket.position += shudder;

            // With a focus target the camera holds its composed frame; without
            // one it tracks the climbing ship until the follow time expires.
            if (cam != null && cameraFocusTarget == null && flightTime <= cameraFollowDuration)
            {
                cam.position = Vector3.SmoothDamp(cam.position,
                    rocket.position + cameraOffset, ref cameraVelocity, cameraDamping);
            }

            yield return null;
        }

        Destroy(rocket.gameObject);
    }

    /// <summary>
    /// Watches the engine animation and freezes it on its final frame after
    /// one play-through, whether or not the clip has Loop Time ticked.
    /// </summary>
    private IEnumerator HoldAnimatorLastFrame()
    {
        yield return null; // let the Animator enter its state first

        while (rocketAnimator != null && rocketAnimator.isActiveAndEnabled)
        {
            AnimatorStateInfo state = rocketAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.normalizedTime >= 0.99f)
            {
                // Pin the last frame explicitly, then stop the clock - a
                // looping clip can never wrap back to frame 0 this way.
                rocketAnimator.Play(state.fullPathHash, 0, 0.999f);
                rocketAnimator.speed = 0f;
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// The widened view size: at least cameraWidenFactor times the gameplay
    /// view, and never smaller than what is needed to show the ENTIRE ship
    /// (its renderer bounds plus margin) in a frame centered on the focus.
    /// </summary>
    private float ComputeShipFitOrthoSize(float baseSize, Vector3 focusCenter, float aspect)
    {
        float size = baseSize * cameraWidenFactor;

        if (autoFitShip && rocket != null)
        {
            Renderer[] shipRenderers = rocket.GetComponentsInChildren<Renderer>();
            if (shipRenderers.Length > 0)
            {
                Bounds b = shipRenderers[0].bounds;
                foreach (Renderer r in shipRenderers)
                    b.Encapsulate(r.bounds);

                // Half-extents of the frame needed, measured from the focus center.
                float needY = Mathf.Max(Mathf.Abs(b.max.y - focusCenter.y), Mathf.Abs(b.min.y - focusCenter.y));
                float needX = Mathf.Max(Mathf.Abs(b.max.x - focusCenter.x), Mathf.Abs(b.min.x - focusCenter.x));

                float fit = Mathf.Max(needY, needX / Mathf.Max(0.1f, aspect)) * (1f + fitMargin);
                size = Mathf.Max(size, fit);
            }
        }

        return size;
    }

    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        }

        if (rocket != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawLine(transform.position, rocket.position);
            Gizmos.DrawRay(rocket.position, Vector3.up * 3f);
        }

        if (cameraFocusTarget != null)
        {
            // The composed camera frame during the launch (widened view).
            Gizmos.color = new Color(0.3f, 0.7f, 1f);
            Gizmos.DrawWireSphere(cameraFocusTarget.position, 0.3f);
            Camera main = Camera.main;
            if (main != null && main.orthographic)
            {
                float h = main.orthographicSize * cameraWidenFactor * 2f;
                float w = h * main.aspect;
                Gizmos.DrawWireCube(cameraFocusTarget.position + new Vector3(cameraOffset.x, cameraOffset.y, 0f),
                    new Vector3(w, h, 0f));
            }
        }
    }
}
