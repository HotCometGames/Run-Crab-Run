using UnityEngine;

// Design doc section 29 ("Creature Data") — every animal's numbers live in one of these
// assets instead of being hardcoded, so you can tune Wolf vs Fox vs Deer entirely
// from the Inspector without touching a script.
//
// UNITY SETUP:
//   Right-click in Project window -> Create -> RunCrabRun -> Creature Data
//   Make one asset per creature type (e.g. "Data_Wolf", "Data_Fox", "Data_Deer", "Data_Sheep").
public enum CreatureDifficulty { Easy, Medium, Hard }

[CreateAssetMenu(fileName = "NewCreatureData", menuName = "RunCrabRun/Creature Data")]
public class CreatureData : ScriptableObject
{
    [Header("Identity")]
    public string creatureName = "Creature";
    public bool isPredator = false;
    public CreatureDifficulty difficulty = CreatureDifficulty.Easy;

    [Header("Movement")]
    [Tooltip("Normal wander speed.")]
    public float moveSpeed = 2.5f;
    [Tooltip("Harmless-animal flee speed.")]
    public float fleeSpeed = 4f;
    [Tooltip("Predator chase speed.")]
    public float chaseSpeed = 4.5f;

    [Header("Detection")]
    [Tooltip("Radius at which this creature reacts to the player or to predators (should roughly match the DetectionZone collider radius).")]
    public float detectionRange = 5f;
    [Tooltip("Distance at which a predator's Attack() fires (one-hit kill per design doc section 16).")]
    public float attackRange = 0.6f;
    [Tooltip("Cooldown time between a predator's Attack() calls (one-hit kill per design doc section 16).")]
    public float attackCooldown = 0.6f;
    [Tooltip("How long a predator keeps searching after losing the player before giving up.")]
    public float loseInterestTime = 3f;

    [Header("Wander")]
    public float wanderRadius = 6f;
    public float minWanderPause = 1f;
    public float maxWanderPause = 3f;

    [Header("Predator Hunger (invisible)")]
    [Tooltip("Max hunger for predators. Drains over time; predator dies at 0.")]
    public float maxHunger = 100f;
    [Tooltip("Hunger lost per second. Predator dies when it hits 0.")]
    public float hungerDrainPerSecond = 5f;
    [Tooltip("Hunger restored when the predator eats a prey animal.")]
    public float hungerOnEat = 40f;

    [Header("Friendly Animal Hunger")]
    [Tooltip("Max hunger for friendly animals. They seek food when low.")]
    public float friendlyMaxHunger = 100f;
    [Tooltip("Hunger lost per second for friendly animals. They die at 0.")]
    public float friendlyHungerDrainPerSecond = 3f;
    [Tooltip("Hunger restored when a friendly animal eats from a food source.")]
    public float friendlyHungerOnEat = 40f;
    [Tooltip("How long it takes a friendly animal to eat (seconds).")]
    public float eatDuration = 1.3f;
    [Tooltip("Hunger level (0-1) at which a friendly animal starts seeking food.")]
    [Range(0f, 1f)]
    public float hungerSeekThreshold = 0.3f;
}
