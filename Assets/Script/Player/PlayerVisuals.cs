using UnityEngine;

/// <summary>
/// All cosmetic motion for the character, driven by <see cref="PlayerController"/>
/// state: facing flip, a run lean, a squash on jump/land and the idle breathing.
///
/// Everything here writes to <see cref="visualTarget"/> — ideally a child
/// holding the SpriteRenderer — never to the physics body, so no animation can
/// desync the collider. Facing, breathing and squash all touch localScale, so
/// they are combined into a single write per frame instead of fighting.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerVisuals : MonoBehaviour
{
    [Tooltip("Transform that is flipped/leaned/breathed — a child with the SpriteRenderer. " +
             "If empty, this transform is used (works, but put the sprite on a child if the collider ever drifts).")]
    [SerializeField] private Transform visualTarget;

    [Header("Facing & lean")]
    [Tooltip("Tick if the source PNG faces left, so the flip logic is inverted.")]
    [SerializeField] private bool spriteFacesLeft;

    [Tooltip("How far the character tilts into its run direction, in degrees.")]
    [SerializeField] private float leanAngle = 8f;

    [Tooltip("How quickly the lean and the facing flip settle, in 1/seconds. Higher = snappier.")]
    [SerializeField] private float leanResponse = 12f;

    [Header("Stumble")]
    [Tooltip("While moving, the character rocks back and forth around its center by +/- this many degrees. 0 = off.")]
    [Range(0f, 45f)]
    [SerializeField] private float stumbleAngle = 10f;

    [Tooltip("Rocking cycles per second while moving. Higher = frantic waddle, lower = lazy sway.")]
    [Min(0.1f)]
    [SerializeField] private float stumbleRate = 4f;

    [Tooltip("Also stumble while airborne. Off = feet-on-ground waddle only.")]
    [SerializeField] private bool stumbleInAir = false;

    [Header("Breathing")]
    [Tooltip("Breaths per second while idle. Roughly 0.2-0.5 reads as calm.")]
    [SerializeField] private float breathingRate = 0.35f;

    [Tooltip("How much the width swells at the top of a breath. 0.03 = 3% wider.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float breathingAmount = 0.04f;

    [Tooltip("Fade breathing out while running or airborne so it does not fight the motion.")]
    [SerializeField] private bool breatheOnlyWhenIdle = true;

    [Header("Jump & land squash")]
    [Tooltip("Vertical stretch applied at the moment of a jump. 0.15 = 15% taller, thinner.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float jumpStretch = 0.15f;

    [Tooltip("Vertical squash applied at the moment of landing.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float landSquash = 0.12f;

    [Tooltip("How quickly squash/stretch springs back to normal, in 1/seconds.")]
    [SerializeField] private float squashRecovery = 10f;

    private PlayerController controller;
    private Vector3 baseScale;
    private float facing = 1f;        // smoothed -1..1
    private float currentLean;        // smoothed degrees
    private float squash;             // +stretch / -squash impulse, decays to 0
    private float breathPhase;
    private float breathWeight = 1f;  // fades in/out with idleness
    private float stumblePhase;
    private float stumbleWeight;      // fades in while moving, out while idle
    private bool wasGrounded = true;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (visualTarget == null)
            visualTarget = transform;

        baseScale = visualTarget.localScale;
        facing = spriteFacesLeft ? -1f : 1f;
    }

    private void OnEnable()
    {
        controller.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        controller.Jumped -= OnJumped;
    }

    private void OnJumped(bool isDoubleJump)
    {
        // The double jump pops a little harder so the player can feel it registered.
        squash = jumpStretch * (isDoubleJump ? 1.4f : 1f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        float move = controller.MoveInput;

        // --- facing: flip toward the last non-zero input, smoothly ---
        if (!Mathf.Approximately(move, 0f))
        {
            float desired = Mathf.Sign(move) * (spriteFacesLeft ? -1f : 1f);
            facing = Mathf.MoveTowards(facing, desired, leanResponse * dt * 2f);
        }

        // --- lean: tilt into the run direction, upright when idle ---
        float targetLean = -move * leanAngle;
        currentLean = Mathf.Lerp(currentLean, targetLean, 1f - Mathf.Exp(-leanResponse * dt));

        // --- stumble: rocking oscillation around the center while moving ---
        bool moving = !Mathf.Approximately(move, 0f) && (stumbleInAir || controller.IsGrounded);
        stumbleWeight = Mathf.MoveTowards(stumbleWeight, moving ? 1f : 0f, 6f * dt);
        stumblePhase += stumbleRate * 2f * Mathf.PI * dt;
        float stumble = Mathf.Sin(stumblePhase) * stumbleAngle * stumbleWeight;

        // --- landing squash ---
        bool groundedNow = controller.IsGrounded;
        if (groundedNow && !wasGrounded)
            squash = -landSquash;
        wasGrounded = groundedNow;
        squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-squashRecovery * dt));

        // --- breathing: sine on width, with the inverse on height to keep volume ---
        bool idle = groundedNow && Mathf.Approximately(move, 0f);
        float targetWeight = (!breatheOnlyWhenIdle || idle) ? 1f : 0f;
        breathWeight = Mathf.MoveTowards(breathWeight, targetWeight, 4f * dt);
        breathPhase += breathingRate * 2f * Mathf.PI * dt;
        float breath = Mathf.Sin(breathPhase) * breathingAmount * breathWeight;

        // --- single combined write ---
        // Width: facing sign, breathing swell, inverse of squash (jump = thinner).
        // Height: inverse breathing, plus squash (jump = taller, land = shorter).
        float facingSign = facing < 0f ? -1f : 1f;
        float x = baseScale.x * facingSign * (1f + breath - squash * 0.5f);
        float y = baseScale.y * (1f - breath * 0.5f + squash);

        visualTarget.localScale = new Vector3(x, y, baseScale.z);
        visualTarget.localRotation = Quaternion.Euler(0f, 0f, currentLean + stumble);
    }
}
