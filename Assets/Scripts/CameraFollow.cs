using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Dead Zone")]
    [SerializeField] private float deadZoneWidth = 1f;
    [SerializeField] private float deadZoneHeight = 1f;

    [Header("World Bounds")]
    [SerializeField] private bool constrainToBounds = true;
    [SerializeField] private Vector2 minimumBounds = new Vector2(-13.5f, -8.5f);
    [SerializeField] private Vector2 maximumBounds = new Vector2(13.5f, 8.5f);

    private Vector3 offset;
    private Camera cameraComponent;

    private void Start()
    {
        cameraComponent = GetComponent<Camera>();

        // Find player automatically if one wasn't assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Stop if we couldn't find the player
        if (player == null)
        {
            Debug.LogError("CameraFollow: No Player found! Assign the Player in the Inspector or give the Player the 'Player' tag.");
            enabled = false;
            return;
        }

        // Center the player while preserving the camera's depth.
        offset = new Vector3(0f, 0f, transform.position.z - player.position.z);
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 cameraPosition = transform.position;
        Vector3 desiredPosition = player.position + offset;

        // Calculate distance from camera to player
        float dx = desiredPosition.x - cameraPosition.x;
        float dy = desiredPosition.y - cameraPosition.y;

        // Horizontal dead zone
        if (Mathf.Abs(dx) > deadZoneWidth)
        {
            cameraPosition.x = desiredPosition.x -
                Mathf.Sign(dx) * deadZoneWidth;
        }

        // Vertical dead zone
        if (Mathf.Abs(dy) > deadZoneHeight)
        {
            cameraPosition.y = desiredPosition.y -
                Mathf.Sign(dy) * deadZoneHeight;
        }

        // Keep the camera's original Z position
        if (constrainToBounds && cameraComponent != null && cameraComponent.orthographic)
        {
            Vector2 activeMinimum = WorldBoundary.Instance != null ? WorldBoundary.Instance.WorldMinimum : minimumBounds;
            Vector2 activeMaximum = WorldBoundary.Instance != null ? WorldBoundary.Instance.WorldMaximum : maximumBounds;
            float halfHeight = cameraComponent.orthographicSize;
            float halfWidth = halfHeight * cameraComponent.aspect;
            cameraPosition.x = ClampAxis(cameraPosition.x, activeMinimum.x + halfWidth, activeMaximum.x - halfWidth,
                (activeMinimum.x + activeMaximum.x) * 0.5f);
            cameraPosition.y = ClampAxis(cameraPosition.y, activeMinimum.y + halfHeight, activeMaximum.y - halfHeight,
                (activeMinimum.y + activeMaximum.y) * 0.5f);
        }

        transform.position = cameraPosition;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float fallbackCenter)
    {
        return minimum <= maximum ? Mathf.Clamp(value, minimum, maximum) : fallbackCenter;
    }
}
