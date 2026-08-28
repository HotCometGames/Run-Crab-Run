using UnityEngine;

// Design doc section 8 + 28 ("I'd actually make Imposter a modifier/component, rather
// than making every disguised creature a completely separate AI type").
//
// This turns an ordinary HarmlessAnimal prefab into a disguised predator. It works by
// keeping BOTH a HarmlessAnimal component and a disabled Predator component (e.g. Wolf)
// on the SAME GameObject, and swapping which one is active when the player gets close.
//
// UNITY SETUP — building an "Imposter Sheep" prefab step by step:
//   1. Duplicate your normal "Sheep" prefab, rename it "Sheep_Imposter".
//   2. It already has: Rigidbody2D, physical Collider2D, HarmlessAnimal script,
//      a "DetectionZone" child (used by HarmlessAnimal to notice real predators and flee).
//   3. Add a Wolf (or Fox) component to the SAME root GameObject.
//        - Assign it a CreatureData asset (its TRUE stats, e.g. Data_Wolf).
//        - In the Inspector, tick the checkbox next to the Wolf component OFF (disabled).
//          ImposterComponent.Awake() also force-disables it in code as a safety net.
//   4. Add a SECOND child GameObject called "RevealZone" with its own CircleCollider2D
//      (isTrigger) sized smaller than the DetectionZone — this is "how close the player
//      has to get before the disguise breaks." Add a DetectionZone script to it.
//      Leave this child object DEACTIVATED in the Inspector for now — ImposterComponent
//      doesn't need it active until reveal, but you CAN leave it active; either works
//      since HandleDetectionEnter early-outs while the Predator is disabled.
//   5. Give the Wolf component its OWN separate "Detection Zone" child too (this is what
//      lets it keep noticing the player normally once revealed) — leave that child
//      DEACTIVATED in the Inspector; ImposterComponent activates it on reveal.
//   6. Add this ImposterComponent script to the root GameObject.
//        - Drag the Wolf component into "Hidden Predator".
//        - Drag the "RevealZone" child's DetectionZone into "Reveal Zone".
//   7. Do NOT change the GameObject's Tag from "Animal" — ImposterComponent/Predator
//      switch it to "Predator" automatically at the moment of reveal, not before.
//
// Result: from spawn, this GameObject looks and behaves exactly like a normal sheep
// (wanders, flees real predators). The moment the player enters the RevealZone, the
// HarmlessAnimal is disabled, the hidden Wolf is enabled and immediately told to chase,
// and the tag flips to "Predator" — so any nearby harmless animals correctly flee it too.
[RequireComponent(typeof(HarmlessAnimal))]
public class ImposterComponent : MonoBehaviour
{
    [Tooltip("The predator behavior to enable on reveal (a Wolf/Fox component on this SAME GameObject). Must start DISABLED in the Inspector.")]
    public Predator hiddenPredator;

    [Tooltip("Small child trigger zone — separate from the hidden predator's own DetectionZone — that fires the reveal when the player gets close enough.")]
    public DetectionZone revealZone;

    [Tooltip("Sprite to show when the predator disguise is revealed.")]
    public Sprite predatorSprite;

    private HarmlessAnimal disguise;
    private SpriteRenderer spriteRenderer;
    private bool revealed;

    private void Awake()
    {
        disguise = GetComponent<HarmlessAnimal>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Safety net: guarantee the true identity never runs until reveal, even if
        // someone forgot to untick the checkbox in the Inspector.
        if (hiddenPredator != null) hiddenPredator.enabled = false;

        if (revealZone != null) revealZone.OnEnter += HandleRevealZoneEnter;
    }

    private void OnDestroy()
    {
        if (revealZone != null) revealZone.OnEnter -= HandleRevealZoneEnter;
    }

    private void HandleRevealZoneEnter(Collider2D other)
    {
        if (revealed || !other.CompareTag("Player")) return;
        Reveal();
    }

    private void Reveal()
    {
        revealed = true;

        if (spriteRenderer != null && predatorSprite != null)
            spriteRenderer.sprite = predatorSprite;

        disguise.enabled = false; // stops wander/flee coroutines via HarmlessAnimal.OnDisable

        if (hiddenPredator != null)
        {
            if (hiddenPredator.detectionZone != null)
                hiddenPredator.detectionZone.gameObject.SetActive(true);

            hiddenPredator.enabled = true;     // OnEnable sets tag = "Predator" and starts its state machine
            hiddenPredator.ForceBeginChase();  // skip straight to chasing instead of waiting on wander/detection
        }

        if (revealZone != null) revealZone.gameObject.SetActive(false);
    }
}
