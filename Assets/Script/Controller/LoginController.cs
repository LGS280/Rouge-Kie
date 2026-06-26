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

    [Header("UI Feedback")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text regMessageText;

    [Header("Backend")]
    [SerializeField] private string backendBase = "https://localhost:7075";

    private void Start()
    {
        ShowLoginPanel();
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

        HideMessages();
    }

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

        using (var request = new UnityWebRequest(backendBase + "/api/auth/send-register-otp", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new AcceptAllCerts();

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
                    PlayerPrefs.SetString("username", resp.username);
                    PlayerPrefs.SetInt("user_id", resp.userId);
                    PlayerPrefs.Save();

                    ShowLoginMessage("Đăng nhập thành công!", Color.green);

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
                var resp = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                ShowRegisterMessage(resp?.message ?? "Đăng ký thành công! Hãy quay lại để đăng nhập.", Color.green);
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

        using (var req = UnityWebRequest.Get(backendBase + "/api/users"))
        {
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            req.certificateHandler = new AcceptAllCerts();
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