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
    public float timeToRegenHealth = 15f; // seconds of not taking damage before health starts to regen
    public float lastDamageTime = 0f; // time of last damage taken, used to determine when to start regen

    private PlayerController controller;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        float hungerDrain = hungerDrainPerSecond * (controller.IsSprinting ? sprintHungerMultiplier : 1f);
        hunger -= hungerDrain * Time.deltaTime;
        thirst -= thirstDrainPerSecond * Time.deltaTime;

        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        thirst = Mathf.Clamp(thirst, 0f, maxThirst);

        if (lastDamageTime >= timeToRegenHealth && health < maxHealth)
        {
            health += 1; // regen 1 health per second
            health = Mathf.Clamp(health, 0f, maxHealth);
            lastDamageTime = 0f; // reset timer after regen
        } else
        {
            lastDamageTime += Time.deltaTime;
        }

        if (hunger <= 0f || thirst <= 0f)
        {
            controller.Die();
        }
    }

    // Called by ResourceNode.cs when the player eats/drinks.
    public void ConsumeFood(float amount) => hunger = Mathf.Clamp(hunger + amount, 0f, maxHunger);
    public void ConsumeWater(float amount) => thirst = Mathf.Clamp(thirst + amount, 0f, maxThirst);
    public void TakeDamage(float amount)
    {
        health -= amount;
        lastDamageTime = 0;

        if (health <= 0f)
        {
            controller.Die();
        }
    }
}
