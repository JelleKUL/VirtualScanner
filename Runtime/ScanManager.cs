using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.WSA;

namespace JelleKUL.Scanner
{
    public class ScanManager : MonoBehaviour
{
        public VirtualScanner scanner;
        public Vector3 boundingBoxSize = new Vector3(10, 10, 10);
        //public bool mirrorX = false;
        public bool saveInWorldSpace = false;
        public bool exportBoundingBoxes = true;
        public bool exportGTMeshes = true;
        [BrowsePath(BrowseMode.Folder)]
        public string savePath;
        [ButtonBool("ScanAndIsolate")]
        public bool scanAndIsolate;
        [ButtonBool("GetSceneOcclusion")]
        public bool CalculateOcclusions;
        [ButtonBool("ScanEmptyScene")]
        public bool scanEmptyScene;

        [Header("Scene Occlusion")]
        public bool exportOcclusionVoxels = false;
        public float voxelSize = 0.1f;

        private List<CaptureObject> captureObjects = new List<CaptureObject>();
        private VoxelGrid sceneOcclusionGrid;
        [HideInInspector]
        public List<SerialisedBoundingBox> bbs;

        private string saveFolderPath = "";

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, boundingBoxSize);
            Gizmos.matrix = Matrix4x4.identity;

            if (captureObjects.Count > 0)
            {
                foreach (CaptureObject cap in captureObjects)
                {
                    Bounds bounds = cap.GetBoundingbox();
                    Gizmos.color = Color.cyan;
                    Gizmos.matrix = cap.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
                    Gizmos.matrix = Matrix4x4.identity;
                }
            }
            
            if(sceneOcclusionGrid != null && sceneOcclusionGrid.occludedVoxels.Count > 0)
            {
                sceneOcclusionGrid.Show(false, true);
            }
        }
        [ContextMenu("Find CaptureObjects")]
        public void FindCaptureObjects()
        {
            captureObjects.Clear();
            Collider[] cols = Physics.OverlapBox(transform.position, boundingBoxSize / 2);

            foreach (Collider col in cols)
            {
                if (col.TryGetComponent(out CaptureObject captureObject))
                {
                    captureObjects.Add(captureObject);
                    print(captureObject.name);
                }
            }
            
        }
        [ContextMenu("Scan and Isolate")]
        public void ScanAndIsolate()
        {
            float now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            print("Started Scanning...");
            // scan environment
            scanner.ScanEnvironment();
            print("Looking for active CaptureObjects...");
            // get all the Capture objects in range
            FindCaptureObjects();
            //create a new directory
            saveFolderPath = Directory.CreateDirectory(Path.Join(savePath, scanner.scanID)).FullName;
            List<ScannedPoint> insidePoints = new List<ScannedPoint>();
            Vector3 min = transform.position - boundingBoxSize / 2;
            Vector3 max = transform.position + boundingBoxSize / 2;

            foreach (var point in scanner.scannedPoints)
            {
                Vector3 p = point.position;
                if (p.x >= min.x && p.x <= max.x &&
                    p.y >= min.y && p.y <= max.y &&
                    p.z >= min.z && p.z <= max.z)
                {
                    insidePoints.Add(point);
                }
            }
            print("Exporting pointcloud...");
            scanner.ExportPointCloud(insidePoints, Path.Join(saveFolderPath, "main.txt"));
            scanner.ExportRenderTexture(scanner.colorTexture,Path.Join(saveFolderPath, "pano.png"));
            File.WriteAllText(Path.Join(saveFolderPath,"main_bb.json"), GetBoundingBoxesString());
            if (exportOcclusionVoxels)
            {
                print("Calculating Scene Occlusions...");
                GetSceneOcclusion();
                File.WriteAllText(Path.Join(saveFolderPath, "main_voxels.json"), sceneOcclusionGrid.ToJsonString());

            }
            print("Isolating objects...");
            // Isolate all the points of the capture objects & optionally create an occlusiongrid
            foreach (CaptureObject cap in captureObjects)
            {
                //export pointcloud
                cap.IsolatePoints();
                if (cap.points.Count <= 0)
                {
                    Debug.Log("failed");
                    continue;
                }
                Debug.Log(cap.name + cap.points);
                string name = cap.gameObject.name + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                cap.saveInWorldSpace = saveInWorldSpace;
                cap.pointSavePath = Path.Join(saveFolderPath, name + "_points.txt");
                cap.ExportPointCloud();

                // Also export the voxel occupation grid per object
                if (exportOcclusionVoxels)
                {
                    cap.CreateOccupiedVoxelGrid();
                    cap.voxelSavePath = Path.Join(saveFolderPath, name + "_voxels.json");
                    cap.ExportVoxels();
                }
                if (exportBoundingBoxes)
                {
                    File.WriteAllText(Path.Join(saveFolderPath, name + "_bb.json"), cap.GetBoundingBoxCornerPoints().toJsonstring());
                }
                if (exportGTMeshes)
                {
                    PLYWriter.ExportMesh(fileName : Path.Join(saveFolderPath, name + "_gt.ply"), cap.GetMesh());
                }
                
            }
            float elapsed = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - now;
            print("Done in " + (elapsed/1000).ToString("0.##") + " seconds.");

        }
        [ContextMenu("Get Bounding Boxes")]
        public string GetBoundingBoxesString()
        {
            bbs = new List<SerialisedBoundingBox>();
            
            foreach (CaptureObject cap in captureObjects)
            {
                bbs.Add(cap.GetBoundingBoxCornerPoints());
            }
            SerialisedBoundingBoxList list = new SerialisedBoundingBoxList(bbs.ToArray());
            return list.toJsonstring();
        }
        
        public void GetSceneOcclusion()
        {
            float max = Mathf.Max(boundingBoxSize.x, Mathf.Max(boundingBoxSize.y, boundingBoxSize.z));
            sceneOcclusionGrid = new VoxelGrid(Mathf.CeilToInt(max / voxelSize), voxelSize, transform.position, transform.rotation);
            sceneOcclusionGrid.CreateEmptyGrid(Mathf.CeilToInt(max / voxelSize));
            sceneOcclusionGrid.CheckSceneOcclusion(scanner.transform.position);
        }

        public void ScanEmptyScene()
        {
            if(captureObjects.Count == 0)
            {
                FindCaptureObjects();
            }
            foreach (CaptureObject cap in captureObjects)
            {
                cap.gameObject.SetActive(false);
            }
            scanner.ScanEnvironment();
        }


    }
}

