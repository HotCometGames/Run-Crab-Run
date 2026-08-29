using UnityEngine;

// Design doc section 19: don't make the map harder, make the THREAT DENSITY increase
// over time (0-2min / 2-5min / 5-10min / 10+min tiers). SpawnManager reads CurrentTier
// each time it spawns something.
//
// UNITY SETUP:
//   Add this to the same "GameManager" GameObject (or its own empty GameObject).
//   The default 4 tiers below match the doc's example numbers — tune freely in the
//   Inspector (it's a public array, so it's fully editable without touching code).
[System.Flags]
public enum PredatorDifficulty
{
    Easy   = 1,
    Medium = 2,
    Hard   = 4,
    All    = Easy | Medium | Hard
}

public class DifficultyManager : MonoBehaviour
{
    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Seconds into the run when this tier begins.")]
        public float startTime;
        [Tooltip("Seconds between spawn batches.")]
        public float spawnInterval;
        [Range(0f, 1f), Tooltip("Chance a spawned harmless animal is secretly an imposter.")]
        public float imposterChance;
        [Tooltip("Minimum predators per batch.")]
        public int minPredators;
        [Tooltip("Maximum predators per batch.")]
        public int maxPredators;
        [Tooltip("Minimum harmless animals per batch.")]
        public int minFriendlies;
        [Tooltip("Maximum harmless animals per batch.")]
        public int maxFriendlies;
        [Tooltip("Which predator difficulty levels are allowed to spawn in this tier.")]
        public PredatorDifficulty allowedPredatorDifficulties;
    }

    public Tier[] tiers = new Tier[]
    {
        new Tier { startTime = 0f,   spawnInterval = 12f, imposterChance = 0.10f, minPredators = 0, maxPredators = 1, minFriendlies = 1, maxFriendlies = 2, allowedPredatorDifficulties = PredatorDifficulty.Easy }, // 0-2 min
        new Tier { startTime = 120f, spawnInterval = 9f,  imposterChance = 0.20f, minPredators = 1, maxPredators = 2, minFriendlies = 2, maxFriendlies = 3, allowedPredatorDifficulties = PredatorDifficulty.Easy | PredatorDifficulty.Medium }, // 2-5 min
        new Tier { startTime = 300f, spawnInterval = 6f,  imposterChance = 0.30f, minPredators = 1, maxPredators = 3, minFriendlies = 2, maxFriendlies = 4, allowedPredatorDifficulties = PredatorDifficulty.Easy | PredatorDifficulty.Medium | PredatorDifficulty.Hard }, // 5-10 min
        new Tier { startTime = 600f, spawnInterval = 4f,  imposterChance = 0.40f, minPredators = 2, maxPredators = 4, minFriendlies = 3, maxFriendlies = 5, allowedPredatorDifficulties = PredatorDifficulty.Medium | PredatorDifficulty.Hard }, // 10+ min
    };

    public Tier CurrentTier { get; private set; }

    private void Awake()
    {
        RefreshTier();
    }

    private void Update()
    {
        RefreshTier();
    }

    private void RefreshTier()
    {
        if (tiers == null || tiers.Length == 0) return;

        float t = GameManager.Instance != null ? GameManager.Instance.SurvivalTime : 0f;
        Tier selected = tiers[0];
        float selectedStart = float.NegativeInfinity;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (t >= tiers[i].startTime && tiers[i].startTime >= selectedStart)
            {
                selected = tiers[i];
                selectedStart = tiers[i].startTime;
            }
        }

        CurrentTier = selected;
    }
}
