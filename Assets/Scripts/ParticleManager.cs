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

        foreach (var e in effects)
        {
            effectLookup[e.type] = e;
            pools[e.type] = new Queue<GameObject>();
        }
    }

    public void Play(ParticleType type, Vector3 position)
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

        float duration = effect.overrideDuration > 0f
            ? effect.overrideDuration
            : GetParticleDuration(obj);

        StartCoroutine(ReturnToPool(type, obj, duration));
    }

    private float GetParticleDuration(GameObject obj)
    {
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) return ps.main.duration + ps.main.startLifetime.constantMax;
        return 2f;
    }

    private IEnumerator ReturnToPool(ParticleType type, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj != null)
        {
            obj.SetActive(false);
            pools[type].Enqueue(obj);
        }
    }
}
