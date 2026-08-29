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
    [Tooltip("Avoid spawning creatures on top of an animal that is already using the same edge point.")]
    [Min(0f)] public float minSpawnDistanceFromCreatures = 1.35f;

    private const float MinimumFriendlyPopulationFraction = 0.5f;

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

        CountCreatureGameObjects(
            out int currentCount,
            out int currentFriendlyCount,
            out int currentPredatorCount);
        int remaining = maxTotalCreatures - currentCount;
        if (remaining <= 0) return;

        int minPredators = Mathf.Max(0, Mathf.Min(tier.minPredators, tier.maxPredators));
        int maxPredators = Mathf.Max(minPredators, Mathf.Max(tier.minPredators, tier.maxPredators));
        int minFriendlies = Mathf.Max(0, Mathf.Min(tier.minFriendlies, tier.maxFriendlies));
        int maxFriendlies = Mathf.Max(minFriendlies, Mathf.Max(tier.minFriendlies, tier.maxFriendlies));

        int wantedPredators = Random.Range(minPredators, maxPredators + 1);
        int wantedFriendlies = Random.Range(minFriendlies, maxFriendlies + 1);

        // Predation creates empty slots by removing friendlies. If predators always
        // claim those slots first, a capped scene slowly becomes all predators. Keep
        // at least half the ecosystem harmless, including still-disguised imposters.
        int minimumFriendlyPopulation = Mathf.CeilToInt(
            Mathf.Max(0, maxTotalCreatures) * MinimumFriendlyPopulationFraction);
        int friendlyDeficit = Mathf.Max(0, minimumFriendlyPopulation - currentFriendlyCount);
        int predatorCapacity = Mathf.Max(
            0,
            maxTotalCreatures - minimumFriendlyPopulation - currentPredatorCount);
        wantedPredators = Mathf.Min(wantedPredators, predatorCapacity);

        int predatorCount = 0;
        int friendlyCount = Mathf.Min(wantedFriendlies, Mathf.Min(friendlyDeficit, remaining));
        wantedFriendlies -= friendlyCount;
        remaining -= friendlyCount;

        // Randomized allocation avoids favoring either group when only part of the
        // requested batch fits, after the friendly reserve has been protected.
        while (remaining > 0 && (wantedPredators > 0 || wantedFriendlies > 0))
        {
            bool chooseFriendly = wantedFriendlies > 0 &&
                (wantedPredators <= 0 || Random.Range(0, wantedPredators + wantedFriendlies) < wantedFriendlies);

            if (chooseFriendly)
            {
                friendlyCount++;
                wantedFriendlies--;
            }
            else
            {
                predatorCount++;
                wantedPredators--;
            }

            remaining--;
        }

        float imposterChance = difficulty != null ? tier.imposterChance : 0.15f;

        for (int i = 0; i < predatorCount; i++)
        {
            GameObject prefab = GetRandomPredator(tier.allowedPredatorDifficulties);
            if (prefab == null) break;
            if (!TryChooseSpawnPosition(out Vector3 pos)) continue;

            Instantiate(prefab, pos, Quaternion.identity);
            ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, pos);
        }

        for (int i = 0; i < friendlyCount; i++)
        {
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

            if (!TryChooseSpawnPosition(out Vector3 pos)) continue;

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

    private void CountCreatureGameObjects(
        out int total,
        out int friendlies,
        out int predators)
    {
        Creature[] creatures = FindObjectsByType<Creature>();
        HashSet<GameObject> gameObjects = new HashSet<GameObject>();
        foreach (Creature creature in creatures)
        {
            if (creature != null)
                gameObjects.Add(creature.gameObject);
        }

        total = gameObjects.Count;
        friendlies = 0;
        predators = 0;

        foreach (GameObject creatureObject in gameObjects)
        {
            if (creatureObject.CompareTag("Animal")) friendlies++;
            else if (creatureObject.CompareTag("Predator")) predators++;
        }
    }

    private bool TryChooseSpawnPosition(out Vector3 position)
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        position = transform.position;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null) continue;

            Vector3 candidate = point.position +
                (Vector3)(Random.insideUnitCircle * Mathf.Max(0f, spawnRadius));
            bool clearOfPlayer = player == null ||
                Vector2.Distance(candidate, player.position) >= minSpawnDistanceFromPlayer;
            if (!clearOfPlayer || IsNearCreature(candidate)) continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private bool IsNearCreature(Vector2 position)
    {
        float radius = Mathf.Max(0f, minSpawnDistanceFromCreatures);
        if (radius <= 0f) return false;

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, radius);
        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == null || overlap.isTrigger) continue;
            if (overlap.GetComponentInParent<Creature>() != null) return true;
        }

        return false;
    }
}
