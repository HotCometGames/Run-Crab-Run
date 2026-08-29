using System.Collections.Generic;
using UnityEngine;

// Behaviour scripts decide where a creature wants to go; this component decides how
// it gets there. Keeping locomotion in one place prevents wander, flee, and chase from
// snapping the Rigidbody in different ways, and lets an imposter keep its momentum
// when its harmless behaviour is swapped for its hidden predator behaviour.
public enum CreatureMovementStyle { Wander, Flee, Chase, Search, SeekFood }

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CreatureMotor2D : MonoBehaviour
{
    private static readonly HashSet<CreatureMotor2D> ActiveMotors = new HashSet<CreatureMotor2D>();

    // Large enough for the scene's 30-creature cap plus the player and boundary.
    // Dynamic-body hits are discarded below, but still occupy cast result slots.
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[48];

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private ContactFilter2D obstacleFilter;

    private Creature intentOwner;
    private CreatureData settings;
    private CreatureMovementStyle movementStyle;
    private Vector2 requestedDirection;
    private Vector2 requestedTarget;
    private float requestedSpeed;
    private float stopRadius;
    private float slowRadius;
    private bool hasTarget;
    private Transform separationIgnore;

    private Vector2 lastHeading = Vector2.right;
    private float wanderNoiseOffset;
    private float avoidanceSide;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        wanderNoiseOffset = Random.Range(0f, 1000f);
        avoidanceSide = Random.value < 0.5f ? -1f : 1f;

        obstacleFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true
        };
        obstacleFilter.SetLayerMask(Physics2D.AllLayers);
    }

    private void OnEnable() => ActiveMotors.Add(this);

    private void OnDisable()
    {
        ActiveMotors.Remove(this);
        intentOwner = null;
        separationIgnore = null;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void OnDestroy() => ActiveMotors.Remove(this);

    public void DriveTo(
        Creature owner,
        CreatureData data,
        Vector2 target,
        float speed,
        CreatureMovementStyle style,
        float arrivalStopRadius,
        float arrivalSlowRadius,
        Transform ignoreForSeparation = null)
    {
        if (owner == null || data == null) return;

        intentOwner = owner;
        settings = data;
        movementStyle = style;
        requestedTarget = target;
        requestedSpeed = Mathf.Max(0f, speed);
        stopRadius = Mathf.Max(0f, arrivalStopRadius);
        slowRadius = Mathf.Max(stopRadius + 0.01f, arrivalSlowRadius);
        separationIgnore = ignoreForSeparation;
        hasTarget = true;
    }

    public void DriveDirection(
        Creature owner,
        CreatureData data,
        Vector2 direction,
        float speed,
        CreatureMovementStyle style)
    {
        if (owner == null || data == null) return;

        intentOwner = owner;
        settings = data;
        movementStyle = style;
        requestedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        requestedSpeed = Mathf.Max(0f, speed);
        separationIgnore = null;
        hasTarget = false;
    }

    // Braking remains an intent instead of setting velocity to zero, so stopping at a
    // wander destination or attack range still has a small, natural ease-out.
    public void Brake(Creature owner, CreatureData data, CreatureMovementStyle style)
    {
        if (owner == null || data == null) return;

        intentOwner = owner;
        settings = data;
        movementStyle = style;
        requestedDirection = Vector2.zero;
        requestedSpeed = 0f;
        separationIgnore = null;
        hasTarget = false;
    }

    // Only the behaviour that supplied the current intent may release it. This is
    // important on imposters, where two Creature components share this one motor.
    public void Release(Creature owner)
    {
        if (intentOwner != owner) return;
        intentOwner = null;
        requestedSpeed = 0f;
        separationIgnore = null;
        hasTarget = false;
    }

    // Reveal particles hide this tiny momentum correction. Momentum already aimed at
    // the player is retained; sideways or away-from-player glide is removed so an
    // imposter visibly commits to the pounce instead of continuing its disguise path.
    public void RedirectForReveal(Creature owner, CreatureData data, Vector2 target)
    {
        if (owner == null || data == null || body == null) return;

        Vector2 towardTarget = target - body.position;
        if (towardTarget.sqrMagnitude < 0.0001f) return;

        towardTarget.Normalize();
        float retainedForwardSpeed = Mathf.Clamp(
            Vector2.Dot(body.linearVelocity, towardTarget),
            0f,
            data.chaseSpeed);

        body.linearVelocity = towardTarget * retainedForwardSpeed;
        lastHeading = towardTarget;
    }

    private void FixedUpdate()
    {
        if (body == null) return;

        if (intentOwner == null || !intentOwner.isActiveAndEnabled || settings == null)
        {
            EaseVelocityTo(Vector2.zero, settings != null ? settings.deceleration : 10f, Time.fixedDeltaTime);
            UpdateFacing(body.linearVelocity);
            return;
        }

        GetStyleMultipliers(
            movementStyle,
            out float accelerationMultiplier,
            out float decelerationMultiplier,
            out float turnMultiplier,
            out float separationMultiplier,
            out float avoidanceMultiplier);

        Vector2 desiredDirection = requestedDirection;
        float desiredSpeed = requestedSpeed;

        if (hasTarget)
        {
            Vector2 toTarget = requestedTarget - body.position;
            float distance = toTarget.magnitude;
            desiredDirection = distance > 0.0001f ? toTarget / distance : Vector2.zero;

            if (distance <= stopRadius)
            {
                desiredSpeed = 0f;
            }
            else if (distance < slowRadius)
            {
                float arrival = Mathf.InverseLerp(stopRadius, slowRadius, distance);
                arrival = arrival * arrival * (3f - 2f * arrival);
                desiredSpeed *= arrival;

                float brakingDistance = Mathf.Max(0f, distance - stopRadius);
                float brakingLimit = Mathf.Sqrt(2f * Mathf.Max(0.1f, settings.deceleration) * brakingDistance);
                desiredSpeed = Mathf.Min(desiredSpeed, brakingLimit);
            }

            if (movementStyle == CreatureMovementStyle.Wander && desiredDirection.sqrMagnitude > 0f)
            {
                float noise = Mathf.PerlinNoise(wanderNoiseOffset, Time.time * settings.wanderNoiseFrequency) * 2f - 1f;
                float arrivalWeight = Mathf.InverseLerp(stopRadius, slowRadius, distance);
                desiredDirection = Rotate(desiredDirection, noise * settings.wanderNoiseAngle * arrivalWeight);
            }
        }

        if (desiredSpeed > 0.01f && desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            Vector2 separation = CalculateSeparation(settings.separationRadius);
            if (separation.sqrMagnitude > 0.0001f)
            {
                desiredDirection = (desiredDirection +
                    separation * settings.separationWeight * separationMultiplier).normalized;
            }

            Vector2 avoidance = CalculateObstacleAvoidance(desiredDirection, desiredSpeed);
            if (avoidance.sqrMagnitude > 0.0001f)
            {
                desiredDirection = (desiredDirection +
                    avoidance * settings.obstacleAvoidanceWeight * avoidanceMultiplier).normalized;
            }

            desiredDirection = LimitTurn(desiredDirection, settings.turnSpeed * turnMultiplier, Time.fixedDeltaTime);
        }

        Vector2 targetVelocity = desiredDirection * desiredSpeed;
        float currentSpeed = body.linearVelocity.magnitude;
        float changeRate = desiredSpeed >= currentSpeed
            ? settings.acceleration * accelerationMultiplier
            : settings.deceleration * decelerationMultiplier;

        EaseVelocityTo(targetVelocity, Mathf.Max(0.1f, changeRate), Time.fixedDeltaTime);

        if (body.linearVelocity.sqrMagnitude > 0.0025f)
            lastHeading = body.linearVelocity.normalized;

        UpdateFacing(body.linearVelocity);
    }

    private Vector2 CalculateSeparation(float radius)
    {
        if (radius <= 0f) return Vector2.zero;

        float radiusSquared = radius * radius;
        Vector2 separation = Vector2.zero;

        foreach (CreatureMotor2D other in ActiveMotors)
        {
            if (other == null || other == this || other.body == null || !other.gameObject.activeInHierarchy)
                continue;
            if (separationIgnore != null && other.transform == separationIgnore)
                continue;

            Vector2 offset = body.position - other.body.position;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared >= radiusSquared) continue;

            if (distanceSquared < 0.0001f)
            {
                separation += Rotate(Vector2.right, wanderNoiseOffset % 360f);
                continue;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            float strength = 1f - distance / radius;
            separation += offset / distance * strength * strength;
        }

        return Vector2.ClampMagnitude(separation, 1f);
    }

    private Vector2 CalculateObstacleAvoidance(Vector2 direction, float speed)
    {
        if (bodyCollider == null || settings.obstacleLookAhead <= 0f) return Vector2.zero;

        float lookAhead = settings.obstacleLookAhead + speed * 0.12f;
        int hitCount = bodyCollider.Cast(direction, obstacleFilter, obstacleHits, lookAhead, true);
        RaycastHit2D nearestHit = default;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
                continue;

            Rigidbody2D hitBody = hit.rigidbody;
            if (hitBody != null && hitBody != body && hitBody.bodyType != RigidbodyType2D.Static)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestHit = hit;
                nearestDistance = hit.distance;
            }
        }

        if (nearestHit.collider == null) return Vector2.zero;

        Vector2 tangent = new Vector2(-nearestHit.normal.y, nearestHit.normal.x);
        float tangentAlignment = Vector2.Dot(tangent, direction);
        if (Mathf.Abs(tangentAlignment) < 0.05f)
            tangent *= avoidanceSide;
        else if (tangentAlignment < 0f)
            tangent = -tangent;

        float urgency = 1f - Mathf.Clamp01(nearestDistance / lookAhead);
        Vector2 steer = nearestHit.normal * 0.65f + tangent * 0.9f;
        return steer.normalized * urgency;
    }

    private Vector2 LimitTurn(Vector2 desiredDirection, float degreesPerSecond, float deltaTime)
    {
        // There is no visible momentum to preserve near rest. Taking the first intent
        // immediately avoids a hard-coded heading bias and makes emergency flee react
        // away from danger on its very first physics step.
        if (body.linearVelocity.sqrMagnitude <= 0.01f)
        {
            lastHeading = desiredDirection;
            return desiredDirection;
        }

        Vector2 from = body.linearVelocity.normalized;

        float angle = Vector2.SignedAngle(from, desiredDirection);
        float limitedAngle = Mathf.Clamp(angle, -degreesPerSecond * deltaTime, degreesPerSecond * deltaTime);
        return Rotate(from, limitedAngle).normalized;
    }

    private void EaseVelocityTo(Vector2 targetVelocity, float changeRate, float deltaTime)
    {
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, changeRate * deltaTime);
    }

    private void UpdateFacing(Vector2 velocity)
    {
        if (spriteRenderer == null || settings == null || Mathf.Abs(velocity.x) < settings.spriteFlipThreshold)
            return;

        bool movingLeft = velocity.x < 0f;
        spriteRenderer.flipX = settings.spriteFacesRightByDefault ? movingLeft : !movingLeft;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private static void GetStyleMultipliers(
        CreatureMovementStyle style,
        out float acceleration,
        out float deceleration,
        out float turn,
        out float separation,
        out float avoidance)
    {
        switch (style)
        {
            case CreatureMovementStyle.Flee:
                acceleration = 1.5f; deceleration = 1.2f; turn = 1.35f; separation = 0.65f; avoidance = 1.15f;
                break;
            case CreatureMovementStyle.Chase:
                acceleration = 1.35f; deceleration = 1.3f; turn = 1.2f; separation = 0.25f; avoidance = 1.1f;
                break;
            case CreatureMovementStyle.Search:
                acceleration = 0.85f; deceleration = 1.1f; turn = 0.85f; separation = 0.7f; avoidance = 1f;
                break;
            case CreatureMovementStyle.SeekFood:
                acceleration = 0.8f; deceleration = 1f; turn = 0.8f; separation = 0.8f; avoidance = 1f;
                break;
            default:
                acceleration = 0.75f; deceleration = 0.85f; turn = 0.75f; separation = 1f; avoidance = 1f;
                break;
        }
    }
}
