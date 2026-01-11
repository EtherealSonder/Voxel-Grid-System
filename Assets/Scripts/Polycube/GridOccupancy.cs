using System.Collections.Generic;
using UnityEngine;

public class GridOccupancy : MonoBehaviour
{
    private readonly HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();

    public void ClearAll()
    {
        occupied.Clear();
    }

    public bool IsOccupied(Vector3Int cell)
    {
        return occupied.Contains(cell);
    }

    public List<Vector3Int> GetCells(PolycubeDefinition def, Vector3Int pivotCell, Quaternion rotation)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        if (def == null)
        {
            return result;
        }

        IReadOnlyList<Vector3Int> cells = def.GetCells();
        result.Capacity = Mathf.Max(result.Capacity, cells.Count);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int ro = RotateOffsetToGrid(cells[i], rotation);
            result.Add(pivotCell + ro);
        }

        return result;
    }

    public bool CanPlace(PolycubeDefinition def, Vector3Int pivotCell, Quaternion rotation)
    {
        if (def == null)
        {
            return false;
        }

        Vector3Int size = GetWorldSize();

        IReadOnlyList<Vector3Int> cells = def.GetCells();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int ro = RotateOffsetToGrid(cells[i], rotation);
            Vector3Int worldCell = pivotCell + ro;

            if (!InBounds(worldCell, size))
            {
                return false;
            }

            if (occupied.Contains(worldCell))
            {
                return false;
            }
        }

        return true;
    }

    public void RegisterPlaced(PolycubeDefinition def, Vector3Int pivotCell, Quaternion rotation)
    {
        if (def == null)
        {
            return;
        }

        IReadOnlyList<Vector3Int> cells = def.GetCells();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int ro = RotateOffsetToGrid(cells[i], rotation);
            occupied.Add(pivotCell + ro);
        }
    }

    public void UnregisterPlaced(PolycubeDefinition def, Vector3Int pivotCell, Quaternion rotation)
    {
        if (def == null)
        {
            return;
        }

        IReadOnlyList<Vector3Int> cells = def.GetCells();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int ro = RotateOffsetToGrid(cells[i], rotation);
            occupied.Remove(pivotCell + ro);
        }
    }

    public Vector3Int ClampPivotToBounds(PolycubeDefinition def, Vector3Int pivotCell, Quaternion rotation)     // Clamp pivot so the shape stays fully inside bounds.

    {
        if (def == null)
        {
            return pivotCell;
        }

        Vector3Int size = GetWorldSize();

        Vector3Int min;
        Vector3Int max;
        GetRotatedMinMax(def, rotation, out min, out max);

        // Need pivot + min >= 0 and pivot + max <= size - 1
        int minX = -min.x;
        int minY = -min.y;
        int minZ = -min.z;

        int maxX = (size.x - 1) - max.x;
        int maxY = (size.y - 1) - max.y;
        int maxZ = (size.z - 1) - max.z;

        pivotCell.x = Mathf.Clamp(pivotCell.x, minX, maxX);
        pivotCell.y = Mathf.Clamp(pivotCell.y, minY, maxY);
        pivotCell.z = Mathf.Clamp(pivotCell.z, minZ, maxZ);

        return pivotCell;
    }

    private void GetRotatedMinMax(PolycubeDefinition def, Quaternion rotation, out Vector3Int min, out Vector3Int max)
    {
        min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        IReadOnlyList<Vector3Int> cells = def.GetCells();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int ro = RotateOffsetToGrid(cells[i], rotation);
            min = Vector3Int.Min(min, ro);
            max = Vector3Int.Max(max, ro);
        }

        if (cells.Count == 0)
        {
            min = Vector3Int.zero;
            max = Vector3Int.zero;
        }
    }

    private static Vector3Int RotateOffsetToGrid(Vector3Int offset, Quaternion rotation)
    {
        Vector3 v = rotation * (Vector3)offset;

        int rx = Mathf.RoundToInt(v.x);
        int ry = Mathf.RoundToInt(v.y);
        int rz = Mathf.RoundToInt(v.z);

        return new Vector3Int(rx, ry, rz);
    }

    private static bool InBounds(Vector3Int c, Vector3Int size)
    {
        return c.x >= 0 && c.x < size.x &&
               c.y >= 0 && c.y < size.y &&
               c.z >= 0 && c.z < size.z;
    }

    private Vector3Int GetWorldSize()
    {
        if (WorldManager.Instance == null)
        {
            return new Vector3Int(32, 16, 32);
        }

        return WorldManager.Instance.worldSize;
    }
}
