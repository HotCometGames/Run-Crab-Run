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
    private float fleeUntil;

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
            bool threatIsFarAway = Vector2.Distance(transform.position, fleeingFrom.position) > data.detectionRange * 1.5f;
            if (threatIsFarAway && Time.time >= fleeUntil)
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
            MoveInDirection(away, data.fleeSpeed);
        }
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        // Real predators AND revealed imposters both end up tagged "Predator" at runtime,
        // so this one check covers both cases automatically.
        if (other.CompareTag("Predator"))
        {
            StartFlee(other.transform, data.minFleeTime);
        }
        else if (other.CompareTag("Player") && Random.value < data.playerAvoidanceChance)
        {
            // This is deliberately subtle and probabilistic: a normal animal can shy
            // away, but an imposter still has no reliable visual tell before revealing.
            StartFlee(other.transform, data.playerAvoidanceTime);
        }
    }

    protected override void HandleDetectionExit(Collider2D other) { /* handled by distance check in Update */ }

    private void StartFlee(Transform threat, float minimumDuration)
    {
        fleeingFrom = threat;
        fleeUntil = Time.time + minimumDuration;
        CurrentState = State.Flee;
        StopWandering();
    }

    private void EndFlee()
    {
        fleeingFrom = null;
        fleeUntil = 0f;
        CurrentState = State.Wander;
        StartWandering();
    }

    public void Die()
    {
        Destroy(gameObject);
    }

    protected override bool ShouldInterruptWander() => CurrentState == State.Flee;

    protected override Vector2 ChooseWanderTarget()
    {
        // Sheep are gently social and deer occasionally move with a nearby animal.
        // This creates natural-looking groups without making every animal clump up.
        if (Random.value < data.flockTargetChance && data.flockSearchRadius > 0f)
        {
            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, data.flockSearchRadius);
            foreach (Collider2D candidate in nearby)
            {
                if (candidate.gameObject != gameObject && candidate.CompareTag("Animal"))
                {
                    Vector2 offset = Random.insideUnitCircle * data.flockArrivalRadius;
                    return (Vector2)candidate.transform.position + offset;
                }
            }
        }

        return base.ChooseWanderTarget();
    }
}
