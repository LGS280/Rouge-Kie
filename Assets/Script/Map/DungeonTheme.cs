using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "DungeonTheme", menuName = "Rogue-kie/Dungeon Theme")]
public class DungeonTheme : ScriptableObject
{
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

    [System.Serializable]
    public class WallDecorationSet
    {
        public TileBase wallDefault;
        public TileBase wallBottomFoot;
        public int minClusterSize = 2;
        public int maxClusterSize = 4;
    }
}