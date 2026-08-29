using UnityEngine;

// Design doc section 10, 11, 22: shared brain for every real predator (Wolf, Fox, Bird)
// AND for the predator hidden inside an Imposter (see ImposterComponent.cs).
//
// IMPORTANT: this component can exist in a DISABLED state on an Imposter prefab
// (see ImposterComponent). OnEnable is used instead of Awake/Start for anything that
// should only happen once this predator is actually "active" — that's what lets an
// imposter's hidden Wolf component sit dormant until the reveal.
//
// Predators have an invisible hunger bar that drains over time. If it hits 0, the
// predator dies. Eating a prey animal (tagged "Animal") restores hunger.
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

    protected Vector2 lastKnownPlayerPos;
    protected float searchTimer;
    public float attackTimer = 0;

    [Header("Hunger (invisible)")]
    public float hunger;

    private Transform chasingPrey;
    private const string TagAnimal = "Animal";

    private void OnEnable()
    {
        gameObject.tag = "Predator";
        CurrentState = State.Wander;

        if (data == null)
        {
            hunger = 0f;
            StopWandering();
            return;
        }

        hunger = data.maxHunger;
        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();
        chasingPrey = null;
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

        // A player can leave a hiding spot while already inside the trigger, which
        // does not fire OnTriggerEnter2D again. Polling only this single distance while
        // wandering closes that blind spot without adding a world scan.
        if (CurrentState == State.Wander && player != null && !PlayerIsHidden() &&
            DistanceToPlayer() <= data.detectionRange)
        {
            BeginChase();
        }

        switch (CurrentState)
        {
            case State.Chase: DoChase(); break;
            case State.ChasePrey: DoChasePrey(); break;
            case State.Search: DoSearch(); break;
        }
    }

    protected virtual void DoChase()
    {
        if (player == null) return;

        if (PlayerIsHidden())
        {
            BeginSearch();
            return;
        }

        lastKnownPlayerPos = player.position;
        MoveTowards(player.position, data.chaseSpeed);

        if (DistanceToPlayer() <= data.attackRange)
        {
            Attack();
        }
        else if (DistanceToPlayer() > data.detectionRange)
        {
            BeginSearch();
        }
    }

    private void DoChasePrey()
    {
        if (data.hungerDrainPerSecond <= 0f || !TryGetActivePrey(out _))
        {
            EndChasePrey();
            return;
        }

        float dist = Vector2.Distance(rb.position, chasingPrey.position);
        if (dist > data.detectionRange * 2f)
        {
            EndChasePrey();
            return;
        }

        MoveTowards(chasingPrey.position, data.chaseSpeed);

        if (dist <= data.attackRange)
        {
            EatPrey();
        }
    }

    protected virtual void DoSearch()
    {
        searchTimer -= Time.fixedDeltaTime;
        MoveTowards(lastKnownPlayerPos, data.moveSpeed);

        if (!PlayerIsHidden() && DistanceToPlayer() <= data.detectionRange)
        {
            BeginChase();
            return;
        }

        if (searchTimer <= 0f || Vector2.Distance(rb.position, lastKnownPlayerPos) < 0.2f)
        {
            EndSearch();
        }
    }

    protected virtual void BeginChase()
    {
        if (data == null || player == null) return;

        CurrentState = State.Chase;
        chasingPrey = null;
        lastKnownPlayerPos = player.position;
        StopWandering();
    }

    private void BeginChasePrey(Transform prey)
    {
        CurrentState = State.ChasePrey;
        chasingPrey = prey;
        StopWandering();
    }

    private void EndChasePrey()
    {
        chasingPrey = null;
        CurrentState = State.Wander;
        StartWandering();
    }

    protected virtual void BeginSearch()
    {
        CurrentState = State.Search;
        chasingPrey = null;
        searchTimer = data.loseInterestTime;
    }

    protected virtual void EndSearch()
    {
        CurrentState = State.Wander;
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
            EndChasePrey();
            return;
        }

        attackTimer = data.attackCooldown;

        hunger = Mathf.Min(hunger + data.hungerOnEat, data.maxHunger);

        animal.Die();

        EndChasePrey();
    }

    private bool TryGetActivePrey(out HarmlessAnimal animal)
    {
        animal = null;
        if (chasingPrey == null || !chasingPrey.CompareTag(TagAnimal)) return false;

        animal = chasingPrey.GetComponent<HarmlessAnimal>();
        return animal != null && animal.enabled;
    }

    private void Die()
    {
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        Destroy(gameObject);
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (!enabled || data == null) return;

        if (CurrentState == State.Wander || CurrentState == State.Search)
        {
            if (other.CompareTag("Player") && !PlayerIsHidden())
            {
                BeginChase();
                return;
            }

            if (data.hungerDrainPerSecond > 0f && other.CompareTag(TagAnimal) &&
                CurrentState == State.Wander)
            {
                var animal = other.GetComponent<HarmlessAnimal>();
                if (animal != null && animal.enabled)
                {
                    BeginChasePrey(other.transform);
                }
            }
        }
        else if (CurrentState == State.ChasePrey)
        {
            if (other.CompareTag("Player") && !PlayerIsHidden())
            {
                BeginChase();
            }
        }
    }

    protected override void HandleDetectionExit(Collider2D other) { /* range loss is handled inside DoChase/DoSearch/DoChasePrey */ }

    protected override bool ShouldInterruptWander() => CurrentState != State.Wander;

    // Called by ImposterComponent the instant a disguise is revealed, to skip straight
    // to chasing instead of waiting for this predator's own DetectionZone to fire.
    public void ForceBeginChase()
    {
        if (data == null || player == null) return;
        SkipSpawnDelay();
        BeginChase();
    }
}
