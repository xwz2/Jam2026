using UnityEngine;

/// <summary>
/// All character sounds, driven by <see cref="PlayerController"/> state the
/// same way <see cref="PlayerVisuals"/> drives the animation: footsteps loop
/// while running on the ground, jump plays per jump (double jump slightly
/// higher-pitched), landing plays on touchdown. AudioSources are built in
/// code; the global <see cref="AudioMaster"/> coefficient applies on top.
///
/// Character audio is 2D on purpose: the camera follows the player, so the
/// character is always "here" — distance attenuation would never trigger.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Footsteps")]
    [Tooltip("Looping run sound. Plays only while alive, grounded and actually moving.")]
    [SerializeField] private AudioClip footstepsSound;

    [Range(0f, 1f)]
    [SerializeField] private float footstepsVolume = 0.5f;

    [Tooltip("Horizontal speed (units/sec) above which footsteps start.")]
    [Min(0f)]
    [SerializeField] private float footstepsMinSpeed = 0.5f;

    [Tooltip("How quickly footsteps swell/fade when starting and stopping, in 1/seconds. Avoids harsh cuts.")]
    [Min(1f)]
    [SerializeField] private float footstepsFadeResponse = 12f;

    [Tooltip("Playback speed of the footsteps loop. 1 = as recorded, 1.5 = faster steps (also raises pitch), " +
             "0.8 = slower/heavier. Match this to how fast the character's run feels.")]
    [Range(0.25f, 3f)]
    [SerializeField] private float footstepsSpeed = 1f;

    [Header("Jump")]
    [SerializeField] private AudioClip jumpSound;

    [Range(0f, 1f)]
    [SerializeField] private float jumpVolume = 0.8f;

    [Tooltip("Pitch multiplier for the mid-air (double) jump, so the two jumps read differently.")]
    [SerializeField] private float doubleJumpPitch = 1.15f;

    [Header("Landing")]
    [SerializeField] private AudioClip landingSound;

    [Range(0f, 1f)]
    [SerializeField] private float landingVolume = 0.7f;

    [Tooltip("Minimum downward speed (units/sec) at touchdown for the landing sound to play. " +
             "Filters out micro-landings from walking over bumpy blocks.")]
    [Min(0f)]
    [SerializeField] private float landingMinFallSpeed = 1.5f;

    private PlayerController controller;
    private Rigidbody2D rb;
    private AudioSource footstepsSource;
    private AudioSource oneShotSource;
    private bool wasGrounded = true;
    private float lastFallSpeed;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();

        if (footstepsSound != null)
        {
            footstepsSource = gameObject.AddComponent<AudioSource>();
            footstepsSource.clip = footstepsSound;
            footstepsSource.loop = true;
            footstepsSource.playOnAwake = false;
            footstepsSource.volume = 0f;
            footstepsSource.spatialBlend = 0f;
        }

        // One shared source for jump/landing one-shots (PlayOneShot allows overlap).
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;
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
        if (jumpSound == null)
            return;

        oneShotSource.pitch = isDoubleJump ? doubleJumpPitch : 1f;
        oneShotSource.PlayOneShot(jumpSound, jumpVolume);
    }

    private void Update()
    {
        UpdateFootsteps();
        UpdateLanding();
    }

    private void UpdateFootsteps()
    {
        if (footstepsSource == null)
            return;

        bool shouldPlay = controller.IsAlive
                          && controller.IsGrounded
                          && Mathf.Abs(controller.HorizontalSpeed) > footstepsMinSpeed;

        // Applied every frame so the inspector slider works live in Play mode.
        footstepsSource.pitch = footstepsSpeed;

        // Ramp the volume instead of hard Play/Stop so steps never click on and off.
        float target = shouldPlay ? footstepsVolume : 0f;
        footstepsSource.volume = Mathf.Lerp(footstepsSource.volume, target,
            1f - Mathf.Exp(-footstepsFadeResponse * Time.deltaTime));

        if (shouldPlay && !footstepsSource.isPlaying)
            footstepsSource.Play();
        else if (!shouldPlay && footstepsSource.isPlaying && footstepsSource.volume < 0.01f)
            footstepsSource.Stop();
    }

    private void UpdateLanding()
    {
        // Sample fall speed BEFORE the grounded flag flips: on the touchdown
        // frame the physics has already zeroed the velocity, so we remember
        // how fast the character was falling one frame earlier.
        bool groundedNow = controller.IsGrounded;

        if (groundedNow && !wasGrounded &&
            landingSound != null && lastFallSpeed >= landingMinFallSpeed)
        {
            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(landingSound, landingVolume);
        }

        wasGrounded = groundedNow;
        if (!groundedNow && rb != null)
            lastFallSpeed = Mathf.Max(0f, -rb.linearVelocity.y);
    }
}
