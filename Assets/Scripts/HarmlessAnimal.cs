using UnityEngine;

// Design doc section 7 + 22: harmless animals (Deer, Sheep) just wander and flee
// from anything tagged "Predator" — they never attack the player.
// They also get hungry and must seek out food to stay alive.
//
// UNITY SETUP:
//   - Create a "Deer" and a "Sheep" prefab, each with this script attached
//     (on top of the standard Creature setup — Rigidbody2D, collider, DetectionZone child).
//   - Set the GameObject's Tag to "Animal" (create this tag if it doesn't exist).
//   - Assign a CreatureData asset (e.g. Data_Deer) with isPredator = false.
public class HarmlessAnimal : Creature
{
    public enum State { Wander, Flee, Eat }
    public State CurrentState { get; private set; } = State.Wander;

    [Header("Food Detection")]
    [Tooltip("How far to scan for food nodes.")]
    public float foodSearchRadius = 8f;

    private Transform fleeingFrom;
    private float fleeWobbleTimer;
    private float fleeWobbleSign;

    private float hunger;
    private float eatTimer;
    private ResourceNode targetFood;

    private void OnEnable()
    {
        CurrentState = State.Wander;
        if (data != null) hunger = data.friendlyMaxHunger;
        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();
        fleeingFrom = null;
        targetFood = null;
    }

    protected override void Update()
    {
        base.Update();

        if (data == null) return;

        // Drain hunger
        if (CurrentState != State.Eat)
            hunger -= data.friendlyHungerDrainPerSecond * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, data.friendlyMaxHunger);

        // Die from starvation
        if (hunger <= 0f)
        {
            Die();
            return;
        }

        switch (CurrentState)
        {
            case State.Wander:
                if (hunger <= data.friendlyMaxHunger * data.hungerSeekThreshold)
                    SeekFood();
                break;

            case State.Eat:
                eatTimer -= Time.deltaTime;
                if (eatTimer <= 0f)
                    FinishEating();
                break;

            case State.Flee:
                if (fleeingFrom != null &&
                    Vector2.Distance(transform.position, fleeingFrom.position) > data.detectionRange * 1.5f)
                {
                    EndFlee();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (CurrentState == State.Flee && fleeingFrom != null)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)fleeingFrom.position).normalized;

            fleeWobbleTimer -= Time.fixedDeltaTime;
            if (fleeWobbleTimer <= 0f)
            {
                fleeWobbleSign = -fleeWobbleSign;
                fleeWobbleTimer = Random.Range(0.15f, 0.4f);
            }

            Vector2 perp = new Vector2(-away.y, away.x) * fleeWobbleSign;
            Vector2 dir = (away + perp * 0.4f).normalized;

            rb.MovePosition(rb.position + dir * data.fleeSpeed * Time.fixedDeltaTime);
        }
        else if (CurrentState == State.Eat)
        {
            // Stay still while eating
        }
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (other.CompareTag("Predator"))
        {
            if (CurrentState == State.Eat)
                CancelEat();
            StartFlee(other.transform);
        }
    }

    protected override void HandleDetectionExit(Collider2D other) { /* handled by distance check in Update */ }

    private void SeekFood()
    {
        ResourceNode nearest = FindNearestFood();
        if (nearest == null) return;

        targetFood = nearest;
        StopWandering();
        CurrentState = State.Wander; // stay in Wander but we'll move toward food in a coroutine
        StartCoroutine(MoveToFood());
    }

    private System.Collections.IEnumerator MoveToFood()
    {
        while (targetFood != null && CurrentState == State.Wander)
        {
            float dist = Vector2.Distance(rb.position, targetFood.transform.position);
            if (dist < 0.5f)
            {
                StartEating();
                yield break;
            }
            MoveTowards(targetFood.transform.position, data.moveSpeed);
            yield return new WaitForFixedUpdate();
        }
    }

    private ResourceNode FindNearestFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, foodSearchRadius);
        ResourceNode nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            var node = hit.GetComponent<ResourceNode>();
            if (node != null && node.type == ResourceNode.ResourceType.Food)
            {
                float dist = Vector2.Distance(transform.position, node.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = node;
                }
            }
        }
        return nearest;
    }

    private void StartEating()
    {
        CurrentState = State.Eat;
        eatTimer = data.eatDuration;
        StopAllCoroutines();
    }

    private void FinishEating()
    {
        hunger = Mathf.Min(hunger + data.friendlyHungerOnEat, data.friendlyMaxHunger);
        targetFood = null;
        CurrentState = State.Wander;
        StartWandering();
    }

    private void CancelEat()
    {
        targetFood = null;
        eatTimer = 0f;
    }

    private void StartFlee(Transform threat)
    {
        fleeingFrom = threat;
        CurrentState = State.Flee;
        StopAllCoroutines();
        fleeWobbleTimer = 0f;
        fleeWobbleSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void EndFlee()
    {
        fleeingFrom = null;
        CurrentState = State.Wander;
        StartWandering();
    }

    public void Die()
    {
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        Destroy(gameObject);
    }

    protected override bool ShouldInterruptWander() => CurrentState == State.Flee;
}
