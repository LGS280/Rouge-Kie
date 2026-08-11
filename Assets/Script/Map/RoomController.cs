using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Normal, // Phòng thường có quái
    Start,  // Phòng xuất phát (Home)
    Boss,   // Phòng Boss
    Chest,  // Phòng rương báu
    Portal  // Phòng Cổng Dịch Chuyển (phòng trống dành riêng cho Portal qua tầng)
}

public class RoomController : MonoBehaviour
{
    [Header("Multiplayer Settings (Auto-generated)")]
    // TỰ ĐỘNG HÓA: ID này sẽ tự sinh bằng GetInstanceID().ToString() lúc Runtime, không cần điền tay nữa!
    public string roomUniqueId { get; set; }

    [Header("Room Status")]
    public RoomType roomType = RoomType.Normal; // Loại phòng (mặc định là Normal)
    public bool isVisited = false;              // Trạng thái đã đi qua phòng
    public bool roomCleared = false;
    public bool roomStarted = false;
    private Transform currentPlayer;
    [HideInInspector] public bool chestSpawned = false;
    
    [Header("Reward Chest Prefab")]
    public GameObject chestPrefab;

    // Cache lại Collider của phòng để chia sẻ cho các MobAI lấy biên di chuyển
    public Collider2D RoomCollider { get; private set; }

    private readonly List<RoomDoor> doors = new List<RoomDoor>();
    private readonly List<MobHealth> mobs = new List<MobHealth>();

    private void Awake()
    {
        RoomCollider = GetComponent<Collider2D>();

        // TỰ ĐỘNG SINH ID: Lấy mã InstanceID độc nhất của Object này trong Scene hiện tại
        if (string.IsNullOrEmpty(roomUniqueId))
        {
            roomUniqueId = gameObject.GetInstanceID().ToString();
        }
    }

    public void AddDoor(RoomDoor door)
    {
        if (door != null && !doors.Contains(door))
        {
            doors.Add(door);
            door.SetOwnerRoom(this);
        }
    }

    public void AddMob(MobHealth mob)
    {
        if (mob == null || mobs.Contains(mob))
            return;

        mobs.Add(mob);
        mob.OnDeath += OnMobDeath;

        // Tự động gán tham chiếu phòng cho MobAI mới nạp
        MobAI mobAI = mob.GetComponent<MobAI>();
        if (mobAI != null)
        {
            mobAI.myRoom = this;
        }

        Debug.Log($"{gameObject.name} AddMob: {mob.name}");
    }

    private void Update()
    {
        if (roomStarted && !roomCleared)
        {
            CheckRoomCleared();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Kiểm tra xem có phải là người chơi nội bộ (local player) hay không
            // Tránh việc remote player đi qua cửa kích hoạt minimap của người chơi hiện tại
            if (collision.GetComponent<PlayerController>() != null)
            {
                isVisited = true;
                if (MinimapManager.Instance != null)
                {
                    MinimapManager.Instance.OnPlayerEnterRoom(this);
                }
            }

            currentPlayer = collision.transform;
            TryStartRoomCombat();
        }
    }

    public void TryStartRoomCombat()
    {
        CollectMobsInsideRoom();
        CollectDoorsNearRoom();

        Debug.Log($"{gameObject.name} TryStartRoomCombat - AliveMobs: {GetAliveMobCount()}");

        if (roomCleared || roomStarted)
            return;

        // KIỂM TRA AN TOÀN: Chỉ bắt đầu combat nếu người chơi thực sự đã bước vào trong phòng.
        // Việc này tránh trường hợp người chơi đứng ở hành lang chạm nhẹ vào trigger cửa làm sập cửa sớm.
        if (currentPlayer == null)
        {
            GameObject pObj = GameObject.FindWithTag("Player");
            if (pObj != null) currentPlayer = pObj.transform;
        }

        if (currentPlayer != null && RoomCollider != null)
        {
            Bounds bounds = RoomCollider.bounds;
            bounds.Expand(0.8f); // Mở rộng biên an toàn 0.8 unit để bao phủ cả mép trong của cửa
            if (!bounds.Contains(currentPlayer.position))
            {
                return; // Chưa bước vào phòng, bỏ qua đóng cửa
            }
        }

        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);

        if (GetAliveMobCount() <= 0)
        {
            if (isMultiplayer)
            {
                NetworkManager.Instance.SendRoomClearedEvent(roomUniqueId);
            }
            else
            {
                ClearRoom();
            }
            return;
        }

        // Gửi ID tự động lên Server
        if (isMultiplayer && currentPlayer != null)
        {
            // SỬA THEO YÊU CẦU: Lấy tọa độ mép cửa phía trong phòng (safeSpot) thay vì giữa phòng,
            // tránh trường hợp giữa phòng có vật cản.
            Vector3 roomCenter = transform.position;
            Vector3 dirToCenter = (roomCenter - currentPlayer.position).normalized;
            Vector3 safeTeleportPos = currentPlayer.position + dirToCenter * 2.0f; // Đẩy vào trong 2 unit từ mép cửa

            NetworkManager.Instance.SendRoomCombatTrigger(roomUniqueId, safeTeleportPos);
        }
        else
        {
            ExecuteStartCombatLocal();
        }
    }

    public void ExecuteStartCombatLocal()
    {
        if (roomStarted || roomCleared) return;

        CollectMobsInsideRoom();
        CollectDoorsNearRoom();

        roomStarted = true;
        PushPlayerInsideRoom();
        CloseDoors();
        ActivateAllMobsInRoom();
    }

    public void ExecuteClearRoomLocal()
    {
        ExecuteClearRoomLocal(transform.position); // Mặc định sinh rương ở tâm phòng
    }

    public void ExecuteClearRoomLocal(Vector3 spawnPosition)
    {
        roomCleared = true;
        roomStarted = false;
        CollectDoorsNearRoom();
        OpenDoors();

        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.LogRoomCleared();
        }

        // Thông báo cho MinimapManager biết phòng này đã được dọn sạch quái
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.OnRoomCleared(this);
        }

        // Sinh rương thưởng khi dọn sạch phòng quái (Bỏ qua phòng xuất phát Start và phòng Portal)
        if (roomType != RoomType.Start && roomType != RoomType.Portal && !chestSpawned)
        {
            chestSpawned = true;
            Vector3 chestPos = spawnPosition;
            SpawnRewardChest(chestPos);
        }

        // BỔ SUNG: Nếu đây là phòng Boss hoặc phòng Portal, tự động đảm bảo Cổng Dịch Chuyển xuất hiện
        if (roomType == RoomType.Boss || roomType == RoomType.Portal)
        {
            EnsureTeleportPortalExists();
        }
    }

    /// <summary>
    /// Tự động đảm bảo Cổng Dịch Chuyển tồn tại tại tâm phòng Portal (hoặc phòng Boss) khi hạ gục Miniboss
    /// </summary>
    public void EnsureTeleportPortalExists()
    {
        if (GameObject.Find("TeleportPortal") != null) return;

        RoomController[] allRooms = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        RoomController portalRoom = null;
        foreach (var r in allRooms)
        {
            if (r.roomType == RoomType.Portal)
            {
                portalRoom = r;
                break;
            }
        }

        if (portalRoom != null)
        {
            portalRoom.SpawnTeleportPortal();
        }
        else
        {
            SpawnTeleportPortal();
        }
    }

    /// <summary>
    /// Sinh cổng dịch chuyển mượt mà tại tâm phòng
    /// </summary>
    public void SpawnTeleportPortal()
    {
        // 0. Hủy bỏ tất cả các cổng cũ trong Scene trước khi tạo cổng mới tại tầng hiện tại
        TeleportPortal[] oldPortals = Object.FindObjectsByType<TeleportPortal>(FindObjectsSortMode.None);
        foreach (var p in oldPortals)
        {
            if (p != null && p.gameObject != null) Destroy(p.gameObject);
        }

        Debug.Log($"[RoomController] Đang khởi tạo cổng dịch chuyển mới tại phòng {gameObject.name}");

        // 1. Tạo GameObject Portal mới
        GameObject portalObj = new GameObject("TeleportPortal");
        portalObj.transform.position = transform.position; // Đặt tại tâm phòng

        // 2. Thêm SpriteRenderer và tạo Texture Cổng Xanh Cyan phát sáng rực rỡ 64x64
        SpriteRenderer renderer = portalObj.AddComponent<SpriteRenderer>();

        Texture2D portalTex = new Texture2D(64, 64);
        Color cyanCore = new Color(0f, 1f, 1f, 0.95f);
        Color cyanEdge = new Color(0f, 0.5f, 0.9f, 0.3f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                if (dist <= 30f)
                {
                    float alpha = Mathf.Clamp01(1f - (dist / 30f));
                    portalTex.SetPixel(x, y, Color.Lerp(cyanCore, cyanEdge, dist / 30f) * alpha);
                }
                else
                {
                    portalTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        portalTex.Apply();

        renderer.sprite = Sprite.Create(portalTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 32f);
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 25; // Nổi hoàn toàn trên tất cả gạch sàn Tilemap
        portalObj.transform.localScale = new Vector3(2.5f, 2.5f, 1f);

        // 3. Thêm Collider 2D làm vùng va chạm Trigger
        CircleCollider2D col = portalObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        // 4. Gắn script quản lý chuyển tiếp
        portalObj.AddComponent<TeleportPortal>();
    }

    private void SpawnRewardChest(Vector3 spawnPosition)
    {
        if (chestPrefab != null)
        {
            // Tìm vị trí an toàn không bị kẹt hoặc đè bởi vật thể/tường
            Vector3 safePos = GetSafeChestSpawnPosition(spawnPosition);
            GameObject chestObj = Instantiate(chestPrefab, safePos, Quaternion.identity);
            
            // Đặt làm con của Room để quản lý phân cấp gọn gàng
            chestObj.transform.SetParent(transform);
            
            Debug.Log($"[RoomController] Đã sinh Rương Thưởng tại vị trí an toàn {safePos} ở phòng {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[RoomController] Chưa gán chestPrefab cho RoomController tại phòng {gameObject.name}. Vui lòng kéo thả vào Map_Generator.");
        }
    }

    /// <summary>
    /// Tìm vị trí an toàn tuyệt đối để sinh Rương thưởng trong phòng (không dính tường, không bị vật cản đè lên, không bị kẹt)
    /// </summary>
    public Vector3 GetSafeChestSpawnPosition(Vector3 targetPos)
    {
        // 1. Nếu phòng có Collider, lấy ranh giới phòng an toàn (lùi vào 1.5 unit từ biên ngoài)
        Bounds roomBounds = (RoomCollider != null) ? RoomCollider.bounds : new Bounds(transform.position, new Vector3(10f, 10f, 0f));
        Vector3 roomCenter = roomBounds.center;

        // Giới hạn biên an toàn tối đa bên trong phòng
        float minX = roomBounds.min.x + 1.5f;
        float maxX = roomBounds.max.x - 1.5f;
        float minY = roomBounds.min.y + 1.5f;
        float maxY = roomBounds.max.y - 1.5f;

        // Clamp vị trí ban đầu nằm gọn trong phòng
        Vector3 clampedPos = new Vector3(
            Mathf.Clamp(targetPos.x, minX, maxX),
            Mathf.Clamp(targetPos.y, minY, maxY),
            targetPos.z
        );

        // 2. Kiểm tra xem vị trí ban đầu có hoàn toàn an toàn hay không
        if (IsPositionSafeForChest(clampedPos))
        {
            return clampedPos;
        }

        // 3. Nếu vị trí ban đầu dính vật cản/tường, tiến hành tìm kiếm theo bán kính xoắn ốc (Spiral/Ring search)
        float[] searchDistances = new float[] { 0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f };
        Vector2[] directions = new Vector2[]
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(0.707f, 0.707f), new Vector2(-0.707f, 0.707f),
            new Vector2(0.707f, -0.707f), new Vector2(-0.707f, -0.707f)
        };

        // Tìm từ vị trí quái chết trước
        foreach (float dist in searchDistances)
        {
            foreach (Vector2 dir in directions)
            {
                Vector3 candidate = clampedPos + (Vector3)(dir * dist);
                candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
                candidate.y = Mathf.Clamp(candidate.y, minY, maxY);

                if (IsPositionSafeForChest(candidate))
                {
                    return candidate;
                }
            }
        }

        // 4. Nếu vị trí quanh quái chết đều dính vật cản, tìm từ tâm phòng (roomCenter)
        if (IsPositionSafeForChest(roomCenter))
        {
            return roomCenter;
        }

        foreach (float dist in searchDistances)
        {
            foreach (Vector2 dir in directions)
            {
                Vector3 candidate = roomCenter + (Vector3)(dir * dist);
                candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
                candidate.y = Mathf.Clamp(candidate.y, minY, maxY);

                if (IsPositionSafeForChest(candidate))
                {
                    return candidate;
                }
            }
        }

        // Fallback cuối cùng: Trả về tâm phòng
        return roomCenter;
    }

    /// <summary>
    /// Kiểm tra vị trí chỉ định có bị dính tường, dính Tilemap vật cản hoặc bị đè bởi Collider vật cản không
    /// </summary>
    private bool IsPositionSafeForChest(Vector3 pos)
    {
        // 1. Kiểm tra Tilemap vật cản / tường của DungeonGenerator
        DungeonGenerator generator = FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            Vector3Int cellPos = (generator.floorTilemap != null) ? generator.floorTilemap.WorldToCell(pos) : Vector3Int.FloorToInt(pos);

            // Nếu ô trùng tường hoặc trùng vật cản đá -> Không an toàn
            if (generator.wallTilemap != null && generator.wallTilemap.HasTile(cellPos)) return false;
            if (generator.obstacleTilemap != null && generator.obstacleTilemap.HasTile(cellPos)) return false;
        }

        // 2. Kiểm tra va chạm Physics2D xung quanh vị trí rương (bán kính 0.6 unit)
        // Tìm xem có Collider nào thuộc vật thể (Obstacle/Wall) che chắn không
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(pos, 0.6f);
        foreach (var col in hitColliders)
        {
            if (col == null || col.isTrigger) continue;

            // Nếu trúng Collider của tường/vật cản/cửa -> Không an toàn
            string cName = col.name;
            if (cName.Contains("Door") || cName.Contains("Obstacle") || cName.Contains("Pillar") || cName.Contains("Wall") || cName.Contains("Tilemap"))
            {
                return false;
            }
        }

        return true;
    }

    private void PushPlayerInsideRoom()
    {
        if (currentPlayer == null) return;

        // Bỏ qua nếu đang chơi Multi, vì MultiplayerSyncManager đã tự dịch chuyển (tránh đẩy 2 lần)
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer) return;

        Vector3 roomCenter = transform.position;
        Vector3 dir = (roomCenter - currentPlayer.position).normalized;

        currentPlayer.position += dir * 1.5f;
    }

    private void ActivateAllMobsInRoom()
    {
        foreach (MobHealth mob in mobs)
        {
            if (mob != null)
            {
                MobAI mobAI = mob.GetComponent<MobAI>();
                if (mobAI != null)
                {
                    mobAI.ActivateMob();
                }
            }
        }
    }

    private void OnMobDeath(MobHealth deadMob)
    {
        Debug.Log($"{gameObject.name} Mob Died: {deadMob.name}");
        
        // Sinh rương tại vị trí quái cuối cùng chết
        Vector3 spawnPos = (deadMob != null) ? deadMob.transform.position : transform.position;
        CheckRoomCleared(spawnPos);
    }

    private Vector3 GetRandomPositionInRoom()
    {
        if (RoomCollider == null) return transform.position;

        Bounds bounds = RoomCollider.bounds;
        Vector3 center = bounds.center;
        
        // Thu hẹp vùng tìm kiếm xung quanh tâm phòng (tránh sát tường ở các góc kẹt)
        float minX = Mathf.Max(bounds.min.x + 2.0f, center.x - 3.0f);
        float maxX = Mathf.Min(bounds.max.x - 2.0f, center.x + 3.0f);
        float minY = Mathf.Max(bounds.min.y + 2.0f, center.y - 3.0f);
        float maxY = Mathf.Min(bounds.max.y - 2.0f, center.y + 3.0f);

        // Nếu phòng quá nhỏ không đủ biên, trả về tâm phòng làm dự phòng
        if (minX >= maxX || minY >= maxY) return center;

        Vector3 randomPos = center;
        int maxAttempts = 30;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            float rx = Random.Range(minX, maxX);
            float ry = Random.Range(minY, maxY);
            randomPos = new Vector3(rx, ry, transform.position.z);

            // Kiểm tra: Nằm trong phạm vi bán kính [1.2, 3.0] tính từ tâm phòng
            float dist = Vector3.Distance(randomPos, center);
            if (dist >= 1.2f && dist <= 3.0f)
            {
                return randomPos;
            }
        }

        return randomPos;
    }

    private void CheckRoomCleared()
    {
        CheckRoomCleared(transform.position);
    }

    private void CheckRoomCleared(Vector3 spawnPosition)
    {
        if (GetAliveMobCount() <= 0)
        {
            bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
            if (isMultiplayer)
            {
                NetworkManager.Instance.SendRoomClearedEvent(roomUniqueId);
            }
            else
            {
                ClearRoom(spawnPosition);
            }
        }
    }

    private void ClearRoom()
    {
        ClearRoom(transform.position);
    }

    private void ClearRoom(Vector3 spawnPosition)
    {
        ExecuteClearRoomLocal(spawnPosition);
    }

    private int GetAliveMobCount()
    {
        int count = 0;
        foreach (MobHealth mob in mobs)
        {
            if (mob != null && !mob.isDead)
                count++;
        }
        return count;
    }

    private void CloseDoors()
    {
        Debug.Log($"{gameObject.name} CLOSE DOORS - DoorCount: {doors.Count}");
        foreach (RoomDoor door in doors)
        {
            if (door != null)
            {
                door.CloseDoor();
            }
        }
    }

    private void OpenDoors()
    {
        Debug.Log($"{gameObject.name} OPEN DOORS");
        foreach (RoomDoor door in doors)
        {
            if (door != null)
                door.OpenDoor();
        }
    }

    private void CollectMobsInsideRoom()
    {
        if (RoomCollider == null)
            return;

        MobHealth[] allMobs = Object.FindObjectsByType<MobHealth>(FindObjectsSortMode.None);

        foreach (MobHealth mob in allMobs)
        {
            if (mob == null || mob.isDead || mobs.Contains(mob))
                continue;

            if (RoomCollider.OverlapPoint(mob.transform.position))
            {
                AddMob(mob);
            }
        }
    }

    private void CollectDoorsNearRoom()
    {
        if (RoomCollider == null)
            return;

        Bounds roomBounds = RoomCollider.bounds;
        roomBounds.Expand(8f);

        RoomDoor[] allDoors = Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None);

        foreach (RoomDoor door in allDoors)
        {
            if (door == null || doors.Contains(door))
                continue;

            Collider2D[] doorColliders = door.GetComponents<Collider2D>();
            bool isNearRoom = false;

            foreach (Collider2D doorCol in doorColliders)
            {
                if (doorCol != null && roomBounds.Intersects(doorCol.bounds))
                {
                    isNearRoom = true;
                    break;
                }
            }

            if (isNearRoom)
            {
                AddDoor(door);
            }
        }
    }
}