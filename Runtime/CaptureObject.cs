using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;


namespace JelleKUL.Scanner
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer))]
    public class CaptureObject : MonoBehaviour
    {
        [ReadOnlyValue]
        public string scanID = "";
        [Header("Point Settings")]
        public float expandRatio = 1;
        
        public VirtualScanner scanner;
        [HideInInspector]
        public string pointSavePath;
        public bool saveInWorldSpace = true;

        [Header("Voxel Settings")]
        public int voxelDimension = 8;
        public bool checkFullScene = true;
        [HideInInspector]
        public string voxelSavePath;

        [Header("Visualisation Settings")]
        public bool drawPoints = true;
        public float pointSize = 0.1f;
        public bool drawOccupiedVoxels = false;
        public bool drawOccludedVoxels = true;


        public List<ScannedPoint> points = new List<ScannedPoint>();
        private VoxelGrid grid;
        MeshCollider col;
        Renderer rend;
        MeshFilter filter;


        // Start is called before the first frame update
        void Start()
        {
            Setup();
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void Setup()
        {
            col = GetComponent<MeshCollider>();
            rend = GetComponent<Renderer>();
            filter = GetComponent<MeshFilter>();

            Collider[] colliders = GetComponents<Collider>();
            foreach (Collider collider in colliders)
            {
                if (collider.enabled && collider.GetType() != typeof(MeshCollider))
                {
                    collider.enabled = false;
                }
            }
            if(col.sharedMesh == null)
            {
                SetCombinedMesh();
            }
        }

        private void OnDrawGizmosSelected()
        {
            //if (!Application.isPlaying) return;

            Bounds bounds = GetBoundingbox();
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
            //print(bounds.center);
            Gizmos.matrix = Matrix4x4.identity;

            if (points.Count > 0)
            {
                if (drawPoints)
                {
                    foreach (var point in points)
                    {
                        point.Show(pointSize);
                    }
                }
                
            }
            if (grid != null && grid.voxels.Count >0)
            {
                grid.Show(drawOccupiedVoxels, drawOccludedVoxels);
            }
        }

        public Bounds GetBoundingbox()
        {
            if(!col) col = GetComponent<MeshCollider>();
            if (!rend) rend = col.GetComponent<Renderer>();
            Bounds bounds = col.TryGetComponent(out Renderer r)? r.localBounds: col.bounds;
            bounds.Expand((expandRatio - 1) * Max(bounds.extents));
            return bounds;
        }
        public void SetCombinedMesh()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            Dictionary<Material, List<CombineInstance>> materialToCombine = new Dictionary<Material, List<CombineInstance>>();

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.transform == transform) continue;
                if (mf.sharedMesh == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Material[] materials = mr.sharedMaterials;

                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                {
                    Material mat = materials[Mathf.Min(sub, materials.Length - 1)];

                    if (!materialToCombine.ContainsKey(mat))
                        materialToCombine[mat] = new List<CombineInstance>();

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = sub,
                        transform = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                    };

                    materialToCombine[mat].Add(ci);
                
                }

                //disable the rendere and colliders
                mr.enabled = false;
                Collider[] colliders = mf.GetComponents<Collider>();
                foreach (Collider collider in colliders)
                {
                    collider.enabled = false;
                }
                Destroy(mf.gameObject);
            }
            //destroy all childeren
            //foreach (MeshFilter mf in meshFilters) Destroy(mf.gameObject);

            // Build final combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = "CombinedMesh";

            List<CombineInstance> finalCombine = new List<CombineInstance>();
            List<Material> finalMaterials = new List<Material>();

            foreach (var kvp in materialToCombine)
            {
                Mesh tempMesh = new Mesh();
                tempMesh.CombineMeshes(kvp.Value.ToArray(), true, true);

                CombineInstance ci = new CombineInstance
                {
                    mesh = tempMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };

                finalCombine.Add(ci);
                finalMaterials.Add(kvp.Key);
            }

            combinedMesh.CombineMeshes(finalCombine.ToArray(), false, false);

            // After combinedMesh.CombineMeshes(...)
            combinedMesh.RecalculateBounds();

            // Choose your pivot target in world space:
            // Option A — bottom centre of the combined mesh
            Bounds bounds = combinedMesh.bounds; // bounds are in local space already
            Vector3 pivotOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // Option B — centre of the mesh
            // Vector3 pivotOffset = combinedMesh.bounds.center;

            // Option C — the parent transform's current world position expressed in local space
            // Vector3 pivotOffset = transform.InverseTransformPoint(transform.position); // = Vector3.zero

            // Shift all vertices by -pivotOffset
            Vector3[] vertices = combinedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= pivotOffset;
            combinedMesh.vertices = vertices;
            combinedMesh.RecalculateBounds();

            // Move the GameObject to compensate so nothing appears to shift in world space
            transform.position += transform.TransformVector(pivotOffset);

            filter.sharedMesh = combinedMesh;
            rend.sharedMaterials = finalMaterials.ToArray();

            // Build collider (single mesh version)
            MeshCollider col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();

            col.sharedMesh = null;
            col.sharedMesh = combinedMesh;
        }
        
        [ContextMenu("Isolate Points")]
        public void IsolatePoints()
        {
            if (!scanner)
            {
                scanner = GameObject.FindGameObjectWithTag("Scanner").GetComponent<VirtualScanner>();
            }
            points = new List<ScannedPoint>();

            Bounds bounds = GetBoundingbox();

            foreach (var point in scanner.scannedPoints)
            {
                if (bounds.Contains(transform.InverseTransformPoint(point.position)))
                {
                    points.Add(point);
                }
            }
            //CreateOccupiedVoxelGrid();
        }
        [ContextMenu("Export Pointcloud")]
        public void ExportPointCloud()
        {
            if (!saveInWorldSpace)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    ScannedPoint newPoint = points[i];
                    newPoint.position = transform.InverseTransformPoint(points[i].position);
                    points[i] = newPoint;
                }
            }

            scanner.ExportPointCloud(points, pointSavePath);
        }
        [ContextMenu("Export Voxels")]
        public void ExportVoxels()
        {
            File.WriteAllText(voxelSavePath, grid.ToJsonString());
        }

        public float Max(Vector3 vector)
        {
            return Mathf.Max(Mathf.Max(vector.x, vector.y), vector.z);
        }

        public void CreateOccupiedVoxelGrid()
        {
            if (points.Count <= 0)
            {
                Debug.LogWarning(gameObject.name + " has no points, can't create occupation grid");
                return;
            }

            Bounds bounds = col.bounds;
            bounds.Expand((expandRatio - 1) * Max(bounds.extents));

            Vector3 center = bounds.center;
            float maxboundSize = Max(bounds.size);

            grid = new VoxelGrid(voxelDimension, maxboundSize / voxelDimension, center, transform.rotation);

            List<Vector3> positions = new List<Vector3>(points.Count);
            foreach (var pt in points)
                positions.Add(pt.position);
            grid.VoxelizePoints(positions);
            grid.FindOccludedVoxels(scanner.transform.position);
            if(checkFullScene) grid.CheckSceneOcclusion(scanner.transform.position);

            
        }

        public Mesh GetMesh()
        {
            if(TryGetComponent(out MeshFilter filter))
            {
                return filter.mesh;
            }
            Debug.LogWarning(name + " does not contain a meshfilter, returning empty mesh");
            return new Mesh();
        }

        public SerialisedBoundingBox GetBoundingBoxCornerPoints()
        {
            Bounds bounds = GetBoundingbox();

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] corners = new Vector3[8];

            // Bottom
            corners[0] = transform.TransformPoint(new Vector3(min.x, min.y, min.z));
            corners[1] = transform.TransformPoint(new Vector3(max.x, min.y, min.z));
            corners[2] = transform.TransformPoint(new Vector3(max.x, min.y, max.z));
            corners[3] = transform.TransformPoint(new Vector3(min.x, min.y, max.z));

            // Top
            corners[4] = transform.TransformPoint(new Vector3(min.x, max.y, min.z));
            corners[5] = transform.TransformPoint(new Vector3(max.x, max.y, min.z));
            corners[6] = transform.TransformPoint(new Vector3(max.x, max.y, max.z));
            corners[7] = transform.TransformPoint(new Vector3(min.x, max.y, max.z));

            return new SerialisedBoundingBox(name, corners);
        }
    }

    [System.Serializable]
    public class Voxel
    {
        public Vector3Int gridIndex = Vector3Int.zero;
        public Color color = Color.white;
        public float distance = 0;
        public Vector3 worldPos = Vector3.zero;

        public Voxel(Vector3Int gridIndex, Color color)
        {
            this.gridIndex = gridIndex;
            this.color = color;
        }
        public Voxel(Vector3Int gridIndex, Vector3 worldPos)
        {
            this.gridIndex = gridIndex;
            this.worldPos = worldPos;
        }
        //Return the normalised position of the center of the voxel
        public Vector3 GetPosition(int dimension)
        {
            Vector3 position = -0.5f * Vector3.one;
            position += (Vector3)gridIndex * (1.0f / dimension) + Vector3.one * (0.5f / dimension);
            return position;
        }
    }
    [System.Serializable]
    public class SerialisedBoundingBox
    {
        public string id;
        public Vector3[] boundingPoints;

        public SerialisedBoundingBox(string id,Vector3[] points)
        {
            this.id = id;
            this.boundingPoints = points;
        }
        public string toJsonstring()
        {
            string jsonString = JsonUtility.ToJson(this, prettyPrint: true);
            return jsonString;
        }
    }
    [System.Serializable]
    public class SerialisedBoundingBoxList
    {
        public SerialisedBoundingBox[] boxes;

        public SerialisedBoundingBoxList(SerialisedBoundingBox[] boxes)
        {
            this.boxes = boxes;
        }
        public string toJsonstring()
        {
            string jsonString = JsonUtility.ToJson(this, prettyPrint: true);
            return jsonString;
        }
    }

    [System.Serializable]
    public class VoxelGrid
    {
        public float voxelSize = 1;
        public Vector3 origin = Vector3.zero;
        public Quaternion rotation = Quaternion.identity;
        public List<Voxel> voxels = new List<Voxel>();
        public int[,,] voxelGrid = new int[100, 100, 100];
        public List<Voxel> occludedVoxels = new List<Voxel>();
        public int[,,] occludedVoxelGrid = new int[100, 100, 100];
        public int dimension = 64;

        public VoxelGrid(int dimension, float voxelSize, Vector3 origin, Quaternion rotation)
        {
            this.dimension = dimension;
            this.voxelSize = voxelSize;
            this.origin = origin;
            this.rotation = rotation;
        }

  

        public string ToJsonString()
        {

            string jsonString = JsonUtility.ToJson(this, prettyPrint: true);

            return jsonString;
        }

        public void CreateEmptyGrid(int dimension)
        {
            this.dimension = dimension;
            // init a zero array
            voxelGrid = new int[dimension, dimension, dimension];
            occludedVoxelGrid = new int[dimension, dimension, dimension];
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    for (int k = 0; k < dimension; k++)
                    {
                        voxelGrid[i, j, k] = 0;
                        occludedVoxelGrid[i, j, k] = 0;
                    }
                }
            }
        }

        public void VoxelizePoints(List<Vector3> points)
        {
            voxels.Clear();
            System.Array.Clear(voxelGrid, 0, voxelGrid.Length);

            // Precompute rotation inverse (world → local)
            Quaternion invRot = Quaternion.Inverse(rotation);
            float half = dimension * voxelSize * 0.5f;

            foreach (var p in points)
            {
                // Transform world point into local voxel space
                Vector3 localPos = invRot * (p - origin);

                // Convert to voxel coordinates (0..dimension-1)
                int x = Mathf.FloorToInt((localPos.x + half) / voxelSize);
                int y = Mathf.FloorToInt((localPos.y + half) / voxelSize);
                int z = Mathf.FloorToInt((localPos.z + half) / voxelSize);

                // Bounds check
                if (x < 0 || y < 0 || z < 0 || x >= dimension || y >= dimension || z >= dimension)
                    continue;

                // Mark voxel as occupied
                if (voxelGrid[x, y, z] == 0)
                {
                    voxelGrid[x, y, z] = 1;

                    Vector3 worldPos = origin + rotation * (new Vector3(
                        (x + 0.5f) * voxelSize - half,
                        (y + 0.5f) * voxelSize - half,
                        (z + 0.5f) * voxelSize - half
                    ));

                    voxels.Add(new Voxel(new Vector3Int(x, y, z), worldPos));
                }
            }
        }

        public void Show(bool showVoxels = true, bool showOccluded = true)
        {
            Matrix4x4 trans = new Matrix4x4();
            trans.SetTRS(origin, rotation, Vector3.one);
            Gizmos.matrix = trans;
            if (showOccluded || showVoxels)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(Vector3.zero, voxelSize * Vector3.one * dimension);
            }
            if (showOccluded)
            {
                Gizmos.color = Color.red;
                foreach (Voxel voxel in occludedVoxels)
                {
                    Gizmos.DrawCube(voxel.GetPosition(dimension) * voxelSize * dimension, Vector3.one * voxelSize);
                }
            }
            if (showVoxels)
            {
                Gizmos.color = Color.green;
                foreach (Voxel voxel in voxels)
                {
                    Gizmos.DrawCube(voxel.GetPosition(dimension) * voxelSize * dimension, Vector3.one * voxelSize);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }


        public static List<Vector3Int> Bresenham3D(Vector3Int p0, Vector3Int p1)
        {
            List<Vector3Int> points = new List<Vector3Int>();
            Vector3Int p = p0;

            int dx = Mathf.Abs(p1.x - p0.x);
            int dy = Mathf.Abs(p1.y - p0.y);
            int dz = Mathf.Abs(p1.z - p0.z);

            int sx = p0.x < p1.x ? 1 : -1;
            int sy = p0.y < p1.y ? 1 : -1;
            int sz = p0.z < p1.z ? 1 : -1;

            int dx2 = dx << 1;
            int dy2 = dy << 1;
            int dz2 = dz << 1;

            if (dx >= dy && dx >= dz)
            {
                int err1 = dy2 - dx;
                int err2 = dz2 - dx;
                for (int i = 0; i <= dx; i++)
                {
                    points.Add(p);
                    if (err1 > 0)
                    {
                        p.y += sy;
                        err1 -= dx2;
                    }
                    if (err2 > 0)
                    {
                        p.z += sz;
                        err2 -= dx2;
                    }
                    err1 += dy2;
                    err2 += dz2;
                    p.x += sx;
                }
            }
            else if (dy >= dx && dy >= dz)
            {
                int err1 = dx2 - dy;
                int err2 = dz2 - dy;
                for (int i = 0; i <= dy; i++)
                {
                    points.Add(p);
                    if (err1 > 0)
                    {
                        p.x += sx;
                        err1 -= dy2;
                    }
                    if (err2 > 0)
                    {
                        p.z += sz;
                        err2 -= dy2;
                    }
                    err1 += dx2;
                    err2 += dz2;
                    p.y += sy;
                }
            }
            else
            {
                int err1 = dy2 - dz;
                int err2 = dx2 - dz;
                for (int i = 0; i <= dz; i++)
                {
                    points.Add(p);
                    if (err1 > 0)
                    {
                        p.y += sy;
                        err1 -= dz2;
                    }
                    if (err2 > 0)
                    {
                        p.x += sx;
                        err2 -= dz2;
                    }
                    err1 += dy2;
                    err2 += dx2;
                    p.z += sz;
                }
            }

            return points;
        }

        private static bool IsInsideGrid(Vector3Int p, int sizeX, int sizeY, int sizeZ)
        {
            return p.x >= 0 && p.x < sizeX &&
                p.y >= 0 && p.y < sizeY &&
                p.z >= 0 && p.z < sizeZ;
        }
        public Vector3 RoundToGridIndex(Vector3 vector, float gridSize)
        {

            vector /= gridSize;
            Vector3 roundedVector = new Vector3(FloorToGrid(vector.x), FloorToGrid(vector.y), FloorToGrid(vector.z));

            return roundedVector;

        }
        private int FloorToGrid(float val)
        {
            return Mathf.FloorToInt(val >= 0 ? val : val + 1);
        }
        public void FindOccludedVoxels(Vector3 viewpoint)
        {
            Vector3 localViewpoint = Quaternion.Inverse(rotation) * (viewpoint - origin);
            List<Vector3Int> occluded = new List<Vector3Int>();

            for (int x = 0; x < dimension; x++)
            {
                for (int y = 0; y < dimension; y++)
                {
                    for (int z = 0; z < dimension; z++)
                    {
                        if (voxelGrid[x, y, z] != 0) continue;

                        Vector3Int target = new Vector3Int(x, y, z);
                        List<Vector3Int> rayPath = Bresenham3D(Vector3Int.RoundToInt(RoundToGridIndex(localViewpoint, voxelSize)), target);

                        bool blocked = false;
                        foreach (var point in rayPath)
                        {
                            if (point == target) break; // Stop before reaching the voxel itself

                            if (IsInsideGrid(point, dimension, dimension, dimension))
                            {
                                if (voxelGrid[point.x, point.y, point.z] == 1)
                                {
                                    blocked = true;
                                    break;
                                }
                            }
                        }

                        if (blocked)
                            occluded.Add(target);
                    }
                }
            }

            occludedVoxelGrid = new int[dimension, dimension, dimension];
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    for (int k = 0; k < dimension; k++)
                    {
                        occludedVoxelGrid[i, j, k] = 0;
                    }
                }
            }
            foreach (Vector3Int i in occluded)
            {
                occludedVoxelGrid[i[0], i[1], i[2]] = 1;
                float half = dimension * voxelSize * 0.5f;
                Vector3 worldPos = origin + rotation * (new Vector3(
                        (i[0] + 0.5f) * voxelSize - half,
                        (i[1] + 0.5f) * voxelSize - half,
                        (i[2] + 0.5f) * voxelSize - half
                    ));

                occludedVoxels.Add(new Voxel(new Vector3Int(i[0], i[1], i[2]), worldPos));
            }
        }

        public void CheckSceneOcclusion(Vector3 viewpoint)
        {
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    for (int k = 0; k < dimension; k++)
                    {
                        if (occludedVoxelGrid[i, j, k] == 0 && voxelGrid[i, j, k] == 0)
                        {
                            Vector3 VoxelLocalPos = -0.5f * Vector3.one;
                            VoxelLocalPos += new Vector3(i,j,k) * (1.0f / dimension) + Vector3.one * (0.5f / dimension);
                            Vector3 VoxelWorldPos = (rotation * (VoxelLocalPos * dimension * voxelSize)) + origin;
                            RaycastHit hit;
                            // Does the ray intersect any objects excluding the player layer
                            if (Physics.Raycast(VoxelWorldPos, (viewpoint - VoxelWorldPos).normalized, out hit, Vector3.Magnitude(viewpoint - VoxelWorldPos)))
                            {
                                Debug.Log("hit an object: " + i + ", " + j + ", " + k);
                                Debug.DrawRay(VoxelWorldPos, (viewpoint - VoxelWorldPos).normalized * hit.distance, Color.yellow, 10);
                                occludedVoxels.Add(new Voxel(new Vector3Int(i, j, k), VoxelWorldPos));
                            }
                        }
                    }
                }
            }
        }


    }
}