using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // Compatibility alias for existing scene/code references.
    public static SoundManager instance => Instance;

    [SerializeField] private AudioSource soundFXObject;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public AudioSource PlaySound(AudioClip audioClip, Transform spawnTransform, float volume = 1f)
    {
        Vector3 position = spawnTransform != null ? spawnTransform.position : transform.position;
        return PlaySound(audioClip, position, volume);
    }

    public AudioSource PlaySound(AudioClip audioClip, Vector3 position, float volume = 1f)
    {
        if (audioClip == null || soundFXObject == null) return null;

        AudioSource audioSource = Instantiate(soundFXObject, position, Quaternion.identity);
        audioSource.clip = audioClip;
        audioSource.volume = Mathf.Clamp01(volume) * GameAudioSettings.EffectsVolume;
        audioSource.Play();

        float playbackSpeed = Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch));
        Destroy(audioSource.gameObject, audioClip.length / playbackSpeed);
        return audioSource;
    }
}
