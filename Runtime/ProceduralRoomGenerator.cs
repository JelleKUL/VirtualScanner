using System.Collections;
using System.Collections.Generic;
using JelleKUL.Scanner;
using UnityEngine;
using UnityEngine.Scripting;

public class ProceduralRoomGenerator : MonoBehaviour
{
    public PrefabListScriptableObject prefabList;

    [Header("Seed")]
    public bool useFixedSeed = false;
    public int seed = 12345;

    [Header("Scanmanager")]
    public ScanManager scanManager;
    public bool repositionScanManager = true;

    [ButtonBool("GenerateRoom")]
    public bool generateRoom;
    [ButtonBool("RemoveRoom")]
    public bool removeRoom;
    [ButtonBool("RegenerateFurniture")]
    public bool generateFurnitureOnly;
    [ButtonBool("RemoveFurniture")]
    public bool removeFurnitureOnly;

    System.Random rng;

    [SerializeField] int floorWidth;
    [SerializeField] int floorLength;

    private bool roomGenerated = false;
    private Vector3 roomSize;
    private Vector3 roomCenter;

    // Separate parent transforms so furniture can be cleared independently
    private Transform roomParent;
    private Transform furnitureParent;

    private List<Transform> emptyWalls = new List<Transform>();

    void Start() { }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Generates the full room (structure + furniture).</summary>
    [ContextMenu("Generate Room")]
    public void GenerateRoom()
    {
        if (roomGenerated) RemoveRoom();
        InitializeRandom();
        prefabList.ColorMaterials(seed);
        roomParent     = CreateChildParent("Room_Structure");
        furnitureParent = CreateChildParent("Room_Furniture");

        GenerateFloor();
        CalculateRoomSizeAndCenter();
        GenerateWalls();
        GenerateCeiling();
        PlaceAllFurniture();

        if (repositionScanManager) RepositionScanManager();

        print($"Generated room | seed: {seed} | size: ({floorWidth}, {floorLength})");
        roomGenerated = true;
    }

    /// <summary>Removes everything (structure + furniture).</summary>
    [ContextMenu("Remove Room")]
    public void RemoveRoom()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        roomParent      = null;
        furnitureParent = null;
        roomGenerated   = false;
    }

    /// <summary>Replaces only the furniture, keeping walls/floor/ceiling intact.</summary>
    [ContextMenu("Regenerate Furniture")]
    public void RegenerateFurniture()
    {
        if (!roomGenerated)
        {
            Debug.LogWarning("No room exists yet — call GenerateRoom first.");
            return;
        }

        RemoveFurniture();
        InitializeRandom();          // re-seeds so each call gives a new layout
        furnitureParent = CreateChildParent("Room_Furniture");
        PlaceAllFurniture();

        print($"Regenerated furniture | seed: {seed}");
    }

    /// <summary>Destroys only furniture objects, leaving the room structure.</summary>
    [ContextMenu("Remove Furniture")]
    public void RemoveFurniture()
    {
        if (furnitureParent != null)
        {
            Destroy(furnitureParent.gameObject);
            furnitureParent = null;
        }
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    void InitializeRandom()
    {
        if (!useFixedSeed)
            seed = unchecked((int)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        rng = new System.Random(seed);
    }

    Transform CreateChildParent(string parentName)
    {
        GameObject go = new GameObject(parentName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    // -------------------------------------------------------------------------
    // Structure generation  (children of roomParent)
    // -------------------------------------------------------------------------

    void GenerateFloor()
    {
        floorWidth  = rng.Next(prefabList.minRoomSize.x, prefabList.maxRoomSize.x + 1);
        floorLength = rng.Next(prefabList.minRoomSize.y, prefabList.maxRoomSize.y + 1);

        for (int x = 0; x < floorWidth; x++)
            for (int z = 0; z < floorLength; z++)
            {
                Vector3 pos = new Vector3(x * prefabList.gridSize, 0, z * prefabList.gridSize);
                Instantiate(prefabList.floor, pos, Quaternion.identity, roomParent);
            }
    }

    void GenerateWalls()
    {
        emptyWalls.Clear();
        for (int x = 0; x < floorWidth; x++)
        {
            PlaceWall(x, 0,               Vector3.forward, x == 0, x == floorWidth - 1 || x == 0); // south
            PlaceWall(x, floorLength - 1, Vector3.back,    false,  x == floorWidth - 1 || x == 0); // north
        }

        for (int z = 0; z < floorLength; z++)
        {
            PlaceWall(0,              z, Vector3.right, false, z == 0); // west
            PlaceWall(floorWidth - 1, z, Vector3.left);                 // east
        }
    }

    void PlaceWall(int x, int z, Vector3 forward, bool placeDoor = false, bool skipFurniturePlacement = false)
    {
        Vector3    pos = new Vector3(x * prefabList.gridSize, 0, z * prefabList.gridSize);
        pos -= forward * prefabList.gridSize / 2;
        Quaternion rot = Quaternion.LookRotation(forward);

        float      r      = (float)rng.NextDouble();
        GameObject prefab = null;

        if (r < prefabList.doorChance || placeDoor)
        {
            prefab = prefabList.door;
            Instantiate(prefab, pos, rot, roomParent);
        }
        else if (r < prefabList.doorChance + prefabList.windowChance)
        {
            prefab = prefabList.window;
            Instantiate(prefab, pos, rot, roomParent);
        }

        else
        {
            prefab = prefabList.wall;
            // Wall furniture is spawned into furnitureParent so it can be cleared separately
            //if ((float)rng.NextDouble() < prefabList.wallFurnitureChance && !skipFurniturePlacement)
            //    PlaceWallFurniture(pos, forward);
            GameObject newWall = Instantiate(prefab, pos, rot, roomParent);
            if(!skipFurniturePlacement) emptyWalls.Add(newWall.transform); // for potential future use (e.g. placing furniture on walls)
        }

        
    }

    void GenerateCeiling()
    {
        for (int x = 0; x < floorWidth; x++)
            for (int z = 0; z < floorLength; z++)
            {
                Vector3 pos = new Vector3(x * prefabList.gridSize, prefabList.ceilingHeight, z * prefabList.gridSize);
                Instantiate(prefabList.ceiling, pos, Quaternion.identity, roomParent);
            }
    }

    // -------------------------------------------------------------------------
    // Furniture placement  (children of furnitureParent)
    // -------------------------------------------------------------------------

    void PlaceAllFurniture()
    {
        PlaceWallFurniturePass();
        PlaceSpaceFurniture();
    }

    /// <summary>
    /// Iterates wall tiles again to place wall furniture independently of
    /// structure generation, so it can be called on its own during regeneration.
    /// </summary>
    void PlaceWallFurniturePass()
    {
        foreach (Transform emptyWall in emptyWalls)
        {
            Vector3 forward = emptyWall.forward;
            Vector3 pos     = emptyWall.position;
            if ((float)rng.NextDouble() < prefabList.wallFurnitureChance)
                PlaceWallFurniture(pos, forward);
        }
    }

    void PlaceWallFurniture(Vector3 position, Vector3 forward)
    {
        GameObject newObj = Instantiate(
            prefabList.wallFurniture[rng.Next(prefabList.wallFurniture.Length)],
            furnitureParent);

        newObj.GetComponent<CaptureObject>().Setup();
        Bounds furnitureBounds = newObj.GetComponent<CaptureObject>().GetBoundingbox();

        float offset = (furnitureBounds.extents.z - furnitureBounds.center.z)
                       * newObj.transform.lossyScale.z + 0.1f;
        newObj.transform.position += position + forward * offset;

        Vector3 flatForward       = new Vector3(forward.x, 0f, forward.z).normalized;
        Vector3 flatPrefabForward = new Vector3(0f, 0f, 1f);
        float   angle             = Vector3.SignedAngle(flatPrefabForward, flatForward, Vector3.up);
        newObj.transform.rotation = Quaternion.Euler(0f, angle, 0f) * newObj.transform.rotation;

        float groundOffset = GetObjectFloorHeight(furnitureBounds) * newObj.transform.lossyScale.y;
        if (newObj.transform.position.y < groundOffset)
            newObj.transform.position += Vector3.up * (groundOffset - newObj.transform.position.y);
    }

    void PlaceSpaceFurniture(int maxAttempts = 100)
    {
        if (prefabList.spaceFurniture.Length == 0) return;

        Bounds       zone         = new Bounds(roomCenter, roomSize - new Vector3(1, 0, 1) * prefabList.gridSize * 2);
        List<Bounds> placedBounds = new List<Bounds>();
        int          maxFurniture = Mathf.FloorToInt(zone.size.x * zone.size.z * prefabList.centerFurnitureDensity);

        for (int i = 0; i < maxFurniture; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                GameObject prefab = prefabList.spaceFurniture[rng.Next(prefabList.spaceFurniture.Length)];
                GameObject obj    = Instantiate(prefab, furnitureParent);

                obj.GetComponent<CaptureObject>().Setup();
                Bounds localBounds = obj.GetComponent<CaptureObject>().GetBoundingbox();

                float      rotY    = rng.Next(0, 4) * 90f;
                Quaternion rot     = Quaternion.Euler(0, rotY, 0);
                Vector3    right   = rot * Vector3.right   * localBounds.size.x * obj.transform.lossyScale.x;
                Vector3    forward = rot * Vector3.forward * localBounds.size.z * obj.transform.lossyScale.z;
                Vector3    size    = new Vector3(
                    Mathf.Abs(right.x) + Mathf.Abs(forward.x),
                    localBounds.size.y,
                    Mathf.Abs(right.z) + Mathf.Abs(forward.z));

                float x = zone.min.x + size.x * 0.5f + (float)rng.NextDouble() * (zone.max.x - zone.min.x);
                float z = zone.min.z + size.z * 0.5f + (float)rng.NextDouble() * (zone.max.z - zone.min.z);

                Vector3 pos         = new Vector3(x, zone.min.y, z);
                Bounds  worldBounds = new Bounds(pos + rot * localBounds.center, size);

                if (!IntersectsAny(worldBounds, placedBounds))
                {
                    obj.transform.position = pos;
                    obj.transform.rotation = rot;

                    float groundOffset = GetObjectFloorHeight(obj.GetComponent<MeshRenderer>().localBounds)
                                         * obj.transform.lossyScale.y;
                    if (obj.transform.position.y < groundOffset)
                        obj.transform.position += Vector3.up * (groundOffset - obj.transform.position.y);

                    placedBounds.Add(worldBounds);
                    placed = true;
                    break;
                }
                else
                {
                    Destroy(obj);
                }
            }

            if (!placed)
                Debug.Log("Failed to place object after max attempts.");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    float GetObjectFloorHeight(Bounds bounds) => bounds.extents.y - bounds.center.y;

    bool IntersectsAny(Bounds b, List<Bounds> others)
    {
        foreach (var o in others)
            if (b.Intersects(o)) return true;
        return false;
    }

    void CalculateRoomSizeAndCenter()
    {
        roomSize = new Vector3(
            floorWidth  * prefabList.gridSize,
            prefabList.ceilingHeight,
            floorLength * prefabList.gridSize);

        roomCenter = roomSize / 2 - new Vector3(prefabList.gridSize / 2, 0, prefabList.gridSize / 2);
    }

    void RepositionScanManager()
    {
        scanManager.boundingBoxSize  = roomSize + Vector3.one * 0.2f;
        scanManager.transform.position = roomCenter;
    }
}