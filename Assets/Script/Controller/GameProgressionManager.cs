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

    /// <summary>
    /// Chuyển sang Tầng kế tiếp (Floor Transition)
    /// </summary>
    public void StartNextFloor()
    {
        currentFloor++;
        Debug.Log($"[GameProgressionManager] Đang chuyển sang Tầng {currentFloor}/{maxFloor}...");

        if (currentFloor > maxFloor)
        {
            // Nếu đã vượt qua tầng 5 -> Chiến thắng game!
            Debug.Log("[GameProgressionManager] Đã vượt qua tầng cuối cùng! Chiến thắng trận đấu!");
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
        // 1. Tìm và vô hiệu hoá tạm thời di chuyển của người chơi để tránh di chuyển lúc load
        GameObject player = GameObject.FindWithTag("Player");
        PlayerMovement movement = null;
        if (player != null)
        {
            movement = player.GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = false;
        }

        // 2. Clear bản đồ cũ và sinh bản đồ mới ngẫu nhiên
        DungeonGenerator generator = FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            generator.GenerateSoulKnightMap();
            yield return new WaitForSeconds(0.2f); // Chờ gạch sàn sinh xong
        }

        // 3. Dịch chuyển người chơi về tâm phòng xuất phát mới
        if (player != null && generator != null)
        {
            // Start room nằm ở gridPos (0,0) nên world center của nó luôn là:
            float startCenterX = generator.fixedRoomWidth / 2f;
            float startCenterY = generator.fixedRoomHeight / 2f;
            player.transform.position = new Vector3(startCenterX, startCenterY, 0);
            Debug.Log($"[GameProgressionManager] Đã dịch chuyển người chơi về phòng xuất phát mới: ({startCenterX}, {startCenterY})");
        }

        // 4. Kích hoạt lại di chuyển người chơi
        if (movement != null)
        {
            movement.enabled = true;
        }

        Debug.Log($"[GameProgressionManager] Tải Tầng {currentFloor} thành công!");
    }
}
