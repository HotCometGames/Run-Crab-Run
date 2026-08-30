using System.Collections.Generic;
using UnityEngine;

// Design doc section 10, 11, 22: shared brain for every real predator (Wolf, Fox, Bird)
// AND for the predator hidden inside an Imposter (see ImposterComponent.cs).
//
// IMPORTANT: this component can exist in a DISABLED state on an Imposter prefab
// (see ImposterComponent). OnEnable is used instead of Awake/Start for anything that
// should only happen once this predator is actually "active" — that's what lets an
// imposter's hidden Wolf component sit dormant until the reveal.
//
// Predators may opportunistically hunt harmless animals tagged "Animal". Optional
// invisible hunger can also be enabled per CreatureData asset, but prey hunting is
// deliberately independent from starvation so a zero drain does not disable the AI.
//
// UNITY SETUP for a standalone predator (e.g. Wolf prefab):
//   - Standard Creature setup (Rigidbody2D, physical collider, DetectionZone child).
//   - Add the concrete subclass (Wolf.cs or Fox.cs), NOT this class directly (it's abstract).
//   - Assign a CreatureData asset with isPredator = true.
//   - You do NOT need to manually set the Tag — OnEnable sets it to "Predator" automatically.
public abstract class Predator : Creature
{
    public enum State { Wander, Chase, ChasePrey, Search }
    public State CurrentState { get; protected set; } = State.Wander;
    public bool CanCurrentlySeePlayer => data != null && CanSeePlayer();

    protected Vector2 lastKnownPlayerPos;
    protected float searchTimer;
    public float attackTimer = 0;

    [Header("Hunger (invisible)")]
    public float hunger;

    public HarmlessAnimal CurrentPrey { get; private set; }

    private readonly HashSet<HarmlessAnimal> nearbyPrey = new HashSet<HarmlessAnimal>();
    private Vector2 searchTarget;
    private float searchPauseUntil;
    private float playerChaseElapsed;
    private float playerOutsideDetectionTimer;
    private float playerChaseStartSpeed;
    private float playerChaseAcceleration;
    private float playerChaseTopSpeed;
    private float preyOutsideDetectionTimer;
    private float nextPreyHuntTime;
    private float nextPreyScanTime;
    private const string TagAnimal = "Animal";
    private const float MovingTargetStopRangeMultiplier = 0.55f;
    private const float MovingTargetSlowRangeMultiplier = 0.9f;
    private const float ContactClosingSpeed = 0.55f;
    private const float MaximumPursuitLeadTime = 0.3f;
    private const float PreyScanInterval = 0.4f;

    protected float PlayerDetectionRange =>
        data != null ? data.EffectivePlayerDetectionRange : 0f;

    private void OnEnable()
    {
        RebaseWanderOrigin();
        gameObject.tag = "Predator";
        CurrentState = State.Wander;
        CurrentPrey = null;
        attackTimer = 0f;
        searchTimer = 0f;
        searchPauseUntil = 0f;
        nearbyPrey.Clear();
        ResetPlayerChaseRamp();
        preyOutsideDetectionTimer = 0f;
        nextPreyHuntTime = 0f;
        nextPreyScanTime = 0f;
        ResetSubclassStateOnActivation();

        if (data == null)
        {
            hunger = 0f;
            StopWandering();
            return;
        }

        hunger = data.maxHunger;
        StartWandering();
    }

    protected virtual void ResetSubclassStateOnActivation() { }

    private void OnDisable()
    {
        StopWandering();
        CurrentPrey = null;
        nearbyPrey.Clear();
        ResetPlayerChaseRamp();
        preyOutsideDetectionTimer = 0f;
        ReleaseMotor();
    }

    protected virtual void FixedUpdate()
    {
        if (data == null) return;

        if (attackTimer > 0f)
            attackTimer -= Time.fixedDeltaTime;

        if (data.maxHunger > 0f && data.hungerDrainPerSecond > 0f)
        {
            hunger -= data.hungerDrainPerSecond * Time.fixedDeltaTime;
            if (hunger <= 0f)
            {
                hunger = 0f;
                Die();
                return;
            }
        }

        if (!IsReadyToMove) return;

        PrunePreyCandidates();

        if (CurrentState == State.Chase)
        {
            playerChaseElapsed += Time.fixedDeltaTime;
            if (DistanceToPlayer() > PlayerDetectionRange)
                playerOutsideDetectionTimer += Time.fixedDeltaTime;
            else
                playerOutsideDetectionTimer = 0f;
        }

        // Poll every non-player state because visibility and Fox's reacquire cooldown
        // can change while the player remains inside the same trigger. The player is
        // always a higher-priority target than prey once they are actually visible.
        if (CurrentState != State.Chase && CanSeePlayer())
        {
            BeginChase();
        }

        if (CurrentState == State.Wander)
            TryBeginPreyHunt();

        switch (CurrentState)
        {
            case State.Chase: DoChase(); break;
            case State.ChasePrey: DoChasePrey(); break;
            case State.Search: DoSearch(); break;
        }
    }

    protected virtual void DoChase()
    {
        if (player == null)
        {
            StopMoving(CreatureMovementStyle.Chase);
            return;
        }

        if (PlayerIsHidden())
        {
            BeginSearch();
            return;
        }

        float distance = DistanceToPlayer();
        lastKnownPlayerPos = player.position;

        if (distance <= data.attackRange)
        {
            StopMoving(CreatureMovementStyle.Chase);
            Attack();
        }
        else if (ShouldAbandonPlayerChase(distance))
        {
            BeginSearch();
        }
        else
        {
            PursuePlayer();
        }
    }

    private void DoChasePrey()
    {
        if (!data.huntsPrey || !TryGetActivePrey(out HarmlessAnimal animal))
        {
            EndChasePrey(false);
            return;
        }

        float distance = Vector2.Distance(rb.position, animal.transform.position);
        if (distance > data.detectionRange)
            preyOutsideDetectionTimer += Time.fixedDeltaTime;
        else
            preyOutsideDetectionTimer = 0f;

        float pursuitRange = data.detectionRange *
            Mathf.Max(1f, data.preyPursuitRangeMultiplier);
        if (distance > pursuitRange ||
            (distance > data.detectionRange && preyOutsideDetectionTimer >= data.loseInterestTime))
        {
            EndChasePrey(false);
            return;
        }

        if (distance <= data.attackRange)
        {
            StopMoving(CreatureMovementStyle.Chase);
            EatPrey();
        }
        else
        {
            PursuePrey(animal.transform);
        }
    }

    private void PursuePlayer()
    {
        float rampedSpeed = Mathf.Min(
            playerChaseTopSpeed,
            playerChaseStartSpeed + playerChaseAcceleration * playerChaseElapsed);

        MoveTowards(
            player.position,
            CalculateMovingTargetApproachSpeed(player, rampedSpeed),
            CreatureMovementStyle.Chase,
            data.attackRange * MovingTargetStopRangeMultiplier,
            data.attackRange * MovingTargetSlowRangeMultiplier,
            player);
    }

    // Fleeing prey should not use ordinary arrival slowdown outside attack range:
    // a wolf easing down toward a fleeing deer's speed can otherwise settle into an
    // endless standoff. A short velocity lead creates a smoother intercept while the
    // motor still owns acceleration, turning, obstacle avoidance, and braking.
    private void PursuePrey(Transform target)
    {
        if (target == null) return;

        Vector2 targetPosition = target.position;
        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && targetBody.linearVelocity.sqrMagnitude > 0.01f)
        {
            float distance = Vector2.Distance(rb.position, targetPosition);
            float closingScale = Mathf.Max(
                0.1f,
                data.chaseSpeed + targetBody.linearVelocity.magnitude);
            float leadTime = Mathf.Min(
                MaximumPursuitLeadTime,
                distance / closingScale * 0.45f);
            Vector2 lead = Vector2.ClampMagnitude(
                targetBody.linearVelocity * leadTime,
                Mathf.Max(0.25f, data.attackRange));
            targetPosition += lead;
        }

        MoveTowards(
            ClampToWorld(targetPosition, 0.85f),
            CalculateMovingTargetApproachSpeed(target, data.chaseSpeed),
            CreatureMovementStyle.Chase,
            data.attackRange * MovingTargetStopRangeMultiplier,
            data.attackRange * MovingTargetSlowRangeMultiplier,
            target);
    }

    // Cap approach speed using the distance needed to brake before contact. The
    // target's outward speed plus a small closing margin is the terminal speed, so
    // a moving crab or deer cannot create an arrival-curve standoff just outside
    // attack range. Stationary targets still get a controlled, natural approach.
    protected float CalculateMovingTargetApproachSpeed(Transform target, float maximumSpeed)
    {
        if (target == null) return 0f;

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        float outwardSpeed = 0f;

        Vector2 targetVelocity = Vector2.zero;
        if (target == player && playerController != null)
            targetVelocity = playerController.MovementVelocity;
        else
            targetVelocity = target.GetComponent<Rigidbody2D>()?.linearVelocity ?? Vector2.zero;

        if (targetVelocity.sqrMagnitude > 0.0001f && distance > 0.001f)
        {
            outwardSpeed = Mathf.Max(
                0f,
                Vector2.Dot(targetVelocity, toTarget / distance));
        }

        float brakingDistance = Mathf.Max(0f, distance - data.attackRange);
        // Plan with the base deceleration. Chase locomotion brakes a little harder,
        // leaving enough safety margin for the one-step delay in physics contacts.
        float chaseDeceleration = Mathf.Max(0.1f, data.deceleration);
        float closingSpeed = Mathf.Sqrt(
            ContactClosingSpeed * ContactClosingSpeed +
            2f * chaseDeceleration * brakingDistance);

        return Mathf.Min(Mathf.Max(0f, maximumSpeed), outwardSpeed + closingSpeed);
    }

    protected virtual void DoSearch()
    {
        searchTimer -= Time.fixedDeltaTime;

        if (!PlayerIsHidden() && DistanceToPlayer() <= PlayerDetectionRange)
        {
            BeginChase();
            if (CurrentState == State.Chase) return;
        }

        if (searchTimer <= 0f)
        {
            EndSearch();
            return;
        }

        if (Vector2.Distance(rb.position, searchTarget) <= 0.25f)
        {
            StopMoving(CreatureMovementStyle.Search);

            if (searchPauseUntil <= 0f)
            {
                searchPauseUntil = Time.time + Random.Range(0.18f, 0.5f);
            }
            else if (Time.time >= searchPauseUntil)
            {
                float searchRadius = Mathf.Clamp(PlayerDetectionRange * 0.3f, 0.8f, 2.4f);
                searchTarget = ClampToWorld(
                    lastKnownPlayerPos + Random.insideUnitCircle * searchRadius,
                    0.85f);
                searchPauseUntil = 0f;
            }
        }
        else
        {
            searchPauseUntil = 0f;
            MoveTowards(
                searchTarget,
                data.moveSpeed,
                CreatureMovementStyle.Search,
                0.22f,
                data.arrivalSlowRadius);
        }
    }

    protected virtual void BeginChase()
    {
        if (data == null || player == null) return;

        CurrentState = State.Chase;
        CurrentPrey = null;
        preyOutsideDetectionTimer = 0f;
        lastKnownPlayerPos = player.position;
        ConfigurePlayerChaseRamp();
        StopWandering();
    }

    private void BeginChasePrey(HarmlessAnimal prey)
    {
        if (!IsValidPrey(prey)) return;

        CurrentState = State.ChasePrey;
        CurrentPrey = prey;
        preyOutsideDetectionTimer = 0f;
        StopWandering();
    }

    private void EndChasePrey(bool caughtPrey)
    {
        HarmlessAnimal finishedPrey = CurrentPrey;
        CurrentPrey = null;
        playerOutsideDetectionTimer = 0f;
        preyOutsideDetectionTimer = 0f;
        if (finishedPrey != null)
            nearbyPrey.Remove(finishedPrey);

        if (caughtPrey)
            nextPreyHuntTime = Time.time + Mathf.Max(0f, data.preyHuntCooldown);

        StopMoving(CreatureMovementStyle.Chase);
        CurrentState = State.Wander;
        if (!TryBeginPreyHunt())
            StartWandering();
    }

    protected virtual void BeginSearch()
    {
        CurrentState = State.Search;
        CurrentPrey = null;
        playerOutsideDetectionTimer = 0f;
        preyOutsideDetectionTimer = 0f;
        searchTimer = data.loseInterestTime;
        searchTarget = ClampToWorld(lastKnownPlayerPos, 0.85f);
        searchPauseUntil = 0f;
        StopWandering();
    }

    protected virtual void EndSearch()
    {
        StopMoving(CreatureMovementStyle.Search);
        CurrentState = State.Wander;
        if (!TryBeginPreyHunt())
            StartWandering();
    }

    // PlayerSurvival determines the actual health pool; the default game setup uses
    // three hits, which leaves room for the damage-feedback effect.
    protected virtual void Attack()
    {
        if (attackTimer > 0f) return;
        attackTimer = data.attackCooldown;
        player.GetComponent<PlayerSurvival>()?.TakeDamage(1f);
    }

    private void EatPrey()
    {
        if (attackTimer > 0f) return;

        if (!TryGetActivePrey(out HarmlessAnimal animal))
        {
            EndChasePrey(false);
            return;
        }

        if (!animal.TryDie())
        {
            EndChasePrey(false);
            return;
        }

        attackTimer = data.attackCooldown;
        hunger = Mathf.Min(hunger + data.hungerOnEat, data.maxHunger);
        EndChasePrey(true);
    }

    private bool TryGetActivePrey(out HarmlessAnimal animal)
    {
        animal = CurrentPrey;
        return IsValidPrey(animal);
    }

    private bool TryBeginPreyHunt()
    {
        if (data == null || !data.huntsPrey || CurrentState != State.Wander ||
            Time.time < nextPreyHuntTime)
        {
            return false;
        }

        RefreshPreyCandidates();

        HarmlessAnimal nearest = null;
        float nearestSqrDistance = data.detectionRange * data.detectionRange;

        foreach (HarmlessAnimal candidate in nearbyPrey)
        {
            if (!IsValidPrey(candidate)) continue;

            float sqrDistance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
            if (sqrDistance > nearestSqrDistance) continue;

            if (nearest == null || sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }
        }

        if (nearest == null) return false;
        BeginChasePrey(nearest);
        return CurrentState == State.ChasePrey;
    }

    // Imposters use this during their calm window so EndSearch cannot immediately
    // replace player pursuit with an animal hunt and postpone the disguise forever.
    public void DelayPreyHunting(float duration)
    {
        nextPreyHuntTime = Mathf.Max(
            nextPreyHuntTime,
            Time.time + Mathf.Max(0f, duration));
    }

    private void RefreshPreyCandidates()
    {
        if (Time.time < nextPreyScanTime) return;
        nextPreyScanTime = Time.time + PreyScanInterval;

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(rb.position, data.detectionRange);
        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == null || overlap.isTrigger) continue;

            HarmlessAnimal animal = overlap.GetComponentInParent<HarmlessAnimal>();
            if (IsValidPrey(animal))
                nearbyPrey.Add(animal);
        }
    }

    private void PrunePreyCandidates()
    {
        nearbyPrey.RemoveWhere(candidate => !IsValidPrey(candidate));
    }

    private bool IsValidPrey(HarmlessAnimal animal)
    {
        return animal != null && animal.gameObject != gameObject && animal.IsAlive &&
            animal.isActiveAndEnabled && animal.CompareTag(TagAnimal);
    }

    private bool CanSeePlayer()
    {
        return player != null && !PlayerIsHidden() && DistanceToPlayer() <= PlayerDetectionRange;
    }

    protected bool ShouldAbandonPlayerChase(float distance)
    {
        if (distance <= PlayerDetectionRange) return false;
        return playerOutsideDetectionTimer >= Mathf.Max(0f, data.playerPursuitGraceTime);
    }

    protected float CurrentPlayerChaseSpeed => Mathf.Min(
        playerChaseTopSpeed,
        playerChaseStartSpeed + playerChaseAcceleration * playerChaseElapsed);

    private void ConfigurePlayerChaseRamp()
    {
        ResetPlayerChaseRamp();

        if (data.playerCatchUpTime <= 0f || rb == null || player == null)
        {
            playerChaseStartSpeed = data.chaseSpeed;
            playerChaseTopSpeed = data.chaseSpeed;
            return;
        }

        Vector2 towardPlayer = (Vector2)player.position - rb.position;
        float distance = towardPlayer.magnitude;
        float forwardSpeed = 0f;
        if (distance > 0.001f)
        {
            forwardSpeed = Mathf.Max(
                0f,
                Vector2.Dot(rb.linearVelocity, towardPlayer / distance));
        }

        float playerSprintSpeed = playerController != null
            ? playerController.moveSpeed * Mathf.Max(1f, playerController.sprintMultiplier)
            : data.chaseSpeed;
        float catchUpTime = Mathf.Max(0.1f, data.playerCatchUpTime);
        float gapToClose = Mathf.Max(0f, distance - data.attackRange);

        // For a linear speed ramp, this acceleration closes the initial gap at the
        // configured time even when the crab immediately sprints straight away.
        float requiredAcceleration = 2f *
            (gapToClose + (playerSprintSpeed - forwardSpeed) * catchUpTime) /
            (catchUpTime * catchUpTime);

        playerChaseStartSpeed = forwardSpeed;
        playerChaseAcceleration = Mathf.Max(0f, requiredAcceleration);
        float requiredTopSpeed = forwardSpeed + playerChaseAcceleration * catchUpTime;
        float safetyCap = Mathf.Max(playerSprintSpeed + 0.1f, data.playerChaseSpeedCap);
        playerChaseTopSpeed = Mathf.Min(requiredTopSpeed, safetyCap);
    }

    private void ResetPlayerChaseRamp()
    {
        playerChaseElapsed = 0f;
        playerOutsideDetectionTimer = 0f;
        playerChaseStartSpeed = 0f;
        playerChaseAcceleration = 0f;
        playerChaseTopSpeed = 0f;
    }

    private void Die()
    {
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        Destroy(gameObject);
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (!enabled || data == null) return;

        // Only the animal's solid root collider represents a prey sighting. Ignoring
        // its own trigger children avoids duplicate enter/exit events at inflated
        // trigger-radius distances.
        if (!other.isTrigger)
        {
            HarmlessAnimal animal = other.GetComponentInParent<HarmlessAnimal>();
            if (IsValidPrey(animal))
            {
                nearbyPrey.Add(animal);
            }
        }

        // Trigger overlap includes the player's body radius. The center-distance
        // check avoids entering Chase at the trigger fringe only to drop it again.
        if (other.CompareTag("Player") && CurrentState != State.Chase && CanSeePlayer())
        {
            BeginChase();
            return;
        }

        // Selection is deferred to FixedUpdate so all trigger callbacks from this
        // physics step contribute candidates before the nearest one is chosen.
    }

    protected override void HandleDetectionExit(Collider2D other)
    {
        if (other.isTrigger) return;

        HarmlessAnimal animal = other.GetComponentInParent<HarmlessAnimal>();
        if (animal != null)
            nearbyPrey.Remove(animal);
    }

    protected override bool ShouldInterruptWander() => CurrentState != State.Wander;

    // Called by ImposterComponent the instant a disguise is revealed, to skip straight
    // to chasing instead of waiting for this predator's own DetectionZone to fire.
    public void ForceBeginChase()
    {
        if (data == null || player == null) return;
        SkipSpawnDelay();
        motor?.RedirectForReveal(this, data, player.position);
        BeginChase();
    }
}
