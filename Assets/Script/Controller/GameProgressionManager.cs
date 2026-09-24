using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý tiến trình trò chơi (Leo tầng, độ khó và chuyển cảnh giữa các tầng)
/// </summary>
public class GameProgressionManager : MonoBehaviour
{
    private static GameProgressionManager _instance;
    public static GameProgressionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameProgressionManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("GameProgressionManager");
                    _instance = obj.AddComponent<GameProgressionManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Tiến Trình")]
    public int currentFloor = 1; // Tầng hiện tại (1 đến 5)
    public const int maxFloor = 5;

    [Header("Cân Bằng Độ Khó")]
    [Tooltip("Hệ số nhân máu quái vật tăng thêm mỗi tầng")]
    public float hpMultiplierPerFloor = 0.3f; // Tầng 1: 1.0x, Tầng 2: 1.3x, Tầng 3: 1.6x, Tầng 4: 1.9x, Tầng 5: 2.2x

    [Header("Trạng Thái Transiton")]
    public bool isTransitioning = false; // Cờ bảo vệ ngăn chặn gọi nhảy tầng trùng lặp

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Khởi động lại tiến trình khi chơi lượt mới (Reset về Floor 1)
    /// </summary>
    public void ResetProgression()
    {
        currentFloor = 1;
        isTransitioning = false;
        Debug.Log("[GameProgressionManager] Đã khởi tạo lại tiến trình màn chơi về Tầng 1.");
    }

    /// <summary>
    /// Lấy số lượng người chơi trong phòng (Solo: 1, Co-op: 2-4)
    /// </summary>
    public int GetPlayerCount()
    {
        return NetworkManager.Instance != null ? NetworkManager.Instance.GetCoopPlayerCount() : 1;
    }

    /// <summary>
    /// Lấy hệ số nhân máu của quái vật thường cho tầng hiện tại (Kết hợp độ khó tầng và Co-op Scaling từ Database)
    /// </summary>
    public float GetMonsterHPMultiplier()
    {
        int players = GetPlayerCount();
        LevelConfig levelCfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetLevelConfig(currentFloor) : null;

        float floorMult = (levelCfg != null && levelCfg.difficultyMultiplier > 0f)
            ? levelCfg.difficultyMultiplier
            : (1.0f + (currentFloor - 1) * hpMultiplierPerFloor);

        float coopScalingFactor = (levelCfg != null) ? levelCfg.coopMobHPMultiplier : 0.4f;
        float coopMult = 1.0f + Mathf.Max(0, players - 1) * coopScalingFactor;

        return floorMult * coopMult;
    }

    /// <summary>
    /// Lấy hệ số nhân máu của Boss / Mini-Boss trong chế độ Co-op (Lấy tỉ lệ từ Database)
    /// </summary>
    public float GetBossHPMultiplier()
    {
        int players = GetPlayerCount();
        if (players <= 1) return 1.0f;

        LevelConfig levelCfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetLevelConfig(currentFloor) : null;
        float coopBossScaling = (levelCfg != null) ? levelCfg.coopBossHPMultiplier : 0.6f;

        return 1.0f + (players - 1) * coopBossScaling;
    }

    /// <summary>
    /// Lấy số lượng phòng cần sinh cho tầng dựa theo cấu hình Database và số lượng người chơi Co-op
    /// </summary>
    public int GetRoomCountForFloor(int floor)
    {
        int players = GetPlayerCount();
        LevelConfig levelCfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetLevelConfig(floor) : null;

        int baseRooms = (levelCfg != null && levelCfg.baseRoomCount > 0)
            ? levelCfg.baseRoomCount
            : (7 + (floor - 1)); // Fallback dự phòng: Tầng 1: 7, Tầng 2: 8, Tầng 3: 9...

        int extraCoopRooms = (players > 1)
            ? ((levelCfg != null && levelCfg.coopExtraRooms >= 0) ? levelCfg.coopExtraRooms : 2)
            : 0;

        return baseRooms + extraCoopRooms;
    }

    /// <summary>
    /// Lấy số lượng phòng rương cần sinh cho tầng dựa theo cấu hình Database và số lượng người chơi Co-op
    /// Solo: 1 phòng rương. Co-op: Tầng 1, 2 là 1 phòng rương, Tầng 3+ là 2 phòng rương (hoặc theo cấu hình Database)
    /// </summary>
    public int GetChestRoomCountForFloor(int floor)
    {
        int players = GetPlayerCount();
        LevelConfig levelCfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetLevelConfig(floor) : null;

        int baseChest = (levelCfg != null && levelCfg.chestRoomCount > 0)
            ? levelCfg.chestRoomCount
            : 1;

        int extraChest = (players > 1)
            ? ((levelCfg != null && levelCfg.coopExtraChestRooms >= 0) ? levelCfg.coopExtraChestRooms : (floor >= 3 ? 1 : 0))
            : 0;

        return baseChest + extraChest;
    }

    /// <summary>
    /// Lấy số lượng quái thường cần sinh trong phòng (Tăng thêm dựa theo số lượng người chơi Co-op từ Database)
    /// </summary>
    public int GetMobCountPerRoom(int baseMin, int baseMax)
    {
        int players = GetPlayerCount();
        LevelConfig levelCfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetLevelConfig(currentFloor) : null;

        int extraMobsPerPlayer = (levelCfg != null) ? levelCfg.coopExtraMobsPerRoom : 1;
        int extraMobs = Mathf.Max(0, players - 1) * extraMobsPerPlayer;

        int min = baseMin + extraMobs;
        int max = baseMax + extraMobs;

        return UnityEngine.Random.Range(min, max + 1);
    }

    private void Start()
    {
        // BỔ SUNG: Đăng ký lắng nghe sự kiện chuyển tầng đồng bộ qua mạng từ NetworkManager
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnFloorTransitionSynced += HandleSyncedFloorTransition;
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký sự kiện để tránh memory leak
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnFloorTransitionSynced -= HandleSyncedFloorTransition;
        }
    }

    /// <summary>
    /// Xử lý tín hiệu chuyển tầng đồng bộ từ mạng SignalR
    /// </summary>
    private void HandleSyncedFloorTransition(int targetFloor)
    {
        if (isTransitioning)
        {
            Debug.LogWarning($"[GameProgressionManager] Nhận lệnh chuyển Tầng {targetFloor} từ mạng nhưng đang transition, bỏ qua.");
            return;
        }

        Debug.Log($"[GameProgressionManager] Co-op Mode: Nhận tín hiệu đồng bộ chuyển sang Tầng {targetFloor} từ mạng.");

        // BỔ SUNG: Kiểm tra theo tham số mạng targetFloor (targetFloor == 2 khi vừa xong Tầng 1, targetFloor == 4 khi vừa xong Tầng 3)
        // Đảm bảo cả Host và Client 100% cùng hiển thị Bảng chọn Buff
        if ((targetFloor == 2 || targetFloor == 4) && UpgradeSelectionUI.Instance != null)
        {
            UpgradeSelectionUI.Instance.OpenUpgradeMenu(() =>
            {
                ExecuteFloorTransition(targetFloor);
            });
        }
        else
        {
            ExecuteFloorTransition(targetFloor);
        }
    }

    /// <summary>
    /// Chuyển sang Tầng kế tiếp (Floor Transition)
    /// </summary>
    public void StartNextFloor()
    {
        ExecuteFloorTransition(currentFloor + 1);
    }

    /// <summary>
    /// Thực thi chuyển tầng tới mục tiêu targetFloor
    /// </summary>
    public void ExecuteFloorTransition(int targetFloor)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[GameProgressionManager] Đang trong quá trình chuyển tầng, bỏ qua yêu cầu gọi trùng lặp.");
            return;
        }

        int activeMaxFloor = GameConfigManager.Instance != null ? GameConfigManager.Instance.GetMaxFloor(maxFloor) : maxFloor;
        isTransitioning = true;
        currentFloor = targetFloor;
        Debug.Log($"[GameProgressionManager] Đang chuyển sang Tầng {currentFloor}/{activeMaxFloor}...");

        // Hiển thị Màn hình Chờ Tải Tầng mới (Giao diện Tiếng Anh, comment Tiếng Việt)
        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.ShowLoading($"SECTOR {currentFloor} - 1", "Generating new dungeon sector...");
        }

        if (currentFloor > activeMaxFloor)
        {
            // Nếu đã vượt qua tầng cuối cùng -> Chiến thắng game!
            Debug.Log("[GameProgressionManager] Đã vượt qua tầng cuối cùng! Chiến thắng trận đấu!");
            isTransitioning = false;
            if (LoadingScreenUI.Instance != null) LoadingScreenUI.Instance.HideLoading();
            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.EndRun(true);
            }
            return;
        }

        // Bắt đầu quy trình chuyển đổi tầng trong Scene chơi
        StartCoroutine(TransitionToNextFloorRoutine());
    }

    private IEnumerator TransitionToNextFloorRoutine()
    {
        // 1. Tìm và vô hiệu hoá di chuyển + TẤT CẢ Collider của người chơi (bao gồm cả các object con)
        GameObject player = GameObject.FindWithTag("Player");
        PlayerController movement = null;
        Collider2D[] playerColliders = null;

        if (player != null)
        {
            movement = player.GetComponent<PlayerController>();
            if (movement != null) movement.enabled = false;

            playerColliders = player.GetComponentsInChildren<Collider2D>();
            foreach (var col in playerColliders)
            {
                if (col != null) col.enabled = false;
            }
            Debug.Log($"[GameProgressionManager] Đã vô hiệu hoá {playerColliders.Length} Collider của Player.");
        }

        // 2. Clear bản đồ cũ và sinh bản đồ mới ngẫu nhiên
        DungeonGenerator generator = FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            generator.GenerateSoulKnightMap();
            yield return new WaitForSeconds(0.2f); // Chờ gạch sàn sinh xong

            // BỔ SUNG: Làm mới ID mạng cho Phòng và Quái vật của tầng mới cho Co-op
            if (MultiplayerSyncManager.Instance != null)
            {
                MultiplayerSyncManager.Instance.RefreshRoomAndMobNetworkCache();
            }
        }

        // 3. Dịch chuyển người chơi về tâm phòng xuất phát mới (lấy tọa độ thực tế của RoomController Start)
        if (player != null && generator != null)
        {
            RoomController[] controllers = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
            RoomController startRoom = null;
            foreach (var rc in controllers)
            {
                if (rc.roomType == RoomType.Start)
                {
                    startRoom = rc;
                    break;
                }
            }

            if (startRoom != null)
            {
                player.transform.position = startRoom.transform.position;
                Physics2D.SyncTransforms(); // Đồng bộ vị trí vật lý ngay lập tức để tránh lỗi vị trí cũ
                Debug.Log($"[GameProgressionManager] Đã dịch chuyển Player về phòng Start: {startRoom.transform.position}");
            }
            else
            {
                // Fallback nếu không tìm thấy RoomController Start
                float startCenterX = generator.fixedRoomWidth / 2f;
                float startCenterY = generator.fixedRoomHeight / 2f;
                player.transform.position = new Vector3(startCenterX, startCenterY, 0);
                Physics2D.SyncTransforms();
                Debug.LogWarning("[GameProgressionManager] Không tìm thấy Room Start, dùng tọa độ dự phòng.");
            }
        }

        RookieHealth health = (player != null) ? player.GetComponent<RookieHealth>() : null;
        bool isDeadPlayer = health != null && health.isDead;

        // 4. Kích hoạt lại di chuyển và toàn bộ collider của người chơi (CHỈ KHI NGƯỜI CHƠI CÒN SỐNG)
        if (!isDeadPlayer)
        {
            if (playerColliders != null)
            {
                foreach (var col in playerColliders)
                {
                    if (col != null) col.enabled = true;
                }
                Debug.Log("[GameProgressionManager] Đã kích hoạt lại toàn bộ Collider của Player.");
            }
            if (movement != null)
            {
                movement.enabled = true;
            }
        }
        else
        {
            // Nếu người chơi đang bị hy sinh: Khóa vận tốc, chuyển Kinematic để cố định xác tại phòng Start tầng mới
            Rigidbody2D rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            Debug.Log("[GameProgressionManager] Player đang trong trạng thái hy sinh, giữ nguyên vô hiệu hóa điều khiển và collider.");
        }

        yield return new WaitForSeconds(0.4f); // Chờ hiệu ứng mượt trước khi làm mờ ẩn Loading Screen

        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.HideLoading();
        }

        isTransitioning = false;
        Debug.Log($"[GameProgressionManager] Tải Tầng {currentFloor} thành công!");
    }
}
