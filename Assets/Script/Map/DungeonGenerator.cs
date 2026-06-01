using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tilemaps Component")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap obstacleTilemap;

    [Header("Door Tilemap")]
    public Tilemap doorTilemap;      // Ch?a ph?n G?C (D??i)
    public Tilemap doorTopTilemap;   // Ch?a ph?n NG?N (Trên)

    [Header("Danh sách g?ch sàn")]
    public TileBase[] baseTiles;
    public TileBase[] detailTiles;
    public TileBase[] shadowTiles;

    [Header("Wall Tiles")]
    public TileBase wallTopSide;
    public TileBase wallTopBot;
    public TileBase wallDefault;
    public TileBase wallBottomFoot;

    [Header("Door Tiles")]
    public TileBase doorTile;        // Sprite ph?n g?c c?a
    public TileBase doorTopTile;     // Sprite ph?n ng?n c?a

    [Header("B? Asset V?t C?n")]
    public TileBase obstacleTile;

    [Header("C?u hình Random Kích Th??c Phòng")]
    public int minRoomSize = 14;
    public int maxRoomSize = 24;

    [Header("Kích th??c phòng hi?n t?i")]
    [SerializeField] private int currentWidth;
    [SerializeField] private int currentHeight;

    [Header("Door Size")]
    public int doorSize = 4;

    [ContextMenu("Generate Test Room")]
    public void GenerateTestRoom()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        obstacleTilemap.ClearAllTiles();

        if (doorTilemap != null) doorTilemap.ClearAllTiles();
        if (doorTopTilemap != null) doorTopTilemap.ClearAllTiles();

        currentWidth = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;
        currentHeight = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;

        int midX = currentWidth / 2;
        int midY = currentHeight / 2;
        int halfDoor = doorSize / 2;

        // =========================
        // V? PHÒNG
        // =========================

        for (int x = 0; x < currentWidth; x++)
        {
            for (int y = currentHeight - 1; y >= 0; y--)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);

                // =========================
                // CHECK DOOR
                // =========================
                bool isTopDoor = (y == currentHeight - 1) && (x >= midX - halfDoor && x < midX + halfDoor);
                bool isBottomDoor = (y == 0) && (x >= midX - halfDoor && x < midX + halfDoor);
                bool isLeftDoor = (x == 0) && (y >= midY - halfDoor && y < midY + halfDoor);
                bool isRightDoor = (x == currentWidth - 1) && (y >= midY - halfDoor && y < midY + halfDoor);

                // =========================
                // TOP DOOR (?ã ??ng b? G?c & Ng?n)
                // =========================
                if (isTopDoor)
                {
                    Vector3Int basePos = tilePos + Vector3Int.down; // H? g?c xu?ng y - 1
                    Vector3Int topPos = tilePos;                   // H? ng?n xu?ng y

                    // Lót g?ch sàn ? ô tr?ng phía d??i c?a (y - 2)
                    floorTilemap.SetTile(basePos + Vector3Int.down, baseTiles[Random.Range(0, baseTiles.Length)]);

                    // Xóa t??ng t?i v? trí ??t ng?n c?a (y) và g?c c?a (y - 1)
                    wallTilemap.SetTile(topPos, null);
                    wallTilemap.SetTile(basePos, null);

                    // ??t c?a vào 2 Tilemap t??ng ?ng
                    doorTilemap.SetTile(basePos, doorTile);
                    if (doorTopTilemap != null && doorTopTile != null)
                    {
                        doorTopTilemap.SetTile(topPos, doorTopTile);
                    }

                    continue;
                }

                // =========================
                // BOTTOM DOOR (?ã h? toàn b? c?u trúc xu?ng 1 ô)
                // =========================
                if (isBottomDoor)
                {
                    Vector3Int basePos = tilePos + Vector3Int.down; // H? g?c ra ngoài phòng (y - 1)
                    Vector3Int topPos = tilePos;                   // H? ng?n xu?ng ngay ô biên (y)

                    // Lót g?ch sàn t?i ô g?c c?a ngoài phòng và ô ng?n c?a trong phòng
                    floorTilemap.SetTile(basePos, baseTiles[Random.Range(0, baseTiles.Length)]);
                    floorTilemap.SetTile(topPos, baseTiles[Random.Range(0, baseTiles.Length)]);

                    // Xóa t??ng t?i v? trí biên (y) và chân t??ng phía d??i n?u có (y - 1)
                    wallTilemap.SetTile(topPos, null);
                    wallTilemap.SetTile(basePos, null);

                    // ??t c?a vào 2 Tilemap t??ng ?ng
                    doorTilemap.SetTile(basePos, doorTile);
                    if (doorTopTilemap != null && doorTopTile != null)
                    {
                        doorTopTilemap.SetTile(topPos, doorTopTile);
                    }

                    continue;
                }

                // =========================
                // LEFT / RIGHT DOOR 
                // =========================
                if (isLeftDoor || isRightDoor)
                {
                    floorTilemap.SetTile(tilePos, baseTiles[Random.Range(0, baseTiles.Length)]);
                    wallTilemap.SetTile(tilePos, null);

                    // ??t ph?n g?c c?a
                    doorTilemap.SetTile(tilePos, doorTile);

                    // ??t ph?n ng?n c?a nhô lên trên 1 ô
                    if (doorTopTilemap != null && doorTopTile != null)
                    {
                        doorTopTilemap.SetTile(tilePos + Vector3Int.up, doorTopTile);
                    }

                    continue;
                }

                // =========================
                // CHÂN T??NG PHÍA TRÊN
                // =========================
                if (y == currentHeight - 2 && x > 0 && x < currentWidth - 1 && !(x >= midX - halfDoor && x < midX + halfDoor))
                {
                    if (shadowTiles.Length > 0)
                    {
                        floorTilemap.SetTile(tilePos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                    }
                    wallTilemap.SetTile(tilePos, wallTopBot);
                    continue;
                }

                // =========================
                // T??NG NGOÀI
                // =========================
                if (x == 0 || x == currentWidth - 1 || y == 0 || y == currentHeight - 1)
                {
                    if (y == currentHeight - 1)
                    {
                        wallTilemap.SetTile(tilePos, wallTopSide);
                    }
                    else if (y == 0)
                    {
                        wallTilemap.SetTile(tilePos, wallDefault);

                        bool isBottomDoorArea = (x >= midX - halfDoor && x < midX + halfDoor);
                        if (!isBottomDoorArea)
                        {
                            Vector3Int footPos = new Vector3Int(x, y - 1, 0);
                            wallTilemap.SetTile(footPos, wallBottomFoot);
                        }
                    }
                    else
                    {
                        wallTilemap.SetTile(tilePos, wallDefault);
                    }
                }
                // =========================
                // FLOOR TRONG PHÒNG
                // =========================
                else
                {
                    if (x == 1 || x == currentWidth - 2 || y == 1)
                    {
                        if (shadowTiles.Length > 0)
                        {
                            floorTilemap.SetTile(tilePos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                            continue;
                        }
                    }

                    float rand = Random.value;
                    if (rand < 0.90f)
                    {
                        floorTilemap.SetTile(tilePos, baseTiles[Random.Range(0, baseTiles.Length)]);
                    }
                    else
                    {
                        floorTilemap.SetTile(tilePos, detailTiles[Random.Range(0, detailTiles.Length)]);
                    }
                }
            }
        }

        SpawnCenterObstacle(midX, midY);
    }

    private void SpawnCenterObstacle(int centerX, int centerY)
    {
        //
        // =========================
        // LAYOUT 1 - D?U X
        // =========================
        int[,] layout1 = new int[,]
        {
    { 1, 0, 0, 0, 0, 0, 1 },
    { 0, 1, 0, 0, 0, 1, 0 },
    { 0, 0, 1, 1, 1, 0, 0 },
    { 0, 0, 1, 0, 1, 0, 0 },
    { 0, 0, 1, 1, 1, 0, 0 },
    { 0, 1, 0, 0, 0, 1, 0 },
    { 1, 0, 0, 0, 0, 0, 1 }
        };

        //
        // =========================
        // LAYOUT 2 - KHUNG VUÔNG
        // =========================
        int[,] layout2 = new int[,]
        {
    { 1, 1, 1, 1 },
    { 1, 0, 0, 1 },
    { 1, 0, 0, 1 },
    { 1, 1, 1, 1 }
        };

        //
        // =========================
        // LAYOUT 3 - 4 GÓC
        // =========================
        int[,] layout3 = new int[,]
        {
    { 1, 0, 0, 1 },
    { 0, 0, 0, 0 },
    { 0, 0, 0, 0 },
    { 1, 0, 0, 1 }
        };

        //
        // =========================
        // LAYOUT 4 - C?T GI?A
        // =========================
        int[,] layout4 = new int[,]
        {
    { 0, 1, 0 },
    { 0, 1, 0 },
    { 0, 1, 0 },
    { 0, 1, 0 },
    { 0, 1, 0 }
        };

        //
        // =========================
        // LAYOUT 5 - 2 C?C HAI BÊN
        // =========================
        int[,] layout5 = new int[,]
        {
    { 1, 1, 0, 0, 1, 1 },
    { 1, 1, 0, 0, 1, 1 },
    { 1, 1, 0, 0, 1, 1 }
        };

        //
        // =========================
        // LAYOUT 6 - GÓC TRÊN TRÁI
        // =========================
        int[,] layout6 = new int[,]
        {
    { 1, 1, 1, 0, 0 },
    { 1, 1, 0, 0, 0 },
    { 1, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 0 }
        };

        //
        // =========================
        // LAYOUT 7 - GÓC D??I PH?I
        // =========================
        int[,] layout7 = new int[,]
        {
    { 0, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 1 },
    { 0, 0, 0, 1, 1 },
    { 0, 0, 1, 1, 1 }
        };

        //
        // =========================
        // LAYOUT 8 - HÌNH CH? U
        // =========================
        int[,] layout8 = new int[,]
        {
    { 1, 0, 0, 0, 1 },
    { 1, 0, 0, 0, 1 },
    { 1, 0, 0, 0, 1 },
    { 1, 1, 1, 1, 1 }
        };

        //
        // =========================
        // LAYOUT 9 - HAI GÓC ??I DI?N
        // =========================
        int[,] layout9 = new int[,]
        {
    { 1, 1, 0, 0, 0, 0 },
    { 1, 1, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 0, 0 },
    { 0, 0, 0, 0, 1, 1 },
    { 0, 0, 0, 0, 1, 1 }
        };

        //
        // =========================
        // LAYOUT 10 - NHI?U C?C NH?
        // =========================
        int[,] layout10 = new int[,]
        {
    { 1, 0, 1, 0, 1 },
    { 0, 0, 0, 0, 0 },
    { 1, 0, 1, 0, 1 },
    { 0, 0, 0, 0, 0 },
    { 1, 0, 1, 0, 1 }
        };

        //
        // =========================
        // RANDOM CH?N LAYOUT
        // =========================

        int[][,] layouts = new int[][,]
        {
    layout1,
    layout2,
    layout3,
    layout4,
    layout5,
    layout6,
    layout7,
    layout8,
    layout9,
    layout10
        };

        int[,] selectedLayout = layouts[Random.Range(0, layouts.Length)];

        int layoutHeight = selectedLayout.GetLength(0);
        int layoutWidth = selectedLayout.GetLength(1);

        int startX = centerX - (layoutWidth / 2);
        int startY = centerY - (layoutHeight / 2);

        for (int row = 0; row < layoutHeight; row++)
        {
            for (int col = 0; col < layoutWidth; col++)
            {
                if (selectedLayout[row, col] != 1) continue;

                int targetX = startX + col;
                int targetY = startY + (layoutHeight - 1 - row);
                Vector3Int currentPos = new Vector3Int(targetX, targetY, 0);

                obstacleTilemap.SetTile(currentPos, obstacleTile);

                bool isBottomEmpty = (row == layoutHeight - 1) || (selectedLayout[row + 1, col] == 0);

                if (isBottomEmpty && targetY - 1 >= 0)
                {
                    obstacleTilemap.SetTile(new Vector3Int(targetX, targetY - 1, 0), wallTopBot);

                    if (targetY - 2 >= 0 && shadowTiles.Length > 0)
                    {
                        Vector3Int shadowPos = new Vector3Int(targetX, targetY - 2, 0);
                        floorTilemap.SetTile(shadowPos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                    }
                }
            }
        }
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (doorTilemap != null) doorTilemap.ClearAllTiles();
        if (doorTopTilemap != null) doorTopTilemap.ClearAllTiles();
        if (obstacleTilemap != null) obstacleTilemap.ClearAllTiles();

        Debug.Log("?ã xoá s?ch toàn b? Map!");
    }
}