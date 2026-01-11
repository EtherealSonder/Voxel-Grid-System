using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    [Header("File Settings")]
    [SerializeField] private string filePrefix = "save_slot_";
    [SerializeField] private string fileExtension = ".json";

    private const int SaveVersion = 1;


    private WorldManager world;
    private GridOccupancy occupancy;
    private Transform polycubeContainer;
    private PolycubeSpawner spawner;
    private GameObject unitCubePrefab;


    private void Awake()
    {
        CacheDependencies();
    }

    private void Update()
    {

    }


    #region Slot logic

    public void SaveToSlot(int slot)
    {
        slot = SanitizeSlot(slot);

        if (!EnsureReady())
            return;

        WorldSaveData saveData = BuildSaveDataSnapshot();

        string json = JsonUtility.ToJson(saveData, true);
        string path = GetSavePathForSlot(slot);

        try
        {
            File.WriteAllText(path, json);
            Debug.Log("Saved slot " + slot + " to: " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save slot " + slot + ". Error: " + e.Message);
        }
    }

    public void LoadFromSlot(int slot)
    {
        slot = SanitizeSlot(slot);

        if (!EnsureReady())
            return;

        string path = GetSavePathForSlot(slot);
        if (!File.Exists(path))
        {
            Debug.LogWarning("No save file found for slot " + slot + " at: " + path);
            return;
        }

        WorldSaveData saveData = ReadSaveFile(path);
        if (saveData == null)
            return;

        ApplyWorldSettings(saveData);
        ClearSpawnedPolycubes();
        occupancy.ClearAll();

        RebuildPolycubesFromSave(saveData);

        Debug.Log("Loaded slot " + slot + " from: " + path);
    }

    public bool DoesSlotExist(int slot)
    {
        slot = SanitizeSlot(slot);
        return File.Exists(GetSavePathForSlot(slot));
    }

    public void DeleteSlot(int slot)
    {
        slot = SanitizeSlot(slot);

        string path = GetSavePathForSlot(slot);
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
            Debug.Log("Deleted slot " + slot + ": " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to delete slot " + slot + ". Error: " + e.Message);
        }
    }

    public bool TryGetSlotTimestamp(int slot, out string savedAtLocal)
    {
        slot = SanitizeSlot(slot);
        savedAtLocal = string.Empty;

        string path = GetSavePathForSlot(slot);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            WorldSaveData data = JsonUtility.FromJson<WorldSaveData>(json);

            if (data == null)
                return false;

            if (string.IsNullOrEmpty(data.savedAtLocal))
                return false;

            savedAtLocal = data.savedAtLocal;
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Save Implementation

    private WorldSaveData BuildSaveDataSnapshot()
    {
        WorldSaveData save = new WorldSaveData();
        save.version = SaveVersion;
        save.savedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        save.worldSize = world.worldSize;

        PolycubeInstance[] instances = FindObjectsByType<PolycubeInstance>(FindObjectsSortMode.None);

        for (int i = 0; i < instances.Length; i++)
        {
            PolycubeInstance inst = instances[i];
            if (inst == null)
                continue;

            PolycubeDefinition def = inst.GetDefinition();
            if (def == null)
                continue;

            PolycubeSaveData poly = new PolycubeSaveData();
            poly.definitionId = def.GetId();

            // Avoid spawner-dependent inference as much as possible.
            // With current architecture, we treat "not found in spawner list" as procedural.
            PolycubeDefinition predefined = spawner != null ? spawner.GetDefinitionById(poly.definitionId) : null;
            poly.isProcedural = (predefined == null);

            if (poly.isProcedural)
            {
                IReadOnlyList<Vector3Int> cells = def.GetCells();
                poly.cells = new List<Vector3Int>(cells.Count);
                for (int c = 0; c < cells.Count; c++)
                {
                    poly.cells.Add(cells[c]);
                }
            }

            poly.pivotCell = inst.GetPivotCell();
            poly.rotation = inst.transform.rotation;
            poly.color = inst.GetColor();

            save.polycubes.Add(poly);
        }

        return save;
    }

    #endregion

    #region Load Implementation

    private WorldSaveData ReadSaveFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            WorldSaveData data = JsonUtility.FromJson<WorldSaveData>(json);

            if (data == null)
            {
                Debug.LogError("Save file parsed as null.");
                return null;
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to read save file. Error: " + e.Message);
            return null;
        }
    }

    private void ApplyWorldSettings(WorldSaveData saveData)
    {
        // Your WorldManager exposes worldSize + ApplyWorldSettings().
        world.worldSize = saveData.worldSize;
        world.ApplyWorldSettings();
    }

    private void RebuildPolycubesFromSave(WorldSaveData saveData)
    {
        for (int i = 0; i < saveData.polycubes.Count; i++)
        {
            PolycubeSaveData p = saveData.polycubes[i];
            if (p == null)
                continue;

            PolycubeDefinition def = ResolveDefinitionForLoad(p, i);
            if (def == null)
                continue;

            string name = "Polycube_Load_" + i.ToString("00") + "_" + def.GetId();
            GameObject parent = new GameObject(name);
            parent.transform.SetParent(polycubeContainer, false);

            PolycubeInstance inst = parent.AddComponent<PolycubeInstance>();

            // Build then apply state. Your Build does not accept pivot.
            inst.Build(def, unitCubePrefab, p.color);
            inst.ApplyState(p.pivotCell, p.rotation);

            if (occupancy.CanPlace(def, p.pivotCell, p.rotation))
            {
                occupancy.RegisterPlaced(def, p.pivotCell, p.rotation);
            }
            else
            {
                Debug.LogWarning("Loaded polycube '" + def.GetId() + "' collides or is out of bounds. Visual placed, occupancy skipped.");
            }
        }
    }

    private PolycubeDefinition ResolveDefinitionForLoad(PolycubeSaveData p, int index)
    {
        if (!p.isProcedural)
        {
            PolycubeDefinition predefined = spawner != null ? spawner.GetDefinitionById(p.definitionId) : null;
            if (predefined == null)
            {
                Debug.LogWarning("Missing definition id '" + p.definitionId + "'. Skipping.");
                return null;
            }

            return predefined;
        }

        if (p.cells == null || p.cells.Count == 0)
        {
            Debug.LogWarning("Procedural polycube '" + p.definitionId + "' has no cells in save data. Skipping.");
            return null;
        }

        PolycubeDefinition runtimeDef = ScriptableObject.CreateInstance<PolycubeDefinition>();
        runtimeDef.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        string runtimeId = p.definitionId;
        if (string.IsNullOrEmpty(runtimeId))
            runtimeId = "R_Load_" + index.ToString("00");

        runtimeDef.InitializeRuntime(runtimeId, p.cells);
        return runtimeDef;
    }

    #endregion

    private void ClearSpawnedPolycubes()
    {
        if (polycubeContainer == null)
        {
            Debug.LogError("Polycube container is missing. Aborting clear.");
            return;
        }

        for (int i = polycubeContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = polycubeContainer.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private int SanitizeSlot(int slot)
    {
        if (slot < 1)
            slot = 1;

        return slot;
    }

    private string GetSavePathForSlot(int slot)
    {
        string fileName = filePrefix + slot.ToString("00") + fileExtension;
        return Path.Combine(Application.persistentDataPath, fileName);
    }


    private void CacheDependencies()
    {
        world = WorldManager.Instance;
        if (world == null)
        {
            Debug.LogError("WorldManager.Instance is missing. SaveLoadManager cannot function.");
            return;
        }

        occupancy = world.occupancy;
        spawner = world.polycubeSpawner;
        polycubeContainer = world.polyCubeContainer;
        unitCubePrefab = world.unitCubePrefab;
    }

    private bool EnsureReady()
    {
        if (world == null)
            CacheDependencies();

        if (world == null)
        {
            Debug.LogError("WorldManager.Instance is missing.");
            return false;
        }

        if (occupancy == null || spawner == null || polycubeContainer == null || unitCubePrefab == null)
        {
            Debug.LogError("SaveLoadManager missing dependencies. Check WorldManager references.");
            return false;
        }

        return true;
    }


    [Serializable]
    private class WorldSaveData
    {
        public int version;
        public string savedAtLocal;

        public Vector3Int worldSize;
        public List<PolycubeSaveData> polycubes = new List<PolycubeSaveData>();
    }

    [Serializable]
    private class PolycubeSaveData
    {
        public string definitionId;
        public bool isProcedural;

        public List<Vector3Int> cells;

        public Vector3Int pivotCell;
        public Quaternion rotation;
        public Color color;
    }

}
