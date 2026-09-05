using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player control for a single falling block. The block falls under normal 2D
/// physics; while it is "active" the player can nudge it left/right and spin it
/// with Q / E. Once it touches the ground or the stack and stops moving it
/// reports itself as landed, which is what makes the <see cref="GameManager"/>
/// start counting down to the next spawn.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BlockController : MonoBehaviour
{
    [Header("Control")]
    [Tooltip("Horizontal speed, in units per second, while the player holds A/D or the arrow keys.")]
    [SerializeField] private float moveSpeed = 4f;

    [Tooltip("Rotation speed, in degrees per second, while the player holds Q or E.")]
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Falling")]
    [Tooltip("Gravity multiplier applied to the block on spawn. Lower = slower fall. 1 = normal Unity gravity.")]
    [SerializeField] private float gravityScale = 0.35f;

    [Tooltip("Maximum downward speed, in units per second. Keeps the block from accelerating forever. 0 = no limit.")]
    [SerializeField] private float maxFallSpeed = 2f;

    [Tooltip("Downward speed while the player holds S or the down arrow, to drop the block early.")]
    [SerializeField] private float softDropSpeed = 8f;

    [Header("Landing detection")]
    [Tooltip("The block counts as resting below this linear speed, in units per second.")]
    [SerializeField] private float settleSpeedThreshold = 0.25f;

    [Tooltip("The block counts as resting below this spin speed, in degrees per second.")]
    [SerializeField] private float settleAngularThreshold = 20f;

    [Tooltip("How long the block must stay still, while touching something, before it reports as landed.")]
    [SerializeField] private float settleTime = 0.25f;

    [Tooltip("Stop listening to the keyboard as soon as the block lands.")]
    [SerializeField] private bool releaseControlOnLanding = true;

    [Header("Bounds")]
    [Tooltip("Clamp the block between minX and maxX while the player controls it.")]
    [SerializeField] private bool clampHorizontally = true;
    [SerializeField] private float minX = -8f;
    [SerializeField] private float maxX = 8f;

    private Rigidbody2D rb;
    private float moveInput;
    private float rotateInput;
    private bool softDropping;
    private int contactCount;
    private float settleTimer;

    /// <summary>Raised once, the first time the block comes to rest on the ground or the stack.</summary>
    public event Action<BlockController> Landed;

    /// <summary>True while this block listens to the keyboard.</summary>
    public bool IsControllable { get; private set; }

    /// <summary>True once the block has settled. Never goes back to false.</summary>
    public bool HasLanded { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ApplyGravityScale();
    }

    /// <summary>Called by the GameManager right after spawning so every block shares the same tuning.</summary>
    public void Configure(float moveSpeed, float rotationSpeed, float gravityScale, float maxFallSpeed,
        float softDropSpeed, float settleSpeedThreshold, float settleAngularThreshold, float settleTime,
        bool releaseControlOnLanding, bool clampHorizontally, float minX, float maxX)
    {
        this.moveSpeed = moveSpeed;
        this.rotationSpeed = rotationSpeed;
        this.gravityScale = gravityScale;
        this.maxFallSpeed = maxFallSpeed;
        this.softDropSpeed = softDropSpeed;
        this.settleSpeedThreshold = settleSpeedThreshold;
        this.settleAngularThreshold = settleAngularThreshold;
        this.settleTime = settleTime;
        this.releaseControlOnLanding = releaseControlOnLanding;
        this.clampHorizontally = clampHorizontally;
        this.minX = minX;
        this.maxX = maxX;

        ApplyGravityScale();
    }

    /// <summary>Overrides whatever gravity scale the prefab was saved with.</summary>
    private void ApplyGravityScale()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = gravityScale;
    }

    public void TakeControl()
    {
        IsControllable = true;
    }

    public void ReleaseControl()
    {
        IsControllable = false;
        moveInput = 0f;
        rotateInput = 0f;
        softDropping = false;
    }

    /// <summary>Forces the block to report as landed, used by the safety timeout in the GameManager.</summary>
    public void ForceLanded()
    {
        MarkLanded();
    }

    private void Update()
    {
        if (!IsControllable)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        moveInput = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput += 1f;

        // Q spins counter-clockwise (positive Z in 2D), E spins clockwise.
        rotateInput = 0f;
        if (keyboard.qKey.isPressed) rotateInput += 1f;
        if (keyboard.eKey.isPressed) rotateInput -= 1f;

        softDropping = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
    }

    private void FixedUpdate()
    {
        if (IsControllable)
            ApplyInput();
        else
            LimitFallSpeed();

        UpdateLanding();
    }

    private void ApplyInput()
    {
        // Drive the horizontal axis directly and leave the vertical axis to gravity.
        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInput * moveSpeed;

        if (softDropping && softDropSpeed > 0f)
            velocity.y = -softDropSpeed;
        else if (maxFallSpeed > 0f && velocity.y < -maxFallSpeed)
            velocity.y = -maxFallSpeed;

        rb.linearVelocity = velocity;

        if (!Mathf.Approximately(rotateInput, 0f))
        {
            rb.angularVelocity = 0f;
            rb.MoveRotation(rb.rotation + rotateInput * rotationSpeed * Time.fixedDeltaTime);
        }

        if (clampHorizontally)
        {
            Vector2 position = rb.position;
            float clampedX = Mathf.Clamp(position.x, minX, maxX);
            if (!Mathf.Approximately(clampedX, position.x))
            {
                rb.position = new Vector2(clampedX, position.y);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    /// <summary>Keeps already-dropped blocks from building up huge speed as the stack grows.</summary>
    private void LimitFallSpeed()
    {
        if (maxFallSpeed <= 0f)
            return;

        Vector2 velocity = rb.linearVelocity;
        if (velocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(velocity.x, -maxFallSpeed);
    }

    /// <summary>
    /// The block has landed once it is touching something and has stayed slow
    /// for <see cref="settleTime"/> seconds. Player input counts as movement, so
    /// the block only settles when the player stops steering it.
    /// </summary>
    private void UpdateLanding()
    {
        if (HasLanded)
            return;

        bool steering = IsControllable &&
                        (!Mathf.Approximately(moveInput, 0f) || !Mathf.Approximately(rotateInput, 0f) || softDropping);

        bool resting = contactCount > 0 &&
                       !steering &&
                       rb.linearVelocity.magnitude <= settleSpeedThreshold &&
                       Mathf.Abs(rb.angularVelocity) <= settleAngularThreshold;

        if (!resting)
        {
            settleTimer = 0f;
            return;
        }

        settleTimer += Time.fixedDeltaTime;
        if (settleTimer >= settleTime)
            MarkLanded();
    }

    private void MarkLanded()
    {
        if (HasLanded)
            return;

        HasLanded = true;

        if (releaseControlOnLanding)
            ReleaseControl();

        Landed?.Invoke(this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        contactCount++;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        contactCount = Mathf.Max(0, contactCount - 1);
    }
}
