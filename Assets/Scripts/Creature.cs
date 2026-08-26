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
    [Header("Data")]
    public CreatureData data;

    [Header("Detection")]
    [Tooltip("This creature's own detection radius child object. See DetectionZone.cs.")]
    public DetectionZone detectionZone;

    protected Rigidbody2D rb;
    protected Transform player;
    protected PlayerController playerController;

    protected Vector2 spawnPoint;
    private Vector2 wanderTarget;
    private Coroutine wanderRoutine;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPoint = transform.position;

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
        if (detectionZone != null)
        {
            detectionZone.OnEnter -= HandleDetectionEnter;
            detectionZone.OnExit -= HandleDetectionExit;
        }
    }

    // Subclasses decide what "noticing" something means (flee for HarmlessAnimal, chase for Predator).
    protected abstract void HandleDetectionEnter(Collider2D other);
    protected abstract void HandleDetectionExit(Collider2D other);

    protected float DistanceToPlayer() =>
        player == null ? Mathf.Infinity : Vector2.Distance(transform.position, player.position);

    protected bool PlayerIsHidden() =>
        playerController != null && playerController.IsHidden;

    protected void MoveTowards(Vector2 target, float speed)
    {
        Vector2 dir = (target - rb.position).normalized;
        rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
    }

    // Wanders to random points around this creature's spawn location, forever,
    // until a subclass state (Flee / Chase / Search) interrupts it.
    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            Vector2 offset = Random.insideUnitCircle * data.wanderRadius;
            wanderTarget = spawnPoint + offset;

            while (Vector2.Distance(rb.position, wanderTarget) > 0.15f)
            {
                if (ShouldInterruptWander()) yield break;
                MoveTowards(wanderTarget, data.moveSpeed);
                yield return new WaitForFixedUpdate();
            }

            yield return new WaitForSeconds(Random.Range(data.minWanderPause, data.maxWanderPause));
        }
    }

    // Override in a subclass to bail out of wandering early (e.g. a predator that just spotted the player).
    protected virtual bool ShouldInterruptWander() => false;

    protected void StartWandering()
    {
        StopWandering();
        wanderRoutine = StartCoroutine(WanderRoutine());
    }

    protected void StopWandering()
    {
        if (wanderRoutine != null) StopCoroutine(wanderRoutine);
        wanderRoutine = null;
    }
}
