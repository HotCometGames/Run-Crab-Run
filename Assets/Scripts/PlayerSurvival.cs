using UnityEngine;

// Design doc section 5: hunger and thirst constantly decrease; sprinting drains hunger
// faster; hitting zero on either kills the player.
//
// UNITY SETUP:
//   Add this to the same "Player" GameObject as PlayerController.
//   Wire the UIManager's hunger/thirst Sliders to read from this component (see UIManager.cs).
[RequireComponent(typeof(PlayerController))]
public class PlayerSurvival : MonoBehaviour
{
    [Header("Hunger")]
    public float maxHunger = 100f;
    public float hunger = 100f;
    public float hungerDrainPerSecond = 0.75f;
    public float sprintHungerMultiplier = 2f;

    [Header("Thirst")]
    public float maxThirst = 100f;
    public float thirst = 100f;
    public float thirstDrainPerSecond = 1f; // slightly faster than hunger per design doc section 5

    [Header("Health")]
    public float maxHealth = 3f;
    public float health = 3f;
    [Tooltip("Seconds without damage before health regeneration starts.")]
    public float timeToRegenHealth = 15f;
    [Tooltip("Health restored per second after the regeneration delay.")]
    public float healthRegenPerSecond = 1f;
    [Tooltip("Brief grace period after a hit so overlapping predators cannot remove every heart in one frame.")]
    public float damageInvulnerabilityTime = 0.65f;
    [HideInInspector] public float lastDamageTime = 0f;

    private PlayerController controller;
    private float invulnerabilityTimer;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (controller == null || controller.IsDead) return;

        float hungerDrain = hungerDrainPerSecond * (controller.IsSprinting ? sprintHungerMultiplier : 1f);
        hunger -= hungerDrain * Time.deltaTime;
        thirst -= thirstDrainPerSecond * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        thirst = Mathf.Clamp(thirst, 0f, maxThirst);

        lastDamageTime += Time.deltaTime;
        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);

        if (lastDamageTime >= timeToRegenHealth && health < maxHealth)
            health = Mathf.MoveTowards(health, maxHealth, healthRegenPerSecond * Time.deltaTime);

        if (hunger <= 0f || thirst <= 0f)
        {
            controller.Die();
        }
    }

    // Called by ResourceNode.cs when the player eats/drinks.
    public bool ConsumeFood(float amount)
    {
        if (controller == null || controller.IsDead || amount <= 0f || hunger >= maxHunger) return false;
        hunger = Mathf.Clamp(hunger + amount, 0f, maxHunger);
        return true;
    }

    public bool ConsumeWater(float amount)
    {
        if (controller == null || controller.IsDead || amount <= 0f || thirst >= maxThirst) return false;
        thirst = Mathf.Clamp(thirst + amount, 0f, maxThirst);
        return true;
    }

    public void TakeDamage(float amount)
    {
        if (controller == null || controller.IsDead || amount <= 0f || invulnerabilityTimer > 0f) return;

        health = Mathf.Max(0f, health - amount);
        lastDamageTime = 0f;
        invulnerabilityTimer = Mathf.Max(0f, damageInvulnerabilityTime);
        controller.ShowDamageFeedback();

        if (health <= 0f)
            controller.Die();
    }
}
