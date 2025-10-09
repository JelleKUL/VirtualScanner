using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JelleKUL.Scanner
{
    public class ObjectScanner : MonoBehaviour
    {

        public VirtualScanner scanner;
        public CaptureObject capObject;
        public Vector3 minAngles;
        public Vector3 maxAngles;
        public float distance = 1;

        public int nrOfRandomSamples = 1;



        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
        [ContextMenu("RandomScan")]

        public void SampleAsync()
        {
            StartCoroutine(RandomSampleObject());
        }
        public IEnumerator RandomSampleObject()
        {
            for (int i = 0; i < nrOfRandomSamples; i++)
            {
                Quaternion lookDirection = Quaternion.Euler(RandomVector3(minAngles, maxAngles));
                Vector3 point = lookDirection * Vector3.forward * distance;
                print("moving scanner to point: " + i);
                scanner.transform.position = point;
                yield return new WaitForSeconds(0.1f);
                print("Scanning Environment");
                scanner.ScanEnvironment();
                yield return new WaitForSeconds(0.5f);
                print("Isolating Points");
                capObject.IsolatePoints();
                yield return new WaitForSeconds(0.5f);
            }


        }
        
        public Vector3 RandomVector3(Vector3 minVector, Vector3 maxVector)
        {
            return new Vector3(
                Random.Range(minVector.x, maxVector.x),
                Random.Range(minVector.y, maxVector.y),
                Random.Range(minVector.z, maxVector.z));
        }
    }
}