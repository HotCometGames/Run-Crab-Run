using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Reads DifficultyManager.CurrentTier to decide how often to spawn batches,
// how many predators and friendly animals per batch, and how likely a
// spawned harmless animal is secretly an Imposter.
//
// UNITY SETUP:
//   - Add this to the "GameManager" GameObject (or its own "SpawnManager" GameObject).
//   - Drag your DifficultyManager into the "Difficulty" field.
//   - Create several empty "SpawnPoint" GameObjects around the edges of your forest
//     tilemap and drag them all into "Spawn Points".
//   - Drag your prefabs into the three arrays:
//       Harmless Animal Prefabs -> plain Deer/Sheep (no ImposterComponent)
//       Imposter Animal Prefabs -> the Sheep_Imposter / Deer_Imposter prefabs from
//                                   ImposterComponent.cs's setup instructions
//       Predator Prefabs        -> standalone Wolf/Fox prefabs
public class SpawnManager : MonoBehaviour
{
    [Header("References")]
    public DifficultyManager difficulty;
    public Transform[] spawnPoints;

    [Header("Prefabs")]
    public GameObject[] harmlessAnimalPrefabs;
    public GameObject[] imposterAnimalPrefabs;
    public GameObject[] predatorPrefabs;

    [Header("Spawn Limits")]
    [Tooltip("Maximum total creatures alive at once. Batches won't exceed this cap.")]
    public int maxTotalCreatures = 50;
    [Tooltip("Random offset radius around each spawn point.")]
    public float spawnRadius = 1.5f;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(4f);
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
        SpawnBatch();

        while (true)
        {
            float interval = difficulty != null ? difficulty.CurrentTier.spawnInterval : 8f;
            yield return new WaitForSeconds(interval);

            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            SpawnBatch();
        }
    }

    private void SpawnBatch()
    {
        if (spawnPoints.Length == 0) return;

        DifficultyManager.Tier tier = difficulty != null ? difficulty.CurrentTier : default;

        int currentCount = FindObjectsOfType<Creature>().Length;
        int remaining = maxTotalCreatures - currentCount;
        if (remaining <= 0) return;

        int predatorCount = Random.Range(
            Mathf.Max(tier.minPredators, 0),
            Mathf.Max(tier.maxPredators, 0) + 1
        );
        int friendlyCount = Random.Range(
            Mathf.Max(tier.minFriendlies, 0),
            Mathf.Max(tier.maxFriendlies, 0) + 1
        );

        predatorCount = Mathf.Min(predatorCount, remaining);
        remaining -= predatorCount;
        friendlyCount = Mathf.Min(friendlyCount, remaining);

        float imposterChance = difficulty != null ? tier.imposterChance : 0.15f;

        for (int i = 0; i < predatorCount; i++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = point.position + (Vector3)offset;

            GameObject prefab = GetRandomPredator(tier.allowedPredatorDifficulties);
            if (prefab == null) break;

            Instantiate(prefab, pos, Quaternion.identity);
            ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, pos);
        }

        for (int i = 0; i < friendlyCount; i++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = point.position + (Vector3)offset;
            GameObject prefab;

            if (Random.value < imposterChance && imposterAnimalPrefabs.Length > 0)
            {
                prefab = imposterAnimalPrefabs[Random.Range(0, imposterAnimalPrefabs.Length)];
            }
            else if (harmlessAnimalPrefabs.Length > 0)
            {
                prefab = harmlessAnimalPrefabs[Random.Range(0, harmlessAnimalPrefabs.Length)];
            }
            else
            {
                continue;
            }

            Instantiate(prefab, pos, Quaternion.identity);
            ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, pos);
        }
    }

    private GameObject GetRandomPredator(PredatorDifficulty allowed)
    {
        List<GameObject> valid = new List<GameObject>();
        foreach (var prefab in predatorPrefabs)
        {
            var creature = prefab.GetComponent<Creature>();
            if (creature != null && creature.data != null &&
                (allowed & (PredatorDifficulty)(1 << (int)creature.data.difficulty)) != 0)
            {
                valid.Add(prefab);
            }
        }
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }
}
