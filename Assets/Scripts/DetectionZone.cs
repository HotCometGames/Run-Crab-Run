using UnityEngine;

// UNITY SETUP:
//   Create an empty CHILD GameObject under any creature (e.g. "DetectionZone").
//   Add a CircleCollider2D to it, tick "Is Trigger", and set its Radius to match
//   the creature's CreatureData.detectionRange.
//   Add this script to that same child object.
//   Drag the child object into the parent creature's "Detection Zone" field in the Inspector.
//
// Why a separate child object instead of one collider on the creature itself?
// Because most creatures need TWO different-sized colliders: a small solid one for
// physical collision, and a large trigger one for "notice things at a distance."
// Keeping them as separate GameObjects avoids Unity collider-layering headaches.
[RequireComponent(typeof(Collider2D))]
public class DetectionZone : MonoBehaviour
{
    public System.Action<Collider2D> OnEnter;
    public System.Action<Collider2D> OnExit;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        OnEnter?.Invoke(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        OnExit?.Invoke(other);
    }
}
