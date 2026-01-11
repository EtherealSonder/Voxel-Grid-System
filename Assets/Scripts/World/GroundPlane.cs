using UnityEngine;

public class GroundPlane : MonoBehaviour
{
    [Header("Plane Settings")]
    [SerializeField] private float y = 0f;

    public void FitToWorld(Vector3Int worldSize)
    {
        if (worldSize.x <= 0 || worldSize.z <= 0)
            return;

        transform.position = new Vector3(worldSize.x * 0.5f, y, worldSize.z * 0.5f);

        transform.localScale = new Vector3( worldSize.x / 10f, 1f, worldSize.z / 10f );
    }
}
