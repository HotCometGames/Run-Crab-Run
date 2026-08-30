using UnityEngine;

// Builds four static 2D colliders around the illustrated play area. Keeping the
// bounds on the Environment object makes the map, resources, and walls share one
// coordinate space while still allowing the artist to reposition that root.
[DefaultExecutionOrder(-200)]
public class WorldBoundary : MonoBehaviour
{
    public static WorldBoundary Instance { get; private set; }

    [SerializeField] private Vector2 minimumBounds = new Vector2(-13.5f, -8.5f);
    [SerializeField] private Vector2 maximumBounds = new Vector2(13.5f, 8.5f);
    [SerializeField, Min(0.1f)] private float wallThickness = 0.75f;

    public Vector2 WorldMinimum => transform.TransformPoint(minimumBounds);
    public Vector2 WorldMaximum => transform.TransformPoint(maximumBounds);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{name}: duplicate WorldBoundary disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        BuildWalls();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildWalls()
    {
        Transform existing = transform.Find("Runtime World Walls");
        if (existing != null) return;

        GameObject root = new GameObject("Runtime World Walls");
        root.transform.SetParent(transform, false);

        float width = Mathf.Max(0.1f, maximumBounds.x - minimumBounds.x);
        float height = Mathf.Max(0.1f, maximumBounds.y - minimumBounds.y);
        float centerX = (minimumBounds.x + maximumBounds.x) * 0.5f;
        float centerY = (minimumBounds.y + maximumBounds.y) * 0.5f;

        CreateWall(root.transform, "Left", new Vector2(minimumBounds.x - wallThickness * 0.5f, centerY),
            new Vector2(wallThickness, height + wallThickness * 2f));
        CreateWall(root.transform, "Right", new Vector2(maximumBounds.x + wallThickness * 0.5f, centerY),
            new Vector2(wallThickness, height + wallThickness * 2f));
        CreateWall(root.transform, "Bottom", new Vector2(centerX, minimumBounds.y - wallThickness * 0.5f),
            new Vector2(width + wallThickness * 2f, wallThickness));
        CreateWall(root.transform, "Top", new Vector2(centerX, maximumBounds.y + wallThickness * 0.5f),
            new Vector2(width + wallThickness * 2f, wallThickness));
    }

    private static void CreateWall(Transform parent, string wallName, Vector2 localPosition, Vector2 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.8f);
        Vector2 center = (minimumBounds + maximumBounds) * 0.5f;
        Vector2 size = maximumBounds - minimumBounds;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(center, size);
    }
}
