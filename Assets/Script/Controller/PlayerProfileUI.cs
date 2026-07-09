using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[Serializable]
public class ProfileResponseData
{
    public int profileId;
    public int userId;
    public string displayName;
    public int standardCurrency; // Quy đổi thành Gem
    public int premiumCurrency;  // Quy đổi thành Ruby
    public int totalRuns;
    public int highestWave;
    public int totalKills;
    public string updatedAt;
}

/// <summary>
/// Quản lý giao diện hiển thị Hồ sơ người chơi (Player Profile) và Nút đăng nhập động tại Main Menu
/// </summary>
public class PlayerProfileUI : MonoBehaviour
{
    // Singleton Instance để truy cập nhanh từ các Controller khác
    public static PlayerProfileUI Instance { get; private set; }

    [Header("Profile UI Elements")]
    [SerializeField] private GameObject profileContainer; // Panel tổng thể chứa thông tin profile (hiện khi đã login)
    [SerializeField] private GameObject loginButton;       // Nút bấm đăng nhập nhanh ở Menu chính (hiện khi chưa login)
    
    [Header("Text Fields")]
    [SerializeField] private TMP_Text displayNameText;     // Hiển thị tên người chơi
    [SerializeField] private TMP_Text gemText;             // Hiển thị Gem (tiền tệ vĩnh viễn quy đổi từ Coin)
    [SerializeField] private TMP_Text rubyText;            // Hiển thị Ruby (tiền tệ nạp từ tiền thật)
    [SerializeField] private TMP_Text recordText;          // Hiển thị kỷ lục vượt ải và số kills

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // Gắn sự kiện click cho nút Login để mở giao diện đăng nhập additively
        if (loginButton != null)
        {
            Button btn = loginButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(OnLoginButtonClicked);
            }
        }
        
        RefreshProfile();
    }

    /// <summary>
    /// Bấm nút Đăng nhập nhanh sẽ tải Scene Login đè lên sảnh chính
    /// </summary>
    private void OnLoginButtonClicked()
    {
        if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("LoginScrene").isLoaded)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScrene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
        }
    }

    /// <summary>
    /// Cập nhật hiển thị ẩn/hiện Panel Profile hoặc Nút Đăng nhập dựa trên trạng thái Login
    /// </summary>
    public void RefreshProfile()
    {
        bool loggedIn = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn;

        if (loggedIn)
        {
            if (profileContainer != null) profileContainer.SetActive(true);
            if (loginButton != null) loginButton.SetActive(false);
            LoadProfileFromServer();
        }
        else
        {
            if (profileContainer != null) profileContainer.SetActive(false);
            if (loginButton != null) loginButton.SetActive(true);
        }
    }

    // Thực hiện gọi API lấy thông tin Profile qua ApiClient
    private void LoadProfileFromServer()
    {
        if (ApiClient.Instance == null) return;

        ApiClient.Instance.Get("/playerprofile", (json) =>
        {
            try
            {
                var profile = JsonUtility.FromJson<ProfileResponseData>(json);
                if (profile != null)
                {
                    UpdateUI(profile);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileUI] Lỗi phân tích cú pháp dữ liệu profile: {ex.Message}");
            }
        }, (err) =>
        {
            Debug.LogError($"[ProfileUI] Lỗi khi tải dữ liệu profile từ API: {err}");
        });
    }

    // Đổ dữ liệu đã phân tích được lên các TextMeshPro UI Elements tương ứng
    private void UpdateUI(ProfileResponseData data)
    {
        if (displayNameText != null) displayNameText.text = data.displayName;
        if (gemText != null) gemText.text = $"{data.standardCurrency} Gem";
        if (rubyText != null) rubyText.text = $"{data.premiumCurrency} Ruby";
        if (recordText != null) recordText.text = $"Kỷ lục: Wave {data.highestWave} | Kills: {data.totalKills}";
    }
}
