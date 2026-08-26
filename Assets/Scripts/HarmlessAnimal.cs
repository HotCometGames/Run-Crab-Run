using UnityEngine;

// Design doc section 7 + 22: harmless animals (Deer, Sheep) just wander and flee
// from anything tagged "Predator" — they never attack the player.
//
// UNITY SETUP:
//   - Create a "Deer" and a "Sheep" prefab, each with this script attached
//     (on top of the standard Creature setup — Rigidbody2D, collider, DetectionZone child).
//   - Set the GameObject's Tag to "Animal" (create this tag if it doesn't exist).
//   - Assign a CreatureData asset (e.g. Data_Deer) with isPredator = false.
public class HarmlessAnimal : Creature
{
    public enum State { Wander, Flee }
    public State CurrentState { get; private set; } = State.Wander;

    private Transform fleeingFrom;

    private void OnEnable()
    {
        CurrentState = State.Wander;
        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();
        fleeingFrom = null;
    }

    private void Update()
    {
        // Stop fleeing once the threat is far enough away, then resume wandering.
        if (CurrentState == State.Flee && fleeingFrom != null)
        {
            if (Vector2.Distance(transform.position, fleeingFrom.position) > data.detectionRange * 1.5f)
            {
                EndFlee();
            }
        }
    }

    private void FixedUpdate()
    {
        if (CurrentState == State.Flee && fleeingFrom != null)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)fleeingFrom.position).normalized;
            rb.MovePosition(rb.position + away * data.fleeSpeed * Time.fixedDeltaTime);
        }
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        // Real predators AND revealed imposters both end up tagged "Predator" at runtime,
        // so this one check covers both cases automatically.
        if (other.CompareTag("Predator"))
        {
            StartFlee(other.transform);
        }
    }

    protected override void HandleDetectionExit(Collider2D other) { /* handled by distance check in Update */ }

    private void StartFlee(Transform threat)
    {
        fleeingFrom = threat;
        CurrentState = State.Flee;
        StopWandering();
    }

    private void EndFlee()
    {
        fleeingFrom = null;
        CurrentState = State.Wander;
        StartWandering();
    }

    protected override bool ShouldInterruptWander() => CurrentState == State.Flee;
}
