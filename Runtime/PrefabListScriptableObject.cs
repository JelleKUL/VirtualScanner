using UnityEngine;
// Use the CreateAssetMenu attribute to allow creating instances of this ScriptableObject from the Unity Editor.
[CreateAssetMenu(fileName = "PrefabList", menuName = "ScriptableObjects/newPrefabList", order = 1)]
public class PrefabListScriptableObject : ScriptableObject
{
    public string style;
    public float gridSize = 2;
    public float ceilingHeight = 3;
    [Tooltip("The change a door spawns, together with windowChance, cannot be higher than 1")]
    public float doorChance = 0.1f;
    [Tooltip("The change a window spawns, together with doorChance, cannot be higher than 1")]
    public float windowChance = 0.2f;
    public GameObject floor, wall, ceiling, door, window;
    public float furnitureChance = 0.5f;
    public GameObject[] wallFurniture;
    public float centerFurnitureDensity = 0.3f;
    public GameObject[] spaceFurniture;

}