using System.Collections.Generic;
using UnityEngine;

public class PolycubeInstance : MonoBehaviour
{

    [SerializeField] private PolycubeDefinition definition;
    [SerializeField] private Vector3Int pivotCell;
    [SerializeField] private Color shapeColor = Color.white;

    private readonly List<Transform> spawnedCubes = new List<Transform>();

    private MaterialPropertyBlock materialProperty;

    public PolycubeDefinition GetDefinition()
    {
        return definition;
    }

    public Vector3Int GetPivotCell()
    {
        return pivotCell;
    }

    public Color GetColor()
    {
        return shapeColor;
    }


    public void Build(PolycubeDefinition def, GameObject unitCubePrefab, Color color)
    {
        definition = def;
        shapeColor = color;

        if (definition == null)
        {
            Debug.LogError("PolycubeInstance.Build called with null definition.");
            return;
        }

        ClearSpawnedChildren();
        EnsureMPB();
        ApplyMPBColor(shapeColor);

        SpawnChildrenFromDefinition(unitCubePrefab);
    }

    public void ApplyState(Vector3Int newPivotCell, Quaternion newRotation)
    {
        pivotCell = newPivotCell;

        transform.position = CellToWorld(pivotCell);
        transform.rotation = newRotation;
    }

    public void ApplyColor(Color newColor)
    {
        shapeColor = newColor;

        EnsureMPB();
        ApplyMPBColor(shapeColor);

        for (int i = 0; i < spawnedCubes.Count; i++)
        {
            Transform t = spawnedCubes[i];
            if (t == null) continue;

            Renderer r = t.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.SetPropertyBlock(materialProperty);
            }
        }
    }

    private void SpawnChildrenFromDefinition(GameObject unitCubePrefab)
    {
        IReadOnlyList<Vector3Int> cells = definition.GetCells();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int o = cells[i];
            GameObject cube = CreateChildCube(unitCubePrefab, o);

            spawnedCubes.Add(cube.transform);

            Renderer r = cube.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.SetPropertyBlock(materialProperty);
            }
        }
    }

    private GameObject CreateChildCube(GameObject unitCubePrefab, Vector3Int localOffset)
    {
        GameObject cube;

        cube = Instantiate(unitCubePrefab, transform);

        cube.name = "Cell_" + localOffset.x + "_" + localOffset.y + "_" + localOffset.z;

        Transform t = cube.transform;
        t.localPosition = new Vector3(localOffset.x, localOffset.y, localOffset.z);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        return cube;
    }

    private void ClearSpawnedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        spawnedCubes.Clear();
    }

    private void EnsureMPB()
    {
        if (materialProperty == null)
        {
            materialProperty = new MaterialPropertyBlock();
        }
        else
        {
            materialProperty.Clear();
        }
    }

    private void ApplyMPBColor(Color color)
    {
        materialProperty.SetColor("_BaseColor", color);
        materialProperty.SetColor("_Color", color);
    }

    private static Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
    }
    public void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enabled;
        }
    }

}
