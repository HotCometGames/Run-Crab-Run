using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Dead Zone")]
    [SerializeField] private float deadZoneWidth = 1f;
    [SerializeField] private float deadZoneHeight = 1f;

    private Vector3 offset;

    private void Start()
    {
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

        // Keep the camera's starting offset
        offset = transform.position - player.position;
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
        transform.position = cameraPosition;
    }
}
