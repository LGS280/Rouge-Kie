using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[System.Serializable]
public class UserData
{
    public string username;
    public string password;
    public string confirmPassword;
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
    [SerializeField] private TMP_InputField loginUsernameInput;
    [SerializeField] private TMP_InputField loginPasswordInput;

    [Header("Register Inputs")]
    [SerializeField] private TMP_InputField regUsernameInput;
    [SerializeField] private TMP_InputField regPasswordInput;
    [SerializeField] private TMP_InputField regConfirmPasswordInput;

    [Header("UI Feedback")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text regMessageText; // Thêm ô này cho Register
    [SerializeField] private string backendBase = "https://localhost:7075";

    private void Start()
    {
        ShowLoginPanel();
    }

    // --- CÁC HÀM HIỂN THỊ PANEL ---
    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);

        // ẨN KHUNG MESSAGE KHI CHUYỂN TRANG
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (regMessageText != null) regMessageText.gameObject.SetActive(false);
    }

    public void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);

        // ẨN KHUNG MESSAGE KHI CHUYỂN TRANG
        if (messageText != null) messageText.gameObject.SetActive(false);
        if (regMessageText != null) regMessageText.gameObject.SetActive(false);
    }

    // --- CÁC HÀM ĐIỀU HƯỚNG NÚT BẤM ---
    public void OnGoToRegisterClick()
    {
        ShowRegisterPanel();
    }

    public void OnBackToLoginClick()
    {
        ShowLoginPanel();
    }

    public void OnCloseLoginClick()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(false);
    }

    // --- CÁC HÀM GỌI API ---
    public void OnLoginClick()
    {
        StartCoroutine(LoginRoutine());
    }

    public void OnRegisterClick()
    {
        if (regPasswordInput.text != regConfirmPasswordInput.text)
        {
            if (regMessageText != null)
            {
                // HIỆN KHUNG VÀ ĐẶT CHỮ BÁO LỖI
                regMessageText.gameObject.SetActive(true);
                regMessageText.text = "Lỗi: Mật khẩu xác nhận không khớp!";
                regMessageText.color = Color.red;
            }
            return;
        }

        StartCoroutine(RegisterRoutine());
    }

    private IEnumerator LoginRoutine()
    {
        var data = new UserData { username = loginUsernameInput.text, password = loginPasswordInput.text };
        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(backendBase + "/api/auth/login", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new AcceptAllCerts();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
                if (resp != null && resp.success)
                {
                    PlayerPrefs.SetString("jwt_token", resp.token);
                    PlayerPrefs.Save();
                    if (messageText != null)
                    {
                        // HIỆN KHUNG VÀ BÁO THÀNH CÔNG
                        messageText.gameObject.SetActive(true);
                        messageText.text = "Đăng nhập thành công!";
                        messageText.color = Color.green;
                    }
                    StartCoroutine(GetUsersRoutine());
                    yield break;
                }
                else
                {
                    if (messageText != null)
                    {
                        // HIỆN KHUNG VÀ BÁO THẤT BẠI
                        messageText.gameObject.SetActive(true);
                        messageText.text = resp?.message ?? "Đăng nhập thất bại";
                        messageText.color = Color.red;
                    }
                }
            }
            else
            {
                if (messageText != null)
                {
                    // HIỆN KHUNG VÀ BÁO LỖI MẠNG
                    messageText.gameObject.SetActive(true);
                    messageText.text = "Lỗi mạng: " + request.error;
                    messageText.color = Color.red;
                }
            }
        }
    }

    private IEnumerator RegisterRoutine()
    {
        var data = new UserData { username = regUsernameInput.text, password = regPasswordInput.text, confirmPassword = regConfirmPasswordInput.text };
        string jsonData = JsonUtility.ToJson(data);

        using (var request = new UnityWebRequest(backendBase + "/api/auth/register", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new AcceptAllCerts();

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (regMessageText != null)
                {
                    // HIỆN KHUNG VÀ BÁO THÀNH CÔNG
                    regMessageText.gameObject.SetActive(true);
                    regMessageText.text = "Đăng ký thành công! Hãy quay lại để đăng nhập.";
                    regMessageText.color = Color.green;
                }
            }
            else
            {
                if (regMessageText != null)
                {
                    // HIỆN KHUNG VÀ BÁO LỖI ĐĂNG KÝ
                    regMessageText.gameObject.SetActive(true);
                    regMessageText.text = "Lỗi đăng ký: " + request.error;
                    regMessageText.color = Color.red;
                }
            }
        }
    }

    private IEnumerator GetUsersRoutine()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        using (var req = UnityWebRequest.Get(backendBase + "/api/users"))
        {
            if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
            req.certificateHandler = new AcceptAllCerts();
            req.downloadHandler = new DownloadHandlerBuffer();

            yield return req.SendWebRequest();
        }
    }
}