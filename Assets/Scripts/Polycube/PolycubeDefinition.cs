using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Polycubes/Polycube Definition", fileName = "PolycubeDefinition")]
public class PolycubeDefinition : ScriptableObject
{
    private const string DefaultAuthoredId = "P_Unknown";
    private const string DefaultRuntimeId = "R_Unknown";

    [Header("Identity")]
    [SerializeField] private string shapeId = DefaultAuthoredId;

    [Header("Cells (local offsets, 1 cell = 1 unit)")]
    [Tooltip("Each entry is a cube at this local grid offset relative to the pivot (0,0,0).")]
    [SerializeField] private List<Vector3Int> cells = new();


    public string GetId()
    {
        return shapeId;
    }

    public int GetSize()
    {
        return cells.Count;
    }

    public IReadOnlyList<Vector3Int> GetCells()
    {
        return cells;
    }


    public void InitializeRuntime(string runtimeId, List<Vector3Int> runtimeCells) // for procadural shapes
    {
        if (string.IsNullOrWhiteSpace(runtimeId))
            runtimeId = DefaultRuntimeId;

        shapeId = runtimeId;

        cells.Clear();

        if (runtimeCells != null && runtimeCells.Count > 0)
            cells.AddRange(runtimeCells);

        EnforceCellInvariants();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure authored assets always have a usable id
        if (string.IsNullOrWhiteSpace(shapeId))
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                shapeId = name;
            }
            else
            {
                shapeId = DefaultAuthoredId;
            }
        }

        if (cells == null)
        {
            cells = new List<Vector3Int>();
        }
    }
#endif

    private void EnforceCellInvariants()
    {
        if (cells == null)
            cells = new List<Vector3Int>();

        if (cells.Count == 0)
            cells.Add(Vector3Int.zero); // atleast one cell must be there

        if (!cells.Contains(Vector3Int.zero))
            cells.Insert(0, Vector3Int.zero);// pivot is (0,0,0) always

        var seen = new HashSet<Vector3Int>();
        for (int i = cells.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(cells[i]))
                cells.RemoveAt(i);
        }
    }

    public void EnforceInvariantsNow()
    {
        EnforceCellInvariants();
    }
}
