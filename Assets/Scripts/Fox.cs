using UnityEngine;

// Design doc section 11: "Fox = fast but easier to lose."
// Differs from Wolf in code, not just data: it gives up a chase after a fixed duration
// even if it never actually loses sight of the player.
//
// UNITY SETUP:
//   Fox prefab -> add this script -> assign a "Data_Fox" CreatureData asset, e.g.:
//     moveSpeed 2.8, chaseSpeed 5.5, detectionRange 6, attackRange 1.1,
//     loseInterestTime 2, playerCatchUpTime 6.5
//   Optionally tune "Max Chase Duration" in the Inspector (defaults to 8.5 seconds).
public class Fox : Predator
{
    [Tooltip("Max continuous chase time before the fox breaks off. The configured player catch-up window takes precedence when it is longer. Set to 0 to disable this limit.")]
    public float maxChaseDuration = 8.5f;

    private float chaseClock;
    private float reacquirePlayerAt;

    protected override void ResetSubclassStateOnActivation()
    {
        chaseClock = 0f;
        reacquirePlayerAt = 0f;
    }

    protected override void BeginChase()
    {
        if (Time.time < reacquirePlayerAt) return;

        base.BeginChase();
        if (CurrentState == State.Chase)
            chaseClock = 0f;
    }

    protected override void DoChase()
    {
        chaseClock += Time.fixedDeltaTime;
        float effectiveChaseDuration = maxChaseDuration;
        if (effectiveChaseDuration > 0f && data != null && data.playerCatchUpTime > 0f)
            effectiveChaseDuration = Mathf.Max(effectiveChaseDuration, data.playerCatchUpTime + 1f);

        if (effectiveChaseDuration > 0f && chaseClock >= effectiveChaseDuration)
        {
            float breakDuration = data != null ? Mathf.Max(0.25f, data.loseInterestTime) : 1f;
            reacquirePlayerAt = Time.time + breakDuration;
            BeginSearch();
            return;
        }
        base.DoChase();
    }
}
