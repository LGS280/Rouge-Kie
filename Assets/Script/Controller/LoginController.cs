using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[System.Serializable]
public class UserData
{
    public string username;
    public string email;
    public string password;
    public string confirmPassword;
    public string otpCode;
}

[System.Serializable]
public class SendOtpRequest
{
    public string email;
}

[System.Serializable]
public class ApiResponse
{
    public bool success;
    public string message;
}

[System.Serializable]
public class LoginResponse
{
    public bool success;
    public string message;
    public int userId;
    public string username;
    public string token;
}

// Development-only: accepts any TLS certificate (self-signed). Remove for production.
public class AcceptAllCerts : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

public class LoginController : MonoBehaviour
{
    [Header("Scene Pages")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;

    [Header("Login Inputs")]
    [SerializeField] private TMP_InputField loginUsernameInput; // nhập username hoặc email
    [SerializeField] private TMP_InputField loginPasswordInput;

    [Header("Register Inputs")]
    [SerializeField] private TMP_InputField regUsernameInput;
    [SerializeField] private TMP_InputField regEmailInput;
    [SerializeField] private TMP_InputField regPasswordInput;
    [SerializeField] private TMP_InputField regConfirmPasswordInput;
    [SerializeField] private TMP_InputField regOtpInput;
    [SerializeField] private GameObject getOtpButton;

    [Header("UI Feedback")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text regMessageText;

    [SerializeField] private string backendBase = "https://rougekiebe.azurewebsites.net";

    // BỔ SUNG: Hàm lấy URL API động từ appsettings.json nếu có, tránh fix cứng đường dẫn Azure
    private string GetApiUrl(string path)
    {
        string apiBase = backendBase + "/api";
        if (GameConfigManager.Instance != null && !string.IsNullOrEmpty(GameConfigManager.Instance.BaseUrl))
        {
            apiBase = GameConfigManager.Instance.BaseUrl;
        }
        return $"{apiBase}{path}";
    }

    private void Start()
    {
        ShowLoginPanel();

        //đăng kí sự kiện lắng nghe sự kiện input thay đổi trong trường input email
        if (regEmailInput != null)
        {
            regEmailInput.onValueChanged.AddListener(OnEmailValueChanged);
        }
    }

    private void OnDestroy()
    {
        // Hủy lắng nghe khi Object bị xóa để tránh rò rỉ bộ nhớ
        if (regEmailInput != null)
        {
            regEmailInput.onValueChanged.RemoveListener(OnEmailValueChanged);
        }
    }

    // Hàm lắng nghe sự kiện thay đổi text từ Input Field
    private void OnEmailValueChanged(string value)
    {
        UpdateOtpButtonVisibility(value);
    }

    // HÀM CẬP NHẬT: Xử lý làm sạch chuỗi và ẩn/hiện nút OTP chính xác
    private void UpdateOtpButtonVisibility(string textValue)
    {
        if (getOtpButton != null)
        {
            // Loại bỏ các ký tự ẩn đặc biệt của TextMeshPro cùng khoảng trắng thừa
            string cleanedText = textValue.Replace("\u200b", "").Replace("\r", "").Replace("\n", "").Trim();

            // Chỉ hiện nút nếu chuỗi sau khi làm sạch không bị rỗng
            bool shouldShow = !string.IsNullOrEmpty(cleanedText);
            getOtpButton.SetActive(shouldShow);
        }
    }

    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);

        HideMessages();
    }

    public void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);

        //hàm check để ẩn/hiện nút OTP khi hiển thị panel đăng ký
        if (getOtpButton != null)
        {
            getOtpButton.SetActive(regEmailInput != null && !string.IsNullOrWhiteSpace(regEmailInput.text));
        }

        HideMessages();
    }

    public void OnGoToRegisterClick()
    {
        ShowRegisterPanel();
    }

    public void OnBackToLoginClick()
    {
        // Nếu đang ở trang Register, bấm Back sẽ quay lại trang Login Panel
        if (registerPanel != null && registerPanel.activeSelf)
        {
            ShowLoginPanel();
        }
        else
        {
            // Tự động lấy chính xác tên Scene hiện tại đang chứa Script này để Unload an toàn
            string currentSceneName = gameObject.scene.name;
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentSceneName);
        }
    }

    public void OnCloseLoginClick()
    {
        // Tự động lấy chính xác tên Scene hiện tại đang chứa Script này để Unload an toàn
        string currentSceneName = gameObject.scene.name;
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentSceneName);
    }

    public void OnLoginClick()
    {
        StartCoroutine(LoginRoutine());
    }

    public void OnSendOtpClick()
    {
        if (regEmailInput == null || string.IsNullOrWhiteSpace(regEmailInput.text))
        {
            ShowRegisterMessage("Vui lòng nhập email trước khi gửi OTP.", Color.red);
            return;
        }

        StartCoroutine(SendRegisterOtpRoutine());
    }

    public void OnRegisterClick()
    {
        if (regPasswordInput.text != regConfirmPasswordInput.text)
        {
            ShowRegisterMessage("Lỗi: Mật khẩu xác nhận không khớp!", Color.red);
            return;
        }

        if (string.IsNullOrWhiteSpace(regOtpInput.text))
        {
            ShowRegisterMessage("Vui lòng nhập mã OTP.", Color.red);
            return;
        }

        StartCoroutine(RegisterRoutine());
    }

    private IEnumerator SendRegisterOtpRoutine()
    {
        var data = new SendOtpRequest
        {
            email = regEmailInput.text.Trim()
        };

        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/send-register-otp"), "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            //request.certificateHandler = new AcceptAllCerts();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                ShowRegisterMessage(resp?.message ?? "Mã OTP đã được gửi đến email.", Color.green);
            }
            else
            {
                ShowRegisterMessage(GetErrorMessage(request, "Lỗi gửi OTP"), Color.red);
            }
        }
    }

    private IEnumerator LoginRoutine()
    {
        var data = new UserData
        {
            username = loginUsernameInput.text.Trim(), // backend nhận username hoặc email ở field này
            password = loginPasswordInput.text
        };

        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/login"), "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            //request.certificateHandler = new AcceptAllCerts();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

                if (resp != null && resp.success)
                {
                    PlayerPrefs.SetString("jwt_token", resp.token);
                    PlayerPrefs.SetString("username", resp.username);
                    PlayerPrefs.SetInt("user_id", resp.userId);
                    PlayerPrefs.Save();

                    NetworkManager.Instance.IsLoggedIn = true;
                    NetworkManager.Instance.LoggedInUsername = resp.username;
                    NetworkManager.Instance.UserRole = "Player";

                    // Tự động gọi Menu chính mở sảnh Co-op
                    LobbyUIController lobbyUI = Object.FindFirstObjectByType<LobbyUIController>();
                    if (lobbyUI != null) lobbyUI.OnCoOpButtonPressed();

                    // Tự giải phóng Scene đăng nhập
                    //UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("LoginScene");
                    ShowLoginMessage("Đăng nhập thành công!", Color.green);

                    // FIX TẠI ĐÂY: Tự động lấy chính xác tên Scene chứa Script này (LoginScene) để giải phóng hoàn toàn
                    string currentSceneName = gameObject.scene.name;
                    UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentSceneName);

                    StartCoroutine(GetUsersRoutine());
                    yield break;
                }

                ShowLoginMessage(resp?.message ?? "Đăng nhập thất bại.", Color.red);
            }
            else
            {
                ShowLoginMessage(GetErrorMessage(request, "Lỗi đăng nhập"), Color.red);
            }
        }
    }

    private IEnumerator RegisterRoutine()
    {
        var data = new UserData
        {
            username = regUsernameInput.text.Trim(),
            email = regEmailInput.text.Trim(),
            password = regPasswordInput.text,
            confirmPassword = regConfirmPasswordInput.text,
            otpCode = regOtpInput.text.Trim()
        };

        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/register"), "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            //request.certificateHandler = new AcceptAllCerts();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                ShowRegisterMessage(resp?.message ?? "Đăng ký thành công! Hãy quay lại để đăng nhập.", Color.green);

                Invoke("ShowLoginPanel", 1.5f);
            }
            else
            {
                ShowRegisterMessage(GetErrorMessage(request, "Lỗi đăng ký"), Color.red);
            }
        }
    }

    private IEnumerator GetUsersRoutine()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");

        using (var req = UnityWebRequest.Get(GetApiUrl("/users")))
        {
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            //req.certificateHandler = new AcceptAllCerts();
            req.downloadHandler = new DownloadHandlerBuffer();

            yield return req.SendWebRequest();
        }
    }

    private void HideMessages()
    {
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (regMessageText != null) regMessageText.gameObject.SetActive(false);
    }

    private void ShowLoginMessage(string message, Color color)
    {
        if (messageText == null) return;

        messageText.gameObject.SetActive(true);
        messageText.text = message;
        messageText.color = color;
    }

    private void ShowRegisterMessage(string message, Color color)
    {
        if (regMessageText == null) return;

        regMessageText.gameObject.SetActive(true);
        regMessageText.text = message;
        regMessageText.color = color;
    }

    private string GetErrorMessage(UnityWebRequest request, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(request.downloadHandler.text))
        {
            var resp = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
            if (resp != null && !string.IsNullOrWhiteSpace(resp.message))
            {
                return resp.message;
            }
        }

        return fallback + ": " + request.error;
    }
}