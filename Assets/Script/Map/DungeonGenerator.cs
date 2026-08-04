using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonGenerator : MonoBehaviour
{
    [System.Serializable]
    public class WallDecorationSet
    {
        public TileBase wallDefault;
        public TileBase wallBottomFoot;
    }

    [Header("Dungeon Theme")]
    public DungeonTheme currentTheme;

    [Header("Tilemaps Component")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap obstacleTilemap;

    [Header("Door Prefab")]
    public GameObject doorPrefab;

    [Header("Chest Prefab")]
    public GameObject chestPrefab;

    [Header("Door Tilemap")]
    public Tilemap doorTilemap;
    public Tilemap doorTopTilemap;

    // Các Tile Assets được lấy động trực tiếp từ currentTheme (DungeonTheme ScriptableObject)
    public TileBase doorTile => currentTheme != null ? currentTheme.doorTile : null;
    public TileBase doorTopTile => currentTheme != null ? currentTheme.doorTopTile : null;

    public TileBase[] baseTiles => currentTheme != null ? currentTheme.baseTiles : null;
    public TileBase[] detailTiles => currentTheme != null ? currentTheme.detailTiles : null;
    public TileBase[] shadowTiles => currentTheme != null ? currentTheme.shadowTiles : null;

    public TileBase wallTopSide => currentTheme != null ? currentTheme.wallTopSide : null;
    public TileBase wallTopBot => currentTheme != null ? currentTheme.wallTopBot : null;
    public TileBase wallDefault => currentTheme != null ? currentTheme.wallDefault : null;
    public TileBase wallBottomFoot => currentTheme != null ? currentTheme.wallBottomFoot : null;

    public TileBase obstacleTile => currentTheme != null ? currentTheme.obstacleTile : null;

    [Header("Room Size")]
    public int minRoomSize = 14;
    public int maxRoomSize = 24;

    [Header("Current Room Size")]
    [SerializeField] private int currentWidth;
    [SerializeField] private int currentHeight;

    [Header("Door Size")]
    public int doorSize = 4;

    [Header("Soul Knight Map Config")]
    public int maxRooms = 8;
    public int corridorLength = 10;
    public int corridorWidth = 6;
    public bool randomRoomSize = false;
    public int fixedRoomWidth = 18;
    public int fixedRoomHeight = 18;
    public bool spawnObstacleInStartRoom = false;

    //[Header("Wall Decoration Sets")]
    //public WallDecorationSet[] wallDecorationSets;

    //[Range(0f, 1f)]
    //public float wallDecorationChance = 0.15f;

    private readonly Dictionary<Vector2Int, MapRoom> roomsByGrid = new Dictionary<Vector2Int, MapRoom>();
    private readonly List<MapConnection> connections = new List<MapConnection>();
    private readonly HashSet<Vector3Int> floorPositions = new HashSet<Vector3Int>();

    private class MapRoom
    {
        public Vector2Int gridPos;
        public RectInt rect;
        public bool isStartRoom;
        public RoomController controller;
        public List<BoundsInt> obstacleBoundsList = new List<BoundsInt>(); // Danh sách vùng giới hạn vật cản trong phòng

        public Vector2Int Center
        {
            get
            {
                return new Vector2Int(
                    rect.xMin + rect.width / 2,
                    rect.yMin + rect.height / 2
                );
            }
        }

        public int Left => rect.xMin;
        public int Right => rect.xMin + rect.width - 1;
        public int Bottom => rect.yMin;
        public int Top => rect.yMin + rect.height - 1;
    }

    //private WallDecorationSet GetRandomWallDecorationSet()
    //{
    //    if (wallDecorationSets != null &&
    //        wallDecorationSets.Length > 0 &&
    //        Random.value < wallDecorationChance)
    //    {
    //        return wallDecorationSets[Random.Range(0, wallDecorationSets.Length)];
    //    }

    //    return null;
    //}

    private class MapConnection
    {
        public MapRoom from;
        public MapRoom to;
        public Vector2Int direction;

        public MapConnection(MapRoom from, MapRoom to, Vector2Int direction)
        {
            this.from = from;
            this.to = to;
            this.direction = direction;
        }
    }

    private void Start()
    {
        // Tự động sinh bản đồ khi Scene SampleScene được nạp nếu bản đồ đang trống
        if (roomsByGrid == null || roomsByGrid.Count == 0)
        {
            GenerateSoulKnightMap();
        }
    }

    /// <summary>
    /// Khởi tạo Hạt giống ngẫu nhiên (Map Seed) đồng bộ cho chế độ Co-op
    /// </summary>
    private void InitMapSeed()
    {
        int currentFloor = 1;
        if (GameProgressionManager.Instance != null)
        {
            currentFloor = GameProgressionManager.Instance.currentFloor;
        }

        bool isMultiplayer = NetworkManager.Instance != null && 
                             NetworkManager.Instance.IsLoggedIn && 
                             !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);

        int mapSeed;
        if (isMultiplayer)
        {
            // Trong Co-op: Dùng Hash của mã phòng (RoomCode) kết hợp với Tầng hiện tại
            // Đảm bảo cả Host và Guest tính ra đúng 1 con số mapSeed DUY NHẤT
            string roomCode = NetworkManager.Instance.CurrentRoomId.ToUpper();
            mapSeed = (roomCode.GetHashCode() ^ (currentFloor * 397)) & 0x7FFFFFFF;
            Debug.Log($"[DungeonGenerator] [Co-op] Nạp Map Seed đồng bộ cho phòng '{roomCode}' (Tầng {currentFloor}): {mapSeed}");
        }
        else
        {
            // Trong Solo: Sinh Seed ngẫu nhiên theo thời gian
            mapSeed = UnityEngine.Random.Range(100000, 999999);
            Debug.Log($"[DungeonGenerator] [Solo] Nạp Map Seed ngẫu nhiên (Tầng {currentFloor}): {mapSeed}");
        }

        // Khởi tạo trạng thái ngẫu nhiên cho toàn bộ hàm UnityEngine.Random trong lần sinh map này
        UnityEngine.Random.InitState(mapSeed);
    }

    [ContextMenu("Generate Soul Knight Map")]
    public void GenerateSoulKnightMap()
    {
        // 1. Nạp Hạt giống ngẫu nhiên đồng bộ cho Co-op
        InitMapSeed();

        ClearMap();

        roomsByGrid.Clear();
        connections.Clear();
        floorPositions.Clear();

        GenerateLayout();
        BuildAllRooms();
        BuildAllCorridors();
        RebuildWallsFromFloorPositions();
        DecorateWallsByCluster();
        CreateAllRoomControllers();
        CreateAllDoors();
        // Phân loại phòng (Start, Boss, Chest, Normal) trước khi sinh vật cản và quái
        CategorizeRooms();
        SpawnAllRoomObstacles();

        SpawnAllRoomMobs();

        // Khởi tạo và đảm bảo Minimap UI hiển thị cho tầng mới
        MinimapManager.EnsureMinimapExists();
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.InitializeWithRooms(GetRoomControllers());
        }

        // BỔ SUNG: Làm mới Cache phòng và quái vật cho MultiplayerSyncManager khi chuyển tầng hầm ngục mới
        if (MultiplayerSyncManager.Instance != null)
        {
            MultiplayerSyncManager.Instance.RefreshRoomAndMobNetworkCache();
        }

        Debug.Log("Đã generate map kiểu Soul Knight và cập nhật Minimap & Cache mạng.");
    }

    private void GenerateLayout()
    {
        Vector2Int startGrid = Vector2Int.zero;

        MapRoom startRoom = CreateMapRoom(startGrid, true);
        roomsByGrid.Add(startGrid, startRoom);

        List<MapRoom> expandableRooms = new List<MapRoom>();
        expandableRooms.Add(startRoom);

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        int safeLoop = 0;

        while (roomsByGrid.Count < maxRooms && safeLoop < 500)
        {
            safeLoop++;

            if (expandableRooms.Count == 0)
            {
                break;
            }

            MapRoom anchor = expandableRooms[Random.Range(0, expandableRooms.Count)];

            List<Vector2Int> possibleDirections = new List<Vector2Int>();

            foreach (Vector2Int dir in directions)
            {
                Vector2Int nextGrid = anchor.gridPos + dir;

                if (!roomsByGrid.ContainsKey(nextGrid))
                {
                    possibleDirections.Add(dir);
                }
            }

            if (possibleDirections.Count == 0)
            {
                expandableRooms.Remove(anchor);
                continue;
            }

            Vector2Int chosenDir = possibleDirections[Random.Range(0, possibleDirections.Count)];
            Vector2Int newGrid = anchor.gridPos + chosenDir;

            MapRoom newRoom = CreateMapRoom(newGrid, false);

            roomsByGrid.Add(newGrid, newRoom);
            expandableRooms.Add(newRoom);
            connections.Add(new MapConnection(anchor, newRoom, chosenDir));
        }

        // Tự động gắn 1 phòng Portal nối tiếp ngay sau phòng Boss
        AttachPortalRoomToBossRoom();
    }

    private void AttachPortalRoomToBossRoom()
    {
        MapRoom bossRoom = null;
        float maxDistance = -1f;

        foreach (var kvp in roomsByGrid)
        {
            if (kvp.Value.isStartRoom) continue;

            float dist = Vector2Int.Distance(kvp.Key, Vector2Int.zero);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                bossRoom = kvp.Value;
            }
        }

        if (bossRoom == null) return;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        foreach (Vector2Int dir in directions)
        {
            Vector2Int nextGrid = bossRoom.gridPos + dir;
            if (!roomsByGrid.ContainsKey(nextGrid))
            {
                MapRoom portalRoom = CreateMapRoom(nextGrid, false);
                roomsByGrid.Add(nextGrid, portalRoom);
                connections.Add(new MapConnection(bossRoom, portalRoom, dir));
                Debug.Log($"[DungeonGenerator] Đã tạo phòng Portal riêng biệt tại {nextGrid} nối tiếp phòng Boss tại {bossRoom.gridPos}");
                break;
            }
        }
    }

    private MapRoom CreateMapRoom(Vector2Int gridPos, bool isStartRoom)
    {
        int roomWidth = randomRoomSize
            ? Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2
            : fixedRoomWidth;

        int roomHeight = randomRoomSize
            ? Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2
            : fixedRoomHeight;

        int stepX = fixedRoomWidth + corridorLength;
        int stepY = fixedRoomHeight + corridorLength;

        int worldX = gridPos.x * stepX;
        int worldY = gridPos.y * stepY;

        return new MapRoom
        {
            gridPos = gridPos,
            rect = new RectInt(worldX, worldY, roomWidth, roomHeight),
            isStartRoom = isStartRoom
        };
    }

    private void BuildAllRooms()
    {
        foreach (MapRoom room in roomsByGrid.Values)
        {
            currentWidth = room.rect.width;
            currentHeight = room.rect.height;

            BuildRect(room.rect);
        }
    }

    private void BuildAllCorridors()
    {
        foreach (MapConnection connection in connections)
        {
            RectInt corridorRect = GetCorridorRect(connection);
            BuildRect(corridorRect);
        }
    }

    private RectInt GetCorridorRect(MapConnection connection)
    {
        MapRoom a = connection.from;
        MapRoom b = connection.to;

        // corridorWidth là b? ngang T?NG c?a hành lang, tính c? 2 hàng/c?t t??ng.
        // V?i doorSize = 4 thì corridorWidth nên là 6 ?? ph?n sàn ?i ???c ?úng 4 ô.
        int thickness = GetCorridorThickness();
        int halfDoor = doorSize / 2;

        if (connection.direction == Vector2Int.right)
        {
            int x = a.Right;
            int y = a.Center.y - halfDoor - 1;
            int width = b.Left - a.Right + 1;
            return new RectInt(x, y, width, thickness);
        }

        if (connection.direction == Vector2Int.left)
        {
            int x = b.Right;
            int y = a.Center.y - halfDoor - 1;
            int width = a.Left - b.Right + 1;
            return new RectInt(x, y, width, thickness);
        }

        if (connection.direction == Vector2Int.up)
        {
            int x = a.Center.x - halfDoor - 1;
            int y = a.Top;
            int height = b.Bottom - a.Top + 1;
            return new RectInt(x, y, thickness, height);
        }

        // Down
        {
            int x = a.Center.x - halfDoor - 1;
            int y = b.Top;
            int height = a.Bottom - b.Top + 1;
            return new RectInt(x, y, thickness, height);
        }
    }

    private int GetCorridorThickness()
    {
        // BuildRect dùng 1 tile vi?n m?i bên, nên ph?n ?i ???c = corridorWidth - 2.
        // ?? c?a 4 ô kh?p v?i hành lang, corridorWidth t?i thi?u ph?i là doorSize + 2.
        int minimumThickness = doorSize + 2;
        return Mathf.Max(corridorWidth, minimumThickness);
    }

    // ?ây là b?n m? r?ng t? BuildSolidRoom c?.
    // Phòng và hành lang ??u g?i hàm này ?? dùng chung b? tile wallTopSide, wallTopBot, wallDefault, wallBottomFoot.
    private void BuildRect(RectInt rect)
    {
        for (int x = 0; x < rect.width; x++)
        {
            for (int y = rect.height - 1; y >= 0; y--)
            {
                int worldX = rect.xMin + x;
                int worldY = rect.yMin + y;
                Vector3Int tilePos = new Vector3Int(worldX, worldY, 0);

                if (y == rect.height - 2 && x > 0 && x < rect.width - 1)
                {
                    SetFloor(tilePos, GetRandomShadowOrBaseTile());
                    wallTilemap.SetTile(tilePos, wallTopBot);
                    continue;
                }

                if (x == 0 || x == rect.width - 1 || y == 0 || y == rect.height - 1)
                {
                    if (y == rect.height - 1)
                    {
                        SetWallIfNoFloor(tilePos, wallTopSide);
                    }
                    else if (y == 0)
                    {
                        SetWallIfNoFloor(tilePos, wallDefault);
                        SetWallIfNoFloor(tilePos + Vector3Int.down, wallBottomFoot);
                    }
                    else
                    {
                        SetWallIfNoFloor(tilePos, wallDefault);
                    }
                }
                else
                {
                    bool useShadow = x == 1 || x == rect.width - 2 || y == 1;

                    if (useShadow && shadowTiles.Length > 0)
                    {
                        SetFloor(tilePos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                    }
                    else
                    {
                        SetFloor(tilePos, GetRandomFloorTile());
                    }
                }
            }
        }
    }

    private void SetFloor(Vector3Int pos, TileBase tile)
    {
        floorPositions.Add(pos);
        floorTilemap.SetTile(pos, tile);
        wallTilemap.SetTile(pos, null);
    }

    private void SetWallIfNoFloor(Vector3Int pos, TileBase tile)
    {
        if (floorPositions.Contains(pos))
        {
            return;
        }

        wallTilemap.SetTile(pos, tile);
    }

    private void RebuildWallsFromFloorPositions()
    {
        if (wallTilemap == null)
        {
            return;
        }

        wallTilemap.ClearAllTiles();

        foreach (Vector3Int floorPos in floorPositions)
        {
            Vector3Int up = floorPos + Vector3Int.up;
            Vector3Int down = floorPos + Vector3Int.down;
            Vector3Int left = floorPos + Vector3Int.left;
            Vector3Int right = floorPos + Vector3Int.right;

            // C?nh trên: wallTopBot n?m ?È lên hàng sàn sát t??ng,
            // wallTopSide n?m ? hàng phía trên. M? r?ng sang trái/ph?i 1 ô ?? không m?t 4 góc phòng.
            if (!floorPositions.Contains(up))
            {
                wallTilemap.SetTile(
    floorPos,
    wallTopBot
);
                SetTopSideWall(up);
                SetTopSideWall(up + Vector3Int.left);
                SetTopSideWall(up + Vector3Int.right);
            }

            // C?nh d??i: wallDefault n?m ngay d??i hàng sàn cu?i,
            // wallBottomFoot n?m d??i wallDefault. M? r?ng sang trái/ph?i 1 ô ?? l?p góc d??i.
            if (!floorPositions.Contains(down))
            {
                SetNormalWall(down);
                SetNormalWall(down + Vector3Int.left);
                SetNormalWall(down + Vector3Int.right);

                SetBottomFootWall(down + Vector3Int.down);
                SetBottomFootWall(down + Vector3Int.down + Vector3Int.left);
                SetBottomFootWall(down + Vector3Int.down + Vector3Int.right);
            }

            // C?nh trái/ph?i.
            if (!floorPositions.Contains(left))
            {
                SetNormalWall(left);
            }

            if (!floorPositions.Contains(right))
            {
                SetNormalWall(right);
            }
        }
    }

    private void SetTopSideWall(Vector3Int pos)
    {
        if (floorPositions.Contains(pos))
        {
            return;
        }

        TileBase current = wallTilemap.GetTile(pos);

        // wallTopSide ???c ?u tiên h?n wallDefault ? 4 góc trên.
        if (current == null || current == wallDefault || current == wallBottomFoot)
        {
            wallTilemap.SetTile(
    pos,
    wallTopSide
);
        }
    }

    private void SetNormalWall(Vector3Int pos)
    {
        if (floorPositions.Contains(pos))
        {
            return;
        }

        TileBase current = wallTilemap.GetTile(pos);

        if (current == wallTopSide || current == wallTopBot)
        {
            return;
        }

        wallTilemap.SetTile(pos, wallDefault);
    }

    private void SetBottomFootWall(Vector3Int pos)
    {
        if (floorPositions.Contains(pos))
        {
            return;
        }

        TileBase current = wallTilemap.GetTile(pos);

        if (current != null)
        {
            return;
        }

        wallTilemap.SetTile(pos, wallBottomFoot);
    }

    private TileBase GetRandomFloorTile()
    {
        if (baseTiles == null || baseTiles.Length == 0)
        {
            return null;
        }

        if (detailTiles != null && detailTiles.Length > 0 && Random.value >= 0.90f)
        {
            return detailTiles[Random.Range(0, detailTiles.Length)];
        }

        return baseTiles[Random.Range(0, baseTiles.Length)];
    }

    private TileBase GetRandomShadowOrBaseTile()
    {
        if (shadowTiles != null && shadowTiles.Length > 0)
        {
            return shadowTiles[Random.Range(0, shadowTiles.Length)];
        }

        return GetRandomFloorTile();
    }

    private void CreateAllDoors()
    {
        foreach (MapConnection connection in connections)
        {
            CreateDoorBetween(connection.from, connection.to, connection.direction);
        }
    }

    private void CreateDoorBetween(MapRoom from, MapRoom to, Vector2Int direction)
    {
        List<RoomDoor> fromDoors = null;
        List<RoomDoor> toDoors = null;

        if (direction == Vector2Int.up)
        {
            fromDoors = CreateHorizontalDoorAt(from.Center.x, from.Top, false);
            toDoors = CreateHorizontalDoorAt(to.Center.x, to.Bottom, true);
        }
        else if (direction == Vector2Int.down)
        {
            fromDoors = CreateHorizontalDoorAt(from.Center.x, from.Bottom, true);
            toDoors = CreateHorizontalDoorAt(to.Center.x, to.Top, false);
        }
        else if (direction == Vector2Int.left)
        {
            fromDoors = CreateVerticalDoorAt(from.Left, from.Center.y, true);
            toDoors = CreateVerticalDoorAt(to.Right, to.Center.y, false);
        }
        else if (direction == Vector2Int.right)
        {
            fromDoors = CreateVerticalDoorAt(from.Right, from.Center.y, false);
            toDoors = CreateVerticalDoorAt(to.Left, to.Center.y, true);
        }

        AddDoorsToRoom(from, fromDoors);
        AddDoorsToRoom(to, toDoors);
    }

    private void AddDoorsToRoom(MapRoom room, List<RoomDoor> doors)
    {
        if (room == null || room.controller == null || doors == null)
        {
            return;
        }

        foreach (RoomDoor door in doors)
        {
            room.controller.AddDoor(door);
        }
    }

    private List<RoomDoor> CreateHorizontalDoorAt(int midX, int y, bool isBottomDoor)
    {
        List<RoomDoor> createdDoors = new List<RoomDoor>();

        int halfDoor = doorSize / 2;

        for (int x = midX - halfDoor; x < midX + halfDoor; x++)
        {
            Vector3Int topPos = new Vector3Int(x, y, 0);
            Vector3Int spawnPos = isBottomDoor ? topPos + Vector3Int.down : topPos;

            SetFloor(topPos, GetRandomFloorTile());
            SetFloor(topPos + Vector3Int.down, GetRandomFloorTile());

            wallTilemap.SetTile(topPos, null);
            wallTilemap.SetTile(topPos + Vector3Int.down, null);

            GameObject doorObj = SpawnDoorPrefab(spawnPos);

            if (doorObj != null)
            {
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();

                if (doorScript != null)
                {
                    doorScript.SetupTriggerCollider(
                        new Vector2(1f, 0.4f),
                        Vector2.zero
                    );

                    createdDoors.Add(doorScript);
                }
            }
        }

        return createdDoors;
    }

    private List<RoomDoor> CreateVerticalDoorAt(int x, int midY, bool triggerToRight)
    {
        Debug.Log($"CreateVerticalDoorAt x={x} midY={midY}");

        List<RoomDoor> createdDoors = new List<RoomDoor>();

        int halfDoor = doorSize / 2;
        int orderOffset = 0;

        for (int y = midY + halfDoor - 1; y >= midY - halfDoor; y--)
        {
            Vector3Int tilePos = new Vector3Int(x, y, 0);

            SetFloor(tilePos, GetRandomFloorTile());
            wallTilemap.SetTile(tilePos, null);

            GameObject doorObj = SpawnDoorPrefab(tilePos);

            if (doorObj != null)
            {
                SpriteRenderer mainRenderer = doorObj.GetComponent<SpriteRenderer>();
                RoomDoor doorScript = doorObj.GetComponent<RoomDoor>();

                if (mainRenderer != null)
                {
                    mainRenderer.sortingOrder += orderOffset;
                }

                if (doorScript != null)
                {
                    if (doorScript.upperSpriteRenderer != null)
                    {
                        doorScript.upperSpriteRenderer.sortingOrder += orderOffset;
                    }

                    doorScript.SetupTriggerCollider(
    new Vector2(1f, 1f),
    Vector2.zero
);

                    createdDoors.Add(doorScript);
                }
            }

            orderOffset++;
        }

        return createdDoors;
    }

    private GameObject SpawnDoorPrefab(Vector3Int tilePosition)
    {
        if (doorPrefab == null)
        {
            return null;
        }

        Vector3 worldPos = floorTilemap.CellToWorld(tilePosition) + new Vector3(0.5f, 0.5f, 0);
        GameObject spawnedDoor = Instantiate(doorPrefab, worldPos, Quaternion.identity);
        spawnedDoor.transform.SetParent(transform);

        return spawnedDoor;
    }

    private void SpawnAllRoomObstacles()
    {
        foreach (MapRoom room in roomsByGrid.Values)
        {
            // Bỏ qua không sinh vật cản ở phòng xuất phát (Start), phòng Rương báu (Chest) và phòng Portal
            if (room.controller != null && (room.controller.roomType == RoomType.Chest || room.controller.roomType == RoomType.Portal || (room.isStartRoom && !spawnObstacleInStartRoom)))
            {
                continue;
            }

            // Nếu là phòng Boss -> Sinh các dạng vật cản ở các rìa/cánh phòng, tuyệt đối không sinh ở tâm phòng
            if (room.controller != null && room.controller.roomType == RoomType.Boss)
            {
                SpawnBossRoomObstacles(room);
                continue;
            }

            // 70% sinh vật cản trung tâm, 30% sinh 4 cột ở 4 góc và để trống ở giữa
            if (Random.value < 0.7f)
            {
                SpawnCenterObstacle(room.Center.x, room.Center.y, room);
            }
            else
            {
                SpawnCornerObstacles(room);
            }
        }
    }

    private void SpawnBossRoomObstacles(MapRoom room)
    {
        if (room == null || obstacleTile == null || obstacleTilemap == null) return;

        float rand = Random.value;
        if (rand < 0.35f)
        {
            // 35% chance: Sinh 4 cột đá ở 4 góc phòng
            SpawnCornerObstacles(room);
        }
        else
        {
            // Sinh các dạng layout vật cản khác nhau ở các cánh/rìa phòng, giữ cho tâm phòng hoàn toàn trống trải
            int[,] selectedLayout = GetRandomObstacleLayout();

            int offsetX = Mathf.Max(3, (room.Right - room.Left) / 4);
            int offsetY = Mathf.Max(3, (room.Top - room.Bottom) / 4);

            if (rand < 0.68f)
            {
                // Sinh ở cánh Trái và Phải của phòng Boss
                SpawnLayoutAt(room.Center.x - offsetX, room.Center.y, selectedLayout, room);
                SpawnLayoutAt(room.Center.x + offsetX, room.Center.y, selectedLayout, room);
            }
            else
            {
                // Sinh ở cánh Trên và Dưới của phòng Boss
                SpawnLayoutAt(room.Center.x, room.Center.y + offsetY, selectedLayout, room);
                SpawnLayoutAt(room.Center.x, room.Center.y - offsetY, selectedLayout, room);
            }
        }
    }

    private void SpawnCornerObstacles(MapRoom room)
    {
        if (obstacleTile == null || obstacleTilemap == null || room == null) return;

        int offset = 3;
        int pillarSize = 2;

        Vector2Int[] corners = new Vector2Int[]
        {
            new Vector2Int(room.Left + offset, room.Bottom + offset),
            new Vector2Int(room.Right - offset - pillarSize + 1, room.Bottom + offset),
            new Vector2Int(room.Left + offset, room.Top - offset - pillarSize + 1),
            new Vector2Int(room.Right - offset - pillarSize + 1, room.Top - offset - pillarSize + 1)
        };

        foreach (var startPos in corners)
        {
            for (int x = 0; x < pillarSize; x++)
            {
                for (int y = 0; y < pillarSize; y++)
                {
                    Vector3Int tilePos = new Vector3Int(startPos.x + x, startPos.y + y, 0);
                    obstacleTilemap.SetTile(tilePos, obstacleTile);

                    if (y == 0)
                    {
                        Vector3Int bottomPos = new Vector3Int(startPos.x + x, startPos.y - 1, 0);
                        obstacleTilemap.SetTile(bottomPos, wallTopBot);

                        Vector3Int shadowPos = new Vector3Int(startPos.x + x, startPos.y - 2, 0);
                        if (shadowTiles != null && shadowTiles.Length > 0 && floorTilemap.HasTile(shadowPos))
                        {
                            floorTilemap.SetTile(shadowPos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                        }
                    }
                }
            }

            BoundsInt pillarBounds = new BoundsInt(
                new Vector3Int(startPos.x - 1, startPos.y - 2, 0),
                new Vector3Int(pillarSize + 2, pillarSize + 3, 1)
            );
            room.obstacleBoundsList.Add(pillarBounds);
        }
    }

    private void SpawnCenterObstacle(int centerX, int centerY, MapRoom room = null)
    {
        if (obstacleTile == null || obstacleTilemap == null) return;
        int[,] selectedLayout = GetRandomObstacleLayout();
        SpawnLayoutAt(centerX, centerY, selectedLayout, room);
    }

    private int[,] GetRandomObstacleLayout()
    {
        int[,] layout1 = new int[,]
        {
            { 1, 0, 0, 0, 0, 0, 1 },
            { 0, 1, 0, 0, 0, 1, 0 },
            { 0, 0, 1, 1, 1, 0, 0 },
            { 0, 0, 1, 1, 1, 0, 0 },
            { 0, 0, 1, 1, 1, 0, 0 },
            { 0, 1, 0, 0, 0, 1, 0 },
            { 1, 0, 0, 0, 0, 0, 1 }
        };

        int[,] layout2 = new int[,]
        {
            { 1, 1, 1, 1 },
            { 1, 1, 1, 1 },
            { 1, 1, 1, 1 },
            { 1, 1, 1, 1 }
        };

        int[,] layout3 = new int[,]
        {
            { 1, 0, 0, 1 },
            { 0, 0, 0, 0 },
            { 0, 0, 0, 0 },
            { 1, 0, 0, 1 }
        };

        int[,] layout4 = new int[,]
        {
            { 0, 1, 0 },
            { 0, 1, 0 },
            { 0, 1, 0 },
            { 0, 1, 0 },
            { 0, 1, 0 }
        };

        int[,] layout5 = new int[,]
        {
            { 1, 1, 0, 0, 1, 1 },
            { 1, 1, 0, 0, 1, 1 },
            { 1, 1, 0, 0, 1, 1 }
        };

        int[,] layout6 = new int[,]
        {
            { 1, 1, 1, 0, 0 },
            { 1, 1, 0, 0, 0 },
            { 1, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0 }
        };

        int[,] layout7 = new int[,]
        {
            { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 1 },
            { 0, 0, 0, 1, 1 },
            { 0, 0, 1, 1, 1 }
        };

        int[,] layout8 = new int[,]
        {
            { 1, 1, 1, 1, 1 },
            { 1, 1, 1, 1, 1 },
            { 1, 1, 1, 1, 1 },
            { 1, 1, 1, 1, 1 }
        };

        int[,] layout9 = new int[,]
        {
            { 1, 1, 0, 0, 0, 0 },
            { 1, 1, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 1, 1 },
            { 0, 0, 0, 0, 1, 1 }
        };

        int[,] layout10 = new int[,]
        {
            { 1, 0, 1, 0, 1 },
            { 0, 0, 0, 0, 0 },
            { 1, 0, 1, 0, 1 },
            { 0, 0, 0, 0, 0 },
            { 1, 0, 1, 0, 1 }
        };

        int[,] layout11 = new int[,]
        {
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 1, 1, 1, 1, 1 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 }
        };

        int[,] layout12 = new int[,]
        {
            { 1, 0, 0, 0, 1 },
            { 1, 0, 0, 0, 1 },
            { 1, 1, 1, 1, 1 },
            { 1, 0, 0, 0, 1 },
            { 1, 0, 0, 0, 1 }
        };

        int[,] layout13 = new int[,]
        {
            { 0, 0, 0, 0, 1 },
            { 0, 0, 0, 1, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 1, 0, 0, 0 },
            { 1, 0, 0, 0, 0 }
        };

        int[][,] layouts = new int[][,]
        {
            layout1, layout2, layout3, layout4, layout5,
            layout6, layout7, layout8, layout9, layout10,
            layout11, layout12, layout13
        };

        return layouts[Random.Range(0, layouts.Length)];
    }

    private void SpawnLayoutAt(int centerX, int centerY, int[,] selectedLayout, MapRoom room = null)
    {
        if (obstacleTile == null || obstacleTilemap == null || selectedLayout == null) return;

        int layoutHeight = selectedLayout.GetLength(0);
        int layoutWidth = selectedLayout.GetLength(1);

        int startX = centerX - (layoutWidth / 2);
        int startY = centerY - (layoutHeight / 2);

        if (room != null)
        {
            BoundsInt bounds = new BoundsInt(
                new Vector3Int(startX - 1, startY - 2, 0),
                new Vector3Int(layoutWidth + 2, layoutHeight + 3, 1)
            );
            room.obstacleBoundsList.Add(bounds);
        }

        for (int row = 0; row < layoutHeight; row++)
        {
            for (int col = 0; col < layoutWidth; col++)
            {
                if (selectedLayout[row, col] != 1) continue;

                int targetX = startX + col;
                int targetY = startY + (layoutHeight - 1 - row);
                Vector3Int currentPos = new Vector3Int(targetX, targetY, 0);

                obstacleTilemap.SetTile(currentPos, obstacleTile);

                bool isBottomEmpty = row == layoutHeight - 1 || selectedLayout[row + 1, col] == 0;

                if (isBottomEmpty)
                {
                    Vector3Int bottomPos = new Vector3Int(targetX, targetY - 1, 0);
                    obstacleTilemap.SetTile(bottomPos, wallTopBot);

                    Vector3Int shadowPos = new Vector3Int(targetX, targetY - 2, 0);

                    if (shadowTiles != null && shadowTiles.Length > 0 && floorTilemap.HasTile(shadowPos))
                    {
                        floorTilemap.SetTile(shadowPos, shadowTiles[Random.Range(0, shadowTiles.Length)]);
                    }
                }
            }
        }
    }

    // Các hàm public c? ???c gi? l?i ?? không m?t workflow test 1 phòng.
    public void CreateTopDoor()
    {
        CreateHorizontalDoorAt(currentWidth / 2, currentHeight - 1, false);
    }

    public void CreateBottomDoor()
    {
        CreateHorizontalDoorAt(currentWidth / 2, 0, true);
    }

    public void CreateLeftDoor()
    {
        CreateVerticalDoorAt(0, currentHeight / 2, true);
    }

    public void CreateRightDoor()
    {
        CreateVerticalDoorAt(currentWidth - 1, currentHeight / 2, false);
    }

    [ContextMenu("Generate Single Room Only")]
    public void GenerateSingleRoomOnly()
    {
        ClearMap();

        currentWidth = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;
        currentHeight = Random.Range(minRoomSize / 2, (maxRoomSize / 2) + 1) * 2;

        RectInt singleRoom = new RectInt(0, 0, currentWidth, currentHeight);
        BuildRect(singleRoom);
        RebuildWallsFromFloorPositions();
        DecorateWallsByCluster();

        if (Random.value > 0.3f)
        {
            CreateTopDoor();
        }

        if (Random.value > 0.3f)
        {
            CreateBottomDoor();
        }

        if (Random.value > 0.3f)
        {
            CreateLeftDoor();
        }

        if (Random.value > 0.3f)
        {
            CreateRightDoor();
        }

        SpawnCenterObstacle(currentWidth / 2, currentHeight / 2);
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        if (floorTilemap != null)
        {
            floorTilemap.ClearAllTiles();
        }

        if (wallTilemap != null)
        {
            wallTilemap.ClearAllTiles();
        }

        if (doorTilemap != null)
        {
            doorTilemap.ClearAllTiles();
        }

        if (doorTopTilemap != null)
        {
            doorTopTilemap.ClearAllTiles();
        }

        if (obstacleTilemap != null)
        {
            obstacleTilemap.ClearAllTiles();
        }

        floorPositions.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        Debug.Log("?ã xoá s?ch toàn b? Tilemap và các Prefab c?a c?.");
    }

    private void DecorateWallsByCluster()
    {
        if (currentTheme == null ||
            currentTheme.wallDecorations == null ||
            currentTheme.wallDecorations.Length == 0)
        {
            return;
        }

        BoundsInt bounds = wallTilemap.cellBounds;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase currentTile = wallTilemap.GetTile(pos);

            if (currentTile != wallDefault &&
                currentTile != wallTopSide)
            {
                continue;
            }

            if (Random.value > currentTheme.decorationChance)
            {
                continue;
            }

            var deco = currentTheme.wallDecorations[
                Random.Range(0, currentTheme.wallDecorations.Length)
            ];

            int size = Random.Range(
                deco.minClusterSize,
                deco.maxClusterSize + 1
            );

            // ===== TƯỜNG TRÊN =====
            if (currentTile == wallTopSide)
            {
                DecorateTopWallCluster(pos, deco, size);
                continue;
            }

            // ===== TƯỜNG DƯỚI =====
            Vector3Int footPos = pos + Vector3Int.down;

            if (wallTilemap.GetTile(footPos) == wallBottomFoot)
            {
                DecorateHorizontalWallCluster(pos, deco, size);
                continue;
            }

            // ===== TƯỜNG TRÁI / PHẢI =====
            bool hasFloorLeft =
                floorPositions.Contains(pos + Vector3Int.left);

            bool hasFloorRight =
                floorPositions.Contains(pos + Vector3Int.right);

            if (hasFloorLeft || hasFloorRight)
            {
                DecorateVerticalWallCluster(pos, deco, size);
            }
        }
    }

    private void DecorateHorizontalWallCluster(
    Vector3Int startPos,
    DungeonTheme.WallDecorationSet deco,
    int size)
    {
        for (int i = 0; i < size; i++)
        {
            Vector3Int wallPos = startPos + Vector3Int.right * i;
            Vector3Int footPos = wallPos + Vector3Int.down;

            if (wallTilemap.GetTile(wallPos) != wallDefault)
                break;

            if (wallTilemap.GetTile(footPos) != wallBottomFoot)
                break;

            wallTilemap.SetTile(wallPos, deco.wallDefault);

            if (deco.wallBottomFoot != null)
            {
                wallTilemap.SetTile(footPos, deco.wallBottomFoot);
            }
        }
    }

    private void DecorateVerticalWallCluster(
    Vector3Int startPos,
    DungeonTheme.WallDecorationSet deco,
    int size)
    {
        for (int i = 0; i < size; i++)
        {
            Vector3Int wallPos = startPos + Vector3Int.down * i;

            if (wallTilemap.GetTile(wallPos) != wallDefault)
                break;

            bool hasFloorLeft = floorPositions.Contains(wallPos + Vector3Int.left);
            bool hasFloorRight = floorPositions.Contains(wallPos + Vector3Int.right);

            if (!hasFloorLeft && !hasFloorRight)
                break;

            wallTilemap.SetTile(wallPos, deco.wallDefault);
        }
    }

    private void DecorateTopWallCluster(
    Vector3Int startPos,
    DungeonTheme.WallDecorationSet deco,
    int size)
    {
        for (int i = 0; i < size; i++)
        {
            Vector3Int topPos = startPos + Vector3Int.right * i;
            Vector3Int bodyPos = topPos + Vector3Int.down;

            if (wallTilemap.GetTile(topPos) != wallTopSide)
                break;

            if (wallTilemap.GetTile(bodyPos) != wallTopBot)
                break;

            wallTilemap.SetTile(topPos, deco.wallDefault);

            if (deco.wallBottomFoot != null)
            {
                wallTilemap.SetTile(bodyPos, deco.wallBottomFoot);
            }
        }
    }

    private void SpawnAllRoomMobs()
    {
        if (currentTheme == null || currentTheme.mobs == null || currentTheme.mobs.Length == 0)
        {
            Debug.LogWarning("CurrentTheme chưa có danh sách mobs.");
            return;
        }

        foreach (MapRoom room in roomsByGrid.Values)
        {
            if (room.isStartRoom && !currentTheme.spawnMobInStartRoom)
                continue;

            // Nếu là phòng Portal (cổng qua tầng), không sinh quái mà khởi tạo cổng dịch chuyển ở tâm phòng
            if (room.controller != null && room.controller.roomType == RoomType.Portal)
            {
                room.controller.roomCleared = true;
                room.controller.chestSpawned = true;
                room.controller.SpawnTeleportPortal();
                Debug.Log($"[DungeonGenerator] Đã khởi tạo Cổng Dịch Chuyển tại tâm phòng Portal: {room.gridPos}");
                continue;
            }

            // Nếu là phòng Rương Báu, không sinh quái mà sinh rương thưởng trực tiếp
            if (room.controller != null && room.controller.roomType == RoomType.Chest)
            {
                SpawnChestInChestRoom(room);
                continue;
            }

            SpawnMobsInRoom(room);
        }
    }

    /// <summary>
    /// Sinh rương thưởng trực tiếp tại tâm phòng Rương Báu
    /// </summary>
    private void SpawnChestInChestRoom(MapRoom room)
    {
        if (room.controller == null) return;

        if (chestPrefab != null)
        {
            Vector3 worldPos = floorTilemap.CellToWorld((Vector3Int)room.Center) + new Vector3(0.5f, 0.5f, 0f);
            if (room.controller != null)
            {
                worldPos = room.controller.GetSafeChestSpawnPosition(worldPos);
            }
            GameObject chestObj = Instantiate(chestPrefab, worldPos, Quaternion.identity);
            chestObj.transform.SetParent(transform);

            // Cấu hình phòng Rương đã được dọn sạch để mở cửa
            room.controller.roomCleared = true;
            room.controller.chestSpawned = true; // Chặn sinh rương thêm lần nữa khi dọn dẹp
            Debug.Log($"[DungeonGenerator] Đã sinh Rương tại phòng Rương báu: {room.gridPos}");
        }
        else
        {
            Debug.LogWarning("[DungeonGenerator] Chưa gán chestPrefab để sinh trong phòng Rương báu.");
        }
    }

    private void SpawnMobsInRoom(MapRoom room)
    {
        // Kiểm tra xem phòng hiện tại có phải là phòng Boss hay không
        bool isBossRoom = room.controller != null && room.controller.roomType == RoomType.Boss;

        int mobCount = isBossRoom ? 1 : Random.Range(
            currentTheme.minMobPerRoom,
            currentTheme.maxMobPerRoom + 1
        );

        for (int i = 0; i < mobCount; i++)
        {
            GameObject mobPrefab = GetRandomMobPrefabFromTheme();

            if (mobPrefab == null)
                continue;

            // Luôn sử dụng GetRandomMobSpawnCell(room) để kiểm tra 4 lớp an toàn (tránh đè vật cản/tường)
            Vector3Int cellPos = GetRandomMobSpawnCell(room);
            Vector3 worldPos = floorTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);

            GameObject mobObj = Instantiate(mobPrefab, worldPos, Quaternion.identity);
            mobObj.transform.SetParent(transform);

            // Cấu hình Boss hoặc Mini-Boss nếu là phòng Boss
            if (isBossRoom)
            {
                int floor = 1;
                if (GameProgressionManager.Instance != null)
                {
                    floor = GameProgressionManager.Instance.currentFloor;
                }

                // Nếu là tầng 5 -> Boss cuối Goliath Root khổng lồ
                if (floor >= 5)
                {
                    mobObj.name = "ELITE BOSS - GOLIATH ROOT";
                    mobObj.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

                    MobHealth mobHealth = mobObj.GetComponent<MobHealth>();
                    if (mobHealth != null)
                    {
                        mobHealth.maxHealth = 1500; // Đặt máu khủng cho Boss cuối
                    }
                }
                else
                {
                    // Mini-Boss ở các tầng dưới
                    mobObj.name = $"MINI BOSS - FLOOR {floor}";
                    mobObj.transform.localScale = new Vector3(1.7f, 1.7f, 1f);

                    MobHealth mobHealth = mobObj.GetComponent<MobHealth>();
                    if (mobHealth != null)
                    {
                        mobHealth.maxHealth = 400 + (floor * 100); // Máu tăng dần qua các tầng
                    }
                }
            }

            MobHealth mobH = mobObj.GetComponent<MobHealth>();
            if (mobH != null && room.controller != null)
            {
                room.controller.AddMob(mobH);
            }
            else
            {
                Debug.LogWarning($"Không add được mob vào room {room.gridPos}");
            }

            MobNetworkIdentity identity = mobObj.GetComponent<MobNetworkIdentity>();
            if (identity != null)
            {
                identity.roomId = room.gridPos.ToString();
            }
        }
    }

    private GameObject GetRandomMobPrefabFromTheme()
    {
        int totalWeight = 0;

        foreach (var mob in currentTheme.mobs)
        {
            if (mob.mobPrefab != null && mob.weight > 0)
            {
                totalWeight += mob.weight;
            }
        }

        if (totalWeight <= 0)
            return null;

        int randomValue = Random.Range(0, totalWeight);

        foreach (var mob in currentTheme.mobs)
        {
            if (mob.mobPrefab == null || mob.weight <= 0)
                continue;

            if (randomValue < mob.weight)
                return mob.mobPrefab;

            randomValue -= mob.weight;
        }

        return null;
    }

    private Vector3Int GetRandomMobSpawnCell(MapRoom room)
    {
        int safeLoop = 0;
        int padding = currentTheme.mobSpawnPadding;

        while (safeLoop < 200)
        {
            safeLoop++;

            int x = Random.Range(room.Left + padding, room.Right - padding + 1);
            int y = Random.Range(room.Bottom + padding, room.Top - padding + 1);

            Vector3Int cellPos = new Vector3Int(x, y, 0);

            if (IsCellValidForMobSpawn(cellPos, room))
            {
                return cellPos;
            }
        }

        // Fallback an toàn: Quét các góc phòng (lùi vào từ tường) để tìm vị trí mở rộng rãi
        for (int offset = 2; offset <= 6; offset++)
        {
            Vector3Int[] candidateCells = new Vector3Int[]
            {
                new Vector3Int(room.Left + offset, room.Bottom + offset, 0),
                new Vector3Int(room.Right - offset, room.Bottom + offset, 0),
                new Vector3Int(room.Left + offset, room.Top - offset, 0),
                new Vector3Int(room.Right - offset, room.Top - offset, 0)
            };

            foreach (var cell in candidateCells)
            {
                if (IsCellValidForMobSpawn(cell, room))
                {
                    return cell;
                }
            }
        }

        // Dự phòng cuối cùng: Trả về mép góc phòng
        return new Vector3Int(room.Left + 2, room.Bottom + 2, 0);
    }

    private bool IsCellValidForMobSpawn(Vector3Int cellPos, MapRoom room)
    {
        // 1. Phải có Tile sàn và KHÔNG được có Tile tường hay Tile vật cản
        if (floorTilemap == null || !floorTilemap.HasTile(cellPos)) return false;
        if (wallTilemap != null && wallTilemap.HasTile(cellPos)) return false;
        if (obstacleTilemap != null && obstacleTilemap.HasTile(cellPos)) return false;

        // 2. Không nằm trong bất kỳ BoundsInt vật cản nào của phòng
        if (room != null && room.obstacleBoundsList != null)
        {
            foreach (var bounds in room.obstacleBoundsList)
            {
                if (bounds.Contains(cellPos))
                {
                    return false;
                }
            }
        }

        // 3. Kiểm tra va chạm vật lý tại vị trí thế giới (tránh kẹt collider)
        Vector3 worldPos = floorTilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, 0.5f);
        foreach (var col in hits)
        {
            if (col == null || col.isTrigger) continue;
            string cName = col.name;
            if (cName.Contains("Door") || cName.Contains("Obstacle") || cName.Contains("Pillar") || cName.Contains("Wall") || cName.Contains("Tilemap"))
            {
                return false;
            }
        }

        // 4. Kiểm tra chống nhốt trong hốc kín: ô này phải có ít nhất 2 ô lân cận (N, S, E, W) là sàn trống
        int openNeighbors = 0;
        Vector3Int[] directions = new Vector3Int[]
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
        };
        foreach (var dir in directions)
        {
            Vector3Int neighborCell = cellPos + dir;
            bool nFloor = floorTilemap.HasTile(neighborCell);
            bool nWall = wallTilemap != null && wallTilemap.HasTile(neighborCell);
            bool nObs = obstacleTilemap != null && obstacleTilemap.HasTile(neighborCell);
            if (nFloor && !nWall && !nObs)
            {
                openNeighbors++;
            }
        }

        if (openNeighbors < 2)
        {
            return false; // Ô bị quây hẹp/kẹt 3-4 hướng bởi tường đá -> Bỏ qua
        }

        return true;
    }

    private void CreateAllRoomControllers()
    {
        foreach (MapRoom room in roomsByGrid.Values)
        {
            GameObject roomObj = new GameObject("Room_" + room.gridPos);
            roomObj.transform.SetParent(transform);

            Vector3 centerWorld = floorTilemap.CellToWorld(
                new Vector3Int(room.Center.x, room.Center.y, 0)
            ) + new Vector3(0.5f, 0.5f, 0f);

            roomObj.transform.position = centerWorld;

            BoxCollider2D trigger = roomObj.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;

            trigger.size = new Vector2(
            room.rect.width - 1,
            room.rect.height - 1
);

            RoomController controller = roomObj.AddComponent<RoomController>();
            controller.chestPrefab = chestPrefab; // Truyền tham chiếu rương thưởng
            room.controller = controller;
        }
    }

    /// <summary>
    /// Lấy danh sách các RoomController theo gridPos để MinimapManager truy vấn
    /// </summary>
    public Dictionary<Vector2Int, RoomController> GetRoomControllers()
    {
        var dict = new Dictionary<Vector2Int, RoomController>();
        foreach (var kvp in roomsByGrid)
        {
            if (kvp.Value.controller != null)
            {
                dict.Add(kvp.Key, kvp.Value.controller);
            }
        }
        return dict;
    }

    /// <summary>
    /// Phân loại phòng (Chỉ gán phòng Start/Home để kiểm tra hoạt động)
    /// </summary>
    private void CategorizeRooms()
    {
        if (roomsByGrid.Count == 0) return;

        // 1. Đặt tất cả các phòng về Normal mặc định
        foreach (var kvp in roomsByGrid)
        {
            if (kvp.Value.controller != null)
            {
                kvp.Value.controller.roomType = RoomType.Normal;
            }
        }

        // 2. Gán phòng Start
        foreach (var kvp in roomsByGrid)
        {
            if (kvp.Value.isStartRoom)
            {
                kvp.Value.isStartRoom = true;
                if (kvp.Value.controller != null)
                {
                    kvp.Value.controller.roomType = RoomType.Start;
                    kvp.Value.controller.isVisited = true;
                }
            }
        }

        // 3. Tìm phòng Boss và phòng Portal
        MapRoom bossRoom = null;
        MapRoom portalRoom = null;
        float maxDistance = -1f;

        foreach (var kvp in roomsByGrid)
        {
            if (kvp.Value.isStartRoom) continue;

            float dist = Vector2Int.Distance(kvp.Key, Vector2Int.zero);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                bossRoom = kvp.Value;
            }
        }

        if (bossRoom != null)
        {
            // Phòng Portal là phòng nối với phòng Boss
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                Vector2Int nGrid = bossRoom.gridPos + dir;
                if (roomsByGrid.TryGetValue(nGrid, out MapRoom candidateRoom))
                {
                    if (!candidateRoom.isStartRoom)
                    {
                        portalRoom = candidateRoom;
                        break;
                    }
                }
            }

            if (bossRoom.controller != null)
            {
                bossRoom.controller.roomType = RoomType.Boss;
                Debug.Log($"[DungeonGenerator] Đã gán phòng Boss tại tọa độ lưới: {bossRoom.gridPos}");
            }
        }

        if (portalRoom != null && portalRoom.controller != null)
        {
            portalRoom.controller.roomType = RoomType.Portal;
            Debug.Log($"[DungeonGenerator] Đã gán phòng Portal riêng biệt tại tọa độ lưới: {portalRoom.gridPos}");
        }

        // 4. Tìm các phòng cụt (chỉ có duy nhất 1 liên kết với các phòng khác) và không trùng Start/Boss/Portal
        List<MapRoom> deadEnds = new List<MapRoom>();
        List<MapRoom> otherCandidates = new List<MapRoom>();

        foreach (var kvp in roomsByGrid)
        {
            MapRoom room = kvp.Value;
            if (room.isStartRoom || room == bossRoom || room == portalRoom) continue;

            int neighbors = 0;
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                if (roomsByGrid.ContainsKey(room.gridPos + dir))
                {
                    neighbors++;
                }
            }

            if (neighbors == 1)
            {
                deadEnds.Add(room);
            }
            else
            {
                otherCandidates.Add(room);
            }
        }

        MapRoom chestRoom = null;
        if (deadEnds.Count > 0)
        {
            chestRoom = deadEnds[UnityEngine.Random.Range(0, deadEnds.Count)];
        }
        else if (otherCandidates.Count > 0)
        {
            chestRoom = otherCandidates[UnityEngine.Random.Range(0, otherCandidates.Count)];
        }

        if (chestRoom != null && chestRoom.controller != null)
        {
            if (chestRoom.controller.roomType == RoomType.Normal)
            {
                chestRoom.controller.roomType = RoomType.Chest;
                Debug.Log($"[DungeonGenerator] Đã gán phòng Rương báu tại tọa độ lưới: {chestRoom.gridPos}");
            }
        }
    }
}

