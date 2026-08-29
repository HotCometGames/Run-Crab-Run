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

    [Header("Natural Steering")]
    [Tooltip("How quickly this creature reaches its requested speed.")]
    [Min(0.1f)] public float acceleration = 8f;
    [Tooltip("How quickly this creature slows down or changes to a lower speed.")]
    [Min(0.1f)] public float deceleration = 12f;
    [Tooltip("Maximum steering rate in degrees per second.")]
    [Min(1f)] public float turnSpeed = 420f;
    [Tooltip("Distance before target movement begins easing to a stop.")]
    [Min(0.1f)] public float arrivalSlowRadius = 0.9f;
    [Tooltip("Base distance used to anticipate solid walls.")]
    [Min(0f)] public float obstacleLookAhead = 0.8f;
    [Tooltip("Strength of steering around solid walls.")]
    [Min(0f)] public float obstacleAvoidanceWeight = 1.35f;
    [Tooltip("Distance at which creatures begin giving one another personal space.")]
    [Min(0f)] public float separationRadius = 1.2f;
    [Tooltip("Strength of personal-space steering.")]
    [Min(0f)] public float separationWeight = 0.75f;
    [Tooltip("Enable when the unflipped source sprite points right. Disable when it points left.")]
    public bool spriteFacesRightByDefault = true;
    [Tooltip("Horizontal speed required before the sprite may flip, preventing rapid flicker near a stop.")]
    [Min(0f)] public float spriteFlipThreshold = 0.15f;

    [Header("Movement Character")]
    [Tooltip("Maximum amount wandering may curve away from a straight route.")]
    [Range(0f, 45f)] public float wanderNoiseAngle = 14f;
    [Tooltip("How quickly the gentle wander curve changes.")]
    [Min(0.01f)] public float wanderNoiseFrequency = 0.35f;
    [Tooltip("Maximum organic side-to-side variation while fleeing.")]
    [Range(0f, 45f)] public float fleeNoiseAngle = 18f;
    [Tooltip("How quickly flee variation changes.")]
    [Min(0.01f)] public float fleeNoiseFrequency = 0.4f;

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
