using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class LASFileWriter
{
    public static void WriteLAS(string path, Vector3[] positions, Color[] colors, Vector3[] normals, float scale = 0.01f)
    {
        if (positions.Length != colors.Length || positions.Length != normals.Length)
            throw new Exception("positions, colors, and normals must have equal length.");

        int pointCount = positions.Length;

        // HEADER VALUES
        const ushort headerSize = 227;
        ushort pointFormat = 2;                    // PF2 = XYZ + RGB  
        ushort baseRecordLength = 26;              // PF2 length  
        ushort extraBytes = 12;                    // normals (float3)  
        ushort totalPointRecordLength = (ushort)(baseRecordLength + extraBytes);

        // We'll add ONE extra-bytes VLR  
        const uint offsetToPointData = headerSize + 54; // Header + 54-byte VLR
        const uint numberOfVLRs = 1;

        using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            // -------------------------------------------------
            // FILE HEADER (LAS 1.2)
            // -------------------------------------------------

            bw.Write(Encoding.ASCII.GetBytes("LASF")); // File signature
            bw.Write((ushort)0); // File source ID
            bw.Write((ushort)0); // Global encoding
            bw.Write(new byte[16]); // Project ID (GUID)
            bw.Write((byte)1); // Version Major
            bw.Write((byte)2); // Version Minor

            WriteFixedString(bw, "UNITY", 32); // System identifier
            WriteFixedString(bw, "VirtualScanner", 32); // Generating software

            bw.Write((ushort)DateTime.Now.Day);     // File creation day
            bw.Write((ushort)DateTime.Now.Year);  // File creation year

            bw.Write(headerSize); // Header size
            bw.Write(offsetToPointData); // Offset to point data
            bw.Write(numberOfVLRs); // Number of VLRs

            bw.Write((byte)pointFormat);              // Point data format
            bw.Write(totalPointRecordLength);         // Point record length
            bw.Write((uint)pointCount);               // Number of point records

            // Number of points by return
            uint[] returns = { (uint)pointCount, 0, 0, 0, 0 };
            for (int i = 0; i < 5; i++)
                bw.Write(returns[i]);

            // Scale factors
            bw.Write(scale);
            bw.Write(scale);
            bw.Write(scale);

            // Offsets (zero)
            bw.Write(0.0);
            bw.Write(0.0);
            bw.Write(0.0);

            // Compute min/max
            (float minX, float minY, float minZ, float maxX, float maxY, float maxZ) = GetBounds(positions);

            bw.Write(minX); bw.Write(minY); bw.Write(minZ);
            bw.Write(maxX); bw.Write(maxY); bw.Write(maxZ);

            // -------------------------------------------------
            // VLR: Extra Bytes (Normals)
            // -------------------------------------------------

            WriteExtraBytesVLR(bw);

            // -------------------------------------------------
            // POINT DATA
            // -------------------------------------------------

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 p = positions[i];
                Vector3 n = normals[i];
                Color c = colors[i];

                // XYZ as scaled integers
                bw.Write((int)(p.x / scale));
                bw.Write((int)(p.y / scale));
                bw.Write((int)(p.z / scale));

                // Intensity (unused)
                bw.Write((ushort)0);

                // Flags
                bw.Write((byte)0);         // Return flags
                bw.Write((byte)1);         // Classification
                bw.Write((sbyte)0);        // Scan angle
                bw.Write((byte)0);         // User data
                bw.Write((ushort)0);       // Point source ID

                // RGB 0–65535
                bw.Write((ushort)(c.r * 65535));
                bw.Write((ushort)(c.g * 65535));
                bw.Write((ushort)(c.b * 65535));

                // --- EXTRA BYTES (Normals as float32) ---
                bw.Write(n.x);
                bw.Write(n.y);
                bw.Write(n.z);
            }
        }
    }

    private static void WriteFixedString(BinaryWriter bw, string text, int length)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        Array.Resize(ref bytes, length);
        bw.Write(bytes);
    }

    private static void WriteExtraBytesVLR(BinaryWriter bw)
    {
        // VLR Header (54 bytes total for one ExtraBytes VLR)
        WriteFixedString(bw, "LASF_Spec", 16);      // User ID
        bw.Write((ushort)4);                        // Record ID: Extra Bytes
        bw.Write((ushort)0);                        // Reserved
        bw.Write((uint)34);                         // VLR data length
        WriteFixedString(bw, "Normal Vectors", 32); // Description (truncated if needed)

        // Extra Bytes VLR body (34 bytes)
        bw.Write((byte)3);        // Number of extra dimensions
        bw.Write((byte)0);        // Reserved

        // For each dimension (X,Y,Z normals)
        for (int i = 0; i < 3; i++)
        {
            bw.Write((byte)4);        // Data type (4 = float32)
            bw.Write((byte)0);        // Options
            bw.Write(new byte[8]);    // No name
            bw.Write(new byte[4]);    // No description
        }
    }

    private static (float, float, float, float, float, float) GetBounds(Vector3[] pts)
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var p in pts)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
            minZ = Mathf.Min(minZ, p.z);

            maxX = Mathf.Max(maxX, p.x);
            maxY = Mathf.Max(maxY, p.y);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        return (minX, minY, minZ, maxX, maxY, maxZ);
    }
}
