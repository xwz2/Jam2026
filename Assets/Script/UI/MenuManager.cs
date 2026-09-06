using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause/start menu flow. The menu starts OPEN (game paused); Continue resumes
/// the game — the camera then glides toward the character via the normal
/// damped follow. Escape re-opens (or closes) the menu at any time. Restart
/// sends the character back to the scene-start position, ignoring checkpoints.
/// Every button press fades the menu out.
///
/// Setup: put this on the menu root (needs a CanvasGroup — added automatically),
/// then wire the buttons' OnClick to <see cref="OnContinuePressed"/> and
/// <see cref="OnRestartPressed"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MenuManager : MonoBehaviour
{
    [Tooltip("The player. Empty = found automatically.")]
    [SerializeField] private PlayerController player;

    [Tooltip("The player's respawn component, used by Restart. Empty = found automatically.")]
    [SerializeField] private PlayerRespawn playerRespawn;

    [Tooltip("The ink bar UI object, hidden while the menu is open. Empty = found automatically via InkBarUI.")]
    [SerializeField] private GameObject inkBarUI;

    [Tooltip("Seconds the menu takes to fade in/out. Uses unscaled time, so it works while paused.")]
    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 0.25f;

    [Tooltip("Freeze the game (Time.timeScale = 0) while the menu is open.")]
    [SerializeField] private bool pauseGameWhileOpen = true;

    [Tooltip("Open the menu on scene start.")]
    [SerializeField] private bool openOnStart = true;

    [Tooltip("After a button press, the camera glides to the character over this many seconds WHILE the " +
             "world is still frozen; time resumes only once the camera arrives.")]
    [Min(0f)]
    [SerializeField] private float focusDuration = 1f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;
    private bool transitioning; // camera focus glide in progress

    /// <summary>True while the menu is open (fully visible or fading in).</summary>
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (playerRespawn == null && player != null)
            playerRespawn = player.GetComponent<PlayerRespawn>();

        if (inkBarUI == null)
        {
            InkBarUI bar = FindFirstObjectByType<InkBarUI>();
            if (bar != null)
                inkBarUI = bar.gameObject;
        }
    }

    private void Start()
    {
        if (openOnStart)
            Open(instant: true);
        else
            CloseInstant();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // Escape toggles: pops the menu up mid-game, and doubles as Continue.
        if (keyboard.escapeKey.wasPressedThisFrame && !transitioning)
        {
            if (IsOpen)
                OnContinuePressed();
            else
                Open(instant: false);
        }
    }

    /// <summary>Hook the Continue button's OnClick here.</summary>
    public void OnContinuePressed()
    {
        if (!IsOpen || transitioning)
            return;

        Close();
    }

    /// <summary>Hook the Restart button's OnClick here.</summary>
    public void OnRestartPressed()
    {
        if (!IsOpen || transitioning)
            return;

        if (playerRespawn != null && player != null)
        {
            // ResetToStart snaps the camera onto the player; undo that snap so
            // the focus glide can travel from the current view to the start.
            Transform cam = player.CameraTransform;
            Vector3 heldView = cam != null ? cam.position : Vector3.zero;
            playerRespawn.ResetToStart();
            if (cam != null)
                cam.position = heldView;
        }

        Close();
    }

    private void Open(bool instant)
    {
        IsOpen = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (pauseGameWhileOpen)
            Time.timeScale = 0f;

        // Freezing the controller stops input AND the camera follow, so the
        // view holds still until Continue releases it toward the character.
        if (player != null)
            player.enabled = false;

        // Gameplay HUD has no business on the menu screen.
        if (inkBarUI != null)
            inkBarUI.SetActive(false);

        if (instant)
            canvasGroup.alpha = 1f;
        else
            StartFade(1f);
    }

    private void Close()
    {
        IsOpen = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        StartFade(0f);
        StartCoroutine(FocusThenResume());
    }

    /// <summary>
    /// The start-of-game beat: world still frozen, menu fading, camera gliding
    /// to the character. Time (and input) resume only once the camera arrives.
    /// </summary>
    private IEnumerator FocusThenResume()
    {
        transitioning = true;

        Transform cam = player != null ? player.CameraTransform : null;
        if (cam != null && focusDuration > 0f)
        {
            Vector3 from = cam.position;
            float t = 0f;
            while (t < focusDuration)
            {
                t += Time.unscaledDeltaTime; // the world is frozen; the camera is not
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / focusDuration));
                cam.position = Vector3.Lerp(from, player.CameraTargetPosition, eased);
                yield return null;
            }
        }

        if (player != null)
        {
            player.SnapCameraToTarget(); // zero the follow spring so gameplay takes over seamlessly
            player.enabled = true;
        }
        if (pauseGameWhileOpen)
            Time.timeScale = 1f;

        // HUD returns exactly when gameplay does — after the camera has settled.
        if (inkBarUI != null)
            inkBarUI.SetActive(true);

        transitioning = false;
    }

    private void CloseInstant()
    {
        // The object stays active (alpha 0 hides it) so Update keeps
        // listening for Escape — deactivating it would kill the reopen key.
        IsOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (inkBarUI != null)
            inkBarUI.SetActive(true);
    }

    private void StartFade(float targetAlpha)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // ticks even while the game is paused
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }
}
