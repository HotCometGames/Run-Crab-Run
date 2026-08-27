using UnityEngine;

// Design doc section 29 ("Creature Data") — every animal's numbers live in one of these
// assets instead of being hardcoded, so you can tune Wolf vs Fox vs Deer entirely
// from the Inspector without touching a script.
//
// UNITY SETUP:
//   Right-click in Project window -> Create -> RunCrabRun -> Creature Data
//   Make one asset per creature type (e.g. "Data_Wolf", "Data_Fox", "Data_Deer", "Data_Sheep").
[CreateAssetMenu(fileName = "NewCreatureData", menuName = "RunCrabRun/Creature Data")]
public class CreatureData : ScriptableObject
{
    [Header("Identity")]
    public string creatureName = "Creature";
    public bool isPredator = false;

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
    [Tooltip("Distance at which a predator's Attack() fires. Keep this close to the body collider so a hit looks fair.")]
    public float attackRange = 0.6f;
    [Tooltip("Time before the predator can damage the player again.")]
    public float attackCooldown = 0.6f;
    [Tooltip("How long a predator keeps searching after losing the player before giving up.")]
    public float loseInterestTime = 3f;

    [Header("Wander")]
    public float wanderRadius = 6f;
    public float minWanderPause = 1f;
    public float maxWanderPause = 3f;

    [Header("Harmless Animal Behaviour")]
    [Tooltip("Chance that this animal briefly moves away when the player enters its detection zone. Keep this low so it is never an imposter tell.")]
    [Range(0f, 1f)] public float playerAvoidanceChance = 0f;
    [Tooltip("How long a player-shy animal keeps fleeing before it may settle down.")]
    public float playerAvoidanceTime = 0.5f;
    [Tooltip("Minimum time spent fleeing a real predator or revealed imposter.")]
    public float minFleeTime = 1.5f;
    [Tooltip("Chance to choose a nearby harmless animal as the next wander destination.")]
    [Range(0f, 1f)] public float flockTargetChance = 0f;
    public float flockSearchRadius = 3f;
    public float flockArrivalRadius = 1.25f;

    [Header("Predator Hunger (invisible)")]
    [Tooltip("Max hunger for predators. Drains over time; predator dies at 0.")]
    public float maxHunger = 100f;
    [Tooltip("Hunger lost per second. Predator dies when it hits 0.")]
    public float hungerDrainPerSecond = 5f;
    [Tooltip("Hunger restored when the predator eats a prey animal.")]
    public float hungerOnEat = 40f;
}
