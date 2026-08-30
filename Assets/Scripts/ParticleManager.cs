using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Reusable particle effect manager with object pooling.
// Call ParticleManager.Instance.Play(type, position) from anywhere.
//
// UNITY SETUP:
//   - Create an empty "ParticleManager" GameObject in your scene.
//   - Add this script.
//   - Drag your particle system prefabs into the "effects" array and assign their type.
//   - Each prefab needs a ParticleSystem component (with Stop Action = Destroy, or
//     the pool will handle deactivation automatically).
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    // Dominant painted fill sampled from the river in map background.png (#8CCCFA).
    private static readonly Color RiverWaterColor = new Color(
        140f / 255f,
        204f / 255f,
        250f / 255f,
        1f);

    public AudioClip PoofSound;

    public enum ParticleType
    {
        SpawnPoof,
        Death,
        Eat,
        WaterSplash
    }

    [System.Serializable]
    public struct ParticleEffect
    {
        public ParticleType type;
        public GameObject prefab;
        [Tooltip("Extra seconds before the object is returned to the pool. " +
                 "Leave at 0 to auto-detect from the ParticleSystem duration.")]
        public float overrideDuration;
    }

    [Header("Effects")]
    public ParticleEffect[] effects;

    private Dictionary<ParticleType, Queue<GameObject>> pools = new Dictionary<ParticleType, Queue<GameObject>>();
    private Dictionary<ParticleType, ParticleEffect> effectLookup = new Dictionary<ParticleType, ParticleEffect>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (effects == null) return;

        foreach (var e in effects)
        {
            effectLookup[e.type] = e;
            pools[e.type] = new Queue<GameObject>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(ParticleType type, Vector3 position, bool playPoofSound = false)
    {
        if (!effectLookup.TryGetValue(type, out var effect) || effect.prefab == null) return;

        GameObject obj;
        var pool = pools[type];

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.transform.position = position;
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(effect.prefab, position, Quaternion.identity);
        }

        ParticleSystem particleSystem = obj.GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.useUnscaledTime = type == ParticleType.Death;
            if (type == ParticleType.WaterSplash)
                ApplyWaterSplashStyle(particleSystem);

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        float duration = effect.overrideDuration > 0f
            ? effect.overrideDuration
            : GetParticleDuration(obj);

        StartCoroutine(ReturnToPool(type, obj, duration));

        if (playPoofSound && PoofSound != null)
            SoundManager.Instance?.PlaySound(PoofSound, position, 0.75f);
    }

    private static void ApplyWaterSplashStyle(ParticleSystem particleSystem)
    {
        var main = particleSystem.main;
        main.startColor = RiverWaterColor;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var fade = particleSystem.colorOverLifetime;
        fade.enabled = true;
        Gradient alphaFade = new Gradient();
        alphaFade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = alphaFade;

        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
            particleRenderer.sortingOrder = 2;
    }

    private float GetParticleDuration(GameObject obj)
    {
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) return ps.main.duration + ps.main.startLifetime.constantMax;
        return 2f;
    }

    private IEnumerator ReturnToPool(ParticleType type, GameObject obj, float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, delay));

        if (obj != null)
        {
            obj.SetActive(false);
            if (pools.TryGetValue(type, out Queue<GameObject> pool))
                pool.Enqueue(obj);
        }
    }
}
