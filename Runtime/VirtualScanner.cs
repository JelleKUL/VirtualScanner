using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Globalization;
using System.Threading;

namespace JelleKUL.Scanner
{
    [System.Serializable]
    public class VirtualScanner : MonoBehaviour
    {
        [Header("Scan Parameters")]
        [Tooltip("Update the scan continuously at runtime")]
        public bool scanContinuous = true;
        public string scannerType = "Default";
        [Tooltip("mm at 10M distance (default 12.5mm)")]
        [Unit("mm")]
        [Min(0)]
        public float scanDensity = 12.5f;
        [Tooltip("The max range of the scanner")]
        [Unit("m")]
        [Min(0f)]
        public float scanRange = 100;
        [Tooltip("The total degrees the vertical axis can cover")]
        [Unit("deg")]
        [Range(0f, 360f)]
        public float VerticalScanRange = 290;
        [Tooltip("The standard deviation for the artificial noise expressed in mm")]
        [Unit("mm")]
        [Min(0f)]
        public float systemNoise = 1;
        [Tooltip("The standard deviation for the artificial noise expressed in %")]
        [Unit("mm/10m")]
        [Min(0f)]
        public float distanceNoise = 1f;
         [Tooltip("The amount of points that have been scanned (read only)")]
        [ReadOnlyValue]
        public int nrOfPoints = 0;
        [Tooltip("The masks that indicates the furniture")]
        public LayerMask scannableLayers;

        [Header("Coloring")]
        public RenderTexture colorTexture;

        [Header("Debug Visualisation")]
        [SerializeField]
        bool drawRays = false;
        [SerializeField]
        bool drawPoints = false;
        [SerializeField]
        float pointSize = 0.01f;

        [Header("Export")]
        [BrowsePath(BrowseMode.File)]
        public string pointSavePath = "";
        [HideInInspector]
        public string scanID;
        [Header("Actions")]
        [ButtonBool("ScanEnvironment")]
        public bool scanEnvironment;
        [ButtonBool("SavePoints")]
        public bool exportPointCloud;



        private List<Transform> scannedObjects = new List<Transform>();

        // Private Properties new scan system
        private float lastDensity = -1;
        [System.NonSerialized]
        private List<ScanParameter> scanParameters = new List<ScanParameter>();
        [HideInInspector]
        [System.NonSerialized]
        public List<ScannedPoint> scannedPoints = new List<ScannedPoint>();

        

        // Start is called before the first frame update
        void Start()
        {

        }

        void Update()
        {
            if (scanContinuous) ScanEnvironment();
        }



        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            if (drawPoints && scannedPoints.Count > 0)
            {
                foreach (var point in scannedPoints)
                {
                    point.Show(pointSize);
                }
            }
            
            if (drawRays && scannedPoints.Count > 0)
            {
                
                foreach (var point in scannedPoints)
                {
                    Gizmos.color = point.color;
                    Gizmos.DrawLine(transform.position, point.position);
                }
            }
            

        }

        public void ScanEnvironment()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Please enter PlayMode to start scanning");
                return;
            }

            if(scanID == "")
            {
            scanID = SceneManager.GetActiveScene().name + "-" +System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            print("Created new scanID: " + scanID);
            }


            //check if the transform has changed, then update scan directions
            if (lastDensity != scanDensity)
            {
                lastDensity = scanDensity;
                UpdateScanParameters(scanDensity);
            }
            print("Casting " + scanParameters.Count + " rays");
            // perform the scan job
            CastRaysJob(transform.position, scanParameters);
            print("Found " + scannedPoints.Count + " points");
            // perform the color job
            if (colorTexture)
            {
                GetPointColorsJob();
                print("Points colored");
            }

        }

        // creates a list of scan directions and corresponding UV coordinates
        // This should only be updated if the Scanner moves
        private void UpdateScanParameters(float density)
        {
            scanParameters = new List<ScanParameter>();
            float rayAngle = 2 * Mathf.Sin(density / 10000f); // mm/10m
            int pointsPerDisc = Mathf.CeilToInt(Mathf.PI * 2 / rayAngle); // the number of horizonontal captured points
            int nrOfDiscs = Mathf.CeilToInt(Mathf.PI / rayAngle); // the number of vertical rows
            Vector3 vector0 = Vector3.forward; // the starting vector

            for (int i = 0; i < nrOfDiscs; i++)
            {
                for (int j = 0; j < pointsPerDisc; j++)
                {
                    if ((i * rayAngle * Mathf.Rad2Deg) > (360 - VerticalScanRange) / 2) // filter out the bottom unscannable rows
                    {
                        Vector3 dir = Quaternion.Euler(0, j * rayAngle * Mathf.Rad2Deg, 0) * Quaternion.Euler(90 - i * rayAngle * Mathf.Rad2Deg, 0, 0) * vector0;
                        Vector2 uv = new Vector2(j * rayAngle * Mathf.Rad2Deg / 360, 1 - ((180 - i * rayAngle * Mathf.Rad2Deg) / 180));
                        scanParameters.Add(new ScanParameter(dir, uv, new Vector2Int(i,j)));
                    }
                }
            }
        }

        // Casts the rays in parallel and update the hits list
        private void CastRaysJob(Vector3 origin, List<ScanParameter> scanParams)
        {
            // reset the points
            scannedPoints = new List<ScannedPoint>();
            int rayCount = scanParams.Count;
            NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(rayCount, Allocator.TempJob);
            NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(rayCount, Allocator.TempJob);
            QueryParameters param = QueryParameters.Default;
            param.layerMask = scannableLayers;
            for (int i = 0; i < rayCount; i++)
            {
                commands[i] = new RaycastCommand(origin, scanParams[i].direction,param, scanRange);
            }
            // Schedule batch of raycasts
            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 32);
            // Complete the job
            handle.Complete();
            nrOfPoints = 0;
            // Process results
            for (int i = 0; i < rayCount; i++)
            {
                if (results[i].collider != null)
                {
                    nrOfPoints++;
                    scannedPoints.Add(new ScannedPoint(
                        systemNoise + distanceNoise > 0 ? AddScannerNoise(origin, results[i].point, results[i].distance, systemNoise, distanceNoise) : results[i].point,
                        results[i].normal,
                        scanParams[i].uv,
                        Color.white,
                        scanParams[i].pointIdx));
                }
            }
            // Dispose arrays
            commands.Dispose();
            results.Dispose();
        }

        private void GetPointColorsJob()
        {
            // keep track of the current rendertexture
            RenderTexture current = RenderTexture.active;
            RenderTexture.active = colorTexture;
            // Create a Texture2D with same size and format
            Texture2D tex = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.RGBA32, false);
            // Copy pixels from GPU -> CPU
            tex.ReadPixels(new Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0);
            tex.Apply();
            //reset rendertexture
            RenderTexture.active = current;
            //convert to native array for job
            NativeArray<Color32> pixels = tex.GetRawTextureData<Color32>();
            // convert scannedPoints to a native array
            NativeArray<ScannedPoint> nativePoints = new NativeArray<ScannedPoint>(scannedPoints.Count, Allocator.TempJob);
            for (int i = 0; i < scannedPoints.Count; i++)
            {
                nativePoints[i] = scannedPoints[i];
            }
            Debug.Log(pixels.Length + " =? " + tex.width + " * " + tex.height + "  = " + tex.width * tex.height);

            // Run job
            var job = new SampleUVJob
            {
                pixels = pixels,
                points = nativePoints,
                texWidth = tex.width,
                texHeight = tex.height
            };

            JobHandle handle = job.Schedule(nativePoints.Length, 64);
            handle.Complete();
            // convert back to list
            for (int i = 0; i < scannedPoints.Count; i++)
            {
                scannedPoints[i] = nativePoints[i];
            }

            nativePoints.Dispose();
            Destroy(tex);
        }

        //random
        public Vector3 AddScannerNoise(Vector3 startPoint, Vector3 hitPoint, float distance, float baseNoise, float distNoise)
        {
            // Direction of the ray
            Vector3 dir = (hitPoint - startPoint).normalized;

            // Gaussian noise with mean 0 and std deviation proportional to distance
            float sigma = baseNoise * 0.001f + distNoise * 0.0001f * distance; // Convert from mm to meters and to mm/10M to meters
            float noise = RandomNormal(0f, sigma);

            // Apply the noise along the ray direction
            float noisyDistance = Mathf.Max(distance + noise, 0f); // avoid negative distance

            // Compute the new noisy point
            return startPoint + dir * noisyDistance;
        }

        /// <summary>
        /// Generates Gaussian noise using the Box–Muller transform.
        /// </summary>
        private float RandomNormal(float mean, float stdDev)
        {
            float u1 = 1.0f - Random.value;  // avoid log(0)
            float u2 = 1.0f - Random.value;
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                                Mathf.Sin(2.0f * Mathf.PI * u2);
            return mean + stdDev * randStdNormal;
        }



        public void SaveToCloudCompareTXT(Vector3[,] points, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var p in points)
                {
                    if (p.sqrMagnitude > scanRange * scanRange) continue;
                    // Use InvariantCulture to avoid commas instead of dots in some locales
                    writer.WriteLine(
                        string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0} {1} {2}",
                            p.x, p.y, p.z
                        )
                    );
                }
            }

            Debug.Log($"Saved {points.Length} points to {filePath}");
        }
        [ContextMenu("Save Points")]
        public void SavePoints()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Please enter PlayMode to export data");
                return;
            }
            ExportPointCloud(scannedPoints, pointSavePath);
            ExportRenderTexture(colorTexture,System.IO.Path.ChangeExtension(pointSavePath, ".png"));
        }
        public void ExportRenderTexture(RenderTexture colorTexture, string filePath)
        {
            // Keep track of current RT
            RenderTexture current = RenderTexture.active;
            RenderTexture.active = colorTexture;

            // Create Texture2D
            Texture2D tex = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.RGBA32, false);

            // Copy pixels
            tex.ReadPixels(new Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0);
            tex.Apply();

            // Restore
            RenderTexture.active = current;

            // Encode
            byte[] bytes = tex.EncodeToPNG();

            // Save file
            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"Saved Pano Image to {filePath}");

            // Cleanup
            Object.Destroy(tex);
        }
        /// <summary>
        /// Saves a 2D array of points with normals to a .txt file in CloudCompare-compatible format.
        /// Each line will be: x y z nx ny nz r g b
        /// </summary>
        /// <param name="points">2D array of 3D positions</param>
        /// <param name="normals">2D array of normals (must match points dimensions)</param>
        /// <param name="filePath">Full path of the output .txt file</param>
        public void ExportPointCloud(List<ScannedPoint>points, string filePath)
        {

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Header information
                // Timestamp
                writer.WriteLine($"# timestamp {System.DateTime.UtcNow:O}");

                // Transform matrix
                Matrix4x4 m = transform.localToWorldMatrix;

                writer.WriteLine("# transform_matrix");
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, 
                    "# {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}", 
                    m.m00, m.m01, m.m02, m.m03, m.m10, m.m11, m.m12, m.m13, m.m20, m.m21, m.m22, m.m23, m.m30, m.m31, m.m32, m.m33));

                // Optional column header
                writer.WriteLine("# x y z nx ny nz r g b");
                
                foreach (ScannedPoint point in points)
                {
                    Vector3 p = point.position;
                    Vector3 n = point.normal;
                    Color32 c = point.color;

                    writer.WriteLine(
                                string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "{0} {1} {2} {3} {4} {5} {6} {7} {8}",
                                    p.x, p.y, p.z,
                                    n.x, n.y, n.z,
                                    c.r, c.g, c.b
                                )
                            );
                }

            }

            Debug.Log($"Saved {points.Count} scan samples (with normals & colors) to {filePath}");
        }
        [ContextMenu("Save LAS Points")]
        public void SaveLASPoints()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Please enter PlayMode to export data");
                return;
            }
            ExportPointCloudLAS(scannedPoints, pointSavePath);
        }

        public void ExportPointCloudLAS(List<ScannedPoint>points, string filePath)
        {
            Vector3[] positions;
            Color[] colors;
            Vector3[] normals;

            SplitScannedPoints(points, out positions, out colors, out normals);
            
            LASFileWriter.WriteLAS(
                filePath,
                positions,
                colors,
                normals
            );
        }
        public void SplitScannedPoints(
            List <ScannedPoint> scanned,
            out Vector3[] positions,
            out Color[] colors,
            out Vector3[] normals
        )
        {
            int count = scanned.Count;

            positions = new Vector3[count];
            colors    = new Color[count];
            normals   = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                positions[i] = scanned[i].position;
                colors[i]    = scanned[i].color;
                normals[i]   = scanned[i].normal;
            }
        }

    }
    [System.Serializable]
    public struct ScanParameter
    {
        public Vector3 direction;
        public Vector2 uv;
        public Vector2Int pointIdx;

        public ScanParameter(Vector3 direction, Vector2 uv, Vector2Int pointIdx)
        {
            this.direction = direction;
            this.uv = uv;
            this.pointIdx = pointIdx;
        }
    }
    [System.Serializable]
    public struct ScannedPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Color32 color;
        public Vector2Int pointIdx;

        public ScannedPoint(Vector3 position, Vector3 normal, Vector2 uv, Color32 color, Vector2Int pointIdx)
        {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
            this.color = color;
            this.pointIdx = pointIdx;
        }

        public void Show(float radius = 0.01f)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
        }
    }

    public struct SampleUVJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> pixels;
        public NativeArray<ScannedPoint> points; // job will write into this
        public int texWidth;
        public int texHeight;

        public float density;

        public void Execute(int index)
        {
            ScannedPoint point = points[index];

            int x = Mathf.Clamp((int)(point.uv.x * texWidth), 0, texWidth - 1);
            int y = Mathf.Clamp((int)(point.uv.y * texHeight), 0, texHeight - 1);

            int pixelIndex = y * texWidth + x;
            point.color = pixels[pixelIndex];
            point.color.a = 255;

            points[index] = point; // write back
        }
    }
}
