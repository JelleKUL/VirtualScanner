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

    System.Random rng;
    [SerializeField]
    int floorWidth;
    [SerializeField]
    int floorLength;
    private bool roomGenerated = false;
    private Vector3 roomSize;
    private Vector3 roomCenter;

    void Start()
    {
        
    }
    [ContextMenu("Generate Room")]
    public void GenerateRoom()
    {
        if(roomGenerated) RemoveRoom();
        
        InitializeRandom();
        //create a random color scheme for the room
        prefabList.ColorMaterials(seed);
        // create a rectangular floor layout, where each floor element is placed at the center og the coordinate
        GenerateFloor();
        CalculateRoomSizeAndCenter();
        // place a wall at each outside block, the center of the wall lies at the end of the block
        GenerateWalls();
        GenerateCeiling();
        PlaceSpaceFurniture();
        if(repositionScanManager) RepositionScanManager();
        print("Generated a room with seed: " + seed + ", of size: (" + floorWidth + ", " + floorLength + ").");
        roomGenerated = true;
    }
    [ContextMenu("Remove Room")]
    public void RemoveRoom()
    {
        foreach(Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        roomGenerated = false;
    }
    void InitializeRandom()
    {
    if(!useFixedSeed)
        seed = unchecked((int)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    rng = new System.Random(seed);
    }

    void GenerateFloor()
    {
        floorWidth = rng.Next(prefabList.minRoomSize.x, prefabList.maxRoomSize.x+1);
        floorLength = rng.Next(prefabList.minRoomSize.y, prefabList.maxRoomSize.y+1);
        
        for(int x = 0; x < floorWidth; x++)
        {
            for(int z = 0; z < floorLength; z++)
            {
                Vector3 pos = new Vector3(x * prefabList.gridSize, 0, z * prefabList.gridSize);

                Instantiate(prefabList.floor, pos, Quaternion.identity, transform);
            }
        }
    }

    void GenerateWalls()
    {
        //always ensure atleast 1 door at the start, the rest is random.
        for(int x = 0; x < floorWidth; x++)
        {
            PlaceWall(x, 0, Vector3.forward, x==0, x == floorWidth-1 || x==0);       // south
            PlaceWall(x, floorLength-1, Vector3.back,false,x == floorWidth-1 || x==0);      // north
        }

        for(int z = 0; z < floorLength; z++)
        {
            PlaceWall(0, z, Vector3.right, false, z==0 );         // west
            PlaceWall(floorWidth-1, z, Vector3.left);       // east
        }
    }
    void PlaceWall(int x, int z, Vector3 forward, bool placeDoor = false, bool skipFurniturePlacement = false)
    {
        //the center position of the current tile
        Vector3 pos = new Vector3(x * prefabList.gridSize, 0, z * prefabList.gridSize);
        //move the wall back by half a tile
        pos -= forward * prefabList.gridSize/2;

        Quaternion rot = Quaternion.LookRotation(forward);

        GameObject prefab;

        float r = (float)rng.NextDouble();

        if(r < prefabList.doorChance || placeDoor)
            prefab = prefabList.door;
        else if(r < prefabList.doorChance + prefabList.windowChance)
            prefab = prefabList.window;
        else
        {
            prefab = prefabList.wall;
            //also chance to spawn an object against the wall
            if((float)rng.NextDouble() < prefabList.wallFurnitureChance && !skipFurniturePlacement)
                PlaceWallFurniture(pos, forward);
        }
        Instantiate(prefab, pos, rot, transform);
    }
    void GenerateCeiling()
    {
        for(int x = 0; x < floorWidth; x++)
        {
            for(int z = 0; z < floorLength; z++)
            {
                Vector3 pos = new Vector3(x * prefabList.gridSize, prefabList.ceilingHeight, z * prefabList.gridSize);

                Instantiate(prefabList.ceiling, pos, Quaternion.identity, transform);
            }
        }
    }
    
    float GetObjectFloorHeight(Bounds bounds)
    {
        // Calculate the height needed so the bottom of the object sits at y = 0
        // The bottom of the object is at: center.y - extents.y
        // To place it at ground level, we need to offset by that amount
        return bounds.extents.y - bounds.center.y;
    }

    void PlaceWallFurniture(Vector3 position, Vector3 forward)
    {
        GameObject newObj = Instantiate(
            prefabList.wallFurniture[rng.Next(prefabList.wallFurniture.Length)], 
            transform
            );
        newObj.GetComponent<CaptureObject>().Setup();
        Bounds furnitureBounds = newObj.GetComponent<CaptureObject>().GetBoundingbox();
        float offset2 = (furnitureBounds.extents.z - furnitureBounds.center.z) * newObj.transform.lossyScale.z + 0.1f;
        newObj.transform.position += position + forward * offset2;

        // Flatten both directions onto the horizontal plane to avoid X/Z axis flipping
        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
        Vector3 flatPrefabForward = new Vector3(0f, 0f, 1f); // prefab's base forward, Y stripped

        // Build a Y-only rotation from the prefab's forward to the wall's forward
        float angle = Vector3.SignedAngle(flatPrefabForward, flatForward, Vector3.up);
        Quaternion yRotation = Quaternion.Euler(0f, angle, 0f);

        // Apply on top of the object's existing rotation (preserves prefab's internal rotation)
        newObj.transform.rotation = yRotation * newObj.transform.rotation;

        float groundOffset = GetObjectFloorHeight(furnitureBounds) * newObj.transform.lossyScale.y;
        if(newObj.transform.position.y < groundOffset)
        {
            newObj.transform.position += Vector3.up * (groundOffset - newObj.transform.position.y);
        }
    }
    bool IntersectsAny(Bounds b, List<Bounds> others)
    {
        foreach (var o in others)
        {
            if (b.Intersects(o))
                return true;
        }
        return false;
    }
    void PlaceSpaceFurniture(int maxAttempts = 100)
    {
        if(prefabList.spaceFurniture.Length == 0) return;
        Bounds zone = new Bounds(roomCenter,roomSize - new Vector3(1,0,1)* prefabList.gridSize*2);
        List<Bounds> placedBounds = new List<Bounds>();
        int maxFurniture = Mathf.FloorToInt((zone.size.x * zone.size.z) * prefabList.centerFurnitureDensity);

        for (int i = 0; i < maxFurniture; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                GameObject prefab = prefabList.spaceFurniture[rng.Next(prefabList.spaceFurniture.Length)];
                GameObject obj = Instantiate(prefab, transform);
                obj.GetComponent<CaptureObject>().Setup();
                Bounds localBounds = obj.GetComponent<CaptureObject>().GetBoundingbox();
                
                // introduce random rotation
                float rotY = rng.Next(0, 4) * 90f;
                Quaternion rot = Quaternion.Euler(0, rotY, 0);
                Vector3 right = rot * Vector3.right * localBounds.size.x * obj.transform.lossyScale.x;
                Vector3 forward = rot * Vector3.forward * localBounds.size.z * obj.transform.lossyScale.z;
                Vector3 size = new Vector3(
                        Mathf.Abs(right.x) + Mathf.Abs(forward.x),
                        localBounds.size.y,
                        Mathf.Abs(right.z) + Mathf.Abs(forward.z));

                float x = zone.min.x + size.x * 0.5f + (float)rng.NextDouble() * (zone.max.x - zone.min.x);
                float z = zone.min.z + size.z * 0.5f + (float)rng.NextDouble() * (zone.max.z - zone.min.z);

                Vector3 pos = new Vector3(x, zone.min.y, z);

                Bounds worldBounds = new Bounds(pos + rot * localBounds.center, size);

                if (!IntersectsAny(worldBounds, placedBounds))
                {
                    obj.transform.position = pos;
                    obj.transform.rotation = rot;
                    float groundOffset = GetObjectFloorHeight(obj.GetComponent<MeshRenderer>().localBounds) * obj.transform.lossyScale.y;
                    if(obj.transform.position.y < groundOffset)
                    {
                        obj.transform.position += Vector3.up * (groundOffset - obj.transform.position.y);
                    }

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
    void CalculateRoomSizeAndCenter()
    {
        roomSize = new Vector3(
            floorWidth * prefabList.gridSize, 
            prefabList.ceilingHeight, 
            floorLength*prefabList.gridSize
            );
        roomCenter = roomSize/2 - new Vector3(prefabList.gridSize/2,0,prefabList.gridSize/2);
    }
    void RepositionScanManager()
    {
        scanManager.boundingBoxSize = roomSize;
        scanManager.transform.position = roomCenter;
        //add a little margin to the bounding box
        scanManager.boundingBoxSize += Vector3.one * 0.2f;
    }

}
