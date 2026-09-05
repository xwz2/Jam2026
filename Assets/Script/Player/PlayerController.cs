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

    /// <summary>-1..1 input the visuals use to face and lean the sprite.</summary>
    public float MoveInput => moveInput;

    /// <summary>Signed horizontal speed, for the visuals and any future animator.</summary>
    public float HorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;

    public bool IsGrounded => grounded;

    public bool IsAlive => alive;

    /// <summary>Raised once when the character dies.</summary>
    public event Action Died;

    /// <summary>Kills the character: flips the alive flag off and stops accepting input.</summary>
    public void Kill()
    {
        if (!alive)
            return;

        alive = false;
        moveInput = 0f;
        jumpQueued = false;
        Died?.Invoke();
    }

    /// <summary>Brings the character back to life; called by the respawn flow after the fade.</summary>
    public void Revive()
    {
        alive = true;
        jumpsUsed = 0;
    }

    /// <summary>Toggle the mid-air second jump — the inspector checkbox, also settable from gameplay code.</summary>
    public bool DoubleJumpEnabled
    {
        get => doubleJumpEnabled;
        set => doubleJumpEnabled = value;
    }

    /// <summary>Raised on every successful jump; the bool is true for the mid-air jump.</summary>
    public event Action<bool> Jumped;

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

    private void FixedUpdate()
    {
        ProbeGround();
        ApplyHorizontalMovement();

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

        // The probe must ignore the character's own collider.
        bool wasGrounded = grounded;
        grounded = false;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(probe, groundCheckRadius, groundLayers))
        {
            if (hit != bodyCollider && !hit.isTrigger)
            {
                grounded = true;
                break;
            }
        }

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
        bool canFirstJump = jumpsUsed == 0 && (grounded || Time.time - lastGroundedTime <= coyoteTime);
        if (canFirstJump)
        {
            Jump(jumpSpeed, isDoubleJump: false);
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
