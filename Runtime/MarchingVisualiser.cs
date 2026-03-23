using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using JelleKUL.Scanner;

/*
public enum MARCHING_MODE {  CUBES, COMPACTCUBES, TETRAHEDRON };
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class MarchingVisualiser : MonoBehaviour
{
    public Material material;

    public MARCHING_MODE mode = MARCHING_MODE.CUBES;

    public int seed = 0;

    public bool padEdges = true;
    public bool smoothNormals = false;

    public bool drawNormals = false;

    private List<GameObject> meshes = new List<GameObject>();

    [SerializeField]
    private VoxelGrid grid;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateMesh(){
        if(grid == null) return;



        //Set the mode used to create the mesh.
        //Cubes is faster and creates less verts, tetrahedrons is slower and creates more verts but better represents the mesh surface.
        Marching marching = null;
        if(mode == MARCHING_MODE.TETRAHEDRON)
            marching = new MarchingTertrahedron();
        else if (mode == MARCHING_MODE.COMPACTCUBES)
            marching = new CompactMarchingCubes();
        else
            marching = new MarchingCubes();

        //Surface is the value that represents the surface of mesh
        //The target value does not have to be the mid point it can be any value with in the range.
        marching.Surface = 0.5f;
        VoxelArray voxels = null;

        if(padEdges){
            //The size of voxel array.
            int width = grid.dimension+2;
            int height = grid.dimension+2;
            int depth = grid.dimension+2;

            voxels = new VoxelArray(width, height, depth);

            //Fill voxels with values. Im using perlin noise but any method to create voxels will work.
            for (int x = 0; x < width-2; x++)
            {
                for (int y = 0; y < height-2; y++)
                {
                    for (int z = 0; z < depth-2; z++)
                    {
                        voxels[x+1,y+1,z+1] = drawer.voxelGrid[x,y,z];
                    }
                }
            }

        }
        else{
            //The size of voxel array.
            int width = drawer.voxelDimension;
            int height = drawer.voxelDimension;
            int depth = drawer.voxelDimension;

            voxels = new VoxelArray(width, height, depth);

            //Fill voxels with values. Im using perlin noise but any method to create voxels will work.
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        voxels[x,y,z] = drawer.voxelGrid[x,y,z];
                    }
                }
            }
        }
        

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();

        //The mesh produced is not optimal. There is one vert for each index.
        //Would need to weld vertices for better quality mesh.
        marching.Generate(voxels.Voxels, verts, indices);

        for (int i = 0; i < verts.Count; i++)
        {
            verts[i] += Vector3.one * drawer.voxelSize;
        }

        CreateMesh32(verts, normals, indices);
        transform.localScale = Vector3.one * drawer.voxelSize;
        if(padEdges) transform.position = (-1) * Vector3.one * drawer.voxelSize;
    }

    private void CreateMesh32(List<Vector3> verts, List<Vector3> normals, List<int> indices)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(indices, 0);

            if (normals.Count > 0)
                mesh.SetNormals(normals);
            else
                mesh.RecalculateNormals();
                

            mesh.RecalculateBounds();

            GetComponent<Renderer>().material = material;
            GetComponent<MeshFilter>().mesh = mesh;
        }
}
*/