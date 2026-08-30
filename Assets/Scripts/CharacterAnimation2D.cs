using UnityEngine;

// Drives the artist's separate PNG frames from movement. Player intent is used because
// MovePosition does not expose dependable Rigidbody velocity; AI creatures use their
// real Rigidbody velocity. Only the visual child rotates, keeping physics stable.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(200)]
public sealed class CharacterAnimation2D : MonoBehaviour
{
    private enum MotionMode { Idle, Walk, Chase, Sprint }

    [Header("Art")]
    [SerializeField] private CharacterAnimationSet animationSet;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Transform visualRoot;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float movementThreshold = 0.05f;
    [SerializeField, Min(1f)] private float visualTurnSpeed = 600f;
    [SerializeField] private bool rotateVisualToMovement = true;

    private Rigidbody2D body;
    private PlayerController playerController;
    private HarmlessAnimal harmlessAnimal;
    private Predator[] predators;
    private MotionMode activeMode = (MotionMode)(-1);
    private float frameClock;

    public bool ControlsVisualFacing =>
        rotateVisualToMovement && visualRoot != null && visualRoot != transform;

    public CharacterAnimationSet CurrentAnimationSet => animationSet;

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        RestartAnimation();
    }

    private void Update()
    {
        if (body == null || targetRenderer == null || animationSet == null) return;

        Vector2 velocity = ResolveMovementVelocity();
        UpdateFacing(velocity);

        MotionMode mode = ResolveMotionMode(velocity);
        if (mode != activeMode)
        {
            activeMode = mode;
            frameClock = 0f;
        }

        ApplyFrame(mode, velocity.magnitude);
    }

    public void Configure(CharacterAnimationSet set, SpriteRenderer renderer)
    {
        animationSet = set;
        targetRenderer = renderer;
        visualRoot = renderer != null ? renderer.transform : null;
        ResolveReferences();
        RestartAnimation();
    }

    public void SetAnimationSet(CharacterAnimationSet set)
    {
        if (animationSet == set) return;
        animationSet = set;
        RestartAnimation();
    }

    private void ResolveReferences()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (visualRoot == null && targetRenderer != null) visualRoot = targetRenderer.transform;
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (harmlessAnimal == null) harmlessAnimal = GetComponent<HarmlessAnimal>();
        if (predators == null || predators.Length == 0) predators = GetComponents<Predator>();
    }

    private void RestartAnimation()
    {
        activeMode = (MotionMode)(-1);
        frameClock = 0f;

        if (targetRenderer != null && animationSet != null && animationSet.idleFrame != null)
            targetRenderer.sprite = animationSet.idleFrame;
    }

    private MotionMode ResolveMotionMode(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= movementThreshold * movementThreshold)
            return MotionMode.Idle;

        if (playerController != null && playerController.IsSprinting)
            return MotionMode.Sprint;

        Predator activePredator = GetActivePredator();
        if (activePredator != null &&
            (activePredator.CurrentState == Predator.State.Chase ||
             activePredator.CurrentState == Predator.State.ChasePrey))
        {
            return MotionMode.Chase;
        }

        return MotionMode.Walk;
    }

    private void ApplyFrame(MotionMode mode, float actualSpeed)
    {
        if (mode == MotionMode.Idle)
        {
            if (animationSet.idleFrame != null && targetRenderer.sprite != animationSet.idleFrame)
                targetRenderer.sprite = animationSet.idleFrame;
            return;
        }

        Sprite[] frames;
        float framesPerSecond;
        switch (mode)
        {
            case MotionMode.Chase:
                bool hasChaseFrames = HasFrames(animationSet.chaseFrames);
                frames = hasChaseFrames ? animationSet.chaseFrames : animationSet.walkFrames;
                framesPerSecond = hasChaseFrames
                    ? animationSet.chaseFramesPerSecond
                    : animationSet.walkFramesPerSecond;
                break;
            case MotionMode.Sprint:
                bool hasSprintFrames = HasFrames(animationSet.sprintFrames);
                frames = hasSprintFrames ? animationSet.sprintFrames : animationSet.walkFrames;
                framesPerSecond = hasSprintFrames
                    ? animationSet.sprintFramesPerSecond
                    : animationSet.walkFramesPerSecond;
                break;
            default:
                frames = animationSet.walkFrames;
                framesPerSecond = animationSet.walkFramesPerSecond;
                break;
        }

        if (!HasFrames(frames))
        {
            if (animationSet.idleFrame != null) targetRenderer.sprite = animationSet.idleFrame;
            return;
        }

        frameClock += Time.deltaTime * Mathf.Max(0.1f, framesPerSecond) *
            CalculatePlaybackScale(mode, actualSpeed);
        int frameIndex = Mathf.FloorToInt(frameClock) % frames.Length;
        Sprite frame = FindFrame(frames, frameIndex);
        if (frame != null && targetRenderer.sprite != frame)
            targetRenderer.sprite = frame;
    }

    private float CalculatePlaybackScale(MotionMode mode, float actualSpeed)
    {
        float referenceSpeed = 1f;

        if (playerController != null)
        {
            referenceSpeed = playerController.moveSpeed *
                (mode == MotionMode.Sprint ? playerController.sprintMultiplier : 1f);
        }
        else
        {
            Predator activePredator = GetActivePredator();
            if (activePredator != null && activePredator.data != null)
            {
                referenceSpeed = mode == MotionMode.Chase
                    ? activePredator.data.chaseSpeed
                    : activePredator.data.moveSpeed;
            }
            else if (harmlessAnimal != null && harmlessAnimal.data != null)
            {
                referenceSpeed = harmlessAnimal.CurrentState == HarmlessAnimal.State.Flee
                    ? harmlessAnimal.data.fleeSpeed
                    : harmlessAnimal.data.moveSpeed;
            }
        }

        return Mathf.Clamp(actualSpeed / Mathf.Max(0.1f, referenceSpeed), 0.7f, 1.45f);
    }

    private Predator GetActivePredator()
    {
        if (predators == null) return null;

        foreach (Predator predator in predators)
        {
            if (predator != null && predator.isActiveAndEnabled)
                return predator;
        }

        return null;
    }

    private Vector2 ResolveMovementVelocity()
    {
        // PlayerController moves through Rigidbody2D.MovePosition, whose reported
        // linearVelocity can be zero when Update runs. Input-derived velocity keeps
        // player facing and frame selection deterministic in every rendered frame.
        return playerController != null
            ? playerController.MovementVelocity
            : body.linearVelocity;
    }

    private void UpdateFacing(Vector2 velocity)
    {
        if (!ControlsVisualFacing ||
            velocity.sqrMagnitude <= movementThreshold * movementThreshold)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg +
            animationSet.movementRotationOffset;
        float angle = Mathf.MoveTowardsAngle(
            visualRoot.localEulerAngles.z,
            targetAngle,
            visualTurnSpeed * Time.deltaTime);
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        targetRenderer.flipX = false;
    }

    private static bool HasFrames(Sprite[] frames)
    {
        if (frames == null) return false;

        foreach (Sprite frame in frames)
        {
            if (frame != null) return true;
        }

        return false;
    }

    private static Sprite FindFrame(Sprite[] frames, int preferredIndex)
    {
        for (int offset = 0; offset < frames.Length; offset++)
        {
            Sprite frame = frames[(preferredIndex + offset) % frames.Length];
            if (frame != null) return frame;
        }

        return null;
    }
}
