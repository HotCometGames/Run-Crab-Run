using UnityEngine;

// Design doc section 6: Berry Bushes, Fruit, and Streams. All three are the same script
// with different Inspector values — a Stream is just a Water node with Uses = 0 (infinite).
//
// UNITY SETUP:
//   - Berry Bush prefab: Type = Food, Restore Amount = 30, Uses = 1,
//     Regen Rate = 0.0222 (one eat empties it; it refills after ~45 seconds).
//   - Fruit prefab: Type = Food, Restore Amount = 15, Uses = 1, Regen Rate = 0.05
//     (single use, regenerates slowly).
//   - Stream prefab: Type = Water, Restore Amount = 0, Uses = 0, Water Regen Per Second = 15
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
    [Tooltip("How many uses to regenerate per second when depleted. 0.0222 = 1 use every 45 seconds.")]
    [Min(0f)] public float regenRate = 0.0222222f;

    [Header("Water (Continuous Regen)")]
    [Tooltip("Thirst restored per second while the player stands in this water source.")]
    public float waterRegenPerSecond = 15f;

    [Header("Feedback")]
    [Tooltip("Available and depleted sprites are paired by index. One pair is chosen per food node when it spawns.")]
    [SerializeField] private Sprite[] fullFoodSprites = new Sprite[0];
    [SerializeField] private Sprite[] depletedFoodSprites = new Sprite[0];
    [SerializeField] private Color depletedTint = new Color(0.45f, 0.45f, 0.45f, 0.65f);
    [SerializeField] private AudioClip interactionSound;

    private int usesLeft;
    private int maxUses;
    private float regenAccumulator;
    private bool playerInWater;
    private Collider2D playerCollider;
    private PlayerSurvival playerSurvival;
    private SpriteRenderer spriteRenderer;
    private Color availableTint = Color.white;
    private Sprite selectedFullSprite;
    private Sprite selectedDepletedSprite;

    private void Awake()
    {
        maxUses = uses;
        usesLeft = uses;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) availableTint = spriteRenderer.color;
        SelectFoodSpritePair();
        UpdateVisualState();
    }

    private void Update()
    {
        if (type == ResourceType.Food && maxUses > 0 && usesLeft < maxUses)
        {
            regenAccumulator += regenRate * Time.deltaTime;
            if (regenAccumulator >= 1f)
            {
                int previousUses = usesLeft;
                int toRestore = Mathf.Min(
                    Mathf.FloorToInt(regenAccumulator),
                    maxUses - usesLeft);
                usesLeft += toRestore;
                regenAccumulator -= toRestore;
                if (usesLeft >= maxUses) regenAccumulator = 0f;
                if (usesLeft != previousUses) UpdateVisualState();
            }
        }

        if (type == ResourceType.Water && playerInWater)
        {
            if (playerCollider == null || playerSurvival == null)
            {
                playerInWater = false;
                playerCollider = null;
                playerSurvival = null;
                return;
            }

            playerSurvival.ConsumeWater(waterRegenPerSecond * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (type == ResourceType.Food)
        {
            if (uses > 0 && usesLeft <= 0) return;

            PlayerSurvival survival = other.GetComponent<PlayerSurvival>();
            if (survival == null) return;

            if (!survival.ConsumeFood(restoreAmount)) return;

            if (uses > 0)
            {
                usesLeft--;
                UpdateVisualState();
            }

            ParticleManager.Instance?.Play(ParticleManager.ParticleType.Eat, transform.position);
            if (interactionSound != null)
                SoundManager.Instance?.PlaySound(interactionSound, transform, 0.65f);
        }
        else if (type == ResourceType.Water)
        {
            playerInWater = true;
            playerCollider = other;
            playerSurvival = other.GetComponent<PlayerSurvival>();

            ParticleManager.Instance?.Play(ParticleManager.ParticleType.WaterSplash, other.transform.position);
            if (playerSurvival != null &&
                playerSurvival.thirst < playerSurvival.maxThirst &&
                interactionSound != null)
                SoundManager.Instance?.PlaySound(interactionSound, other.transform, 0.6f);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (type == ResourceType.Water)
        {
            playerInWater = false;
            playerCollider = null;
            playerSurvival = null;
        }
    }

    private void UpdateVisualState()
    {
        if (spriteRenderer == null || type != ResourceType.Food || maxUses <= 0) return;

        bool isAvailable = usesLeft > 0;
        Sprite stateSprite = isAvailable ? selectedFullSprite : selectedDepletedSprite;
        if (stateSprite != null)
        {
            spriteRenderer.sprite = stateSprite;
            spriteRenderer.color = availableTint;
            return;
        }

        spriteRenderer.color = isAvailable ? availableTint : depletedTint;
    }

    private void SelectFoodSpritePair()
    {
        if (spriteRenderer == null || type != ResourceType.Food) return;
        if (fullFoodSprites == null || depletedFoodSprites == null) return;

        int pairCount = Mathf.Min(fullFoodSprites.Length, depletedFoodSprites.Length);
        if (pairCount <= 0) return;

        int startIndex = Random.Range(0, pairCount);
        for (int offset = 0; offset < pairCount; offset++)
        {
            int index = (startIndex + offset) % pairCount;
            if (fullFoodSprites[index] == null || depletedFoodSprites[index] == null) continue;

            selectedFullSprite = fullFoodSprites[index];
            selectedDepletedSprite = depletedFoodSprites[index];
            return;
        }
    }
}
