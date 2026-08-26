// Design doc section 10: "The wolf teaches the player: predators are dangerous."
// Slow but persistent. No behavior override needed for the MVP — its identity comes
// entirely from the CreatureData asset assigned in the Inspector (low moveSpeed/chaseSpeed,
// large detectionRange, long loseInterestTime so it doesn't give up chasing easily).
//
// UNITY SETUP:
//   Wolf prefab -> add this script -> assign a "Data_Wolf" CreatureData asset, e.g.:
//     moveSpeed 2.0, chaseSpeed 3.2, detectionRange 8, attackRange 0.6, loseInterestTime 5
public class Wolf : Predator
{
}
