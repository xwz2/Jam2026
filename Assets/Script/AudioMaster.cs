using UnityEngine;

/// <summary>
/// Global sound-level coefficient. Drop one instance anywhere in the scene
/// (e.g. on the GameManager object); the slider scales EVERY sound in the game
/// — fireball loops, explosions, anything added later — via AudioListener.volume.
/// Editable live in Play mode, and other scripts can set
/// <see cref="MasterVolume"/> (a future options menu, mute button, etc.).
/// </summary>
public class AudioMaster : MonoBehaviour
{
    [Tooltip("Master coefficient applied to all game audio. 1 = full, 0 = mute.")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    /// <summary>Global volume coefficient, 0..1. Settable from any script.</summary>
    public static float MasterVolume
    {
        get => AudioListener.volume;
        set => AudioListener.volume = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        AudioListener.volume = masterVolume;
    }

    // Lets the inspector slider work live during Play mode.
    private void OnValidate()
    {
        if (Application.isPlaying)
            AudioListener.volume = masterVolume;
    }
}
