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
    [Header("Audio")]
    [SerializeField] private AudioClip CrabWalk;
    [SerializeField, Range(0f, 1f)] private float movementVolume = 0.25f;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float sprintMultiplier = 1.8f;

    [Header("Hiding")]
    [Range(0.1f, 1f), Tooltip("Alpha when hidden in a bush.")]
    public float hiddenAlpha = 0.4f;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float damageFlashDuration = 0.12f;

    public bool IsHidden { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsDead { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource movementAudio;
    private Vector2 moveInput;
    private Color normalSpriteColor = Color.white;
    private int hidingSpotOverlaps;
    private bool movementAudioStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) normalSpriteColor = spriteRenderer.color;
        ConfigureMovementAudio();
    }

    private void Update()
    {
        if (IsDead)
        {
            moveInput = Vector2.zero;
            IsSprinting = false;
            UpdateMovementAudio(false);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(h, v).normalized;

        // Sprinting only counts (and only drains extra hunger) while actually moving.
        IsSprinting = Input.GetKey(KeyCode.LeftShift) && moveInput.sqrMagnitude > 0f;
        UpdateMovementAudio(moveInput.sqrMagnitude > 0f);
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed * (IsSprinting ? sprintMultiplier : 1f);
        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
    }

    // Called by HidingSpot.cs when the crab enters/exits a bush trigger.
    public void EnterHiding()
    {
        hidingSpotOverlaps++;
        if (IsHidden) return;

        IsHidden = true;
        SetAlpha(hiddenAlpha);
    }

    public void ExitHiding()
    {
        hidingSpotOverlaps = Mathf.Max(0, hidingSpotOverlaps - 1);
        if (hidingSpotOverlaps > 0) return;

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
        if (IsDead) return;

        IsDead = true;
        moveInput = Vector2.zero;
        IsSprinting = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (movementAudio != null) movementAudio.Stop();

        // Play the effect before pausing time so its first frame is visible.
        ParticleManager.Instance?.Play(ParticleManager.ParticleType.Death, transform.position);
        GameManager.Instance?.OnPlayerDeath();
    }

    public void ShowDamageFeedback()
    {
        if (!IsDead && spriteRenderer != null)
            StartCoroutine(DamageFlashRoutine());
    }

    private System.Collections.IEnumerator DamageFlashRoutine()
    {
        Color flash = new Color(1f, 0.25f, 0.2f, spriteRenderer.color.a);
        spriteRenderer.color = flash;
        yield return new WaitForSecondsRealtime(damageFlashDuration);

        if (spriteRenderer != null)
        {
            Color restored = normalSpriteColor;
            restored.a = IsHidden ? hiddenAlpha : normalSpriteColor.a;
            spriteRenderer.color = restored;
        }
    }

    private void ConfigureMovementAudio()
    {
        if (CrabWalk == null) return;

        movementAudio = gameObject.AddComponent<AudioSource>();
        movementAudio.clip = CrabWalk;
        movementAudio.playOnAwake = false;
        movementAudio.loop = true;
        movementAudio.spatialBlend = 0f;
        movementAudio.volume = movementVolume * GameAudioSettings.EffectsVolume;
    }

    private void UpdateMovementAudio(bool shouldPlay)
    {
        if (movementAudio == null) return;

        if (shouldPlay)
        {
            if (!movementAudioStarted)
            {
                movementAudio.Play();
                movementAudioStarted = true;
            }
            else if (!movementAudio.isPlaying)
            {
                movementAudio.UnPause();
            }
        }
        else if (movementAudio.isPlaying)
        {
            movementAudio.Pause();
        }
    }
}
