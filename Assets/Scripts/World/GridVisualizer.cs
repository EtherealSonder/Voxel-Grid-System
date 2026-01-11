using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GridVisualizer : MonoBehaviour
{


    public float yOffset = 0.02f;// i added it so the grid line doesnt overlap with plane ground


    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    [SerializeField] private Mesh lineMesh;
    private Vector3Int WorldSize
    {
        get
        {
            if (WorldManager.Instance == null)
                return Vector3Int.zero;

            return WorldManager.Instance.worldSize;
        }
    }

    private bool IsGridVisible
    {
        get
        {
            if (WorldManager.Instance == null)
                return true;

            return WorldManager.Instance.isGridVisible;
        }
    }

    public Bounds LocalBounds
    {
        get
        {
            Vector3Int worldSize = WorldSize;

            Vector3 size = new Vector3(worldSize.x, worldSize.y, worldSize.z);
            Vector3 center = size * 0.5f;
            return new Bounds(center, size);
        }
    }

    private void Awake()
    {
        InitializeComponents();
        InitializeMesh();
        ApplyVisibility();
        BuildGrid();
    }


    void InitializeComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    void InitializeMesh()
    {
        if (lineMesh != null)
            return;


        lineMesh = new Mesh();
        lineMesh.indexFormat = IndexFormat.UInt32;

        if (meshFilter != null)
            meshFilter.sharedMesh = lineMesh;
    }

    void ApplyVisibility()
    {
        if (meshRenderer != null)
            meshRenderer.enabled = IsGridVisible;
    }

    public void BuildGrid()
    {
        if (meshFilter == null)
            return;

        Vector3Int worldSize = WorldSize;

        if (worldSize.x <= 0 || worldSize.y <= 0 || worldSize.z <= 0)
            return;

        InitializeMesh();

        var vertices = new List<Vector3>();
        var indices = new List<int>();

        void AddLine(Vector3 a, Vector3 b)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            indices.Add(start);
            indices.Add(start + 1);
        }

        float maxX = worldSize.x;
        float maxY = worldSize.y;
        float maxZ = worldSize.z;
        DrawBounds(maxX, maxY, maxZ);
        DrawFloorGrid(maxX, maxZ, yOffset);

        void DrawBounds(float xMax, float yMax, float zMax)
        {
            Vector3 c000 = new Vector3(0f, 0f, 0f);
            Vector3 c100 = new Vector3(xMax, 0f, 0f);
            Vector3 c010 = new Vector3(0f, yMax, 0f);
            Vector3 c110 = new Vector3(xMax, yMax, 0f);

            Vector3 c001 = new Vector3(0f, 0f, zMax);
            Vector3 c101 = new Vector3(xMax, 0f, zMax);
            Vector3 c011 = new Vector3(0f, yMax, zMax);
            Vector3 c111 = new Vector3(xMax, yMax, zMax);

            // Bottom 
            AddLine(c000, c100);
            AddLine(c100, c101);
            AddLine(c101, c001);
            AddLine(c001, c000);

            // Top 
            AddLine(c010, c110);
            AddLine(c110, c111);
            AddLine(c111, c011);
            AddLine(c011, c010);

            // sides
            AddLine(c000, c010);
            AddLine(c100, c110);
            AddLine(c101, c111);
            AddLine(c001, c011);
        }

        void DrawFloorGrid(float xMax, float zMax, float y)
        {
            for (int x = 0; x <= worldSize.x; x++)
                AddLine(new Vector3(x, y, 0f), new Vector3(x, y, zMax));

            for (int z = 0; z <= worldSize.z; z++)
                AddLine(new Vector3(0f, y, z), new Vector3(xMax, y, z));
        }

        lineMesh.Clear();
        lineMesh.SetVertices(vertices);
        lineMesh.SetIndices(indices, MeshTopology.Lines, 0);
        lineMesh.RecalculateBounds();

        meshFilter.sharedMesh = lineMesh;
    }

}
