using UnityEngine;

// Design doc section 6: Berry Bushes, Fruit, and Streams. All three are the same script
// with different Inspector values — a Stream is just a Water node with Uses = 0 (infinite).
//
// UNITY SETUP:
//   - Berry Bush prefab: Type = Food, Restore Amount = 30, Uses = 3 (depletes after 3 eats).
//   - Fruit prefab: Type = Food, Restore Amount = 15, Uses = 1 (single use, then disappears).
//   - Stream prefab: Type = Water, Restore Amount = 40, Uses = 0 (infinite, never depletes).
//   Add a Collider2D (Is Trigger) to each prefab sized around the interactable area.
//   The player auto-consumes on overlap — no button press required (matches the doc's
//   emphasis on approach-based risk rather than button-based interaction).
public class ResourceNode : MonoBehaviour
{
    public enum ResourceType { Food, Water }

    [Header("Setup")]
    public ResourceType type = ResourceType.Food;
    public float restoreAmount = 30f;

    [Tooltip("0 = infinite uses (e.g. a stream). Any positive number depletes the node after that many uses.")]
    public int uses = 0;

    private int usesLeft;

    private void Awake()
    {
        usesLeft = uses;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (uses > 0 && usesLeft <= 0) return;

        var survival = other.GetComponent<PlayerSurvival>();
        if (survival == null) return;

        if (type == ResourceType.Food) survival.ConsumeFood(restoreAmount);
        else survival.ConsumeWater(restoreAmount);

        if (uses > 0)
        {
            usesLeft--;
            if (usesLeft <= 0) gameObject.SetActive(false); // depleted
        }
    }
}
