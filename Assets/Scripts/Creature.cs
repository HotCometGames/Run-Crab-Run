using UnityEngine;
using System.Collections;

// Base class per design doc section 28:
//   Creature -> HarmlessAnimal (Deer, Sheep) / Predator (Wolf, Fox, Bird)
//
// Holds everything every animal needs regardless of whether it's harmless or a predator:
// a Rigidbody2D reference, a cached reference to the player, generic "move toward point"
// helpers, and a shared random-wander coroutine.
//
// UNITY SETUP (applies to every creature prefab):
//   - Add a Rigidbody2D. Set Body Type = Dynamic, Gravity Scale = 0, and freeze Z rotation
//     (Constraints -> Freeze Rotation Z) since this is top-down.
//   - Add a Collider2D for physical bumping (e.g. CircleCollider2D, NOT a trigger).
//   - Add a child GameObject with a DetectionZone (see DetectionZone.cs) and assign it
//     to the "Detection Zone" field below.
//   - Assign a CreatureData asset to the "Data" field.
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Creature : MonoBehaviour
{
    // WaitForFixedUpdate is stateless and safe to share across all movement coroutines.
    protected static readonly WaitForFixedUpdate FixedUpdateYield = new WaitForFixedUpdate();

    [Header("Data")]
    public CreatureData data;

    [Header("Detection")]
    [Tooltip("This creature's own detection radius child object. See DetectionZone.cs.")]
    public DetectionZone detectionZone;

    [Header("Spawn")]
    [Tooltip("Seconds to wait after spawning before this creature starts moving.")]
    public float spawnDelay = 1f;

    protected Rigidbody2D rb;
    protected Transform player;
    protected PlayerController playerController;
    protected CreatureMotor2D motor;

    protected Vector2 spawnPoint;
    private Vector2 wanderTarget;
    private Coroutine wanderRoutine;
    private float spawnTimer;
    private Vector2 wanderHeading;

    private const float WanderArrivalDistance = 0.15f;
    private const float WanderNoProgressTimeout = 0.75f;
    private const float WanderProgressDistance = 0.01f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        motor = GetComponent<CreatureMotor2D>();
        if (motor == null) motor = gameObject.AddComponent<CreatureMotor2D>();
        spawnPoint = transform.position;
        spawnTimer = Mathf.Max(0f, spawnDelay);
        wanderHeading = Random.insideUnitCircle.normalized;
        if (wanderHeading.sqrMagnitude < 0.0001f) wanderHeading = Vector2.right;

        if (data == null)
            Debug.LogError($"{name}: {GetType().Name} requires a CreatureData asset.", this);

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }

        if (detectionZone != null)
        {
            detectionZone.OnEnter += HandleDetectionEnter;
            detectionZone.OnExit += HandleDetectionExit;
        }
    }

    protected virtual void OnDestroy()
    {
        motor?.Release(this);

        if (detectionZone != null)
        {
            detectionZone.OnEnter -= HandleDetectionEnter;
            detectionZone.OnExit -= HandleDetectionExit;
        }
    }

    // Subclasses decide what "noticing" something means (flee for HarmlessAnimal, chase for Predator).
    protected abstract void HandleDetectionEnter(Collider2D other);
    protected abstract void HandleDetectionExit(Collider2D other);

    protected bool IsReadyToMove => spawnTimer <= 0f;

    // Used when a dormant behaviour must react immediately, most notably when an
    // imposter enables its hidden Predator component after the disguise breaks.
    protected void SkipSpawnDelay()
    {
        spawnTimer = 0f;
    }

    protected float DistanceToPlayer() =>
        player == null ? Mathf.Infinity : Vector2.Distance(transform.position, player.position);

    protected bool PlayerIsHidden() =>
        playerController != null && playerController.IsHidden;

    protected virtual void Update()
    {
        if (data == null) return;

        if (spawnTimer > 0f)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && wanderRoutine == null && !ShouldInterruptWander())
                StartWandering();
        }
    }

    protected void MoveTowards(
        Vector2 target,
        float speed,
        CreatureMovementStyle style = CreatureMovementStyle.Wander,
        float stopRadius = 0.15f,
        float slowRadius = -1f)
    {
        if (motor == null || data == null) return;
        float resolvedSlowRadius = slowRadius > stopRadius
            ? slowRadius
            : Mathf.Max(stopRadius + 0.1f, data.arrivalSlowRadius);
        motor.DriveTo(this, data, target, speed, style, stopRadius, resolvedSlowRadius);
    }

    protected void MoveInDirection(
        Vector2 direction,
        float speed,
        CreatureMovementStyle style = CreatureMovementStyle.Wander)
    {
        if (motor == null || data == null) return;
        motor.DriveDirection(this, data, direction, speed, style);
    }

    protected void StopMoving(CreatureMovementStyle style = CreatureMovementStyle.Wander) =>
        motor?.Brake(this, data, style);

    protected void ReleaseMotor() => motor?.Release(this);

    // Wanders to random points around this creature's spawn location, forever,
    // until a subclass state (Flee / Chase / Search) interrupts it.
    private IEnumerator WanderRoutine()
    {
        while (data != null)
        {
            wanderTarget = ChooseWanderTarget();

            float startingDistance = Vector2.Distance(rb.position, wanderTarget);
            float estimatedTravelTime = startingDistance / Mathf.Max(data.moveSpeed, 0.1f);
            float travelDeadline = Time.time + Mathf.Clamp(estimatedTravelTime * 2f + 1f, 2f, 12f);
            Vector2 lastProgressPosition = rb.position;
            float noProgressTimer = 0f;

            while (Vector2.Distance(rb.position, wanderTarget) > WanderArrivalDistance)
            {
                if (ShouldInterruptWander()) yield break;
                if (Time.time >= travelDeadline) break;

                MoveTowards(
                    wanderTarget,
                    data.moveSpeed,
                    CreatureMovementStyle.Wander,
                    WanderArrivalDistance,
                    data.arrivalSlowRadius);
                yield return FixedUpdateYield;

                if (Vector2.Distance(rb.position, lastProgressPosition) >= WanderProgressDistance)
                {
                    lastProgressPosition = rb.position;
                    noProgressTimer = 0f;
                }
                else
                {
                    noProgressTimer += Time.fixedDeltaTime;
                    if (noProgressTimer >= WanderNoProgressTimeout) break;
                }
            }

            StopMoving(CreatureMovementStyle.Wander);

            float minPause = Mathf.Max(0f, data.minWanderPause);
            float maxPause = Mathf.Max(minPause, data.maxWanderPause);
            yield return new WaitForSeconds(Random.Range(minPause, maxPause));
        }
    }

    // Override in a subclass to bail out of wandering early (e.g. a predator that just spotted the player).
    protected virtual bool ShouldInterruptWander() => false;

    // Harmless animals can override this to choose a more social wander destination.
    protected virtual Vector2 ChooseWanderTarget()
    {
        if (data == null) return spawnPoint;

        float radius = Mathf.Max(0.5f, data.wanderRadius);
        Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 towardHome = spawnPoint - current;
        float leash = towardHome.magnitude / radius;
        float homeWeight = Mathf.InverseLerp(0.45f, 0.95f, leash);

        Vector2 preferredHeading = wanderHeading;
        if (towardHome.sqrMagnitude > 0.0001f)
            preferredHeading = Vector2.Lerp(wanderHeading, towardHome.normalized, homeWeight).normalized;

        float turnRange = Mathf.Lerp(65f, 20f, homeWeight);
        wanderHeading = Rotate(preferredHeading, Random.Range(-turnRange, turnRange)).normalized;

        float step = Random.Range(radius * 0.3f, radius * 0.65f);
        Vector2 target = current + wanderHeading * step;
        Vector2 fromSpawn = target - spawnPoint;
        if (fromSpawn.magnitude > radius * 0.95f)
            target = spawnPoint + fromSpawn.normalized * radius * 0.95f;

        target = ClampToWorld(target, 0.85f);
        Vector2 actualHeading = target - current;
        if (actualHeading.sqrMagnitude > 0.0001f)
            wanderHeading = actualHeading.normalized;

        return target;
    }

    protected Vector2 ClampToWorld(Vector2 point, float padding)
    {
        WorldBoundary boundary = WorldBoundary.Instance;
        if (boundary == null) return point;

        Vector2 minimum = boundary.WorldMinimum + Vector2.one * Mathf.Max(0f, padding);
        Vector2 maximum = boundary.WorldMaximum - Vector2.one * Mathf.Max(0f, padding);
        if (minimum.x > maximum.x || minimum.y > maximum.y)
            return (boundary.WorldMinimum + boundary.WorldMaximum) * 0.5f;

        return new Vector2(
            Mathf.Clamp(point.x, minimum.x, maximum.x),
            Mathf.Clamp(point.y, minimum.y, maximum.y));
    }

    protected void StartWandering()
    {
        if (data == null || !IsReadyToMove || ShouldInterruptWander()) return;
        StopWandering();
        wanderRoutine = StartCoroutine(WanderRoutine());
    }

    protected void StopWandering()
    {
        if (wanderRoutine != null) StopCoroutine(wanderRoutine);
        wanderRoutine = null;
        StopMoving(CreatureMovementStyle.Wander);
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }
}
