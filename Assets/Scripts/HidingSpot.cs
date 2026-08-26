using UnityEngine;

// Design doc section 15: while hidden, predators can no longer directly chase/attack
// the crab (see Predator.DoChase -> BeginSearch when PlayerIsHidden() is true).
//
// UNITY SETUP:
//   Add this script to any bush/dense-vegetation/rock prefab that should act as a
//   hiding spot. Add a Collider2D to that same object and tick "Is Trigger".
//   No other wiring needed — it finds PlayerController automatically via the collider
//   that walks into it (make sure the Player GameObject is Tag = "Player").
public class HidingSpot : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerController>()?.EnterHiding();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerController>()?.ExitHiding();
        }
    }
}
