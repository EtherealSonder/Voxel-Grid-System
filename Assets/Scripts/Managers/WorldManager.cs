using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("World Settings")]
    public Vector3Int worldSize = new Vector3Int(32, 16, 32);
    public bool isGridVisible = true;

    public Transform polyCubeContainer;
    public GameObject unitCubePrefab;
    public Camera playerCamera;

    [Header("References")]
    public GridVisualizer gridVisualizer;
    public GroundPlane groundPlane;
    public GridOccupancy occupancy;
    public InteractionManager interactionManager;
    public PolycubeSpawner polycubeSpawner;
    public SaveLoadManager saveLoadManager;
    public InteractionFeedbackUI interactionFeedbackUI;
    public FlyingCamera flyingCamera;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CheckReferences();
        ApplyWorldSettings();
    }

    private void CheckReferences()
    {
        if (gridVisualizer == null)
            gridVisualizer = GetComponentInChildren<GridVisualizer>();

        if (groundPlane == null)
            groundPlane = GetComponentInChildren<GroundPlane>();
    }

    public void ApplyWorldSettings()
    {
        if (gridVisualizer != null)
        {
            gridVisualizer.BuildGrid();
        }

        if (groundPlane != null)
        {
            groundPlane.FitToWorld(worldSize);
        }
    }

}
