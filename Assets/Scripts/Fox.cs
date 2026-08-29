using UnityEngine;

// Design doc section 11: "Fox = fast but easier to lose."
// Differs from Wolf in code, not just data: it gives up a chase after a fixed duration
// even if it never actually loses sight of the player.
//
// UNITY SETUP:
//   Fox prefab -> add this script -> assign a "Data_Fox" CreatureData asset, e.g.:
//     moveSpeed 2.8, chaseSpeed 5.5, detectionRange 6, attackRange 0.5, loseInterestTime 2
//   Optionally tune "Max Chase Duration" in the Inspector (defaults to 4 seconds).
public class Fox : Predator
{
    [Tooltip("Max continuous chase time before the fox breaks off, even if it hasn't lost the player.")]
    public float maxChaseDuration = 4f;

    private float chaseClock;
    private float reacquirePlayerAt;

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
        if (maxChaseDuration > 0f && chaseClock >= maxChaseDuration)
        {
            float breakDuration = data != null ? Mathf.Max(0.25f, data.loseInterestTime) : 1f;
            reacquirePlayerAt = Time.time + breakDuration;
            BeginSearch();
            return;
        }
        base.DoChase();
    }
}
