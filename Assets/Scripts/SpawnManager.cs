using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Reads DifficultyManager.CurrentTier to decide how often to spawn animal batches,
// how many animals to add, and how likely each one is secretly an Imposter.
// Standalone predators are never spawned: every Wolf and Fox begins disguised.
//
// UNITY SETUP:
//   - Add this to the "GameManager" GameObject (or its own "SpawnManager" GameObject).
//   - Drag your DifficultyManager into the "Difficulty" field.
//   - Create several empty "SpawnPoint" GameObjects around the edges of your forest
//     tilemap and drag them all into "Spawn Points".
//   - Drag your prefabs into the two arrays:
//       Harmless Animal Prefabs -> plain Deer/Sheep (no ImposterComponent)
//       Imposter Animal Prefabs -> the Sheep_Imposter / Deer_Imposter prefabs from
//                                   ImposterComponent.cs's setup instructions
public class SpawnManager : MonoBehaviour
{
    [Header("References")]
    public DifficultyManager difficulty;
    public Transform[] spawnPoints;

    [Header("Prefabs")]
    public GameObject[] harmlessAnimalPrefabs;
    public GameObject[] imposterAnimalPrefabs;

    [Header("Initial Population")]
    [Min(0), Tooltip("Guaranteed genuine animals spawned immediately before timed batches begin.")]
    public int initialHarmlessCount = 4;

    [Header("Spawn Limits")]
    [Tooltip("Maximum total creatures alive at once. Batches won't exceed this cap.")]
    public int maxTotalCreatures = 50;
    [Min(0), Tooltip("Maximum Wolves present, including dormant Wolves inside unrevealed impostors.")]
    public int maxWolves = 2;
    [Min(0), Tooltip("Starting maximum Foxes. Timed bonus predator slots add to this allowance while the Wolf cap stays fixed.")]
    public int maxFoxes = 2;

    [Header("Predator Population Ramp")]
    [Min(0f), Tooltip("Seconds before the total predator capacity begins increasing.")]
    public float predatorRampStartTime = 120f;
    [Min(0.1f), Tooltip("Seconds between increases to total predator capacity.")]
    public float predatorRampInterval = 35f;
    [Min(0), Tooltip("Predator slots added each interval after the ramp begins.")]
    public int predatorsAddedPerInterval = 1;

    public int CurrentMaxPredators
    {
        get
        {
            int totalCreatureCap = Mathf.Max(0, maxTotalCreatures);
            int basePredatorCap = Mathf.Max(0, maxWolves) + Mathf.Max(0, maxFoxes);
            return Mathf.Min(totalCreatureCap, basePredatorCap + GetPredatorCapacityIncrease());
        }
    }

    [Tooltip("Random offset radius around each spawn point.")]
    public float spawnRadius = 1.5f;
    [Tooltip("Avoid popping new creatures directly beside the player.")]
    public float minSpawnDistanceFromPlayer = 6f;
    [Tooltip("Avoid spawning creatures on top of an animal that is already using the same edge point.")]
    [Min(0f)] public float minSpawnDistanceFromCreatures = 1.35f;

    private void Start()
    {
        SpawnInitialHarmlessAnimals();
        StartCoroutine(SpawnLoop());
    }

    private void SpawnInitialHarmlessAnimals()
    {
        if (initialHarmlessCount <= 0 || spawnPoints == null || spawnPoints.Length == 0)
            return;

        CountCreatureGameObjects(out int currentCount, out _, out _);
        int spawnCount = Mathf.Min(
            initialHarmlessCount,
            Mathf.Max(0, maxTotalCreatures - currentCount));
        if (spawnCount <= 0) return;

        List<GameObject> genuineHarmlessPrefabs = new List<GameObject>();
        if (harmlessAnimalPrefabs != null)
        {
            foreach (GameObject prefab in harmlessAnimalPrefabs)
            {
                if (prefab == null ||
                    prefab.GetComponent<HarmlessAnimal>() == null ||
                    prefab.GetComponent<ImposterComponent>() != null ||
                    prefab.GetComponent<Predator>() != null)
                {
                    continue;
                }

                genuineHarmlessPrefabs.Add(prefab);
            }
        }

        if (genuineHarmlessPrefabs.Count == 0)
        {
            Debug.LogWarning($"{name}: cannot seed the initial population because no genuine harmless prefabs are configured.", this);
            return;
        }

        // Cycle through the genuine prefab pool from a random starting point so the
        // opening population is varied but stays balanced when several types exist.
        int prefabOffset = Random.Range(0, genuineHarmlessPrefabs.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            if (!TryChooseInitialSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning($"{name}: only placed {i} of {spawnCount} initial harmless animals because no clear spawn position remained.", this);
                break;
            }

            GameObject prefab = genuineHarmlessPrefabs[(prefabOffset + i) % genuineHarmlessPrefabs.Count];
            Instantiate(prefab, position, Quaternion.identity);

            // Make each new collider visible to the next placement query this frame.
            Physics2D.SyncTransforms();
        }
    }

    private bool TryChooseInitialSpawnPosition(out Vector3 position)
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        position = transform.position;

        // Keep the opening population outside the batch spawn disks. Otherwise the
        // four stationary spawn-delay colliders would block the normal 0.5s batch.
        float batchPointClearance = Mathf.Max(0f, spawnRadius) +
            Mathf.Max(0f, minSpawnDistanceFromCreatures) + 0.75f;

        for (int attempt = 0; attempt < 128; attempt++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null) continue;

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f) continue;

            float offset = Random.Range(batchPointClearance, batchPointClearance + 2f);
            Vector3 candidate = point.position + (Vector3)(direction * offset);
            if (!IsInsideWorldBounds(candidate, 0.75f)) continue;

            bool clearOfPlayer = player == null ||
                Vector2.Distance(candidate, player.position) >= minSpawnDistanceFromPlayer;
            if (!clearOfPlayer || IsNearCreature(candidate)) continue;

            bool clearOfBatchPoints = true;
            foreach (Transform batchPoint in spawnPoints)
            {
                if (batchPoint != null &&
                    Vector2.Distance(candidate, batchPoint.position) < batchPointClearance)
                {
                    clearOfBatchPoints = false;
                    break;
                }
            }

            if (!clearOfBatchPoints) continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private static bool IsInsideWorldBounds(Vector2 position, float margin)
    {
        if (WorldBoundary.Instance == null) return true;

        Vector2 firstCorner = WorldBoundary.Instance.WorldMinimum;
        Vector2 secondCorner = WorldBoundary.Instance.WorldMaximum;
        float minX = Mathf.Min(firstCorner.x, secondCorner.x) + margin;
        float maxX = Mathf.Max(firstCorner.x, secondCorner.x) - margin;
        float minY = Mathf.Min(firstCorner.y, secondCorner.y) + margin;
        float maxY = Mathf.Max(firstCorner.y, secondCorner.y) - margin;

        return position.x >= minX && position.x <= maxX &&
            position.y >= minY && position.y <= maxY;
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(.5f);
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
            out int currentWolfCount,
            out int currentFoxCount);
        int remaining = maxTotalCreatures - currentCount;
        if (remaining <= 0) return;

        int minFriendlies = Mathf.Max(0, Mathf.Min(tier.minFriendlies, tier.maxFriendlies));
        int maxFriendlies = Mathf.Max(minFriendlies, Mathf.Max(tier.minFriendlies, tier.maxFriendlies));
        int friendlyCount = Mathf.Min(
            Random.Range(minFriendlies, maxFriendlies + 1),
            remaining);

        float imposterChance = difficulty != null ? tier.imposterChance : 0.25f;

        // Every slot begins as an animal. An imposter roll may select any configured
        // disguise whose hidden species still has room under its identity cap.
        for (int i = 0; i < friendlyCount; i++)
        {
            GameObject prefab = null;

            if (Random.value < imposterChance && imposterAnimalPrefabs != null && imposterAnimalPrefabs.Length > 0)
            {
                prefab = GetRandomImposter(currentWolfCount, currentFoxCount);
            }

            // If the roll selected an imposter whose hidden predator species is full,
            // spawn a genuinely harmless animal instead of exceeding the hard cap.
            if (prefab == null && harmlessAnimalPrefabs != null && harmlessAnimalPrefabs.Length > 0)
            {
                prefab = harmlessAnimalPrefabs[Random.Range(0, harmlessAnimalPrefabs.Length)];
            }

            if (prefab == null)
            {
                continue;
            }

            if (!TryChooseSpawnPosition(out Vector3 pos)) continue;

            GameObject spawned = Instantiate(prefab, pos, Quaternion.identity);
            if (spawned == null) continue;

            IncrementSpeciesCounts(prefab, ref currentWolfCount, ref currentFoxCount);
            ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, pos);
        }
    }

    private GameObject GetRandomImposter(
        int currentWolfCount,
        int currentFoxCount)
    {
        List<GameObject> valid = new List<GameObject>();
        if (imposterAnimalPrefabs == null) return null;

        int predatorCapacityIncrease = GetPredatorCapacityIncrease();
        if (currentWolfCount + currentFoxCount >= CurrentMaxPredators) return null;

        foreach (GameObject prefab in imposterAnimalPrefabs)
        {
            if (prefab == null || !HasSpeciesCapacity(
                    prefab,
                    currentWolfCount,
                    currentFoxCount,
                    predatorCapacityIncrease))
                continue;

            ImposterComponent imposter = prefab.GetComponent<ImposterComponent>();
            Predator hiddenPredator = imposter != null ? imposter.hiddenPredator : null;
            if (prefab.GetComponent<HarmlessAnimal>() == null ||
                hiddenPredator == null ||
                hiddenPredator.gameObject != prefab ||
                hiddenPredator.data == null)
            {
                continue;
            }

            valid.Add(prefab);
        }

        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    private bool HasSpeciesCapacity(
        GameObject prefab,
        int currentWolfCount,
        int currentFoxCount,
        int predatorCapacityIncrease)
    {
        if (prefab.GetComponent<Wolf>() != null && currentWolfCount >= Mathf.Max(0, maxWolves))
            return false;

        int effectiveFoxCap = Mathf.Max(0, maxFoxes) + predatorCapacityIncrease;
        if (prefab.GetComponent<Fox>() != null && currentFoxCount >= effectiveFoxCap)
            return false;

        return true;
    }

    private int GetPredatorCapacityIncrease()
    {
        float survivalTime = GameManager.Instance != null ? GameManager.Instance.SurvivalTime : 0f;
        float rampStartTime = Mathf.Max(0f, predatorRampStartTime);
        if (survivalTime <= rampStartTime) return 0;

        float interval = Mathf.Max(0.1f, predatorRampInterval);
        int completedIntervals = Mathf.FloorToInt((survivalTime - rampStartTime) / interval);
        int increasePerInterval = Mathf.Max(0, predatorsAddedPerInterval);
        int maxUsefulIncrease = Mathf.Max(0, maxTotalCreatures);

        if (completedIntervals <= 0 || increasePerInterval <= 0) return 0;
        if (completedIntervals > maxUsefulIncrease / increasePerInterval)
            return maxUsefulIncrease;

        return completedIntervals * increasePerInterval;
    }

    private static void IncrementSpeciesCounts(
        GameObject prefab,
        ref int currentWolfCount,
        ref int currentFoxCount)
    {
        if (prefab.GetComponent<Wolf>() != null) currentWolfCount++;
        if (prefab.GetComponent<Fox>() != null) currentFoxCount++;
    }

    private void CountCreatureGameObjects(
        out int total,
        out int wolves,
        out int foxes)
    {
        Creature[] creatures = FindObjectsByType<Creature>();
        HashSet<GameObject> gameObjects = new HashSet<GameObject>();
        foreach (Creature creature in creatures)
        {
            if (creature != null)
                gameObjects.Add(creature.gameObject);
        }

        total = gameObjects.Count;
        wolves = 0;
        foxes = 0;

        foreach (GameObject creatureObject in gameObjects)
        {
            // Component identity remains stable through an imposter reveal, unlike
            // its tag and enabled state, so dormant predators reserve capacity too.
            if (creatureObject.GetComponent<Wolf>() != null) wolves++;
            if (creatureObject.GetComponent<Fox>() != null) foxes++;
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
