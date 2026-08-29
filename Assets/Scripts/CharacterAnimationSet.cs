using UnityEngine;

// A small data asset for the hand-drawn, frame-by-frame character art. Keeping the
// frames in one reusable asset lets the ordinary Sheep and Sheep Imposter share the
// same disguise animation, then lets the Imposter swap to the Wolf set on reveal.
[CreateAssetMenu(fileName = "NewCharacterAnimationSet", menuName = "RunCrabRun/Character Animation Set")]
public sealed class CharacterAnimationSet : ScriptableObject
{
    [Header("Frames")]
    public Sprite idleFrame;
    public Sprite[] walkFrames;
    public Sprite[] chaseFrames;
    public Sprite[] sprintFrames;

    [Header("Facing")]
    [Tooltip("Degrees added after aiming the artwork's native forward direction toward movement.")]
    public float movementRotationOffset = 90f;

    [Header("Playback")]
    [Min(0.1f)] public float walkFramesPerSecond = 4f;
    [Min(0.1f)] public float chaseFramesPerSecond = 5.5f;
    [Min(0.1f)] public float sprintFramesPerSecond = 5.5f;
}
