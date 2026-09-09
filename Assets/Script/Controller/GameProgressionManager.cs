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
    /// Lấy hệ số nhân máu của quái vật cho tầng hiện tại
    /// </summary>
    public float GetMonsterHPMultiplier()
    {
        // Công thức: 1.0 + (Tầng - 1) * hệ số tăng thêm
        return 1.0f + (currentFloor - 1) * hpMultiplierPerFloor;
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

        isTransitioning = true;
        currentFloor = targetFloor;
        Debug.Log($"[GameProgressionManager] Đang chuyển sang Tầng {currentFloor}/{maxFloor}...");

        // Hiển thị Màn hình Chờ Tải Tầng mới
        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.ShowLoading($"TẦNG {currentFloor} - 1", "Đang khởi tạo cấu trúc hầm ngục mới...");
        }

        if (currentFloor > maxFloor)
        {
            // Nếu đã vượt qua tầng 5 -> Chiến thắng game!
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
