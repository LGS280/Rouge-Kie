using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Quản lý gọi API kiểm tra trạng thái bảo trì máy chủ (/api/maintenance/current)
/// Tự động khởi tạo dạng Singleton DontDestroyOnLoad
/// </summary>
public class MaintenanceManager : MonoBehaviour
{
    private static MaintenanceManager _instance;
    public static MaintenanceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<MaintenanceManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MaintenanceManager");
                    _instance = go.AddComponent<MaintenanceManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public bool IsUnderMaintenance { get; private set; } = false;
    public CurrentMaintenanceStatus CurrentStatus { get; private set; }
    public bool HasUpcomingMaintenance => CurrentStatus != null && CurrentStatus.hasUpcomingMaintenance && CurrentStatus.upcomingMaintenance != null;

    public static bool HasAutoShownUpcomingNoticeThisSession { get; set; } = false;

    public static event Action<CurrentMaintenanceStatus> OnMaintenanceChecked;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private string GetApiUrl(string path)
    {
        string apiBase = "https://localhost:7075/api";
        if (GameConfigManager.Instance != null && !string.IsNullOrEmpty(GameConfigManager.Instance.BaseUrl))
        {
            apiBase = GameConfigManager.Instance.BaseUrl;
        }
        return $"{apiBase}{path}";
    }

    /// <summary>
    /// Gọi API GET /api/maintenance/current để kiểm tra trạng thái máy chủ
    /// </summary>
    /// <param name="onComplete">Callback nhận kết quả CurrentMaintenanceStatus</param>
    /// <param name="showPopupIfMaintenance">Tự động mở Popup nếu máy chủ đang bảo trì</param>
    public void CheckMaintenanceStatus(Action<CurrentMaintenanceStatus> onComplete = null, bool showPopupIfMaintenance = true)
    {
        StartCoroutine(CheckMaintenanceRoutine(onComplete, showPopupIfMaintenance));
    }

    private IEnumerator CheckMaintenanceRoutine(Action<CurrentMaintenanceStatus> onComplete, bool showPopupIfMaintenance)
    {
        string url = GetApiUrl("/maintenance/current");
        using (var req = UnityWebRequest.Get(url))
        {
            req.certificateHandler = new ApiBypassCert();
            req.downloadHandler = new DownloadHandlerBuffer();

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var status = JsonUtility.FromJson<CurrentMaintenanceStatus>(req.downloadHandler.text);
                    if (status != null)
                    {
                        IsUnderMaintenance = status.isUnderMaintenance;
                        CurrentStatus = status;

                        if (IsUnderMaintenance && showPopupIfMaintenance)
                        {
                            bool isAdminOrDev = NetworkManager.Instance != null && 
                                (NetworkManager.Instance.AccountRole == "Developer" || NetworkManager.Instance.AccountRole == "Admin");

                            if (!isAdminOrDev)
                            {
                                MaintenancePopupUI.Instance.Show(status);
                            }
                        }
                        else if (HasUpcomingMaintenance && !HasAutoShownUpcomingNoticeThisSession)
                        {
                            HasAutoShownUpcomingNoticeThisSession = true;
                            MaintenancePopupUI.Instance.ShowUpcomingNotice(status.upcomingMaintenance);
                        }

                        OnMaintenanceChecked?.Invoke(status);
                        onComplete?.Invoke(status);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MaintenanceManager] Lỗi phân tích JSON trạng thái bảo trì: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[MaintenanceManager] Không thể kiểm tra bảo trì từ máy chủ: {req.error}");
            }

            onComplete?.Invoke(null);
        }
    }
}
