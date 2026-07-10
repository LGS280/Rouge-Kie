using System;
using System.Text;
using UnityEngine;
using TMPro;

[Serializable]
public class LeaderboardItem
{
    public int rank;
    public string username;
    public string displayName;
    public int highestWave;
    public int totalKills;
    public int totalRuns;
}

[Serializable]
public class LeaderboardArrayWrapper
{
    public LeaderboardItem[] items;
}

/// <summary>
/// Quản lý giao diện hiển thị Bảng xếp hạng (Leaderboard) trong game Unity
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    private static LeaderboardUI instance;
    public static LeaderboardUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<LeaderboardUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [Header("UI References")]
    [SerializeField] private GameObject leaderboardPanel; // Panel chứa bảng xếp hạng
    [SerializeField] private TMP_Text leaderboardText;      // TextMeshPro hiển thị danh sách xếp hạng

    public bool IsOpen => leaderboardPanel != null && leaderboardPanel.activeSelf;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Start()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
    }

    /// <summary>
    /// Mở bảng xếp hạng và tải dữ liệu mới từ máy chủ
    /// </summary>
    public void ShowLeaderboard()
    {
        gameObject.SetActive(true); // Kích hoạt đối tượng cha chứa script
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
        LoadLeaderboardFromServer();
    }

    /// <summary>
    /// Đóng bảng xếp hạng
    /// </summary>
    public void HideLeaderboard()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
    }

    // Gửi yêu cầu lấy Top 15 người chơi xuất sắc nhất qua ApiClient
    private void LoadLeaderboardFromServer()
    {
        if (ApiClient.Instance == null) return;

        // API bảng xếp hạng cho phép gọi công khai (không cần Token)
        ApiClient.Instance.Get("/leaderboard?top=15", (json) =>
        {
            try
            {
                // Bọc mảng JSON thô thành định dạng đối tượng để tương thích với JsonUtility của Unity
                string wrappedJson = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson<LeaderboardArrayWrapper>(wrappedJson);
                if (wrapper != null && wrapper.items != null)
                {
                    UpdateUI(wrapper.items);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardUI] Lỗi phân tích dữ liệu bảng xếp hạng: {ex.Message}");
            }
        }, (err) =>
        {
            Debug.LogError($"[LeaderboardUI] Lỗi khi tải dữ liệu bảng xếp hạng: {err}");
        });
    }

    // Định dạng và hiển thị bảng xếp hạng lên màn hình sử dụng thẻ Rich Text <pos> để căn cột chính xác tuyệt đối
    private void UpdateUI(LeaderboardItem[] items)
    {
        if (leaderboardText == null) return;

        StringBuilder sb = new StringBuilder();
        // Căn lề các cột chính xác theo số điểm ảnh (pixels) từ lề trái (Tiếng Anh)
        sb.AppendLine("<b>RANK<pos=150>PLAYER<pos=350>WAVE<pos=500>KILLS</b>");
        sb.AppendLine("---------------------------------------------------------------------------");

        foreach (var item in items)
        {
            string nameStr = string.IsNullOrEmpty(item.displayName) ? item.username : item.displayName;
            if (nameStr.Length > 16) nameStr = nameStr.Substring(0, 14) + "..";

            // Sử dụng thẻ <pos> để đẩy text của các cột về đúng vị trí mong muốn
            sb.AppendLine($" {item.rank}<pos=150>{nameStr}<pos=350>{item.highestWave}<pos=500>{item.totalKills}");
        }

        leaderboardText.text = sb.ToString();
    }
}
