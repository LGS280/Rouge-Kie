using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lớp hỗ trợ gọi các API Backend có xác thực JWT và cơ chế tự động xoay vòng Refresh Token
/// </summary>
public class ApiClient : MonoBehaviour
{
    private static ApiClient instance;
    public static ApiClient Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ApiClient>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ApiClient");
                    instance = go.AddComponent<ApiClient>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[ApiClient] Tự động khởi tạo đối tượng ApiClient chạy ngầm.");
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Giữ đối tượng tồn tại xuyên suốt các scene
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    [Serializable]
    private class RefreshRequest
    {
        public string refreshToken;
    }

    [Serializable]
    private class RefreshResponse
    {
        public string accessToken;
        public string refreshToken;
    }

    /// <summary>
    /// Thực hiện gửi yêu cầu HTTP GET
    /// </summary>
    public void Get(string path, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine("GET", path, null, onSuccess, onError));
    }

    /// <summary>
    /// Thực hiện gửi yêu cầu HTTP POST kèm dữ liệu JSON
    /// </summary>
    public void Post(string path, string jsonBody, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine("POST", path, jsonBody, onSuccess, onError));
    }

    /// <summary>
    /// Thực hiện gửi yêu cầu HTTP PUT kèm dữ liệu JSON
    /// </summary>
    public void Put(string path, string jsonBody, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(RequestRoutine("PUT", path, jsonBody, onSuccess, onError));
    }

    // Lấy URL đầy đủ bằng cách kết hợp BaseUrl cấu hình từ appsettings.json
    private string GetFullUrl(string path)
    {
        string apiBase = "https://rougekiebe.azurewebsites.net/api";
        if (GameConfigManager.Instance != null && !string.IsNullOrEmpty(GameConfigManager.Instance.BaseUrl))
        {
            apiBase = GameConfigManager.Instance.BaseUrl;
        }
        return $"{apiBase}{path}";
    }

    /// <summary>
    /// Luồng Coroutine xử lý gửi yêu cầu HTTP chính, tự động đính kèm Token và xử lý mã lỗi 401
    /// </summary>
    private IEnumerator RequestRoutine(string method, string path, string jsonBody, Action<string> onSuccess, Action<string> onError, bool isRetry = false)
    {
        string url = GetFullUrl(path);
        using (var request = new UnityWebRequest(url, method))
        {
            // Đọc và đính kèm JWT Access Token nếu có trong bộ nhớ tạm
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.certificateHandler = new ApiBypassCert(); // Chấp nhận chứng chỉ tự ký cho local development

            yield return request.SendWebRequest();

            // Nhận mã lỗi 401 Unauthorized và chưa thử lại -> Tiến hành làm mới token tự động
            if (request.responseCode == 401 && !isRetry)
            {
                string refreshToken = PlayerPrefs.GetString("refresh_token", "");
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    Debug.Log("[ApiClient] Access Token hết hạn. Đang tiến hành tự động làm mới (Silent Refresh)...");
                    bool refreshSuccess = false;
                    yield return StartCoroutine(RefreshTokenRoutine((success) => refreshSuccess = success));

                    if (refreshSuccess)
                    {
                        // Thử gửi lại yêu cầu gốc với Access Token mới
                        yield return StartCoroutine(RequestRoutine(method, path, jsonBody, onSuccess, onError, true));
                        yield break;
                    }
                }

                // Nếu không có Refresh Token hoặc tự động làm mới thất bại -> Buộc đăng xuất để dọn dẹp session đã hết hạn
                Debug.LogWarning("[ApiClient] Session hết hạn hoặc không hợp lệ. Đang tiến hành đăng xuất tự động.");
                Logout();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string errMsg = string.IsNullOrEmpty(request.downloadHandler.text) ? request.error : request.downloadHandler.text;
                Debug.LogError($"[ApiClient Error] Yêu cầu {method} tới {path} thất bại: {errMsg}");
                onError?.Invoke(errMsg);
            }
        }
    }

    /// <summary>
    /// Luồng Coroutine xử lý trao đổi Refresh Token để lấy cặp Access Token + Refresh Token mới
    /// </summary>
    private IEnumerator RefreshTokenRoutine(Action<bool> onComplete)
    {
        string refreshToken = PlayerPrefs.GetString("refresh_token", "");
        var reqObj = new RefreshRequest { refreshToken = refreshToken };
        string jsonBody = JsonUtility.ToJson(reqObj);
        string url = GetFullUrl("/auth/refresh");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new ApiBypassCert();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<RefreshResponse>(request.downloadHandler.text);
                    if (resp != null && !string.IsNullOrEmpty(resp.accessToken))
                    {
                        // Lưu trữ cặp token mới nhận được
                        PlayerPrefs.SetString("jwt_token", resp.accessToken);
                        PlayerPrefs.SetString("refresh_token", resp.refreshToken);
                        PlayerPrefs.Save();
                        Debug.Log("[ApiClient] Tự động làm mới Refresh Token thành công!");
                        onComplete?.Invoke(true);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ApiClient] Lỗi xử lý dữ liệu trả về của Refresh Token: {ex.Message}");
                }
            }
            
            Debug.LogWarning("[ApiClient] Không thể tự động làm mới Refresh Token. Đăng xuất tài khoản.");
            Logout();
            onComplete?.Invoke(false);
        }
    }

    /// <summary>
    /// Thực hiện xóa sạch thông tin đăng nhập trong PlayerPrefs và thiết lập lại trạng thái Guest
    /// </summary>
    public void Logout()
    {
        PlayerPrefs.DeleteKey("jwt_token");
        PlayerPrefs.DeleteKey("refresh_token");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.DeleteKey("user_id");
        PlayerPrefs.Save();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.IsLoggedIn = false;
            NetworkManager.Instance.LoggedInUsername = "Guest";
            NetworkManager.Instance.UserRole = "Guest";
        }

        // Tải lại cấu hình game dạng Guest (không token)
        GameConfigManager.Instance?.ReloadConfigs();

        // Refresh lai giao dien profile
        PlayerProfileUI.Instance?.RefreshProfile();
    }
}

public class ApiBypassCert : CertificateHandler
{
    // Bỏ qua xác thực chứng chỉ SSL (sử dụng trong môi trường thử nghiệm)
    protected override bool ValidateCertificate(byte[] d) => true;
}
