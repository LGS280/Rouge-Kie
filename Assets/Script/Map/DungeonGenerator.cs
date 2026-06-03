using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Tilemaps Component")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap obstacleTilemap;

    [Header("Door Prefab Block Nhân V?t")]
    public GameObject doorPrefab;   // Kéo Prefab có script RoomDoor vào ?ây

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
    public TileBase doorTile;        // Sprite ph?n g?c c?a (Có th? ?? tr?ng n?u dùng hoàn toàn Prefab)
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
        // 1. Xóa s?ch map c?
        ClearMap();

        // 2. Tính toán kích th??c phòng ng?u nhiên (luôn là s? ch?n)
        currentWidth = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;
        currentHeight = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;

        // 3. B??C 1: D?NG PHÒNG KÍN HOÀN TOÀN
        BuildSolidRoom();

        // 4. B??C 2: TR? C?A (G?n Prefab ??ng)
        if (Random.value > 0.3f) CreateTopDoor();
        if (Random.value > 0.3f) CreateBottomDoor();
        if (Random.value > 0.3f) CreateLeftDoor();
        if (Random.value > 0.3f) CreateRightDoor();

        // 5. Sinh v?t c?n ? gi?a phòng
        int midX = currentWidth / 2;
        int midY = currentHeight / 2;
        SpawnCenterObstacle(midX, midY);
    }

    // ==========================================
    // HÀM V? PHÒNG KÍN (Không quan tâm t?i c?a)
    // ==========================================
    private void BuildSolidRoom()
    {
        for (int x = 0; x < currentWidth; x++)
        {
            for (int y = currentHeight - 1; y >= 0; y--)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);

                if (y == currentHeight - 2 && x > 0 && x < currentWidth - 1)
                {
                    if (shadowTiles.Length > 0)
                    {
                        floorTilemap.SetTile(tilePos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                    }
                    wallTilemap.SetTile(tilePos, wallTopBot);
                    continue;
                }

                if (x == 0 || x == currentWidth - 1 || y == 0 || y == currentHeight - 1)
                {
                    if (y == currentHeight - 1)
                    {
                        wallTilemap.SetTile(tilePos, wallTopSide);
                    }
                    else if (y == 0)
                    {
                        wallTilemap.SetTile(tilePos, wallDefault);
                        wallTilemap.SetTile(new Vector3Int(x, y - 1, 0), wallBottomFoot);
                    }
                    else
                    {
                        wallTilemap.SetTile(tilePos, wallDefault);
                    }
                }
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
    }

    // =========================================================================
    // CÁC HÀM TR? C?A ??NG (Sinh Prefab ?è lên g?ch t??ng có s?n)
    // =========================================================================

    public void CreateTopDoor()
    {
        int midX = currentWidth / 2;
        int halfDoor = doorSize / 2;
        int y = currentHeight - 1;

        for (int x = midX - halfDoor; x < midX + halfDoor; x++)
        {
            Vector3Int topPos = new Vector3Int(x, y, 0);
            Vector3Int basePos = topPos + Vector3Int.down;

            floorTilemap.SetTile(basePos + Vector3Int.down, baseTiles[Random.Range(0, baseTiles.Length)]);
            wallTilemap.SetTile(topPos, null);
            wallTilemap.SetTile(basePos, null);

            GameObject doorObj = SpawnDoorPrefab(basePos);

            // ??I T?I ?ÂY - C?A TRÊN: ??y Trigger lùi D??I (h??ng vào trong lòng phòng)
            if (doorObj != null)
            {
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();
                if (doorScript != null) doorScript.SetupTriggerCollider(new Vector2(1f, 0.2f), new Vector2(0f, -0.4f));
            }
        }
    }

    public void CreateBottomDoor()
    {
        int midX = currentWidth / 2;
        int halfDoor = doorSize / 2;
        int y = 0;

        for (int x = midX - halfDoor; x < midX + halfDoor; x++)
        {
            Vector3Int topPos = new Vector3Int(x, y, 0);
            Vector3Int basePos = topPos + Vector3Int.down;

            floorTilemap.SetTile(basePos, baseTiles[Random.Range(0, baseTiles.Length)]);
            floorTilemap.SetTile(topPos, baseTiles[Random.Range(0, baseTiles.Length)]);
            wallTilemap.SetTile(topPos, null);
            wallTilemap.SetTile(basePos, null);

            // 1. ??I T?I ?ÂY: Sinh c?a t?i basePos thay vì topPos ?? d?i c?a xu?ng 1 ô
            GameObject doorObj = SpawnDoorPrefab(basePos);

            // 2. ??I T?I ?ÂY: Tính l?i Offset cho Trigger Collider
            // Do c?a d?i xu?ng basePos (Y = -1), ?? Trigger n?m ? rìa ngoài cùng (mép d??i c?a ô topPos c?, t?c Y = -0.4f),
            // kho?ng cách tính t? tâm c?a c?a m?i (basePos) lên v? trí ?ó s? là: 1 ô - 0.4f = 0.6f.
            if (doorObj != null)
            {
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();
                if (doorScript != null)
                {
                    // Size d?t ngang (1f, 0.2f), Offset Y ??y lên 0.6f ?? n?m sát rìa ngoài cùng c?a ô c?a m?i d?i
                    doorScript.SetupTriggerCollider(new Vector2(1f, 0.2f), new Vector2(0f, 0.6f));
                }
            }
        }
    }

    public void CreateLeftDoor()
    {
        int midY = currentHeight / 2;
        int halfDoor = doorSize / 2;
        int x = 0;

        int orderOffset = 0;

        for (int y = midY + halfDoor - 1; y >= midY - halfDoor; y--)
        {
            Vector3Int tilePos = new Vector3Int(x, y, 0);
            floorTilemap.SetTile(tilePos, baseTiles[Random.Range(0, baseTiles.Length)]);
            wallTilemap.SetTile(tilePos, null);

            GameObject doorObj = SpawnDoorPrefab(tilePos);

            if (doorObj != null)
            {
                SpriteRenderer mainRenderer = doorObj.GetComponent<SpriteRenderer>();
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();

                if (mainRenderer != null) mainRenderer.sortingOrder += orderOffset;
                if (doorScript != null)
                {
                    if (doorScript.upperSpriteRenderer != null) doorScript.upperSpriteRenderer.sortingOrder += orderOffset;

                    // ??I T?I ?ÂY - C?A TRÁI: ??y Trigger sang PH?I (h??ng vào trong lòng phòng)
                    doorScript.SetupTriggerCollider(new Vector2(0.2f, 1f), new Vector2(0.4f, 0f));
                }
            }
            orderOffset++;
        }
    }

    public void CreateRightDoor()
    {
        int midY = currentHeight / 2;
        int halfDoor = doorSize / 2;
        int x = currentWidth - 1;

        int orderOffset = 0;

        for (int y = midY + halfDoor - 1; y >= midY - halfDoor; y--)
        {
            Vector3Int tilePos = new Vector3Int(x, y, 0);
            floorTilemap.SetTile(tilePos, baseTiles[Random.Range(0, baseTiles.Length)]);
            wallTilemap.SetTile(tilePos, null);

            GameObject doorObj = SpawnDoorPrefab(tilePos);

            if (doorObj != null)
            {
                SpriteRenderer mainRenderer = doorObj.GetComponent<SpriteRenderer>();
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();

                if (mainRenderer != null) mainRenderer.sortingOrder += orderOffset;
                if (doorScript != null)
                {
                    if (doorScript.upperSpriteRenderer != null) doorScript.upperSpriteRenderer.sortingOrder += orderOffset;

                    // ??I T?I ?ÂY - C?A PH?I: ??y Trigger sang TRÁI (h??ng vào trong lòng phòng)
                    doorScript.SetupTriggerCollider(new Vector2(0.2f, 1f), new Vector2(-0.4f, 0f));
                }
            }
            orderOffset++;
        }
    }

    private GameObject SpawnDoorPrefab(Vector3Int tilePosition)
    {
        if (doorPrefab == null) return null;

        Vector3 worldPos = floorTilemap.CellToWorld(tilePosition) + new Vector3(0.5f, 0.5f, 0);
        GameObject spawnedDoor = Instantiate(doorPrefab, worldPos, Quaternion.identity);
        spawnedDoor.transform.SetParent(transform);

        return spawnedDoor; 
    }

    private void SpawnCenterObstacle(int centerX, int centerY)
    {
        int[,] layout1 = new int[,] {
            { 1, 0, 0, 0, 0, 0, 1 },
            { 0, 1, 0, 0, 0, 1, 0 },
            { 0, 0, 1, 1, 1, 0, 0 },
            { 0, 0, 1, 0, 1, 0, 0 },
            { 0, 0, 1, 1, 1, 0, 0 },
            { 0, 1, 0, 0, 0, 1, 0 },
            { 1, 0, 0, 0, 0, 0, 1 }
        };
        int[,] layout2 = new int[,] {
            { 1, 1, 1, 1 },
            { 1, 0, 0, 1 },
            { 1, 0, 0, 1 },
            { 1, 1, 1, 1 }
        };
        int[,] layout3 = new int[,] {
            { 1, 0, 0, 1 },
            { 0, 0, 0, 0 },
            { 0, 0, 0, 0 },
            { 1, 0, 0, 1 }
        };
        int[,] layout4 = new int[,] {
            { 0, 1, 0 }, { 0, 1, 0 }, { 0, 1, 0 }, { 0, 1, 0 }, { 0, 1, 0 }
        };
        int[,] layout5 = new int[,] {
            { 1, 1, 0, 0, 1, 1 }, { 1, 1, 0, 0, 1, 1 }, { 1, 1, 0, 0, 1, 1 }
        };
        int[,] layout6 = new int[,] {
            { 1, 1, 1, 0, 0 }, { 1, 1, 0, 0, 0 }, { 1, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0 }
        };
        int[,] layout7 = new int[,] {
            { 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 1 }, { 0, 0, 0, 1, 1 }, { 0, 0, 1, 1, 1 }
        };
        int[,] layout8 = new int[,] {
            { 1, 0, 0, 0, 1 }, { 1, 0, 0, 0, 1 }, { 1, 0, 0, 0, 1 }, { 1, 1, 1, 1, 1 }
        };
        int[,] layout9 = new int[,] {
            { 1, 1, 0, 0, 0, 0 }, { 1, 1, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 1, 1 }, { 0, 0, 0, 0, 1, 1 }
        };
        int[,] layout10 = new int[,] {
            { 1, 0, 1, 0, 1 }, { 0, 0, 0, 0, 0 }, { 1, 0, 1, 0, 1 }, { 0, 0, 0, 0, 0 }, { 1, 0, 1, 0, 1 }
        };

        int[][,] layouts = new int[][,] {
            layout1, layout2, layout3, layout4, layout5,
            layout6, layout7, layout8, layout9, layout10
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
        // Xóa các ô g?ch trên Tilemap
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (doorTilemap != null) doorTilemap.ClearAllTiles();
        if (doorTopTilemap != null) doorTopTilemap.ClearAllTiles();
        if (obstacleTilemap != null) obstacleTilemap.ClearAllTiles();

        // XÓA CÁC PREFAB C?A ?Ã SINH TR??C ?Ó TRONG SCENE
        // Quét t?t c? các Object con n?m d??i Generator này và tiêu di?t chúng
        for (int i = this.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(this.transform.GetChild(i).gameObject);
        }

        Debug.Log("?ã xoá s?ch toàn b? Tilemap và các Prefab c?a c?!");
    }
}