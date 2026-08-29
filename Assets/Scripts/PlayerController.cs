using UnityEngine;

// Design doc section 4: movement, sprint, hiding. No attack ability by design —
// the crab survives through movement and decision-making, not combat.
//
// UNITY SETUP:
//   - Create a "Player" GameObject, Tag = "Player".
//   - Add Rigidbody2D (Gravity Scale 0, Freeze Rotation Z), a Collider2D, a SpriteRenderer.
//   - Add this script, PlayerSurvival.cs, and (optionally) an Animator.
//   - Input: this uses the default "Horizontal"/"Vertical" axes that ship with every
//     new Unity project (WASD + arrow keys), so no Input Manager changes are required.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{

    [SerializeField] private AudioClip CrabWalk;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float sprintMultiplier = 1.8f;

    [Header("Hiding")]
    [Range(0.1f, 1f), Tooltip("Alpha when hidden in a bush.")]
    public float hiddenAlpha = 0.4f;

    public bool IsHidden { get; private set; }
    public bool IsSprinting { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        SoundManager.instance.PlaySound(CrabWalk, transform, 1f);

    }

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(h, v).normalized;

        // Sprinting only counts (and only drains extra hunger) while actually moving.
        IsSprinting = Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0f;
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed * (IsSprinting ? sprintMultiplier : 1f);
        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
    }

    // Called by HidingSpot.cs when the crab enters/exits a bush trigger.
    public void EnterHiding()
    {
        IsHidden = true;
        SetAlpha(hiddenAlpha);
    }

    public void ExitHiding()
    {
        IsHidden = false;
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    // Called by a Predator on catching the player, or by PlayerSurvival on starvation/dehydration.
    public void Die()
    {
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        GameManager.Instance?.OnPlayerDeath();
    }
}
