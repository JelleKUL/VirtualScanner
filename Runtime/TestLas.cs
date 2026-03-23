using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestLAS : MonoBehaviour
{
    void Start()
    {
        Vector3[] pos = new[]
        {
            new Vector3(0,0,0),
            new Vector3(1,2,1)
        };

        Color[] colors = new[]
        {
            Color.red,
            Color.green
        };

        Vector3[] normals = new[]
        {
            Vector3.up,
            Vector3.forward
        };

        LASFileWriter.WriteLAS(
            Application.dataPath + "/colored_normals.las",
            pos,
            colors,
            normals
        );

        Debug.Log("Written lasfile to: " +  Application.dataPath + "/colored_normals.las");
    }
}
