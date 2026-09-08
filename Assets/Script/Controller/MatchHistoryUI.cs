using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class RunHistoryItemData
{
    public int runId;
    public int userId;
    public int characterId;
    public string characterName;
    public int wavesSurvived;
    public int enemiesKilled;
    public int damageDealt;
    public int currencyEarned;
    public int durationSeconds;
    public string playedAt;
}

[Serializable]
public class RunHistoryArrayWrapper
{
    public RunHistoryItemData[] items;
}

/// <summary>
/// Quản lý giao diện hiển thị Lịch Sử Trận Đấu (Match / Run History) trong game Unity
/// </summary>
public class MatchHistoryUI : MonoBehaviour
{
    private static MatchHistoryUI instance;
    public static MatchHistoryUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<MatchHistoryUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("UI Panel References")]
    [SerializeField] private GameObject historyPanel;         // Panel tổng chứa bảng lịch sử đấu
    [SerializeField] private Button closeButton;              // Nút đóng bảng (X)
    [SerializeField] private Button refreshButton;            // Nút làm mới dữ liệu
    [SerializeField] private TMP_Text emptyHistoryText;       // Dòng chữ báo trống khi chưa có trận đấu

    [Header("Option A: ScrollView Container (Prefab Item)")]
    [SerializeField] private Transform historyListContainer;  // Content của ScrollView để sinh item
    [SerializeField] private GameObject historyItemPrefab;    // Prefab dòng lịch sử đấu (nhân bản từ RoomItem_Prefab)

    [Header("Option B: Fallback Text (Tương tự Leaderboard)")]
    [SerializeField] private TMP_Text historyFullText;        // TextMeshPro hiển thị danh sách dạng văn bản dài

    public bool IsOpen => historyPanel != null && historyPanel.activeSelf;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Start()
    {
        if (historyPanel != null) historyPanel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HideHistory);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(LoadHistoryFromServer);
        }
    }

    /// <summary>
    /// Mở bảng lịch sử đấu và tải dữ liệu mới nhất từ máy chủ
    /// </summary>
    public void ShowHistory()
    {
        gameObject.SetActive(true);
        if (historyPanel != null) historyPanel.SetActive(true);
        LoadHistoryFromServer();
    }

    /// <summary>
    /// Đóng bảng lịch sử đấu
    /// </summary>
    public void HideHistory()
    {
        if (historyPanel != null) historyPanel.SetActive(false);
    }

    /// <summary>
    /// Bật/Tắt bảng lịch sử đấu
    /// </summary>
    public void ToggleHistory()
    {
        if (IsOpen) HideHistory();
        else ShowHistory();
    }

    /// <summary>
    /// Gửi yêu cầu GET /api/runhistory tới máy chủ
    /// </summary>
    public void LoadHistoryFromServer()
    {
        // 1. Kiểm tra trạng thái đăng nhập
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrEmpty(token))
        {
            ShowEmptyMessage("Vui lòng đăng nhập tài khoản để xem lịch sử đấu!");
            ClearContainerItems();
            return;
        }

        if (ApiClient.Instance == null)
        {
            ShowEmptyMessage("Không thể kết nối tới dịch vụ mạng (ApiClient).");
            return;
        }

        ShowEmptyMessage("Đang tải dữ liệu lịch sử đấu...");

        ApiClient.Instance.Get("/runhistory", (json) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json == "[]")
                {
                    ShowEmptyMessage("Bạn chưa có trận đấu nào.\nHãy vào chơi một trận ngay!");
                    ClearContainerItems();
                    return;
                }

                // Bọc mảng JSON để parse bằng JsonUtility của Unity
                string wrappedJson = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson<RunHistoryArrayWrapper>(wrappedJson);

                if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
                {
                    HideEmptyMessage();
                    UpdateUI(wrapper.items);
                }
                else
                {
                    ShowEmptyMessage("Bạn chưa có trận đấu nào.\nHãy vào chơi một trận ngay!");
                    ClearContainerItems();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchHistoryUI] Lỗi phân tích dữ liệu lịch sử: {ex.Message}");
                ShowEmptyMessage("Lỗi xử lý dữ liệu lịch sử đấu.");
            }
        }, (err) =>
        {
            Debug.LogError($"[MatchHistoryUI] Lỗi khi tải lịch sử đấu: {err}");
            ShowEmptyMessage("Không thể tải lịch sử đấu từ máy chủ.\nVui lòng thử lại sau!");
        });
    }

    private void UpdateUI(RunHistoryItemData[] items)
    {
        // Ưu tiên Cách 1: Sinh Prefab vào ScrollView Content nếu có gán Container và Prefab
        if (historyListContainer != null && historyItemPrefab != null)
        {
            ClearContainerItems();

            foreach (var item in items)
            {
                GameObject obj = Instantiate(historyItemPrefab, historyListContainer);
                TMP_Text itemText = obj.GetComponentInChildren<TMP_Text>(true);
                if (itemText != null)
                {
                    itemText.text = FormatItemText(item);
                }
            }
            return;
        }

        // Cách 2 (Fallback): Nạp vào 1 TextMeshPro dài duy nhất
        if (historyFullText != null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine(FormatItemText(item));
                sb.AppendLine("<color=#444444>────────────────────────────────────────</color>");
            }
            historyFullText.text = sb.ToString();
        }
    }

    private string FormatItemText(RunHistoryItemData item)
    {
        bool isVictory = item.wavesSurvived >= 5;
        string statusTag = isVictory
            ? "<color=#00FF66><b>[VICTORY]</b></color>"
            : "<color=#FF4444><b>[DEFEAT]</b></color>";

        int minutes = item.durationSeconds / 60;
        int seconds = item.durationSeconds % 60;
        string timeStr = $"{minutes:D2}:{seconds:D2}";

        string dateStr = "";
        if (!string.IsNullOrEmpty(item.playedAt))
        {
            if (DateTime.TryParse(item.playedAt, out DateTime dt))
            {
                dateStr = $"<color=#888888>[{dt:dd/MM HH:mm}]</color> ";
            }
        }

        return $"{dateStr}{statusTag} <b>Stage {item.wavesSurvived}/5</b> | Time: {timeStr} | Kills: {item.enemiesKilled} | Dmg: {item.damageDealt:N0} | <color=#FFD700>+{item.currencyEarned} Coins</color>";
    }

    private void ClearContainerItems()
    {
        if (historyListContainer != null)
        {
            foreach (Transform child in historyListContainer)
            {
                Destroy(child.gameObject);
            }
        }
        if (historyFullText != null)
        {
            historyFullText.text = "";
        }
    }

    private void ShowEmptyMessage(string msg)
    {
        if (emptyHistoryText != null)
        {
            emptyHistoryText.gameObject.SetActive(true);
            emptyHistoryText.text = msg;
        }
    }

    private void HideEmptyMessage()
    {
        if (emptyHistoryText != null)
        {
            emptyHistoryText.gameObject.SetActive(false);
        }
    }
}
