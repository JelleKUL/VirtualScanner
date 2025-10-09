using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace JelleKUL.Scanner
{
    [RequireComponent(typeof(MeshFilter))]
    public class CaptureObject : MonoBehaviour
    {
        public float expandRatio = 1;
        public bool drawPoints = true;
        public VirtualScanner scanner;
        [HideInInspector]
        public string pointSavePath;

        public bool saveInWorldSpace = true;
        private List<ScannedPoint> points = new List<ScannedPoint>();
        Collider col;

        // Start is called before the first frame update
        void Start()
        {
            col = GetComponent<Collider>();
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Bounds bounds = col.bounds;
            bounds.Expand((expandRatio - 1) * Max(bounds.extents));

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bounds.center - transform.position, bounds.extents * 2);
            Gizmos.matrix = Matrix4x4.identity;

            if (drawPoints && points.Count > 0)
            {
                foreach (var point in points)
                {
                    point.Show();
                }
            }

        }
        [ContextMenu("Isolate Points")]
        public void IsolatePoints()
        {
            points = new List<ScannedPoint>();

            Bounds bounds = col.bounds;
            bounds.Expand((expandRatio - 1) * Max(bounds.extents));

            foreach (var point in scanner.scannedPoints)
            {
                if (bounds.Contains(point.position))
                {
                    points.Add(point);
                }
            }
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

        public float Max(Vector3 vector){
            return Mathf.Max(Mathf.Max(vector.x, vector.y), vector.z);
        }
    }
}