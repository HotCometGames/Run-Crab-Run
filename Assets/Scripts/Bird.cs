// Design doc section 12 + 25: Bird is explicitly optional for the MVP — skip this file
// entirely if you're short on time. Included here in its "heavily simplified" form:
// a predator that is NOT blocked by hiding spots, since it dives from above.
//
// UNITY SETUP (only if you have time after the MVP checklist is done):
//   Bird prefab -> add this script -> assign a "Data_Bird" CreatureData asset.
//   Give it a simple back-and-forth or patrol wander (the default random-wander from
//   Creature.cs works fine as a placeholder).
public class Bird : Predator
{
    protected override void DoChase()
    {
        if (player == null) return;

        // Deliberately skips the PlayerIsHidden() check that Predator.DoChase uses —
        // ground hiding spots (bushes) don't block a bird's view from above.
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
}
