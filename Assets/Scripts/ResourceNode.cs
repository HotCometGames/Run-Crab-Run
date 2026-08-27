using UnityEngine;

// Design doc section 6: Berry Bushes, Fruit, and Streams. All three are the same script
// with different Inspector values — a Stream is just a Water node with Uses = 0 (infinite).
//
// UNITY SETUP:
//   - Berry Bush prefab: Type = Food, Restore Amount = 30, Uses = 3, Regen Rate = 0.1
//     (depletes after 3 eats, bush stays visible, regenerates 1 use every ~10 seconds).
//   - Fruit prefab: Type = Food, Restore Amount = 15, Uses = 1, Regen Rate = 0.05
//     (single use, regenerates slowly).
//   - Stream prefab: Type = Water, Restore Amount = 0, Uses = 0, Water Regen Per Second = 5
//     (infinite uses, gradually replenishes thirst while player stands in it).
//   Add a Collider2D (Is Trigger) to each prefab sized around the interactable area.
//   Food auto-consumes on overlap (no button press). Water replenishes continuously while
//   the player remains inside the trigger.
public class ResourceNode : MonoBehaviour
{
    public enum ResourceType { Food, Water }

    [Header("Setup")]
    public ResourceType type = ResourceType.Food;
    public float restoreAmount = 30f;

    [Tooltip("0 = infinite uses (e.g. a stream). Any positive number depletes the node after that many uses.")]
    public int uses = 0;

    [Header("Food Regeneration")]
    [Tooltip("How many uses to regenerate per second when depleted. 0.1 = 1 use every 10 seconds.")]
    public float regenRate = 0.1f;

    [Header("Water (Continuous Regen)")]
    [Tooltip("Thirst restored per second while the player stands in this water source.")]
    public float waterRegenPerSecond = 5f;

    private int usesLeft;
    private int maxUses;
    private float regenAccumulator;
    private bool playerInWater;
    private Collider2D playerCollider;

    private void Awake()
    {
        maxUses = uses;
        usesLeft = uses;
    }

    private void Update()
    {
        if (type == ResourceType.Food && maxUses > 0 && usesLeft < maxUses)
        {
            regenAccumulator += regenRate * Time.deltaTime;
            if (regenAccumulator >= 1f)
            {
                int toRestore = Mathf.FloorToInt(regenAccumulator);
                usesLeft = Mathf.Min(usesLeft + toRestore, maxUses);
                regenAccumulator -= toRestore;
            }
        }

        if (type == ResourceType.Water && playerInWater && playerCollider != null)
        {
            var survival = playerCollider.GetComponent<PlayerSurvival>();
            if (survival != null)
            {
                survival.ConsumeWater(waterRegenPerSecond * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (type == ResourceType.Food)
        {
            if (uses > 0 && usesLeft <= 0) return;

            var survival = other.GetComponent<PlayerSurvival>();
            if (survival == null) return;

            survival.ConsumeFood(restoreAmount);

            if (uses > 0) usesLeft--;
        }
        else if (type == ResourceType.Water)
        {
            playerInWater = true;
            playerCollider = other;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (type == ResourceType.Water)
        {
            playerInWater = false;
            playerCollider = null;
        }
    }
}
