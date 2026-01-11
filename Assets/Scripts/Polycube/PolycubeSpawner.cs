using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PolycubeSpawner : MonoBehaviour
{

    public enum SpawnMode
    {
        Predefined = 0,
        RandomProcedural = 1
    }

    private GridOccupancy occupancy;
    private Transform polycubeContainer;
    private GameObject unitCubePrefab;

    [Tooltip("Which shape source to use.")]
    public SpawnMode spawnMode = SpawnMode.Predefined;

    [Header("Spawn Count (Randomized)")]
    [Min(5)]
    public int minSpawnCount = 5;

    [Min(5)]
    public int maxSpawnCount = 10;

    [Header("Placement Settings")]
    [Tooltip("If true, all shapes spawn with pivotCell.y = 0.")]
    public bool spawnOnGroundOnly = true;

    [Min(10)]
    public int maxAttemptsPerShape = 500;

    private bool spawnedOnce;


    #region Predefined Definition Generation Inspector

    [Header("Predefined Generation")]
    [SerializeField] private List<PolycubeDefinition> definitions = new List<PolycubeDefinition>();
    public bool weightBySize = true;  // weighted random selection
    [Min(0.1f)]
    public float weightExponent = 2.0f;

    #endregion

    #region Procedural Definition Generation Inspector


    [Header("Procedural Generation")]
    [Range(1, 9)]
    public int randomMinCubes = 1;

    [Range(1, 9)]
    public int randomMaxCubes = 9;

    [Min(10)]
    public int maxRandomShapeBuildAttempts = 200;

    [Min(1)]
    public int candidatesPerShape = 20;

    [Range(0f, 10f)]
    public float verticalityWeight = 4f;

    public int[] sizeWeights = new int[10]
    {
    0,  // unused
    1,  // 1 cube
    1,  // 2 cubes
    1,  // 3 cubes
    2,  // 4 cubes
    7,  // 5 cubes
    9,  // 6 cubes
    10, // 7 cubes
    12, // 8 cubes
    12  // 9 cubes
    };

    #endregion



    private void Start()
    {
        occupancy = WorldManager.Instance.occupancy;
        polycubeContainer = WorldManager.Instance.polyCubeContainer;
        unitCubePrefab = WorldManager.Instance.unitCubePrefab;

        // just to be safe
        if (occupancy == null)
        {
            occupancy = GetComponent<GridOccupancy>();
        }

        if (polycubeContainer == null)
        {
            Transform child = transform.Find("PolyCubeContainer");
            if (child != null)
            {
                polycubeContainer = child;
            }
            else
            {
                polycubeContainer = transform;
            }
        }

        if (spawnedOnce)
        {
            return;
        }

        spawnedOnce = true;
        SpawnInitialSet();
    }


    public void Rebuild(SpawnMode mode, int minCount, int maxCount)
    {
        spawnMode = mode;

        if (minCount < 0) minCount = 0;
        if (maxCount < minCount) maxCount = minCount;

        minSpawnCount = minCount;
        maxSpawnCount = maxCount;

        SpawnInitialSet();
    }



    public void SpawnInitialSet()
    {
        spawnedOnce = true;

        if (occupancy == null)
        {
            Debug.LogError("please attach greidoccupancy component");
            return;
        }

        if (spawnMode == SpawnMode.Predefined)
        {
            if (definitions == null || definitions.Count == 0)
            {
                Debug.LogError("Spawner is in Predefined mode but has no definitions");
                return;
            }
        }

        minSpawnCount = Mathf.Max(5, minSpawnCount);
        maxSpawnCount = Mathf.Max(minSpawnCount, maxSpawnCount);

        int target = Random.Range(minSpawnCount, maxSpawnCount + 1);

        ClearContainerChildren();
        occupancy.ClearAll();

        int placedCount = 0;
        int globalAttempts = 0;
        int globalAttemptCap = target * 50;

        while (placedCount < target && globalAttempts < globalAttemptCap)
        {
            globalAttempts++;
            PolycubeDefinition def;

            if (spawnMode == SpawnMode.Predefined)
            {
                def = PickDefinition();
                if (def == null)
                {
                    continue;
                }
            }
            else
            {
                def = CreateRandomRuntimeDefinition(placedCount);
                if (def == null)
                {
                    continue;
                }
            }

            Vector3Int pivot;
            if (!TryFindPivot(def, out pivot))
            {
                continue;
            }

            Color c = GenerateUniqueColor(placedCount);

            string defId = "Unknown";
            int defSize = 0;

            if (def != null)
            {
                defId = def.GetId();
                defSize = def.GetSize();
            }

            GameObject parent = new GameObject("Polycube_" + (placedCount + 1).ToString("00") + "_" + defId + "_N" + defSize);
            parent.transform.SetParent(polycubeContainer, false);

            PolycubeInstance instance = parent.AddComponent<PolycubeInstance>();
            instance.Build(def, unitCubePrefab, c);
            instance.ApplyState(pivot, Quaternion.identity);

            occupancy.RegisterPlaced(def, pivot, Quaternion.identity);

            placedCount++;
        }
    }

    public PolycubeDefinition GetDefinitionById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            PolycubeDefinition d = definitions[i];
            if (d == null)
            {
                continue;
            }

            if (d.GetId() == id)
            {
                return d;
            }
        }

        return null;
    }


    private bool TryFindPivot(PolycubeDefinition def, out Vector3Int pivotCell)
    {
        Vector3Int size = GetWorldSize();
        Quaternion rotation = Quaternion.identity;

        for (int attempt = 0; attempt < maxAttemptsPerShape; attempt++)
        {
            int x = Random.Range(0, size.x);
            int z = Random.Range(0, size.z);

            int y;
            if (spawnOnGroundOnly)
            {
                y = 0;
            }
            else
            {
                y = Random.Range(0, size.y);
            }

            Vector3Int pivot = new Vector3Int(x, y, z);

            if (occupancy.CanPlace(def, pivot, rotation))
            {
                pivotCell = pivot;
                return true;
            }
        }

        pivotCell = Vector3Int.zero;
        return false;
    }

    private Vector3Int GetWorldSize()
    {
        if (WorldManager.Instance == null)
        {
            return new Vector3Int(32, 16, 32);
        }

        return WorldManager.Instance.worldSize;
    }


    #region Predefined Definition Selection

    private PolycubeDefinition PickDefinition()
    {
        if (!weightBySize)
        {
            return definitions[Random.Range(0, definitions.Count)];
        }

        float total = 0f;
        for (int i = 0; i < definitions.Count; i++)
        {
            PolycubeDefinition d = definitions[i];
            if (d == null)
            {
                continue;
            }

            int size = d.GetSize();
            total += Mathf.Pow(Mathf.Max(1, size), weightExponent);
        }

        if (total <= 0.0001f)
        {
            return definitions[Random.Range(0, definitions.Count)];
        }

        float r = Random.value * total;
        float acc = 0f;

        for (int i = 0; i < definitions.Count; i++)
        {
            PolycubeDefinition d = definitions[i];
            if (d == null)
            {
                continue;
            }

            int size = d.GetSize();
            acc += Mathf.Pow(Mathf.Max(1, size), weightExponent);

            if (r <= acc)
            {
                return d;
            }
        }

        return definitions[definitions.Count - 1];
    }

    #endregion

    #region Procedural Definition Generation

    /*
    I had the procedural idea and pseudo idea, but took help of ai agent to generate code logic for it - 
    Decide how many cubes the shape should have (size selection), 
    Generate several connected random shapes of that size,
    Score each shape based on vertical height, 
    Pick the best-scoring shape,
    Normalize it so offsets start at (0,0,0),
    Wrap it in a runtime PolycubeDefinition.
    */

    private PolycubeDefinition CreateRandomRuntimeDefinition(int index)
    {
        int minCount = Mathf.Clamp(randomMinCubes, 1, 9);
        int maxCount = Mathf.Clamp(randomMaxCubes, 1, 9);

        if (maxCount < minCount)
        {
            maxCount = minCount;
        }

        int cubeCount = PickWeightedCubeCount(minCount, maxCount);//choose how many cubes this shape should have

        List<Vector3Int> bestCandidate = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < candidatesPerShape; i++)         //generate multiple candidates and keep the best one

        {
            List<Vector3Int> candidate = GenerateConnectedShape(cubeCount);
            if (candidate == null)
            {
                continue;
            }

            int score = ScoreVerticality(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate == null)
        {
            return null;
        }

        PolycubeDefinition runtimeDef = ScriptableObject.CreateInstance<PolycubeDefinition>(); //create a runtime definition so procedural and predefined
        runtimeDef.InitializeRuntime(
            "R_" + (index + 1).ToString("00") + "_N" + cubeCount,
            bestCandidate
        );

        return runtimeDef;
    }
    private List<Vector3Int> GenerateConnectedShape(int targetCount) //  this func builds a connected shape by growing from an existing cube.each new cube is added next to an already existing cube.
    {
        if (targetCount <= 0)
        {
            return null;
        }

        HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        List<Vector3Int> cells = new List<Vector3Int>();

        occupied.Add(Vector3Int.zero);
        cells.Add(Vector3Int.zero);

        Vector3Int[] directions =
        {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.forward,
        Vector3Int.back,
        Vector3Int.up,
        Vector3Int.down
    };

        while (cells.Count < targetCount)
        {
            Vector3Int baseCell = cells[Random.Range(0, cells.Count)];

            Vector3Int step = directions[Random.Range(0, directions.Length)];
            Vector3Int nextCell = baseCell + step;

            if (occupied.Contains(nextCell)) // Reject duplicates

            {
                continue;
            }

            if (nextCell.y < 0)
            {
                continue;
            }

            occupied.Add(nextCell);// nrw cube accepted
            cells.Add(nextCell);
        }

        NormalizeToOrigin(cells); // this is to normalize the shape so the minimum corner is at (0,0,0)

        return cells;
    }

    private void NormalizeToOrigin(List<Vector3Int> cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int minZ = int.MaxValue;

        for (int i = 0; i < cells.Count; i++)         //find the minimum extents of the shape's cubes grid offsets

        {
            Vector3Int v = cells[i];

            if (v.x < minX) minX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.z < minZ) minZ = v.z;
        }

        for (int i = 0; i < cells.Count; i++)        // shift every cell so the shape starts at (0,0,0)

        {
            Vector3Int v = cells[i];
            cells[i] = new Vector3Int(
                v.x - minX,
                v.y - minY,
                v.z - minZ
            );
        }
    }

    private int ScoreVerticality(List<Vector3Int> cells) //     Scores a shape based on how tall it is.

    {
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        for (int i = 0; i < cells.Count; i++)
        {
            int y = cells[i].y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        int height = (maxY - minY) + 1;

        int score = cells.Count * 10;

        // Vertical bias
        score += Mathf.RoundToInt(height * verticalityWeight * 10f);

        return score;
    }

    private int PickWeightedCubeCount(int minCount, int maxCount) // this use weighted random selection so larger shapes can be preferred
    {
        if (sizeWeights == null || sizeWeights.Length < 10)
        {
            return Random.Range(minCount, maxCount + 1);
        }

        int totalWeight = 0;
        for (int n = minCount; n <= maxCount; n++)
        {
            totalWeight += Mathf.Max(0, sizeWeights[n]);
        }

        if (totalWeight <= 0)
        {
            return Random.Range(minCount, maxCount + 1);
        }

        int roll = Random.Range(0, totalWeight);
        int accumulated = 0;

        for (int n = minCount; n <= maxCount; n++)
        {
            accumulated += Mathf.Max(0, sizeWeights[n]);
            if (roll < accumulated)
            {
                return n;
            }
        }

        return maxCount;
    }

    #endregion


    private static Color GenerateUniqueColor(int index)
    {
        float hue = Mathf.Repeat(index * 0.61803398875f, 1f); // golden ratio for a spread color palette
        return Color.HSVToRGB(hue, 0.75f, 0.95f);
    }

    private void ClearContainerChildren()
    {
        if (polycubeContainer == null)
        {
            return;
        }

        for (int i = polycubeContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(polycubeContainer.GetChild(i).gameObject);
        }
    }

}
