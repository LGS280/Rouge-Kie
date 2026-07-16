using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Normal, // Phòng thường có quái
    Start,  // Phòng xuất phát (Home)
    Boss,   // Phòng Boss
    Chest   // Phòng rương báu
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
    private bool chestSpawned = false;
    
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

        // Sinh rương thưởng khi dọn sạch phòng quái (Bỏ qua phòng xuất phát Start)
        if (roomType != RoomType.Start && !chestSpawned)
        {
            chestSpawned = true;
            // Nếu là phòng Boss, dịch vị trí rương sang bên cạnh để nhường tâm phòng cho Portal
            Vector3 chestPos = (roomType == RoomType.Boss) ? transform.position + Vector3.right * 2.0f : spawnPosition;
            SpawnRewardChest(chestPos);
        }

        // Sinh cổng dịch chuyển chuyển tầng (Portal) nếu đây là phòng Boss
        if (roomType == RoomType.Boss)
        {
            SpawnTeleportPortal();
        }
    }

    /// <summary>
    /// Sinh cổng dịch chuyển mượt mà tại tâm phòng Boss
    /// </summary>
    private void SpawnTeleportPortal()
    {
        Debug.Log($"[RoomController] Đang khởi tạo cổng dịch chuyển tại phòng Boss {gameObject.name}");

        // 1. Tạo GameObject Portal mới
        GameObject portalObj = new GameObject("TeleportPortal");
        portalObj.transform.position = transform.position; // Đặt tại tâm phòng Boss

        // 2. Thêm SpriteRenderer và thiết lập sprite
        SpriteRenderer renderer = portalObj.AddComponent<SpriteRenderer>();
        Sprite portalSprite = Resources.Load<Sprite>("Minimap/Room"); // Nền ô phòng hình vuông
        if (portalSprite != null)
        {
            renderer.sprite = portalSprite;
        }
        renderer.color = new Color(0f, 0.8f, 1f, 0.8f); // Màu xanh cyan phát sáng mờ ảo
        portalObj.transform.localScale = new Vector3(2.0f, 2.0f, 1f); // Tỷ lệ cổng
        renderer.sortingOrder = 5; // Hiển thị trên mặt đất

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
            // Sinh rương tại vị trí chỉ định (ví dụ vị trí quái cuối cùng chết)
            GameObject chestObj = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
            
            // Đặt làm con của Room để quản lý phân cấp gọn gàng
            chestObj.transform.SetParent(transform);
            
            Debug.Log($"[RoomController] Đã sinh Rương Thưởng tại vị trí {spawnPosition} ở phòng {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[RoomController] Chưa gán chestPrefab cho RoomController tại phòng {gameObject.name}. Vui lòng kéo thả vào Map_Generator.");
        }
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