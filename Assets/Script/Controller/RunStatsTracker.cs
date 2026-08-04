using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Lớp theo dõi thống kê màn chơi (Run Stats), gửi dữ liệu kết thúc trận đấu lên API và hiển thị giao diện kết quả (Thất bại / Chiến thắng)
/// </summary>
public class RunStatsTracker : MonoBehaviour
{
    public static RunStatsTracker Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private GameObject resultPanel;       // Panel hiển thị kết quả cuối trận
    [SerializeField] private TMP_Text resultTitleText;       // Chữ tiêu đề: "CHIẾN THẮNG!" hoặc "THẤT BẠI!"
    [SerializeField] private TMP_Text statsText;             // Thông tin thống kê chi tiết trận đấu
    [SerializeField] private Button returnButton;            // Nút bấm quay trở lại Sảnh chính

    // Các thông số thống kê trận đấu
    public int WavesSurvived { get; private set; } = 1;     // Số Wave sống sót mặc định
    public int EnemiesKilled { get; private set; } = 0;     // Số lượng kẻ địch tiêu diệt
    public float DamageDealt { get; private set; } = 0;     // Lượng sát thương gây ra
    public int CurrencyEarned { get; private set; } = 0;    // Số Coin nhận được trong trận (chỉ có giá trị trong run)
    public int ClearedRoomsCount { get; private set; } = 0; // Số phòng đã dọn dẹp xong quái
    public int TotalCombatRooms { get; private set; } = 0;  // Tổng số phòng có quái cần vượt qua

    private float startTime;
    private bool runEnded = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        startTime = Time.time;
        if (resultPanel != null) resultPanel.SetActive(false);
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnToMenuClicked);
        }
    }

    /// <summary>
    /// Khởi tạo lại toàn bộ chỉ số khi bắt đầu màn chơi mới
    /// </summary>
    public void InitializeRun(int totalCombatRooms)
    {
        TotalCombatRooms = totalCombatRooms;
        ClearedRoomsCount = 0;
        EnemiesKilled = 0;
        DamageDealt = 0;
        CurrencyEarned = 0;
        WavesSurvived = 1;
        startTime = Time.time;
        runEnded = false;

        // Reset lại tiến trình leo tầng về Tầng 1
        if (GameProgressionManager.Instance != null)
        {
            GameProgressionManager.Instance.ResetProgression();
        }

        // Reset lại các Buff của người chơi ở lượt chơi mới
        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.ResetBuffs();
        }

        Debug.Log($"[RunStatsTracker] Khởi tạo Run mới. Tổng số phòng cần dọn: {TotalCombatRooms}");
    }

    /// <summary>
    /// Ghi nhận khi tiêu diệt được một kẻ địch (được gọi từ MobHealth.cs)
    /// Kẻ địch bị tiêu diệt sẽ KHÔNG tự động cộng Coin trực tiếp nữa.
    /// </summary>
    public void LogEnemyKilled()
    {
        if (runEnded) return;
        EnemiesKilled++;
        Debug.Log($"[RunStatsTracker] Đã hạ quái. Tổng số: {EnemiesKilled}, Coin hiện tại: {CurrencyEarned}");
    }

    /// <summary>
    /// Cộng tiền khi nhặt được vàng rơi từ rương (được gọi từ LootItem.cs)
    /// </summary>
    public void AddCurrency(int amount)
    {
        if (runEnded) return;

        int finalAmount = amount;
        if (PlayerBuffManager.Instance != null)
        {
            finalAmount = Mathf.RoundToInt(amount * PlayerBuffManager.Instance.coinGainMultiplier);
        }

        CurrencyEarned += finalAmount;
        Debug.Log($"[RunStatsTracker] Đã nhặt Coin. Cộng thêm: {finalAmount} (Gốc: {amount}), Tổng số: {CurrencyEarned}");
    }

    /// <summary>
    /// Cộng dồn lượng sát thương gây ra cho quái vật (được gọi từ MobHealth.cs)
    /// </summary>
    public void LogDamageDealt(float amount)
    {
        if (runEnded) return;
        DamageDealt += amount;
    }

    /// <summary>
    /// Ghi nhận khi dọn sạch một phòng quái (được gọi từ RoomController.cs)
    /// Việc dọn phòng thành công sẽ KHÔNG tự động cộng Coin nữa.
    /// </summary>
    public void LogRoomCleared()
    {
        if (runEnded) return;
        ClearedRoomsCount++;
        Debug.Log($"[RunStatsTracker] Đã dọn xong phòng ({ClearedRoomsCount}/{TotalCombatRooms}). Coin: {CurrencyEarned}");

        // CHÚ Ý: Đã xoá bỏ điều kiện tự động thắng khi dọn hết phòng (chuyển sang thắng khi qua tầng 5 bằng Portal)
    }

    /// <summary>
    /// Kết thúc trận đấu, tính toán các chỉ số thưởng và gọi API đồng bộ kết quả lên Backend
    /// </summary>
    public void EndRun(bool isVictory)
    {
        if (runEnded) return;
        runEnded = true;

        int durationSeconds = (int)(Time.time - startTime);
        
        // Thưởng thêm Victory Bonus nếu chiến thắng màn chơi
        int victoryBonus = isVictory ? 100 : 0;
        CurrencyEarned += victoryBonus;

        // Nếu chiến thắng, WavesSurvived mặc định = 5 (tầng cuối cùng hoàn thành), ngược lại tính theo tầng hiện tại đang chơi
        if (isVictory)
        {
            WavesSurvived = 5;
        }
        else
        {
            if (GameProgressionManager.Instance != null)
            {
                WavesSurvived = GameProgressionManager.Instance.currentFloor;
            }
            else
            {
                WavesSurvived = Mathf.Clamp(1 + (int)((float)ClearedRoomsCount / Math.Max(1, TotalCombatRooms) * 4), 1, 4);
            }
        }

        // VÔ HIỆU HÓA DI CHUYỂN VÀ SÚNG CỦA PLAYER KHI THẤT BẠI (Chỉ khi Defeat)
        // Khi Hoàn thành game (Victory), giữ cho người chơi vẫn có thể tự do di chuyển và thao tác trong màn chơi.
        if (!isVictory)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PlayerMovement pm = player.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    pm.enabled = false;
                    // Dừng hoạt ảnh di chuyển
                    Animator anim = player.GetComponent<Animator>();
                    if (anim != null) anim.SetFloat("Speed", 0f);
                    // Dừng quán tính vật lý
                    Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }

                // Tìm và tắt các component điều khiển súng/nhắm bắn khi nhân vật hy sinh
                MonoBehaviour[] allScripts = player.GetComponentsInChildren<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
                    {
                        script.enabled = false;
                    }
                }
                Debug.Log("[RunStatsTracker] Đã vô hiệu hoá di chuyển và ngắm bắn của Player do Thất bại.");
            }
        }
        else
        {
            Debug.Log("[RunStatsTracker] Hoàn thành game (Victory)! Giữ nguyên quyền di chuyển và điều khiển cho người chơi.");
        }

        Debug.Log($"[RunStatsTracker] Trận đấu kết thúc. Chiến thắng: {isVictory}. Đang gửi dữ liệu lên Backend...");

        // Gửi kết quả trận đấu lên máy chủ
        SubmitRunHistory(isVictory, durationSeconds);

        // Hiển thị giao diện tổng kết
        ShowResultUI(isVictory, durationSeconds);
    }

    [Serializable]
    private class RunHistoryRequest
    {
        public int characterId;
        public int wavesSurvived;
        public int enemiesKilled;
        public int damageDealt;
        public int currencyEarned;
        public int durationSeconds;
    }

    // Gửi yêu cầu lưu lịch sử trận đấu (POST /runhistory) qua ApiClient
    private void SubmitRunHistory(bool isVictory, int durationSeconds)
    {
        if (ApiClient.Instance == null)
        {
            Debug.LogWarning("[RunStatsTracker] Không tìm thấy ApiClient.Instance. Không thể lưu lịch sử.");
            return;
        }

        // KIỂM TRA CHƯA ĐĂNG NHẬP: Nếu không có Token (chơi offline/test scene trực tiếp), 
        // bỏ qua việc gửi API để tránh hiển thị cảnh báo lỗi 401 Unauthorized.
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[RunStatsTracker] Chơi ở chế độ Offline/Test Scene trực tiếp (Không có Token). Bỏ qua việc gửi lịch sử đấu lên server.");
            return;
        }

        var requestBody = new RunHistoryRequest
        {
            characterId = 1, // Mặc định sử dụng nhân vật Chiến Binh (Kie Warrior - ID 1)
            wavesSurvived = WavesSurvived,
            enemiesKilled = EnemiesKilled,
            damageDealt = (int)DamageDealt,
            currencyEarned = CurrencyEarned,
            durationSeconds = durationSeconds
        };

        string json = JsonUtility.ToJson(requestBody);

        ApiClient.Instance.Post("/runhistory", json, (response) =>
        {
            Debug.Log("[RunStatsTracker] Đã gửi lịch sử đấu thành công: " + response);
            // Refresh thông tin profile hiển thị ở Menu chính
            PlayerProfileUI.Instance?.RefreshProfile();
        }, (error) =>
        {
            Debug.LogError("[RunStatsTracker] Gửi lịch sử đấu thất bại: " + error);
        });
    }

    // Đổ các chỉ số ghi nhận lên màn hình kết quả UI
    private void ShowResultUI(bool isVictory, int durationSeconds)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = isVictory ? "VICTORY!" : "DEFEAT!";
            resultTitleText.color = isVictory ? Color.green : Color.red;
        }

        if (statsText != null)
        {
            string timeStr = $"{durationSeconds / 60:D2}:{durationSeconds % 60:D2}";
            statsText.text = $"Time Played: {timeStr}\n" +
                             $"Stages Cleared: {WavesSurvived}/5\n" +
                             $"Enemies Killed: {EnemiesKilled}\n" +
                             $"Damage Dealt: {(int)DamageDealt}\n" +
                             $"Coins Earned: +{CurrencyEarned} Coins";
        }
    }

    private void OnReturnToMenuClicked()
    {
        // Trở về Scene Menu chính và thực hiện ngắt kết nối dọn dẹp phòng an toàn
        InGameMenuController menuController = FindAnyObjectByType<InGameMenuController>();
        if (menuController != null)
        {
            menuController.QuitToMainMenu();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Menu");
        }
    }
}
