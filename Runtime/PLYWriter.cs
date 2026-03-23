using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JelleKUL.Scanner
{
public class PLYWriter
{
    public static void ExportMesh(string fileName, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        using (StreamWriter sw = new StreamWriter(fileName))
        {
            // PLY header
            sw.WriteLine("ply");
            sw.WriteLine("format ascii 1.0");
            sw.WriteLine($"element vertex {vertices.Length}");
            sw.WriteLine("property float x");
            sw.WriteLine("property float y");
            sw.WriteLine("property float z");

            if (normals != null && normals.Length == vertices.Length)
            {
                sw.WriteLine("property float nx");
                sw.WriteLine("property float ny");
                sw.WriteLine("property float nz");
            }

            sw.WriteLine($"element face {triangles.Length / 3}");
            sw.WriteLine("property list uchar int vertex_indices");
            sw.WriteLine("end_header");

            // Write vertices
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                string line = $"{v.x} {v.y} {v.z}";

                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 n = normals[i];
                    line += $" {n.x} {n.y} {n.z}";
                }

                sw.WriteLine(line);
            }

            // Write faces
            for (int i = 0; i < triangles.Length; i += 3)
            {
                sw.WriteLine($"3 {triangles[i]} {triangles[i + 1]} {triangles[i + 2]}");
            }
        }

        Debug.Log($"Mesh exported to {fileName}");
    }
}
}
