using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// Swaps between an ordinary HarmlessAnimal disguise and a disabled Predator component.
// Both behaviours live on the same GameObject. The hidden Predator must start disabled
// and use its own DetectionZone child.
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

    [Header("Re-disguise")]
    [Min(0f), Tooltip("Continuous calm time after fully losing the player before the predator becomes an animal again.")]
    public float redisguiseDelay = 4f;

    [Tooltip("Optional audio, particles, animation, or camera feedback when the disguise returns.")]
    public UnityEvent onRedisguised;

    private HarmlessAnimal disguise;
    private SpriteRenderer spriteRenderer;
    private CharacterAnimation2D characterAnimation;
    private Rigidbody2D body;
    private Sprite disguiseSprite;
    private CharacterAnimationSet disguiseAnimationSet;
    private readonly HashSet<Collider2D> overlappingPlayerColliders =
        new HashSet<Collider2D>();
    private bool revealed;
    private float calmTimer;

    public bool IsRevealed => revealed;

    private void Awake()
    {
        disguise = GetComponent<HarmlessAnimal>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        characterAnimation = GetComponent<CharacterAnimation2D>();
        body = GetComponent<Rigidbody2D>();
        disguiseSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        disguiseAnimationSet = characterAnimation != null
            ? characterAnimation.CurrentAnimationSet
            : null;

        // Safety net for prefab setup mistakes. OnEnable starts Predator behaviour,
        // so the hidden component must remain dormant until Reveal.
        if (hiddenPredator != null)
            hiddenPredator.enabled = false;

        if (revealZone != null)
        {
            revealZone.OnEnter += HandleRevealZoneEnter;
            revealZone.OnExit += HandleRevealZoneExit;
        }
    }

    private void Update()
    {
        if (!revealed || hiddenPredator == null || !hiddenPredator.isActiveAndEnabled)
            return;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return;

        if (hiddenPredator.CurrentState != Predator.State.Wander ||
            hiddenPredator.CanCurrentlySeePlayer)
        {
            calmTimer = 0f;

            // Prevent Search from immediately handing off into a prey hunt. This
            // leaves a genuine calm window in which the disguise can return.
            if (hiddenPredator.CurrentState == Predator.State.Search)
                hiddenPredator.DelayPreyHunting(redisguiseDelay + 0.25f);

            return;
        }

        float remainingCalmTime = Mathf.Max(0f, redisguiseDelay - calmTimer);
        if (remainingCalmTime > 0f)
            hiddenPredator.DelayPreyHunting(remainingCalmTime + 0.25f);

        calmTimer += Time.deltaTime;

        if (calmTimer >= Mathf.Max(0f, redisguiseDelay) && !PlayerIsInsideRevealZone())
            Redisguise();
    }

    private void OnDestroy()
    {
        if (revealZone != null)
        {
            revealZone.OnEnter -= HandleRevealZoneEnter;
            revealZone.OnExit -= HandleRevealZoneExit;
        }
    }

    private void HandleRevealZoneEnter(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;

        overlappingPlayerColliders.Add(other);
        if (!revealed) Reveal();
    }

    private void HandleRevealZoneExit(Collider2D other)
    {
        if (other != null)
            overlappingPlayerColliders.Remove(other);
    }

    private void Reveal()
    {
        if (hiddenPredator == null || hiddenPredator.data == null)
        {
            Debug.LogError($"{name}: Imposter cannot reveal without a configured hidden Predator.", this);
            return;
        }

        revealed = true;
        calmTimer = 0f;

        // Let effects inspect the harmless appearance before the behaviour swap.
        onRevealed?.Invoke();

        if (spriteRenderer != null && predatorSprite != null)
            spriteRenderer.sprite = predatorSprite;

        characterAnimation?.SetAnimationSet(predatorAnimationSet);

        ParticleManager.Instance?.Play(
            ParticleManager.ParticleType.SpawnPoof,
            transform.position,
            playPoofSound: true);

        disguise.enabled = false;

        if (hiddenPredator.detectionZone != null)
            hiddenPredator.detectionZone.gameObject.SetActive(true);

        hiddenPredator.enabled = true;
        hiddenPredator.ForceBeginChase();
        NotifyNearbyAnimals();
    }

    private void Redisguise()
    {
        // Disable the active role before restoring the harmless one so only one
        // behaviour owns movement and detection at any point in the swap.
        hiddenPredator.enabled = false;

        if (hiddenPredator.detectionZone != null)
            hiddenPredator.detectionZone.gameObject.SetActive(false);

        gameObject.tag = "Animal";

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        characterAnimation?.SetAnimationSet(disguiseAnimationSet);
        if (spriteRenderer != null && disguiseSprite != null)
            spriteRenderer.sprite = disguiseSprite;

        revealed = false;
        calmTimer = 0f;
        disguise.enabled = true;
        NotifyDisguiseOfNearbyPredators();

        ParticleManager.Instance?.Play(ParticleManager.ParticleType.SpawnPoof, transform.position);
        onRedisguised?.Invoke();
    }

    private bool PlayerIsInsideRevealZone()
    {
        overlappingPlayerColliders.RemoveWhere(collider =>
            collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
        return overlappingPlayerColliders.Count > 0;
    }

    private void NotifyDisguiseOfNearbyPredators()
    {
        if (disguise == null || disguise.data == null) return;

        Predator[] predators = FindObjectsByType<Predator>();
        foreach (Predator predator in predators)
        {
            if (predator == null || predator == hiddenPredator ||
                !predator.isActiveAndEnabled || !predator.CompareTag("Predator"))
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, predator.transform.position);
            if (distance <= disguise.data.detectionRange)
                disguise.ReactToThreat(predator.transform);
        }
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
