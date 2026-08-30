using UnityEngine;
using System.Collections.Generic;

// Harmless animals wander, react to nearby creatures, seek food when hungry, and
// periodically visit water. Unrevealed imposters run this exact same behaviour, so
// following an animal to water is useful without becoming a reliable safety test.
public class HarmlessAnimal : Creature
{
    public enum State { Wander, SeekFood, SeekWater, Flee, Eat, Drink }
    public State CurrentState { get; private set; } = State.Wander;
    public bool IsAlive { get; private set; } = true;

    [Header("Resource Detection")]
    [Tooltip("How far to scan for food nodes.")]
    public float foodSearchRadius = 8f;
    [Tooltip("How far to scan for a stream or other water node.")]
    public float waterSearchRadius = 15f;

    [Header("Water Guidance")]
    [Tooltip("Random delay range before this animal's first trip to water.")]
    public Vector2 firstDrinkDelayRange = new Vector2(4f, 10f);
    [Tooltip("Random delay range between later trips to water.")]
    public Vector2 drinkIntervalRange = new Vector2(25f, 45f);
    [Tooltip("How long the animal visibly pauses at the water.")]
    [Min(0.1f)] public float drinkDuration = 2f;

    private Transform fleeingFrom;
    private Transform playerAvoidanceTarget;
    private readonly HashSet<Transform> predatorThreats = new HashSet<Transform>();
    private float fleeUntil;
    private float fleeNoiseOffset;

    private float hunger;
    private ResourceNode targetResource;
    private Collider2D targetResourceCollider;
    private Coroutine resourceSeekRoutine;
    private float resourceActionTimer;
    private float nextFoodSearchTime;
    private float nextWaterSearchTime;
    private float nextDrinkTime;

    private const float ResourceSearchRetryDelay = 2f;
    private const float FoodArrivalDistance = 0.5f;
    private const float WaterArrivalDistance = 0.6f;
    private const float ResourceNoProgressTimeout = 1.5f;
    private const float ResourceProgressDistance = 0.01f;

    private void OnEnable()
    {
        RebaseWanderOrigin();
        IsAlive = true;
        CurrentState = State.Wander;
        fleeingFrom = null;
        playerAvoidanceTarget = null;
        predatorThreats.Clear();
        targetResource = null;
        targetResourceCollider = null;
        resourceSeekRoutine = null;
        resourceActionTimer = 0f;
        nextFoodSearchTime = 0f;
        nextWaterSearchTime = 0f;
        ScheduleNextDrink(true);
        fleeNoiseOffset = Random.Range(0f, 1000f);

        if (data != null)
            hunger = data.friendlyMaxHunger;

        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();

        if (resourceSeekRoutine != null)
            StopCoroutine(resourceSeekRoutine);

        resourceSeekRoutine = null;
        fleeingFrom = null;
        playerAvoidanceTarget = null;
        predatorThreats.Clear();
        targetResource = null;
        targetResourceCollider = null;
        ReleaseMotor();
    }

    protected override void Update()
    {
        base.Update();

        if (!IsAlive || data == null) return;

        if (CurrentState != State.Eat)
            hunger -= data.friendlyHungerDrainPerSecond * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, data.friendlyMaxHunger);
        if (hunger <= 0f)
        {
            TryDie();
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

                if (CurrentState == State.Wander &&
                    Time.time >= nextDrinkTime &&
                    Time.time >= nextWaterSearchTime)
                {
                    BeginSeekingWater();
                }
                break;

            case State.SeekFood:
            case State.SeekWater:
                // MoveToResource owns movement while either state is active.
                break;

            case State.Eat:
                resourceActionTimer -= Time.deltaTime;
                if (resourceActionTimer <= 0f)
                    FinishEating();
                break;

            case State.Drink:
                resourceActionTimer -= Time.deltaTime;
                if (resourceActionTimer <= 0f)
                    FinishDrinking();
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
        if (!TryFindNearestResource(
            ResourceNode.ResourceType.Food,
            foodSearchRadius,
            out ResourceNode nearest,
            out Collider2D nearestCollider))
        {
            nextFoodSearchTime = Time.time + ResourceSearchRetryDelay;
            return;
        }

        BeginSeekingResource(nearest, nearestCollider, State.SeekFood);
    }

    private void BeginSeekingWater()
    {
        if (!TryFindNearestResource(
            ResourceNode.ResourceType.Water,
            waterSearchRadius,
            out ResourceNode nearest,
            out Collider2D nearestCollider))
        {
            nextDrinkTime = Time.time + ResourceSearchRetryDelay;
            nextWaterSearchTime = nextDrinkTime;
            return;
        }

        BeginSeekingResource(nearest, nearestCollider, State.SeekWater);
    }

    private void BeginSeekingResource(
        ResourceNode resource,
        Collider2D resourceCollider,
        State seekingState)
    {
        targetResource = resource;
        targetResourceCollider = resourceCollider;
        CurrentState = seekingState;
        StopWandering();
        resourceSeekRoutine = StartCoroutine(MoveToResource(seekingState));
    }

    private System.Collections.IEnumerator MoveToResource(State seekingState)
    {
        bool seekingWater = seekingState == State.SeekWater;
        float arrivalDistance = seekingWater ? WaterArrivalDistance : FoodArrivalDistance;
        CreatureMovementStyle movementStyle = seekingWater
            ? CreatureMovementStyle.SeekWater
            : CreatureMovementStyle.SeekFood;
        float startingDistance = Vector2.Distance(
            rb.position,
            GetClosestResourcePoint(rb.position));
        float estimatedTravelTime = startingDistance / Mathf.Max(data.moveSpeed, 0.1f);
        float travelDeadline = Time.time + Mathf.Clamp(estimatedTravelTime * 2f + 2f, 3f, 18f);
        Vector2 lastProgressPosition = rb.position;
        float noProgressTimer = 0f;

        while (targetResource != null && CurrentState == seekingState)
        {
            if (Time.time >= travelDeadline) break;

            Vector2 targetPosition = GetClosestResourcePoint(rb.position);
            float distance = Vector2.Distance(rb.position, targetPosition);
            if (distance <= arrivalDistance)
            {
                resourceSeekRoutine = null;
                if (seekingWater)
                    StartDrinking();
                else
                    StartEating();
                yield break;
            }

            MoveTowards(
                targetPosition,
                data.moveSpeed,
                movementStyle,
                Mathf.Max(0.05f, arrivalDistance - 0.05f),
                data.arrivalSlowRadius);
            yield return FixedUpdateYield;

            if (Vector2.Distance(rb.position, lastProgressPosition) >= ResourceProgressDistance)
            {
                lastProgressPosition = rb.position;
                noProgressTimer = 0f;
            }
            else
            {
                noProgressTimer += Time.fixedDeltaTime;
                if (noProgressTimer >= ResourceNoProgressTimeout) break;
            }
        }

        resourceSeekRoutine = null;
        targetResource = null;
        targetResourceCollider = null;

        if (CurrentState != seekingState) yield break;

        CurrentState = State.Wander;
        if (seekingWater)
        {
            nextWaterSearchTime = Time.time + ResourceSearchRetryDelay;
            nextDrinkTime = nextWaterSearchTime;
        }
        else
        {
            nextFoodSearchTime = Time.time + ResourceSearchRetryDelay;
        }
        StartWandering();
    }

    private bool TryFindNearestResource(
        ResourceNode.ResourceType resourceType,
        float searchRadius,
        out ResourceNode nearest,
        out Collider2D nearestCollider)
    {
        ResourceNode[] resources = FindObjectsByType<ResourceNode>();
        nearest = null;
        nearestCollider = null;
        float nearestSqrDistance = Mathf.Max(0f, searchRadius);
        nearestSqrDistance *= nearestSqrDistance;

        foreach (ResourceNode node in resources)
        {
            if (node == null || node.type != resourceType || !node.gameObject.activeInHierarchy)
                continue;

            Collider2D resourceCollider = node.GetComponent<Collider2D>();
            if (resourceCollider != null && !resourceCollider.enabled)
                continue;

            Vector2 targetPoint = resourceCollider != null
                ? resourceCollider.ClosestPoint(transform.position)
                : (Vector2)node.transform.position;
            float sqrDistance = (targetPoint - (Vector2)transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearestSqrDistance = sqrDistance;
            nearest = node;
            nearestCollider = resourceCollider;
        }

        return nearest != null;
    }

    private Vector2 GetClosestResourcePoint(Vector2 fromPosition)
    {
        if (targetResourceCollider != null)
            return targetResourceCollider.ClosestPoint(fromPosition);

        return targetResource != null
            ? (Vector2)targetResource.transform.position
            : fromPosition;
    }

    private void StartEating()
    {
        CurrentState = State.Eat;
        resourceActionTimer = data.eatDuration;
        StopMoving(CreatureMovementStyle.SeekFood);
    }

    private void FinishEating()
    {
        hunger = Mathf.Min(hunger + data.friendlyHungerOnEat, data.friendlyMaxHunger);
        ClearResourceTarget();
        CurrentState = State.Wander;
        StartWandering();
    }

    private void StartDrinking()
    {
        CurrentState = State.Drink;
        resourceActionTimer = Mathf.Max(0.1f, drinkDuration);
        StopMoving(CreatureMovementStyle.SeekWater);
        ParticleManager.Instance?.Play(
            ParticleManager.ParticleType.WaterSplash,
            GetClosestResourcePoint(rb.position));
    }

    private void FinishDrinking()
    {
        ClearResourceTarget();
        ScheduleNextDrink(false);
        CurrentState = State.Wander;
        StartWandering();
    }

    private void ScheduleNextDrink(bool firstVisit)
    {
        Vector2 range = firstVisit ? firstDrinkDelayRange : drinkIntervalRange;
        float minimum = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
        nextDrinkTime = Time.time + Random.Range(minimum, maximum);
    }

    private void CancelResourceActivity()
    {
        if (resourceSeekRoutine != null)
            StopCoroutine(resourceSeekRoutine);

        resourceSeekRoutine = null;
        ClearResourceTarget();
    }

    private void ClearResourceTarget()
    {
        targetResource = null;
        targetResourceCollider = null;
        resourceActionTimer = 0f;
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

        CancelResourceActivity();
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

    // Destroy is deferred until the end of the frame. Marking and disabling the
    // animal immediately makes a simultaneous second predator's kill attempt fail,
    // so death feedback and hunger credit can only happen once.
    public bool TryDie()
    {
        if (!IsAlive) return false;

        IsAlive = false;
        enabled = false;
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        Destroy(gameObject);
        return true;
    }

    public void Die() => TryDie();

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
