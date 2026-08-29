using UnityEngine;
using UnityEngine.Events;

// Swaps an ordinary HarmlessAnimal disguise for a disabled Predator component when
// the player enters the reveal trigger. Both behaviours live on the same GameObject.
// The hidden Predator must start disabled and use its own DetectionZone child.
[RequireComponent(typeof(HarmlessAnimal))]
public class ImposterComponent : MonoBehaviour
{
    [Tooltip("Predator behaviour on this GameObject. It must start disabled in the Inspector.")]
    public Predator hiddenPredator;

    [Tooltip("Small child trigger that reveals the disguise when the player gets close.")]
    public DetectionZone revealZone;

    [Header("Feedback")]
    [Tooltip("Sprite to show when the disguise is revealed.")]
    public Sprite predatorSprite;

    [Tooltip("Animation set to use after the disguise reveals its hidden predator.")]
    public CharacterAnimationSet predatorAnimationSet;

    [Tooltip("Optional reveal audio, particles, animation, or camera feedback.")]
    public UnityEvent onRevealed;

    private HarmlessAnimal disguise;
    private SpriteRenderer spriteRenderer;
    private CharacterAnimation2D characterAnimation;
    private bool revealed;

    private void Awake()
    {
        disguise = GetComponent<HarmlessAnimal>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        characterAnimation = GetComponent<CharacterAnimation2D>();

        // Safety net for prefab setup mistakes. OnEnable starts Predator behaviour,
        // so the hidden component must remain dormant until Reveal.
        if (hiddenPredator != null)
            hiddenPredator.enabled = false;

        if (revealZone != null)
            revealZone.OnEnter += HandleRevealZoneEnter;
    }

    private void OnDestroy()
    {
        if (revealZone != null)
            revealZone.OnEnter -= HandleRevealZoneEnter;
    }

    private void HandleRevealZoneEnter(Collider2D other)
    {
        if (revealed || !other.CompareTag("Player")) return;
        Reveal();
    }

    private void Reveal()
    {
        if (hiddenPredator == null || hiddenPredator.data == null)
        {
            Debug.LogError($"{name}: Imposter cannot reveal without a configured hidden Predator.", this);
            return;
        }

        revealed = true;

        // Let effects inspect the harmless appearance before the behaviour swap.
        onRevealed?.Invoke();

        if (spriteRenderer != null && predatorSprite != null)
            spriteRenderer.sprite = predatorSprite;

        characterAnimation?.SetAnimationSet(predatorAnimationSet);

        ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, transform.position);

        disguise.enabled = false;

        if (hiddenPredator.detectionZone != null)
            hiddenPredator.detectionZone.gameObject.SetActive(true);

        hiddenPredator.enabled = true;
        hiddenPredator.ForceBeginChase();
        NotifyNearbyAnimals();

        if (revealZone != null)
            revealZone.gameObject.SetActive(false);
    }

    private void NotifyNearbyAnimals()
    {
        HarmlessAnimal[] animals = FindObjectsByType<HarmlessAnimal>();
        foreach (HarmlessAnimal animal in animals)
        {
            if (animal == disguise || !animal.enabled || animal.data == null) continue;

            float distance = Vector2.Distance(transform.position, animal.transform.position);
            if (distance <= animal.data.detectionRange)
                animal.ReactToThreat(transform);
        }
    }
}
