using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class ForgotPasswordReq
{
    public string email;
}

[System.Serializable]
public class ResetPasswordReq
{
    public string email;
    public string otpCode;
    public string newPassword;
    public string confirmNewPassword;
}

[System.Serializable]
public class ForgotPasswordResp
{
    public bool success;
    public string message;
}

public class ForgotPasswordController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject emailForgetPanel;
    public GameObject resetPasswordPanel;

    [Header("Email Forget Inputs")]
    public TMP_InputField forgetEmailInput;
    public Button sendOtpButton;

    [Header("Reset Password Inputs")]
    public TMP_InputField resetEmailInput; // Có thể để chế độ Read Only hoặc chặn tương tác
    public TMP_InputField resetOtpInput;
    public TMP_InputField resetNewPasswordInput;
    public TMP_InputField resetConfirmPasswordInput;
    public Button confirmResetButton;
    public Button backToLoginButton; // Gắn thêm nút Back nếu có
    [SerializeField] private string backendBase = "https://rougekiebe.azurewebsites.net";

    // [Header("Optional API URL (Nếu trống tự động lấy từ LoginController/ApiClient)")]
    // public string apiUrl = "https://localhost:7112/api/auth";

    private string GetApiUrl(string path)
    {
        string apiBase = "";
        if (GameConfigManager.Instance != null && !string.IsNullOrEmpty(GameConfigManager.Instance.BaseUrl))
        {
            apiBase = GameConfigManager.Instance.BaseUrl;
        }
        else
        {
            apiBase = LoadBaseUrlFromStreamingAssets();
        }
        if (string.IsNullOrEmpty(apiBase))
        {
            apiBase = backendBase + "/api"; // Fallback to default backend base URL
        }
        return apiBase + path;
    }
    private string LoadBaseUrlFromStreamingAssets()
    {
        try
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "appsettings.json");
            if (File.Exists(filePath))
            {
                string jsonText = File.ReadAllText(filePath);
                jsonText = System.Text.RegularExpressions.Regex.Replace(jsonText, @"^\s*//.*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                ConfigData config = JsonUtility.FromJson<ConfigData>(jsonText);
                if (config != null && !string.IsNullOrEmpty(config.baseUrl))
                {
                    return config.baseUrl;
                }
            }
        }
        catch { }
        return null;
    }

    private void Start()
    {
        if (sendOtpButton != null) sendOtpButton.onClick.AddListener(OnSendOtpClicked);
        if (confirmResetButton != null) confirmResetButton.onClick.AddListener(OnConfirmResetClicked);
        if (backToLoginButton != null) backToLoginButton.onClick.AddListener(BackToLoginPanel);

        // Đảm bảo giấu 2 bảng Quên mật khẩu đi lúc mới bật game
        if (emailForgetPanel) emailForgetPanel.SetActive(false);
        if (resetPasswordPanel) resetPasswordPanel.SetActive(false);
    }

    public void ShowForgetPasswordPanel()
    {
        if (loginPanel) loginPanel.SetActive(false);
        if (resetPasswordPanel) resetPasswordPanel.SetActive(false);
        if (emailForgetPanel) emailForgetPanel.SetActive(true);
        
        if (forgetEmailInput) forgetEmailInput.text = "";
    }

    public void ShowResetPasswordPanel(string lockedEmail)
    {
        if (emailForgetPanel) emailForgetPanel.SetActive(false);
        if (resetPasswordPanel) resetPasswordPanel.SetActive(true);
        
        if (resetEmailInput)
        {
            resetEmailInput.text = lockedEmail;
            resetEmailInput.interactable = false; // Khóa không cho sửa email
        }
        
        if (resetOtpInput) resetOtpInput.text = "";
        if (resetNewPasswordInput) resetNewPasswordInput.text = "";
        if (resetConfirmPasswordInput) resetConfirmPasswordInput.text = "";
    }

    public void BackToLoginPanel()
    {
        if (emailForgetPanel) emailForgetPanel.SetActive(false);
        if (resetPasswordPanel) resetPasswordPanel.SetActive(false);
        if (loginPanel) loginPanel.SetActive(true);
    }

    private void OnSendOtpClicked()
    {
        if (forgetEmailInput == null) return;
        string email = forgetEmailInput.text.Trim();
        
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogWarning("[ForgotPass] Email không được để trống!");
            return;
        }

        sendOtpButton.interactable = false;
        StartCoroutine(SendOtpRoutine(email));
    }

    private void OnConfirmResetClicked()
    {
        if (resetEmailInput == null || resetOtpInput == null || resetNewPasswordInput == null) return;
        
        string email = resetEmailInput.text.Trim();
        string otp = resetOtpInput.text.Trim();
        string newPass = resetNewPasswordInput.text.Trim();
        string confirmPass = resetConfirmPasswordInput.text.Trim();

        if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(newPass) || string.IsNullOrEmpty(confirmPass))
        {
            Debug.LogWarning("[ForgotPass] Vui lòng nhập đầy đủ thông tin!");
            return;
        }

        if (newPass != confirmPass)
        {
            Debug.LogWarning("[ForgotPass] Mật khẩu xác nhận không khớp!");
            return;
        }

        confirmResetButton.interactable = false;
        StartCoroutine(ResetPasswordRoutine(email, otp, newPass, confirmPass));
    }

    private IEnumerator SendOtpRoutine(string email)
    {
        ForgotPasswordReq req = new ForgotPasswordReq { email = email };
        string json = JsonUtility.ToJson(req);
        
        using (var request = new UnityWebRequest(GetApiUrl("/auth/forgot-password"), "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new AcceptAllCerts(); // Bỏ qua lỗi chứng chỉ SSL local

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[ForgotPass] Gửi OTP thành công!");
                ShowResetPasswordPanel(email);
            }
            else
            {
                string error = request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text) ? request.downloadHandler.text : request.error;
                Debug.LogError("[ForgotPass] Lỗi: " + error);
            }
        }
        
        if (sendOtpButton) sendOtpButton.interactable = true;
    }

    private IEnumerator ResetPasswordRoutine(string email, string otp, string newPass, string confirmPass)
    {
        ResetPasswordReq req = new ResetPasswordReq
        {
            email = email,
            otpCode = otp,
            newPassword = newPass,
            confirmNewPassword = confirmPass
        };
        string json = JsonUtility.ToJson(req);

        using (var request = new UnityWebRequest(GetApiUrl("/auth/reset-password"), "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new AcceptAllCerts(); // Bỏ qua lỗi chứng chỉ SSL local

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[ForgotPass] Đổi mật khẩu thành công!");
                BackToLoginPanel();
            }
            else
            {
                string error = request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text) ? request.downloadHandler.text : request.error;
                Debug.LogError("[ForgotPass] Lỗi: " + error);
            }
        }
        
        if (confirmResetButton) confirmResetButton.interactable = true;
    }
}
