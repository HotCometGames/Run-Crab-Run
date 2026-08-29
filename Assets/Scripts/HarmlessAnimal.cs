using UnityEngine;
using System.Collections.Generic;

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
    private Transform playerAvoidanceTarget;
    private readonly HashSet<Transform> predatorThreats = new HashSet<Transform>();
    private float fleeUntil;
    private float fleeNoiseOffset;

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
        playerAvoidanceTarget = null;
        predatorThreats.Clear();
        targetFood = null;
        foodSeekRoutine = null;
        nextFoodSearchTime = 0f;
        fleeNoiseOffset = Random.Range(0f, 1000f);

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
        playerAvoidanceTarget = null;
        predatorThreats.Clear();
        targetFood = null;
        ReleaseMotor();
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
                fleeingFrom = SelectPriorityThreat();
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

        if (CurrentState == State.Flee)
        {
            fleeingFrom = SelectPriorityThreat();
            if (fleeingFrom == null)
            {
                StopMoving(CreatureMovementStyle.Flee);
                return;
            }

            Vector2 away = (Vector2)transform.position - (Vector2)fleeingFrom.position;
            if (away.sqrMagnitude < 0.0001f)
                away = Random.insideUnitCircle;

            away.Normalize();
            float noise = Mathf.PerlinNoise(
                fleeNoiseOffset,
                Time.time * data.fleeNoiseFrequency) * 2f - 1f;
            Vector2 direction = Rotate(away, noise * data.fleeNoiseAngle);
            MoveInDirection(direction, data.fleeSpeed, CreatureMovementStyle.Flee);
        }
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (!enabled || data == null) return;

        if (other.CompareTag("Predator"))
        {
            ReactToThreat(other.transform);
        }
        else if (other.CompareTag("Player") && Random.value < data.playerAvoidanceChance)
        {
            // Keep this subtle and probabilistic so it cannot identify an imposter.
            playerAvoidanceTarget = other.transform;
            StartFlee(data.playerAvoidanceTime);
        }
    }

    protected override void HandleDetectionExit(Collider2D other)
    {
        // Keep departed threats until the minimum flee duration and safe-distance
        // checks pass. The nearest tracked predator remains the priority if several
        // detection zones overlap.
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

            MoveTowards(
                targetFood.transform.position,
                data.moveSpeed,
                CreatureMovementStyle.SeekFood,
                0.45f,
                data.arrivalSlowRadius);
            yield return FixedUpdateYield;
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
        StopMoving(CreatureMovementStyle.SeekFood);
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

    // ImposterComponent uses this when its tag changes while already overlapping an
    // animal's trigger. Changing a tag alone does not produce a new trigger-enter event.
    public void ReactToThreat(Transform threat)
    {
        if (!enabled || data == null || threat == null || threat == transform) return;
        predatorThreats.Add(threat);
        StartFlee(data.minFleeTime);
    }

    private void StartFlee(float minimumDuration)
    {
        Transform threat = SelectPriorityThreat();
        if (threat == null) return;

        CancelFoodActivity();
        fleeingFrom = threat;
        fleeUntil = Mathf.Max(fleeUntil, Time.time + Mathf.Max(0f, minimumDuration));
        CurrentState = State.Flee;
        StopWandering();
    }

    private void EndFlee()
    {
        fleeingFrom = null;
        playerAvoidanceTarget = null;
        predatorThreats.Clear();
        fleeUntil = 0f;
        StopMoving(CreatureMovementStyle.Flee);
        CurrentState = State.Wander;
        StartWandering();
    }

    private Transform SelectPriorityThreat()
    {
        predatorThreats.RemoveWhere(threat =>
            threat == null || !threat.gameObject.activeInHierarchy || !threat.CompareTag("Predator"));

        Transform nearestPredator = null;
        float nearestSqrDistance = Mathf.Infinity;
        foreach (Transform threat in predatorThreats)
        {
            float sqrDistance = ((Vector2)(threat.position - transform.position)).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestPredator = threat;
            }
        }

        return nearestPredator != null ? nearestPredator : playerAvoidanceTarget;
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
                    return ClampToWorld((Vector2)candidate.transform.position + offset, 0.85f);
                }
            }
        }

        return base.ChooseWanderTarget();
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }
}
