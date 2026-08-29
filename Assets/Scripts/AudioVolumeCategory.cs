using UnityEngine;

// Add this to a future AudioSource and choose whether it is music or an effect.
// Its original AudioSource volume is treated as the designer-authored maximum.
[RequireComponent(typeof(AudioSource))]
public class AudioVolumeCategory : MonoBehaviour
{
    public enum Category { Music, Effects }

    public Category category = Category.Effects;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (baseVolume <= 0f) baseVolume = source.volume;
        ApplyVolume();
    }

    public void ApplyVolume()
    {
        if (source == null) return;
        float preference = category == Category.Music
            ? GameAudioSettings.MusicVolume
            : GameAudioSettings.EffectsVolume;
        source.volume = baseVolume * preference;
    }
}
