using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Dead Zone")]
    public float deadZoneWidth = 1f;
    public float deadZoneHeight = 1f;

    private Vector3 offset;

    private void Start()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        offset = transform.position - player.position;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 target = player.position + offset;
        Vector3 current = transform.position;

        float dx = target.x - current.x;
        float dy = target.y - current.y;

        float moveX = 0f;
        float moveY = 0f;

        if (Mathf.Abs(dx) > deadZoneWidth)
            moveX = dx - Mathf.Sign(dx) * deadZoneWidth;

        if (Mathf.Abs(dy) > deadZoneHeight)
            moveY = dy - Mathf.Sign(dy) * deadZoneHeight;

        transform.position = current + new Vector3(moveX, moveY, 0f);
    }
}
