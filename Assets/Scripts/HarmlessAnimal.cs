using UnityEngine;

// Harmless animals wander, react to nearby creatures, and seek food when hungry.
// They never attack the player. Real predators and revealed imposters both use the
// Predator tag, so the same flee path handles either threat.
public class HarmlessAnimal : Creature
{
    public enum State { Wander, SeekFood, Flee, Eat }
    public State CurrentState { get; private set; } = State.Wander;

    [Header("Food Detection")]
    [Tooltip("How far to scan for food nodes.")]
    public float foodSearchRadius = 8f;

    private Transform fleeingFrom;
    private float fleeUntil;
    private float fleeWobbleTimer;
    private float fleeWobbleSign;

    private float hunger;
    private float eatTimer;
    private ResourceNode targetFood;
    private Coroutine foodSeekRoutine;
    private float nextFoodSearchTime;

    private const float FoodSearchRetryDelay = 2f;

    private void OnEnable()
    {
        CurrentState = State.Wander;
        fleeingFrom = null;
        targetFood = null;
        foodSeekRoutine = null;
        nextFoodSearchTime = 0f;

        if (data != null)
            hunger = data.friendlyMaxHunger;

        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();

        if (foodSeekRoutine != null)
            StopCoroutine(foodSeekRoutine);

        foodSeekRoutine = null;
        fleeingFrom = null;
        targetFood = null;
    }

    protected override void Update()
    {
        base.Update();

        if (data == null) return;

        if (CurrentState != State.Eat)
            hunger -= data.friendlyHungerDrainPerSecond * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, data.friendlyMaxHunger);
        if (hunger <= 0f)
        {
            Die();
            return;
        }

        if (!IsReadyToMove) return;

        switch (CurrentState)
        {
            case State.Wander:
                if (hunger <= data.friendlyMaxHunger * data.hungerSeekThreshold &&
                    Time.time >= nextFoodSearchTime)
                {
                    BeginSeekingFood();
                }
                break;

            case State.SeekFood:
                // MoveToFood owns movement while this state is active.
                break;

            case State.Eat:
                eatTimer -= Time.deltaTime;
                if (eatTimer <= 0f)
                    FinishEating();
                break;

            case State.Flee:
                bool threatMissing = fleeingFrom == null;
                bool safelyAway = !threatMissing &&
                    Vector2.Distance(transform.position, fleeingFrom.position) > data.detectionRange * 1.5f;

                if (threatMissing || (safelyAway && Time.time >= fleeUntil))
                    EndFlee();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (!IsReadyToMove || data == null) return;

        if (CurrentState == State.Flee && fleeingFrom != null)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)fleeingFrom.position).normalized;

            fleeWobbleTimer -= Time.fixedDeltaTime;
            if (fleeWobbleTimer <= 0f)
            {
                fleeWobbleSign = -fleeWobbleSign;
                fleeWobbleTimer = Random.Range(0.15f, 0.4f);
            }

            Vector2 perpendicular = new Vector2(-away.y, away.x) * fleeWobbleSign;
            Vector2 direction = (away + perpendicular * 0.4f).normalized;
            MoveInDirection(direction, data.fleeSpeed);
        }
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (data == null) return;

        if (other.CompareTag("Predator"))
        {
            StartFlee(other.transform, data.minFleeTime);
        }
        else if (other.CompareTag("Player") && Random.value < data.playerAvoidanceChance)
        {
            // Keep this subtle and probabilistic so it cannot identify an imposter.
            StartFlee(other.transform, data.playerAvoidanceTime);
        }
    }

    protected override void HandleDetectionExit(Collider2D other)
    {
        // Distance and the minimum flee duration are evaluated in Update.
    }

    private void BeginSeekingFood()
    {
        ResourceNode nearest = FindNearestFood();
        if (nearest == null)
        {
            nextFoodSearchTime = Time.time + FoodSearchRetryDelay;
            return;
        }

        targetFood = nearest;
        CurrentState = State.SeekFood;
        StopWandering();
        foodSeekRoutine = StartCoroutine(MoveToFood());
    }

    private System.Collections.IEnumerator MoveToFood()
    {
        while (targetFood != null && CurrentState == State.SeekFood)
        {
            float distance = Vector2.Distance(rb.position, targetFood.transform.position);
            if (distance < 0.5f)
            {
                foodSeekRoutine = null;
                StartEating();
                yield break;
            }

            MoveTowards(targetFood.transform.position, data.moveSpeed);
            yield return new WaitForFixedUpdate();
        }

        foodSeekRoutine = null;
        targetFood = null;

        if (CurrentState == State.SeekFood)
        {
            CurrentState = State.Wander;
            nextFoodSearchTime = Time.time + FoodSearchRetryDelay;
            StartWandering();
        }
    }

    private ResourceNode FindNearestFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, foodSearchRadius);
        ResourceNode nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            ResourceNode node = hit.GetComponent<ResourceNode>();
            if (node == null || node.type != ResourceNode.ResourceType.Food) continue;

            float distance = Vector2.Distance(transform.position, node.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = node;
            }
        }

        return nearest;
    }

    private void StartEating()
    {
        CurrentState = State.Eat;
        eatTimer = data.eatDuration;
    }

    private void FinishEating()
    {
        hunger = Mathf.Min(hunger + data.friendlyHungerOnEat, data.friendlyMaxHunger);
        targetFood = null;
        CurrentState = State.Wander;
        StartWandering();
    }

    private void CancelFoodActivity()
    {
        if (foodSeekRoutine != null)
            StopCoroutine(foodSeekRoutine);

        foodSeekRoutine = null;
        targetFood = null;
        eatTimer = 0f;
    }

    private void StartFlee(Transform threat, float minimumDuration)
    {
        if (threat == null) return;

        CancelFoodActivity();
        fleeingFrom = threat;
        fleeUntil = Time.time + Mathf.Max(0f, minimumDuration);
        CurrentState = State.Flee;
        StopWandering();
        fleeWobbleTimer = 0f;
        fleeWobbleSign = Random.value < 0.5f ? -1f : 1f;
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
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        Destroy(gameObject);
    }

    protected override bool ShouldInterruptWander() => CurrentState != State.Wander;

    protected override Vector2 ChooseWanderTarget()
    {
        // Light, probabilistic grouping adds life without making ordinary movement
        // into a reliable tell for the hidden-predator mechanic.
        if (data != null && Random.value < data.flockTargetChance && data.flockSearchRadius > 0f)
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
