using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.WSA;

namespace JelleKUL.Scanner
{
    public class ScanManager : MonoBehaviour
{
        public VirtualScanner scanner;
        public SphericalImageCapture imageCapture;
        public Vector3 boundingBoxSize = new Vector3(10, 10, 10);
        public int sceneVoxelResolution = 16;
        //public bool mirrorX = false;
        public bool saveInWorldSpace = false;
        public bool exportBoundingBoxes = true;
        public bool exportGTMeshes = true;
        [BrowsePath(BrowseMode.Folder)]
        public string savePath;
        [ButtonBool("ScanAndIsolate")]
        public bool scanAndIsolate;
        [ButtonBool("GetSceneOcclusion")]
        public bool calculateSceneOcclusion;
        [ButtonBool("ScanEmptyScene")]
        public bool scanEmptyScene;

        [Header("Per Object Occlusion")]
        public bool exportOcclusionVoxels = false;
        public int objectVoxelResolution = 8;

        [Header("Procedural Scanning")]

        public ProceduralRoomGenerator roomGenerator;
        public int numberOfRoomsToGenerate = 1;
        [ButtonBool("ScanProceduralRooms")]
        public bool scanProceduralRooms;


        private List<CaptureObject> captureObjects = new List<CaptureObject>();
        private VoxelGrid sceneOcclusionGrid;
        [HideInInspector]
        public List<SerialisedBoundingBox> bbs;

        private string saveFolderPath = "";

        private bool scanning = false;
        private int roomsScanned = 0;

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
            Gizmos.color = Color.red;
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
                    captureObject.scanID = captureObject.gameObject.name + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                    captureObjects.Add(captureObject);
                    print(captureObject.name);
                }
            }
            
        }
        [ContextMenu("Scan and Isolate")]
        public void ScanAndIsolate()
        {
            scanner.scanID = SceneManager.GetActiveScene().name + "-" +System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            saveFolderPath = Directory.CreateDirectory(Path.Join(savePath, scanner.scanID)).FullName;
            //find and export all the object meshes
            FindCaptureObjects();
            if (exportGTMeshes)
            {
                foreach (CaptureObject cap in captureObjects)
                {
                   
                    PLYWriter.ExportMesh(fileName : Path.Join(saveFolderPath, cap.scanID + "_gt.ply"), cap.GetMesh());
                }
            }
            StartCoroutine(ScanAndIsolateCoroutine());
            
        }
        IEnumerator ScanAndIsolateCoroutine()
        {
            float now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            print("Capturing PanoImage");
            //yield return new WaitForEndOfFrame();
            imageCapture.UpdateTexture();

            print("Started Scanning...");
            yield return 0;
            scanner.ScanEnvironment();

            print("Looking for active CaptureObjects...");
            // get all the Capture objects in range
            yield return 0;
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
            File.WriteAllText(Path.Join(saveFolderPath,"scan_parameters.json"), JsonUtility.ToJson(scanner));
            
            print("Isolating objects...");
            yield return 0;
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
                Debug.Log(cap.scanID + cap.points);
                cap.saveInWorldSpace = saveInWorldSpace;
                cap.pointSavePath = Path.Join(saveFolderPath, cap.scanID + "_points.txt");
                cap.ExportPointCloud();

                // Also export the voxel occupation grid per object
                if (exportOcclusionVoxels)
                {
                    cap.voxelDimension = objectVoxelResolution;
                    cap.CreateOccupiedVoxelGrid();
                    cap.voxelSavePath = Path.Join(saveFolderPath, cap.scanID + "_voxels.json");
                    cap.ExportVoxels();
                }
                if (exportBoundingBoxes)
                {
                    File.WriteAllText(Path.Join(saveFolderPath, cap.scanID + "_bb.json"), cap.GetBoundingBoxCornerPoints().toJsonstring());
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
        [ContextMenu("Get Scan Parameters")]
        public void GetScanParameters()
        {
            print(JsonUtility.ToJson(roomGenerator));
        }
        
        public void GetSceneOcclusion()
        {
            float max = Mathf.Max(boundingBoxSize.x, Mathf.Max(boundingBoxSize.y, boundingBoxSize.z));
            sceneOcclusionGrid = new VoxelGrid(sceneVoxelResolution, max / sceneVoxelResolution, transform.position, transform.rotation);
            sceneOcclusionGrid.CreateEmptyGrid(sceneVoxelResolution);
            sceneOcclusionGrid.CheckSceneOcclusion(scanner.transform.position);
            File.WriteAllText(Path.Join(saveFolderPath, "main_voxels.json"), sceneOcclusionGrid.ToJsonString());
        }
        public void ScanEmptyScene()
        {
        StartCoroutine(ScanEmptySceneCoroutine());
        }

        IEnumerator ScanEmptySceneCoroutine()
        {
            if(captureObjects.Count == 0)
            {
                FindCaptureObjects();
            }
            foreach (CaptureObject cap in captureObjects)
            {
                cap.gameObject.SetActive(false);
            }
            yield return 0;
            imageCapture.UpdateTexture();
            yield return 0;
            scanner.ScanEnvironment();
            
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
            scanner.ExportPointCloud(insidePoints, Path.Join(saveFolderPath, "main_empty.txt"));
            scanner.ExportRenderTexture(scanner.colorTexture,Path.Join(saveFolderPath, "pano_empty.png"));
        }

        public void ScanProceduralRooms()
        {
            if (!scanning)
            {
                StartCoroutine(ScanProceduralRoomsCoroutine());
            }
        }

        IEnumerator ScanProceduralRoomsCoroutine()
        {
            scanning = true;

            while (roomsScanned < numberOfRoomsToGenerate)
            {
                Debug.Log($"Generating room {roomsScanned + 1} of {numberOfRoomsToGenerate}...");
                roomGenerator.GenerateRoom();
                

                // Wait a frame to let Unity process any physics/scene changes from room generation
                yield return new WaitForEndOfFrame();

                // 1. Scan and Isolate
                Debug.Log("Step 1: Scan and Isolate");
                scanner.scanID = roomGenerator.prefabList.style + "_" + scanner.scannerType + "_" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                saveFolderPath = Directory.CreateDirectory(Path.Join(savePath, scanner.scanID)).FullName;
                File.WriteAllText(Path.Join(saveFolderPath,"room_parameters.json"), JsonUtility.ToJson(roomGenerator));
                FindCaptureObjects();
                if (exportGTMeshes)
                {
                    foreach (CaptureObject cap in captureObjects)
                    {
                        PLYWriter.ExportMesh(fileName: Path.Join(saveFolderPath, cap.scanID + "_gt.ply"), cap.GetMesh());
                    }
                }
                yield return StartCoroutine(ScanAndIsolateCoroutine());

                // 2. Calculate Scene Occlusion
                if (exportOcclusionVoxels)
                {
                    Debug.Log("Step 2: Calculate Scene Occlusion");
                    GetSceneOcclusion();
                    yield return null;
                }

                // 3. Scan Empty Scene
                Debug.Log("Step 3: Scan Empty Scene");
                yield return StartCoroutine(ScanEmptySceneCoroutine());

                roomsScanned++;
                Debug.Log($"Room {roomsScanned} complete.");

                // Wait a frame before generating the next room
                yield return null;
            }

            Debug.Log("All rooms scanned.");
            scanning = false;
        }


    }
}

