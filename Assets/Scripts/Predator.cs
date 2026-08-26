using UnityEngine;

// Design doc section 10, 11, 22: shared brain for every real predator (Wolf, Fox, Bird)
// AND for the predator hidden inside an Imposter (see ImposterComponent.cs).
//
// IMPORTANT: this component can exist in a DISABLED state on an Imposter prefab
// (see ImposterComponent). OnEnable is used instead of Awake/Start for anything that
// should only happen once this predator is actually "active" — that's what lets an
// imposter's hidden Wolf component sit dormant until the reveal.
//
// UNITY SETUP for a standalone predator (e.g. Wolf prefab):
//   - Standard Creature setup (Rigidbody2D, physical collider, DetectionZone child).
//   - Add the concrete subclass (Wolf.cs or Fox.cs), NOT this class directly (it's abstract).
//   - Assign a CreatureData asset with isPredator = true.
//   - You do NOT need to manually set the Tag — OnEnable sets it to "Predator" automatically.
public abstract class Predator : Creature
{
    public enum State { Wander, Chase, Search }
    public State CurrentState { get; protected set; } = State.Wander;

    protected Vector2 lastKnownPlayerPos;
    protected float searchTimer;

    private void OnEnable()
    {
        gameObject.tag = "Predator";
        CurrentState = State.Wander;
        StartWandering();
    }

    private void OnDisable()
    {
        StopWandering();
    }

    protected virtual void FixedUpdate()
    {
        switch (CurrentState)
        {
            case State.Chase: DoChase(); break;
            case State.Search: DoSearch(); break;
            // Wander is driven by the coroutine in the base Creature class.
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
        else if (DistanceToPlayer() > data.detectionRange * 1.6f)
        {
            BeginSearch();
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
        CurrentState = State.Chase;
        StopWandering();
    }

    protected virtual void BeginSearch()
    {
        CurrentState = State.Search;
        searchTimer = data.loseInterestTime;
    }

    protected virtual void EndSearch()
    {
        CurrentState = State.Wander;
        StartWandering();
    }

    // One-hit kill per design doc section 16.
    protected virtual void Attack()
    {
        playerController?.Die();
    }

    protected override void HandleDetectionEnter(Collider2D other)
    {
        if (!enabled) return; // defensive: an imposter's hidden predator may still be wired up while disabled
        if (CurrentState == State.Wander && other.CompareTag("Player") && !PlayerIsHidden())
        {
            BeginChase();
        }
    }

    protected override void HandleDetectionExit(Collider2D other) { /* range loss is handled inside DoChase/DoSearch */ }

    protected override bool ShouldInterruptWander() => CurrentState != State.Wander;

    // Called by ImposterComponent the instant a disguise is revealed, to skip straight
    // to chasing instead of waiting for this predator's own DetectionZone to fire.
    public void ForceBeginChase() => BeginChase();
}
