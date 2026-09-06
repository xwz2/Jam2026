using UnityEngine;

public class AudioManager : MonoBehaviour
{

    public static  AudioManager instance;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float volume = 1.0f;


    [SerializeField] private AudioClip burn;
    [SerializeField] private AudioClip button_down;
    [SerializeField] private AudioClip button_up;
    [SerializeField] private AudioClip footsteps;
    [SerializeField] private AudioClip footsteps_reverb;
    [SerializeField] private AudioClip in_wind;
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip landing;
    [SerializeField] private AudioClip nanomachines_draw;
    [SerializeField] private AudioClip platform_hum;
    [SerializeField] private AudioClip wind;


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy (gameObject);
        }
    }
    public void playAudio(AudioClip clip, float volume)
    {
        audioSource.clip = clip;
        audioSource.Play();
    }
}
