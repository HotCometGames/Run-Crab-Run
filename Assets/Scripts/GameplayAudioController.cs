using UnityEngine;

// Owns the two pieces of non-spatial gameplay audio that run for the whole round:
// looping background music and the heartbeat. The heartbeat only plays while the
// player is actually being hunted, i.e. whenever a nearby predator is in its Chase
// state. A wolf wandering or searching nearby stays silent until it commits to a hunt.
[DisallowMultipleComponent]
public sealed class GameplayAudioController : MonoBehaviour
{
    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.45f;

    [Header("Heartbeat")]
    [Tooltip("Optional authored lub-dub clip. A procedural double heartbeat is used when this is empty.")]
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField, Range(0.01f, 1f)] private float farHeartbeatVolume = 0.14f;
    [SerializeField, Range(0.01f, 1f)] private float nearHeartbeatVolume = 0.85f;
    [SerializeField, Min(30f)] private float farHeartbeatBpm = 68f;
    [SerializeField, Min(30f)] private float nearHeartbeatBpm = 120f;
    [SerializeField, Range(1f, 1.5f)] private float nearHeartbeatPitch = 1.2f;
    [SerializeField, Range(0.01f, 0.5f)] private float minimumProximityIntensity = 0.16f;
    [SerializeField, Min(0.1f)] private float intensitySmoothing = 5f;
    [SerializeField, Range(0.02f, 0.5f)] private float threatScanInterval = 0.08f;

    public float DangerIntensity { get; private set; }
    public Predator ClosestPredator { get; private set; }

    private const int HeartbeatSampleRate = 44100;
    private const float HeartbeatDuration = 0.36f;
    private const double ScheduleAheadSeconds = 0.06d;
    private const double FirstBeatDelaySeconds = 0.025d;

    private AudioSource musicSource;
    private readonly AudioSource[] heartbeatSources = new AudioSource[2];
    private AudioClip generatedHeartbeatClip;
    private AudioClip activeHeartbeatClip;
    private Transform player;
    private PlayerController playerController;
    private float targetDangerIntensity;
    private float nextThreatScanTime;
    private double nextHeartbeatDspTime;
    private int nextHeartbeatSource;
    private bool heartbeatActive;
    private bool applicationPaused;

    private void Awake()
    {
        FindPlayer();
        ConfigureMusic();
        ConfigureHeartbeat();
    }

    private void OnEnable()
    {
        AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
    }

    private void OnDisable()
    {
        AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
        StopHeartbeat(true);
    }

    private void OnDestroy()
    {
        if (generatedHeartbeatClip != null)
            Destroy(generatedHeartbeatClip);
    }

    private void Update()
    {
        UpdateMusicVolume();

        float unscaledDeltaTime = Time.unscaledDeltaTime;
        if (ShouldSilenceHeartbeat())
        {
            targetDangerIntensity = 0f;
            DangerIntensity = 0f;
            ClosestPredator = null;
            StopHeartbeat(true);
            return;
        }

        if (Time.unscaledTime >= nextThreatScanTime)
        {
            nextThreatScanTime = Time.unscaledTime + threatScanInterval;
            ScanForClosestPredator();
        }

        float smoothingT = 1f - Mathf.Exp(-intensitySmoothing * unscaledDeltaTime);
        DangerIntensity = Mathf.Lerp(DangerIntensity, targetDangerIntensity, smoothingT);

        if (targetDangerIntensity <= 0f)
        {
            StopHeartbeat(false);
            return;
        }

        StartHeartbeatIfNeeded();
        UpdateHeartbeatVolume();
        ScheduleHeartbeat();
    }

    private void ConfigureMusic()
    {
        if (backgroundMusic == null)
        {
            Debug.LogWarning("GameplayAudioController needs a background music clip.", this);
            return;
        }

        musicSource = CreateAudioSource("Background Music");
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.priority = 160;
        UpdateMusicVolume();
        musicSource.Play();
    }

    private void ConfigureHeartbeat()
    {
        activeHeartbeatClip = heartbeatClip;
        if (activeHeartbeatClip == null)
        {
            generatedHeartbeatClip = CreateProceduralHeartbeat();
            activeHeartbeatClip = generatedHeartbeatClip;
        }

        for (int i = 0; i < heartbeatSources.Length; i++)
        {
            heartbeatSources[i] = CreateAudioSource($"Heartbeat {i + 1}");
            heartbeatSources[i].clip = activeHeartbeatClip;
            heartbeatSources[i].priority = 32;
        }
    }

    private AudioSource CreateAudioSource(string objectName)
    {
        GameObject audioObject = new GameObject(objectName);
        audioObject.transform.SetParent(transform, false);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null) return;

        player = playerObject.transform;
        playerController = playerObject.GetComponent<PlayerController>();
    }

    private bool ShouldSilenceHeartbeat()
    {
        if (applicationPaused || Time.timeScale <= 0f) return true;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return true;

        if (player == null)
            FindPlayer();

        return player == null || (playerController != null && playerController.IsDead);
    }

    private void ScanForClosestPredator()
    {
        Predator[] predators = FindObjectsByType<Predator>();

        Predator closest = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Predator predator in predators)
        {
            if (predator == null || !predator.isActiveAndEnabled || predator.data == null)
                continue;

            // Only a predator actively hunting the player should trigger the heartbeat.
            if (predator.CurrentState != Predator.State.Chase)
                continue;

            float sqrDistance = ((Vector2)(predator.transform.position - player.position)).sqrMagnitude;
            float predatorNearDistance = Mathf.Max(0f, predator.data.attackRange);
            float predatorFarDistance = Mathf.Max(
                predatorNearDistance + 0.01f,
                predator.data.detectionRange);
            if (sqrDistance > predatorFarDistance * predatorFarDistance) continue;
            if (sqrDistance >= closestSqrDistance) continue;

            closest = predator;
            closestSqrDistance = sqrDistance;
        }

        ClosestPredator = closest;
        if (closest == null)
        {
            targetDangerIntensity = 0f;
            return;
        }

        float distance = Mathf.Sqrt(closestSqrDistance);
        float nearDistance = Mathf.Max(0f, closest.data.attackRange);
        float farDistance = Mathf.Max(nearDistance + 0.01f, closest.data.detectionRange);
        float proximity = 1f - Mathf.InverseLerp(nearDistance, farDistance, distance);
        proximity = Mathf.SmoothStep(0f, 1f, proximity);
        targetDangerIntensity = Mathf.Lerp(minimumProximityIntensity, 1f, proximity);
    }

    private void StartHeartbeatIfNeeded()
    {
        if (heartbeatActive || activeHeartbeatClip == null) return;

        heartbeatActive = true;
        nextHeartbeatSource = 0;
        nextHeartbeatDspTime = AudioSettings.dspTime + FirstBeatDelaySeconds;
    }

    private void UpdateHeartbeatVolume()
    {
        float authoredVolume = Mathf.Lerp(
            farHeartbeatVolume,
            nearHeartbeatVolume,
            Mathf.SmoothStep(0f, 1f, DangerIntensity));
        float finalVolume = authoredVolume * GameAudioSettings.EffectsVolume;

        foreach (AudioSource source in heartbeatSources)
        {
            if (source != null)
                source.volume = finalVolume;
        }
    }

    private void ScheduleHeartbeat()
    {
        if (!heartbeatActive || activeHeartbeatClip == null) return;

        double dspTime = AudioSettings.dspTime;
        if (nextHeartbeatDspTime < dspTime - 0.1d)
            nextHeartbeatDspTime = dspTime + FirstBeatDelaySeconds;

        if (nextHeartbeatDspTime > dspTime + ScheduleAheadSeconds) return;

        AudioSource source = heartbeatSources[nextHeartbeatSource];
        source.clip = activeHeartbeatClip;
        float tempoIntensity = Mathf.SmoothStep(0f, 1f, DangerIntensity);
        source.pitch = Mathf.Lerp(1f, nearHeartbeatPitch, tempoIntensity);
        source.PlayScheduled(nextHeartbeatDspTime);
        nextHeartbeatSource = (nextHeartbeatSource + 1) % heartbeatSources.Length;

        float bpm = Mathf.Lerp(farHeartbeatBpm, nearHeartbeatBpm, tempoIntensity);
        nextHeartbeatDspTime += 60d / Mathf.Max(30f, bpm);
    }

    private void StopHeartbeat(bool resetIntensity)
    {
        if (heartbeatActive)
        {
            foreach (AudioSource source in heartbeatSources)
            {
                if (source != null)
                    source.Stop();
            }
        }

        heartbeatActive = false;
        nextHeartbeatDspTime = 0d;

        if (resetIntensity)
            DangerIntensity = 0f;
    }

    private void UpdateMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = backgroundMusicVolume * GameAudioSettings.MusicVolume;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        applicationPaused = pauseStatus;
        if (pauseStatus)
            StopHeartbeat(true);
    }

    private void HandleAudioConfigurationChanged(bool deviceWasChanged)
    {
        StopHeartbeat(true);
        nextThreatScanTime = 0f;
    }

    private static AudioClip CreateProceduralHeartbeat()
    {
        int sampleCount = Mathf.CeilToInt(HeartbeatSampleRate * HeartbeatDuration);
        float[] samples = new float[sampleCount];

        AddHeartbeatPulse(samples, 0.012f, 1f, 68f);
        AddHeartbeatPulse(samples, 0.145f, 0.72f, 58f);

        float peak = 0f;
        foreach (float sample in samples)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        if (peak > 0f)
        {
            float normalization = 0.92f / peak;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= normalization;
        }

        AudioClip clip = AudioClip.Create(
            "Procedural Heartbeat",
            sampleCount,
            1,
            HeartbeatSampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void AddHeartbeatPulse(float[] samples, float startTime, float amplitude, float frequency)
    {
        int startSample = Mathf.FloorToInt(startTime * HeartbeatSampleRate);
        for (int i = startSample; i < samples.Length; i++)
        {
            float localTime = (i - startSample) / (float)HeartbeatSampleRate;
            float envelope = (1f - Mathf.Exp(-localTime * 150f)) * Mathf.Exp(-localTime * 22f);
            float phase = 2f * Mathf.PI *
                (frequency * localTime - 7f * localTime * localTime);
            float body = Mathf.Sin(phase) + 0.28f * Mathf.Sin(phase * 2f);
            float tissue = 0.04f * Mathf.Sin(2f * Mathf.PI * 180f * localTime) *
                Mathf.Exp(-localTime * 38f);
            samples[i] += amplitude * envelope * (body + tissue);
        }
    }
}
