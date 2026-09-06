using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Physics half of the character: horizontal movement, ground detection and a
/// double jump gated by a configurable "double space" window. Everything visual
/// (facing, lean, breathing) lives in <see cref="PlayerVisuals"/>, which reads
/// the state this component exposes; shooting lives in <see cref="DartShooter"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Life")]
    [Tooltip("Whether the character is alive. Turned off by fireball hits; input stops while dead.")]
    [SerializeField] private bool alive = true;

    [Header("Movement")]
    [Tooltip("Top horizontal speed, in units per second.")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("How fast the character reaches top speed, in units per second squared. High = snappy, low = floaty.")]
    [SerializeField] private float acceleration = 60f;

    [Tooltip("How fast the character stops when input is released. Usually higher than acceleration so it does not skate.")]
    [SerializeField] private float deceleration = 80f;

    [Header("Jumping")]
    [Tooltip("Initial upward speed of a jump, in units per second.")]
    [SerializeField] private float jumpSpeed = 7f;

    [Tooltip("Allow a second jump in mid-air. Untick to limit the character to single jumps.")]
    [SerializeField] private bool doubleJumpEnabled = true;

    [Tooltip("Upward speed of the mid-air (second) jump. Often slightly lower than the first jump.")]
    [SerializeField] private float doubleJumpSpeed = 6f;

    [Tooltip("The second Space press only counts as a double jump if it comes within this many seconds " +
             "of the first jump. After the window closes the character is committed to the fall. 0 = no limit.")]
    [Min(0f)]
    [SerializeField] private float doubleJumpMaxDelay = 0.6f;

    [Tooltip("Extra gravity while falling, for a game-feel arc (rise slower than you fall). 1 = symmetric arc.")]
    [SerializeField] private float fallGravityMultiplier = 1.6f;

    [Tooltip("Grace period after walking off a ledge during which the first jump still works (coyote time).")]
    [Min(0f)]
    [SerializeField] private float coyoteTime = 0.1f;

    [Tooltip("Anticipation pause between pressing jump and actually lifting off, during which the visuals " +
             "squash fat on the ground. 0 = instant jump. Applies to the FIRST jump only; double jump is instant.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float jumpAnticipationTime = 0f;

    [Header("Camera")]
    [Tooltip("Move the camera with the character, rigidly locked (no lag, no jitter).")]
    [SerializeField] private bool moveCamera = true;

    [Tooltip("Camera to move. Empty = the Main Camera.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Camera position relative to the character. Keep Z negative so the camera stays back.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1f, -10f);

    [Tooltip("Seconds the camera lags behind the character. 0 = rigid lock (jitter-free but stiff), " +
             "0.1-0.3 = smooth trailing follow.")]
    [Min(0f)]
    [SerializeField] private float cameraDamping = 0.15f;

    [Header("Ground check")]
    [Tooltip("Empty child placed at the character's feet. If empty, the collider's bottom edge is used.")]
    [SerializeField] private Transform groundCheck;

    [Tooltip("Radius of the ground probe circle.")]
    [SerializeField] private float groundCheckRadius = 0.12f;

    [Tooltip("Layers that count as ground. Includes the falling blocks by default (Everything).")]
    [SerializeField] private LayerMask groundLayers = ~0;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float moveInput;
    private bool jumpQueued;
    private bool grounded;
    private float lastGroundedTime = float.NegativeInfinity;
    private float firstJumpTime = float.NegativeInfinity;
    private int jumpsUsed;
    private float baseGravityScale;
    private Vector3 cameraFollowVelocity;
    private float anticipationTimer = -1f; // >= 0 while a first jump is charging

    /// <summary>-1..1 input the visuals use to face and lean the sprite.</summary>
    public float MoveInput => moveInput;

    /// <summary>Signed horizontal speed, for the visuals and any future animator.</summary>
    public float HorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;

    public bool IsGrounded => grounded;

    /// <summary>True while the ground under the character is a drawn ink line (and nothing solid besides).</summary>
    public bool IsOnInk { get; private set; }

    public bool IsAlive => alive;

    /// <summary>Raised once when the character dies.</summary>
    public event Action Died;

    /// <summary>Raised when the character comes back to life at the end of the respawn flow.</summary>
    public event Action Revived;

    /// <summary>Kills the character: flips the alive flag off and stops accepting input.</summary>
    public void Kill()
    {
        if (!alive)
            return;

        alive = false;
        moveInput = 0f;
        jumpQueued = false;
        anticipationTimer = -1f; // a charging jump dies with the character
        Died?.Invoke();
    }

    /// <summary>Brings the character back to life; called by the respawn flow after the fade.</summary>
    public void Revive()
    {
        alive = true;
        jumpsUsed = 0;
        Revived?.Invoke();
    }

    /// <summary>Toggle the mid-air second jump — the inspector checkbox, also settable from gameplay code.</summary>
    public bool DoubleJumpEnabled
    {
        get => doubleJumpEnabled;
        set => doubleJumpEnabled = value;
    }

    /// <summary>Raised on every successful jump; the bool is true for the mid-air jump.</summary>
    public event Action<bool> Jumped;

    /// <summary>Raised when a first jump starts charging (the anticipation squat before liftoff).</summary>
    public event Action JumpCharging;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        baseGravityScale = rb.gravityScale;

        // The character must never tip over from bumping into blocks; the
        // visuals fake the lean instead.
        rb.freezeRotation = true;

        // Physics steps at 50 Hz while rendering runs faster; without
        // interpolation the body visibly stutters whenever it moves.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (!alive)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        moveInput = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;

        // Buffer the press; the jump itself happens in FixedUpdate with the physics.
        if (keyboard.spaceKey.wasPressedThisFrame)
            jumpQueued = true;
    }

    // The camera is written in LateUpdate, not FixedUpdate: the rigidbody's
    // transform is interpolated per rendered frame, so following it from here
    // tracks smooth motion — SmoothDamp on top adds lag without jitter.
    private void LateUpdate()
    {
        if (!moveCamera || cameraTransform == null)
            return;

        Vector3 desired = transform.position + cameraOffset;

        cameraTransform.position = cameraDamping <= 0f
            ? desired
            : Vector3.SmoothDamp(cameraTransform.position, desired, ref cameraFollowVelocity, cameraDamping);
    }

    /// <summary>Where the camera wants to be for this character (position + offset), for cinematic glides.</summary>
    public Vector3 CameraTargetPosition => transform.position + cameraOffset;

    /// <summary>The camera this controller drives (may be null before Awake).</summary>
    public Transform CameraTransform => cameraTransform;

    /// <summary>Puts the camera exactly on target with no easing — call after teleporting the character.</summary>
    public void SnapCameraToTarget()
    {
        if (cameraTransform == null)
            return;

        cameraFollowVelocity = Vector3.zero;
        cameraTransform.position = transform.position + cameraOffset;
    }

    private void FixedUpdate()
    {
        ProbeGround();
        ApplyHorizontalMovement();

        // A charging first jump lifts off once its anticipation window elapses.
        if (anticipationTimer >= 0f)
        {
            anticipationTimer -= Time.fixedDeltaTime;
            if (anticipationTimer < 0f && alive)
                Jump(jumpSpeed, isDoubleJump: false);
        }

        if (jumpQueued)
        {
            jumpQueued = false;
            TryJump();
        }

        // Heavier gravity on the way down makes the jump arc read better.
        rb.gravityScale = rb.linearVelocity.y < -0.01f
            ? baseGravityScale * fallGravityMultiplier
            : baseGravityScale;
    }

    private void ProbeGround()
    {
        Vector2 probe = groundCheck != null
            ? (Vector2)groundCheck.position
            : bodyCollider != null
                ? new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y)
                : rb.position;

        // The probe must ignore the character's own collider. While probing,
        // classify the surface: standing on ANY real ground counts as ground;
        // only pure ink contact counts as "on ink".
        bool wasGrounded = grounded;
        grounded = false;
        bool sawInk = false;
        bool sawSolidGround = false;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(probe, groundCheckRadius, groundLayers))
        {
            if (hit == bodyCollider || hit.isTrigger)
                continue;

            grounded = true;
            if (hit.GetComponentInParent<InkLine>() != null)
                sawInk = true;
            else
                sawSolidGround = true;
        }
        IsOnInk = grounded && sawInk && !sawSolidGround;

        if (grounded)
        {
            lastGroundedTime = Time.time;
            // Touching the ground refills both jumps, per design.
            if (!wasGrounded || rb.linearVelocity.y <= 0.01f)
                jumpsUsed = 0;
        }
    }

    private void ApplyHorizontalMovement()
    {
        float target = moveInput * moveSpeed;
        float rate = Mathf.Approximately(moveInput, 0f) ? deceleration : acceleration;
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, target, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    private void TryJump()
    {
        if (anticipationTimer >= 0f)
            return; // already charging a jump; ignore extra presses until liftoff

        bool canFirstJump = jumpsUsed == 0 && (grounded || Time.time - lastGroundedTime <= coyoteTime);
        if (canFirstJump)
        {
            if (jumpAnticipationTime > 0f)
            {
                // Fat squat first; the actual liftoff fires when the timer runs out.
                anticipationTimer = jumpAnticipationTime;
                JumpCharging?.Invoke();
            }
            else
            {
                Jump(jumpSpeed, isDoubleJump: false);
            }
            return;
        }

        if (!doubleJumpEnabled)
            return;

        bool withinWindow = doubleJumpMaxDelay <= 0f || Time.time - firstJumpTime <= doubleJumpMaxDelay;
        if (jumpsUsed == 1 && withinWindow)
            Jump(doubleJumpSpeed, isDoubleJump: true);
    }

    private void Jump(float speed, bool isDoubleJump)
    {
        // Replace vertical speed instead of adding to it, so the double jump
        // height is consistent whether the character was rising or falling.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed);

        jumpsUsed++;
        if (!isDoubleJump)
            firstJumpTime = Time.time;

        Jumped?.Invoke(isDoubleJump);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
