using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
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
    public string refreshToken;
}

// Model nhận cấu hình bảo mật Google từ file JSON cục bộ
[System.Serializable]
public class GoogleSecrets
{
    public string clientId;
    public string clientSecret;
}

// Model nhận Token trả về từ Google
[System.Serializable]
public class GoogleTokenResponse
{
    public string access_token;
    public string id_token;
    public int expires_in;
    public string token_type;
}

// Model gửi ID Token lên API Backend
[System.Serializable]
public class GoogleLoginRequest
{
    public string idToken;
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

    [Header("Google OAuth 2.0 Settings (PC)")]
    private string googleClientId = "";
    private string googleClientSecret = "";
    private const string GoogleRedirectUri = "http://127.0.0.1:51099/";

    private HttpListener httpListener;
    private string authCodeToExchange = null;

    // Danh sách các GraphicRaycaster thuộc scene khác bị tắt tạm thời
    private readonly List<GraphicRaycaster> _disabledRaycasters = new List<GraphicRaycaster>();

    private void Start()
    {
        // 1. Tự động đọc Client ID và Client Secret từ file Assets/Resources/google_secrets.json
        LoadGoogleSecrets();

        ShowLoginPanel();

        // Đăng ký sự kiện lắng nghe sự kiện input thay đổi trong trường input email
        if (regEmailInput != null)
        {
            regEmailInput.onValueChanged.AddListener(OnEmailValueChanged);
        }

        // Chặn tương tác của các scene khác (Menu) khi Login scene đang mở
        BlockOtherScenesInput();
    }

    private void LoadGoogleSecrets()
    {
        TextAsset secretFile = Resources.Load<TextAsset>("google_secrets");
        if (secretFile != null)
        {
            try
            {
                GoogleSecrets secrets = JsonUtility.FromJson<GoogleSecrets>(secretFile.text);
                googleClientId = secrets.clientId;
                googleClientSecret = secrets.clientSecret;
                Debug.Log("Đã nạp thành công cấu hình bảo mật Google từ file Resources!");
            }
            catch (Exception ex)
            {
                Debug.LogError("Lỗi phân tích cú pháp file JSON bảo mật Google: " + ex.Message);
            }
        }
        else
        {
            Debug.LogError("Không tìm thấy file google_secrets.json tại Assets/Resources/! Vui lòng tạo file này để chạy đăng nhập Google.");
            ShowLoginMessage("Chưa thiết lập file google_secrets.json!", Color.red);
        }
    }

    private void Update()
    {
        // Kiểm tra xem có Authorization Code nào gửi từ Trình duyệt về không (ở luồng chính của Unity)
        if (authCodeToExchange != null)
        {
            string code = authCodeToExchange;
            authCodeToExchange = null; // Clear flag
            StartCoroutine(ExchangeGoogleCodeForToken(code));
        }
    }

    private void OnDestroy()
    {
        // Hủy lắng nghe khi Object bị xóa để tránh rò rỉ bộ nhớ
        if (regEmailInput != null)
        {
            regEmailInput.onValueChanged.RemoveListener(OnEmailValueChanged);
        }

        // Tắt Listener nếu Script bị hủy để giải phóng Port
        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Stop();
        }

        // Khôi phục tương tác cho các scene khác khi Login scene đóng
        RestoreOtherScenesInput();
    }

    /// <summary>
    /// Tắt GraphicRaycaster của tất cả Canvas thuộc scene khác (VD: Menu scene)
    /// để chúng không nhận input khi Login panel đang hiển thị phía trên.
    /// </summary>
    private void BlockOtherScenesInput()
    {
        _disabledRaycasters.Clear();
        string loginSceneName = gameObject.scene.name;

        GraphicRaycaster[] allRaycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
        foreach (GraphicRaycaster raycaster in allRaycasters)
        {
            // Chỉ tắt raycaster thuộc scene KHÁC với Login scene
            if (raycaster.gameObject.scene.name != loginSceneName && raycaster.enabled)
            {
                raycaster.enabled = false;
                _disabledRaycasters.Add(raycaster);
            }
        }
    }

    /// <summary>
    /// Bật lại các GraphicRaycaster đã bị tắt khi Login scene bị unload.
    /// </summary>
    private void RestoreOtherScenesInput()
    {
        foreach (GraphicRaycaster raycaster in _disabledRaycasters)
        {
            // Kiểm tra null để tránh lỗi nếu object đã bị destroy
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }
        _disabledRaycasters.Clear();
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

        // Hàm check để ẩn/hiện nút OTP khi hiển thị panel đăng ký
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
        string currentSceneName = gameObject.scene.name;
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentSceneName);
    }

    public void OnLoginClick()
    {
        StartCoroutine(LoginRoutine());
    }

    // Bắt sự kiện Click nút bấm "Đăng nhập Google"
    public void OnGoogleLoginClick()
    {
        try
        {
            if (string.IsNullOrEmpty(googleClientId))
            {
                ShowLoginMessage("Lỗi: Không tìm thấy Client ID Google cấu hình!", Color.red);
                return;
            }

            StartGoogleLocalServer();

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                             $"client_id={googleClientId}&" +
                             $"redirect_uri={UnityWebRequest.EscapeURL(GoogleRedirectUri)}&" +
                             $"response_type=code&" +
                             $"scope=openid%20email%20profile";

            Application.OpenURL(authUrl);
            ShowLoginMessage("Đang mở trình duyệt để đăng nhập Google...", Color.white);
        }
        catch (Exception ex)
        {
            ShowLoginMessage("Lỗi đăng nhập Google: " + ex.Message, Color.red);
        }
    }

    private void StartGoogleLocalServer()
    {
        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Stop();
        }

        httpListener = new HttpListener();
        httpListener.Prefixes.Add(GoogleRedirectUri);
        httpListener.Start();

        Task.Run(() => ListenForGoogleRedirect());
    }

    private async void ListenForGoogleRedirect()
    {
        try
        {
            HttpListenerContext context = await httpListener.GetContextAsync();
            HttpListenerRequest request = context.Request;

            string code = request.QueryString["code"];
            Debug.Log("Đã nhận được Code từ Google: " + code);

            HttpListenerResponse response = context.Response;
            string responseString = "<html><head><meta charset='utf-8'></head><body><h2 style='text-align:center;font-family:sans-serif;margin-top:50px;'>Đăng nhập thành công! Bạn có thể đóng trình duyệt này và quay lại game.</h2></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

            httpListener.Stop();

            if (!string.IsNullOrEmpty(code))
            {
                authCodeToExchange = code;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Lỗi HttpListener: " + ex.Message);
        }
    }

    private IEnumerator ExchangeGoogleCodeForToken(string code)
    {
        WWWForm form = new WWWForm();
        form.AddField("code", code);
        form.AddField("client_id", googleClientId);
        form.AddField("client_secret", googleClientSecret); // Nạp tự động từ file JSON
        form.AddField("redirect_uri", GoogleRedirectUri);
        form.AddField("grant_type", "authorization_code");

        using (UnityWebRequest request = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string detail = request.error + " | Chi tiết: " + request.downloadHandler.text;
                Debug.LogError("Lỗi Google Exchange Code: " + detail);
                ShowLoginMessage("Lỗi trao đổi: " + detail, Color.red);
                yield break;
            }

            string jsonResult = request.downloadHandler.text;
            GoogleTokenResponse tokenData = JsonUtility.FromJson<GoogleTokenResponse>(jsonResult);

            string idToken = tokenData.id_token;

            // Gửi idToken nhận được lên Backend của bạn
            yield return StartCoroutine(GoogleLoginBackendRoutine(idToken));
        }
    }

    private IEnumerator GoogleLoginBackendRoutine(string idToken)
    {
        var data = new GoogleLoginRequest { idToken = idToken };
        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/google-login"), "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

                if (resp != null && resp.success)
                {
                    ProcessLoginSuccess(resp);
                    yield break;
                }

                ShowLoginMessage(resp?.message ?? "Đăng nhập Google thất bại.", Color.red);
            }
            else
            {
                ShowLoginMessage(GetErrorMessage(request, "Lỗi đăng nhập Google"), Color.red);
            }
        }
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
            username = loginUsernameInput.text.Trim(),
            password = loginPasswordInput.text
        };

        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/login"), "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);

                if (resp != null && resp.success)
                {
                    ProcessLoginSuccess(resp); // Gọi hàm xử lý thành công dùng chung
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

    // TÁI SỬ DỤNG: Hàm xử lý Đăng nhập thành công dùng chung cho cả Login thường và Login Google
    private void ProcessLoginSuccess(LoginResponse resp)
    {
        PlayerPrefs.SetString("jwt_token", resp.token);
        PlayerPrefs.SetString("refresh_token", resp.refreshToken); // Lưu refresh token từ dev
        PlayerPrefs.SetString("username", resp.username);
        PlayerPrefs.SetInt("user_id", resp.userId);
        PlayerPrefs.Save();

        NetworkManager.Instance.IsLoggedIn = true;
        NetworkManager.Instance.LoggedInUsername = resp.username;
        NetworkManager.Instance.UserRole = "Player";

        // Tải lại cấu hình súng/đạn vì giờ đã có token (Cập nhật từ dev)
        GameConfigManager.Instance?.ReloadConfigs();

        // Cập nhật thông tin profile lên UI (Cập nhật từ dev)
        PlayerProfileUI.Instance?.RefreshProfile();

        // Tự động gọi Menu chính mở sảnh Co-op
        LobbyUIController lobbyUI = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
        if (lobbyUI != null) lobbyUI.OnCoOpButtonPressed();

        ShowLoginMessage("Đăng nhập thành công!", Color.green);

        // Tự động giải phóng Scene đăng nhập
        string currentSceneName = gameObject.scene.name;
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentSceneName);

        StartCoroutine(GetUsersRoutine());
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