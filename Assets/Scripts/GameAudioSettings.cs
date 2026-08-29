using UnityEngine;

// Shared, persistent audio preferences. Audio sources can opt in by adding an
// AudioVolumeCategory component; the menu works even before the final audio assets
// have been added to the project.
public static class GameAudioSettings
{
    private const string MusicVolumeKey = "RunCrabRun_MusicVolume";
    private const string EffectsVolumeKey = "RunCrabRun_EffectsVolume";

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
    public static float EffectsVolume => PlayerPrefs.GetFloat(EffectsVolumeKey, 0.8f);

    public static void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        ApplyToActiveSources();
    }

    public static void SetEffectsVolume(float value)
    {
        PlayerPrefs.SetFloat(EffectsVolumeKey, Mathf.Clamp01(value));
        ApplyToActiveSources();
    }

    public static void ApplyToActiveSources()
    {
        foreach (AudioVolumeCategory source in Object.FindObjectsByType<AudioVolumeCategory>(FindObjectsSortMode.None))
        {
            source.ApplyVolume();
        }
    }
}
