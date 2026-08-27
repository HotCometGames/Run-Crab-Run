using UnityEngine;
using System.Collections;

// Reads DifficultyManager.CurrentTier to decide how often to spawn, and how likely a
// spawned harmless animal is secretly an Imposter. Design doc section 19 + 25.
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

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(1f); // wait a few seconds before first spawn
        while (true)
        {
            float interval = difficulty != null ? difficulty.CurrentTier.spawnInterval : 8f;
            yield return new WaitForSeconds(interval);

            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        if (spawnPoints.Length == 0) return;
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];

        float imposterChance = difficulty != null ? difficulty.CurrentTier.imposterChance : 0.15f;
        bool spawnPredator = Random.value < 0.3f; // roughly 30% predators, 70% animals

        GameObject prefab = null;
        if (spawnPredator && predatorPrefabs.Length > 0)
        {
            prefab = predatorPrefabs[Random.Range(0, predatorPrefabs.Length)];
        }
        else if (Random.value < imposterChance && imposterAnimalPrefabs.Length > 0)
        {
            prefab = imposterAnimalPrefabs[Random.Range(0, imposterAnimalPrefabs.Length)];
        }
        else if (harmlessAnimalPrefabs.Length > 0)
        {
            prefab = harmlessAnimalPrefabs[Random.Range(0, harmlessAnimalPrefabs.Length)];
        }

        if (prefab != null)
        {
            Instantiate(prefab, point.position, Quaternion.identity);
        }
    }
}
