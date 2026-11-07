using UnityEngine;

public class GridGizmos : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private Vector2Int cellCount = new(10, 10);
    [SerializeField] private Vector2 mapSize = new(10f, 10f);

    [Header("Gizmos")]
    [SerializeField] private Color gridColor = Color.green;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gridColor;

        Vector3 origin = transform.position;

        float cellSizeX = mapSize.x / cellCount.x;
        float cellSizeZ = mapSize.y / cellCount.y;

        Vector3 start = origin - new Vector3(
            mapSize.x * 0.5f,
            0f,
            mapSize.y * 0.5f
        );

        // Линии вдоль Z
        for (int x = 0; x <= cellCount.x; x++)
        {
            float offset = x * cellSizeX;

            Vector3 pointA = start + new Vector3(offset, 0f, 0f);
            Vector3 pointB = pointA + new Vector3(0f, 0f, mapSize.y);

            Gizmos.DrawLine(pointA, pointB);
        }

        // Линии вдоль X
        for (int z = 0; z <= cellCount.y; z++)
        {
            float offset = z * cellSizeZ;

            Vector3 pointA = start + new Vector3(0f, 0f, offset);
            Vector3 pointB = pointA + new Vector3(mapSize.x, 0f, 0f);

            Gizmos.DrawLine(pointA, pointB);
        }
    }
}
