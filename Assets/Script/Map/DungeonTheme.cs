using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "DungeonTheme", menuName = "Rogue-kie/Dungeon Theme")]
public class DungeonTheme : ScriptableObject
{
    [System.Serializable]
    public class MobSpawnData
    {
        public GameObject mobPrefab;
        public int weight = 1;
    }

    [Header("Mob Spawn")]
    public MobSpawnData[] mobs;
    public int minMobPerRoom = 2;
    public int maxMobPerRoom = 5;
    public bool spawnMobInStartRoom = false;
    public int mobSpawnPadding = 3;

    [Header("Floor")]
    public TileBase[] baseTiles;
    public TileBase[] detailTiles;
    public TileBase[] shadowTiles;

    [Header("Wall")]
    public TileBase wallTopSide;
    public TileBase wallTopBot;
    public TileBase wallDefault;
    public TileBase wallBottomFoot;

    [Header("Wall Decorations")]
    public WallDecorationSet[] wallDecorations;
    [Range(0f, 1f)] public float decorationChance = 0.08f;

    [Header("Obstacle Asset")]
    public TileBase obstacleTile;

    [Header("Door")]
    public TileBase doorTile;
    public TileBase doorTopTile;

    [System.Serializable]
    public class WallDecorationSet
    {
        public TileBase wallDefault;
        public TileBase wallBottomFoot;
        public int minClusterSize = 2;
        public int maxClusterSize = 4;
    }
}