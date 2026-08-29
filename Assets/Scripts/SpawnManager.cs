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
    [Tooltip("Avoid popping new creatures directly beside the player.")]
    public float minSpawnDistanceFromPlayer = 6f;

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
            yield return new WaitForSeconds(Mathf.Max(0.5f, interval));

            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            SpawnBatch();
        }
    }

    private void SpawnBatch()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        DifficultyManager.Tier tier = difficulty != null ? difficulty.CurrentTier : default;

        int currentCount = CountCreatureGameObjects();
        int remaining = maxTotalCreatures - currentCount;
        if (remaining <= 0) return;

        int minPredators = Mathf.Max(0, Mathf.Min(tier.minPredators, tier.maxPredators));
        int maxPredators = Mathf.Max(minPredators, Mathf.Max(tier.minPredators, tier.maxPredators));
        int minFriendlies = Mathf.Max(0, Mathf.Min(tier.minFriendlies, tier.maxFriendlies));
        int maxFriendlies = Mathf.Max(minFriendlies, Mathf.Max(tier.minFriendlies, tier.maxFriendlies));

        int predatorCount = Random.Range(minPredators, maxPredators + 1);
        int friendlyCount = Random.Range(minFriendlies, maxFriendlies + 1);

        predatorCount = Mathf.Min(predatorCount, remaining);
        remaining -= predatorCount;
        friendlyCount = Mathf.Min(friendlyCount, remaining);

        float imposterChance = difficulty != null ? tier.imposterChance : 0.15f;

        for (int i = 0; i < predatorCount; i++)
        {
            Vector3 pos = ChooseSpawnPosition();

            GameObject prefab = GetRandomPredator(tier.allowedPredatorDifficulties);
            if (prefab == null) break;

            Instantiate(prefab, pos, Quaternion.identity);
            ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, pos);
        }

        for (int i = 0; i < friendlyCount; i++)
        {
            Vector3 pos = ChooseSpawnPosition();
            GameObject prefab;

            if (Random.value < imposterChance && imposterAnimalPrefabs != null && imposterAnimalPrefabs.Length > 0)
            {
                prefab = imposterAnimalPrefabs[Random.Range(0, imposterAnimalPrefabs.Length)];
            }
            else if (harmlessAnimalPrefabs != null && harmlessAnimalPrefabs.Length > 0)
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
        if (predatorPrefabs == null) return null;
        if (allowed == 0) allowed = PredatorDifficulty.All;

        foreach (var prefab in predatorPrefabs)
        {
            if (prefab == null) continue;
            var creature = prefab.GetComponent<Creature>();
            if (creature != null && creature.data != null &&
                (allowed & (PredatorDifficulty)(1 << (int)creature.data.difficulty)) != 0)
            {
                valid.Add(prefab);
            }
        }
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private int CountCreatureGameObjects()
    {
        Creature[] creatures = FindObjectsByType<Creature>();
        HashSet<GameObject> gameObjects = new HashSet<GameObject>();
        foreach (Creature creature in creatures)
        {
            if (creature != null)
                gameObjects.Add(creature.gameObject);
        }

        return gameObjects.Count;
    }

    private Vector3 ChooseSpawnPosition()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        Vector3 candidate = transform.position;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null) continue;

            candidate = point.position + (Vector3)(Random.insideUnitCircle * Mathf.Max(0f, spawnRadius));
            if (player == null || Vector2.Distance(candidate, player.position) >= minSpawnDistanceFromPlayer)
                return candidate;
        }

        return candidate;
    }
}
