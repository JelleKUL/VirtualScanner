using UnityEngine;
// Use the CreateAssetMenu attribute to allow creating instances of this ScriptableObject from the Unity Editor.
[CreateAssetMenu(fileName = "PrefabList", menuName = "ScriptableObjects/newPrefabList", order = 1)]
public class PrefabListScriptableObject : ScriptableObject
{
    public string style;
    public float gridSize = 2;
    public Vector2Int minRoomSize = new Vector2Int(3,3);
    public Vector2Int maxRoomSize = new Vector2Int(6,6);
    public float ceilingHeight = 3;
    [Tooltip("The change a door spawns, together with windowChance, cannot be higher than 1")]
    public float doorChance = 0.1f;
    [Tooltip("The change a window spawns, together with doorChance, cannot be higher than 1")]
    public float windowChance = 0.2f;
    public Color minWallColor, maxWallColor, minFloorColor, maxFloorColor, minCeilingColor, maxCeilingColor = Color.white;
    public Material wallMaterial, floorMaterial, ceilingMaterial;
    public GameObject floor, wall, ceiling, door, window;
    public float wallFurnitureChance = 0.5f;
    public GameObject[] wallFurniture;
    public float centerFurnitureDensity = 0.3f;
    public GameObject[] spaceFurniture;

    public void ColorMaterials(int seed)
    {
        System.Random rand = new System.Random(seed);
        if(wallMaterial) wallMaterial.color = RandomColorBetween(minWallColor, maxWallColor, rand);
        if(floorMaterial) floorMaterial.color = RandomColorBetween(minFloorColor, maxFloorColor, rand);
        if(ceilingMaterial) ceilingMaterial.color = RandomColorBetween(minCeilingColor, maxCeilingColor, rand);
    }
    public static Color RandomColorBetween(Color colorA, Color colorB, System.Random rand)
    {
        float hA, sA, vA, hB, sB, vB;
        Color.RGBToHSV(colorA, out hA, out sA, out vA);
        Color.RGBToHSV(colorB, out hB, out sB, out vB);
        
        return Color.HSVToRGB(
            Mathf.Lerp(hA, hB, (float)rand.NextDouble()),
            Mathf.Lerp(sA, sB, (float)rand.NextDouble()),
            Mathf.Lerp(vA, vB, (float)rand.NextDouble())
        );
    }

}