// Design doc section 10: "The wolf teaches the player: predators are dangerous."
// Slow but persistent. No behavior override needed for the MVP — its identity comes
// entirely from the CreatureData asset assigned in the Inspector (low moveSpeed/chaseSpeed,
// large prey detectionRange, a smaller playerDetectionRange, a gradual playerCatchUpTime,
// and long loseInterestTime).
//
// UNITY SETUP:
//   Wolf prefab -> add this script -> assign a "Data_Wolf" CreatureData asset, e.g.:
//     moveSpeed 2.0, chaseSpeed 4.6, detectionRange 8, playerDetectionRange 6,
//     attackRange 1.1, loseInterestTime 5, playerCatchUpTime 6.5
public class Wolf : Predator
{
}
