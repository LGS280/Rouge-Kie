using System;
using System.Collections;
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
    [SerializeField] private ScrollRect scrollRect;           // ScrollRect cuộn danh sách (tự tìm nếu để trống)
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

    private void OnEnable()
    {
        // Tự động tải dữ liệu mỗi khi bảng được bật lên (kể cả khi bật bằng SetActive thông thường)
        LoadHistoryFromServer();
    }

    private void Start()
    {
        if (historyPanel != null && !historyPanel.activeSelf) 
        {
            // Nếu panel đang tắt thì không cần làm gì
        }

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
        Debug.Log("[MatchHistoryUI] Bắt đầu gọi API LoadHistoryFromServer...");

        // 1. Kiểm tra trạng thái đăng nhập
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[MatchHistoryUI] Chưa có jwt_token trong PlayerPrefs. Người chơi chưa đăng nhập!");
            ShowEmptyMessage("<color=#FFAA00>Vui lòng đăng nhập tài khoản để xem lịch sử đấu!</color>");
            ClearContainerItems();
            return;
        }

        if (ApiClient.Instance == null)
        {
            Debug.LogError("[MatchHistoryUI] Không tìm thấy ApiClient.Instance!");
            ShowEmptyMessage("<color=#FF4444>Không thể kết nối tới dịch vụ mạng (ApiClient).</color>");
            return;
        }

        ShowEmptyMessage("<color=#AAAAAA>Đang tải dữ liệu lịch sử đấu từ máy chủ...</color>");

        ApiClient.Instance.Get("/runhistory", (json) =>
        {
            Debug.Log($"[MatchHistoryUI] Phản hồi từ /runhistory: {json}");
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    ShowEmptyMessage("<color=#FFFFFF>Bạn chưa có trận đấu nào.\nHãy vào chơi một trận ngay!</color>");
                    ClearContainerItems();
                    return;
                }

                // Bọc mảng JSON để parse bằng JsonUtility của Unity
                string wrappedJson = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson<RunHistoryArrayWrapper>(wrappedJson);

                if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
                {
                    Debug.Log($"[MatchHistoryUI] Đã nhận được {wrapper.items.Length} trận đấu.");
                    HideEmptyMessage();
                    UpdateUI(wrapper.items);
                }
                else
                {
                    ShowEmptyMessage("<color=#FFFFFF>Bạn chưa có trận đấu nào.\nHãy vào chơi một trận ngay!</color>");
                    ClearContainerItems();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchHistoryUI] Lỗi phân tích dữ liệu lịch sử: {ex.Message}");
                ShowEmptyMessage("<color=#FF4444>Lỗi xử lý dữ liệu lịch sử đấu.</color>");
            }
        }, (err) =>
        {
            Debug.LogError($"[MatchHistoryUI] Lỗi khi tải lịch sử đấu: {err}");
            ShowEmptyMessage($"<color=#FF4444>Lỗi tải lịch sử từ máy chủ: {err}</color>");
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
            historyFullText.gameObject.SetActive(true);
            StringBuilder sb = new StringBuilder();

            // Đệm khoảng trống ở đầu để dòng đầu tiên không bao giờ bị mép trên của Viewport/Mask che khuất
            sb.AppendLine("<size=14>\n</size>");

            foreach (var item in items)
            {
                sb.AppendLine(FormatItemText(item));
                sb.AppendLine("<size=12>\n</size>"); // Tạo khoảng trống đệm thoáng mắt giữa các trận
            }
            historyFullText.text = sb.ToString();

            // Tự động cuộn thanh cuộn lên trên cùng (Top)
            StartCoroutine(ResetScrollToTop());
        }
    }

    private IEnumerator ResetScrollToTop()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        ScrollRect sr = scrollRect != null ? scrollRect : GetComponentInChildren<ScrollRect>();
        if (sr != null)
        {
            sr.verticalNormalizedPosition = 1f;
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

        // DÒNG 1: Ngày giờ diễn ra + Trạng thái Thắng/Thua + Số Tầng vượt qua
        string line1 = $"{dateStr}{statusTag} <b>Stage {item.wavesSurvived}/5</b>";

        // DÒNG 2: Các thông số chi tiết (Thời lượng, Số quái hạ gục, Lượng sát thương, Vàng kiếm được)
        string line2 = $"<color=#DDDDDD>Time: {timeStr} | Kills: {item.enemiesKilled} | Dmg: {item.damageDealt:N0} |</color> <color=#FFD700>+{item.currencyEarned} Coins</color>";

        return $"{line1}\n{line2}";
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
        if (historyFullText != null && historyFullText != emptyHistoryText)
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
        if (emptyHistoryText != null && emptyHistoryText != historyFullText)
        {
            emptyHistoryText.gameObject.SetActive(false);
        }
    }
}
