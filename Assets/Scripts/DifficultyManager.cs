using UnityEngine;

// Design doc section 19: don't make the map harder, make the THREAT DENSITY increase
// over time (0-2min / 2-5min / 5-10min / 10+min tiers). SpawnManager reads CurrentTier
// each time it spawns something.
//
// UNITY SETUP:
//   Add this to the same "GameManager" GameObject (or its own empty GameObject).
//   The default 4 tiers below match the doc's example numbers — tune freely in the
//   Inspector (it's a public array, so it's fully editable without touching code).
public class DifficultyManager : MonoBehaviour
{
    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Seconds into the run when this tier begins.")]
        public float startTime;
        [Tooltip("Seconds between predator/imposter spawn attempts.")]
        public float spawnInterval;
        [Range(0f, 1f), Tooltip("Chance a spawned harmless animal is secretly an imposter.")]
        public float imposterChance;
    }

    public Tier[] tiers = new Tier[]
    {
        new Tier { startTime = 0f,   spawnInterval = 12f, imposterChance = 0.10f }, // 0-2 min
        new Tier { startTime = 120f, spawnInterval = 9f,  imposterChance = 0.20f }, // 2-5 min
        new Tier { startTime = 300f, spawnInterval = 6f,  imposterChance = 0.30f }, // 5-10 min
        new Tier { startTime = 600f, spawnInterval = 4f,  imposterChance = 0.40f }, // 10+ min
    };

    public Tier CurrentTier { get; private set; }

    private void Update()
    {
        if (GameManager.Instance == null || tiers.Length == 0) return;
        float t = GameManager.Instance.SurvivalTime;

        for (int i = tiers.Length - 1; i >= 0; i--)
        {
            if (t >= tiers[i].startTime)
            {
                CurrentTier = tiers[i];
                break;
            }
        }
    }
}
