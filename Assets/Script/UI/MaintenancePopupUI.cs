using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý Hộp thoại thông báo Bảo trì Máy chủ (Maintenance Popup UI)
/// Tự động sinh giao diện Canvas chất lượng cao bằng C# (DontDestroyOnLoad)
/// Toàn bộ nội dung hiển thị chuẩn tiếng Anh (English UI)
/// </summary>
public class MaintenancePopupUI : MonoBehaviour
{
    private static MaintenancePopupUI _instance;
    public static MaintenancePopupUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<MaintenancePopupUI>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("MaintenancePopupUI");
                    _instance = obj.AddComponent<MaintenancePopupUI>();
                }
            }
            return _instance;
        }
    }

    private Canvas popupCanvas;
    private CanvasGroup canvasGroup;
    private GameObject modalBox;
    private Text headerTagText;
    private Text titleText;
    private Text messageText;
    private Text timeText;
    private Text retryBtnText;
    private Button retryButton;
    private Button closeButton;
    private bool isShowing = false;
    private bool isUpcomingNoticeMode = false;
    public UpcomingMaintenanceInfo CurrentUpcomingInfo { get; private set; }

    public static event Action OnPopupClosed;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildMaintenancePopupCanvas();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Hiển thị Hộp thoại thông báo bảo trì từ đối tượng CurrentMaintenanceStatus
    /// </summary>
    public void Show(CurrentMaintenanceStatus status)
    {
        if (status == null) return;
        Show(status.title, status.message, status.remainingMinutes, status.endTime);
    }

    /// <summary>
    /// Hiển thị Hộp thoại thông báo bảo trì sắp tới (Upcoming Maintenance Notice - Không block chơi game)
    /// </summary>
    public void ShowUpcomingNotice(UpcomingMaintenanceInfo upcoming)
    {
        if (upcoming == null) return;
        CurrentUpcomingInfo = upcoming;
        isUpcomingNoticeMode = true;

        if (popupCanvas == null)
        {
            BuildMaintenancePopupCanvas();
        }

        if (headerTagText != null)
        {
            headerTagText.text = "[UPCOMING MAINTENANCE NOTICE]";
            headerTagText.color = new Color(0.3f, 0.85f, 1f, 1f); // Xanh Cyan sáng
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(upcoming.title) ? "Upcoming Scheduled Maintenance" : upcoming.title;
        }

        if (messageText != null)
        {
            string msg = string.IsNullOrWhiteSpace(upcoming.message)
                ? "The game server is scheduled for maintenance. Please finish your battles and save progress before the maintenance begins."
                : upcoming.message;
            messageText.text = msg;
        }

        if (timeText != null)
        {
            string timeStr = "";
            bool hasParsedStart = DateTimeOffset.TryParse(upcoming.startTime, out var parsedStart);
            bool hasParsedEnd = DateTimeOffset.TryParse(upcoming.endTime, out var parsedEnd);

            if (hasParsedStart && hasParsedEnd)
            {
                timeStr = $"Scheduled Period: {parsedStart:yyyy-MM-dd HH:mm} ~ {parsedEnd:HH:mm} (UTC+7)";
            }
            else if (hasParsedStart)
            {
                timeStr = $"Starts: {parsedStart:yyyy-MM-dd HH:mm} (UTC+7)";
            }
            else
            {
                timeStr = $"Starts: {upcoming.startTime}";
            }

            if (upcoming.hoursUntilStart > 0)
            {
                timeStr += $"\n(Starts in approximately ~{upcoming.hoursUntilStart} hours)";
            }
            else if (upcoming.minutesUntilStart > 0)
            {
                timeStr += $"\n(Starts in approximately ~{upcoming.minutesUntilStart} minutes)";
            }

            timeText.text = timeStr;
            timeText.color = new Color(1f, 0.82f, 0.25f, 1f); // Vàng cam ấm
        }

        if (retryBtnText != null)
        {
            retryBtnText.text = "Continue";
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        isShowing = true;
        if (popupCanvas != null)
        {
            popupCanvas.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hiển thị Hộp thoại thông báo bảo trì với các thông số chi tiết (Chế độ chặn máy chủ)
    /// </summary>
    public void Show(string title, string message, int remainingMinutes, string endTime = null)
    {
        isUpcomingNoticeMode = false;

        // Không hiển thị Popup bảo trì nếu người chơi hiện tại là Admin hoặc Developer
        if (NetworkManager.Instance != null && 
            (NetworkManager.Instance.AccountRole == "Developer" || NetworkManager.Instance.AccountRole == "Admin"))
        {
            Hide();
            return;
        }

        if (popupCanvas == null)
        {
            BuildMaintenancePopupCanvas();
        }

        if (headerTagText != null)
        {
            headerTagText.text = "[SERVER UNDER MAINTENANCE]";
            headerTagText.color = new Color(1f, 0.72f, 0.2f, 1f); // Cam vàng cảnh báo
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Server Under Maintenance" : title;
        }

        if (messageText != null)
        {
            messageText.text = string.IsNullOrWhiteSpace(message) 
                ? "The server is currently undergoing maintenance for system updates and optimizations. Please check back later." 
                : message;
        }

        if (timeText != null)
        {
            string timeStr = remainingMinutes > 1 
                ? $"Estimated remaining time: ~{remainingMinutes} minutes" 
                : $"Estimated remaining time: ~{remainingMinutes} minute";

            if (!string.IsNullOrWhiteSpace(endTime))
            {
                if (DateTimeOffset.TryParse(endTime, out var parsedEnd))
                {
                    timeStr += $"\nScheduled end time: {parsedEnd:yyyy-MM-dd HH:mm} (UTC+7)";
                }
                else
                {
                    timeStr += $"\nScheduled end time: {endTime}";
                }
            }
            timeText.text = timeStr;
            timeText.color = new Color(0.4f, 0.85f, 1f, 1f);
        }

        if (retryBtnText != null)
        {
            retryBtnText.text = "Retry";
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        isShowing = true;
        if (popupCanvas != null)
        {
            popupCanvas.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Đóng hộp thoại bảo trì
    /// </summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        isShowing = false;
        if (popupCanvas != null)
        {
            popupCanvas.gameObject.SetActive(false);
        }

        OnPopupClosed?.Invoke();
    }

    private void OnPrimaryButtonClicked()
    {
        if (isUpcomingNoticeMode)
        {
            Hide();
            return;
        }

        OnRetryClicked();
    }

    private void OnRetryClicked()
    {
        if (retryBtnText != null)
        {
            retryBtnText.text = "Checking...";
        }

        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.CheckMaintenanceStatus((status) =>
            {
                if (status != null && !status.isUnderMaintenance)
                {
                    // Máy chủ đã mở lại!
                    if (timeText != null)
                    {
                        timeText.text = "Server is back online! You can now enter the game.";
                        timeText.color = Color.green;
                    }

                    if (retryBtnText != null)
                    {
                        retryBtnText.text = "Online";
                    }

                    StartCoroutine(AutoHideAfterSuccess());
                }
                else if (status != null)
                {
                    // Vẫn đang bảo trì -> Cập nhật lại số phút còn lại
                    Show(status);
                    if (retryBtnText != null)
                    {
                        retryBtnText.text = "Retry";
                    }
                }
                else
                {
                    if (retryBtnText != null)
                    {
                        retryBtnText.text = "Retry";
                    }
                }
            }, showPopupIfMaintenance: false);
        }
    }

    private IEnumerator AutoHideAfterSuccess()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        Hide();
    }

    /// <summary>
    /// Tự động dựng giao diện UI Canvas mờ nền tối cao cấp bằng C# code (Không cần kéo thả Editor)
    /// </summary>
    private void BuildMaintenancePopupCanvas()
    {
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Root GameObject + Canvas Component
        GameObject canvasObj = new GameObject("MaintenancePopupCanvas");
        canvasObj.transform.SetParent(transform, false);

        popupCanvas = canvasObj.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 10000; // Sorting cao nhất để luôn nổi trên mọi màn hình

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 2. Nền che mờ (Overlay) chắn toàn màn hình
        GameObject overlayObj = new GameObject("OverlayBackground", typeof(RectTransform), typeof(Image));
        overlayObj.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.color = new Color(0.02f, 0.02f, 0.04f, 0.88f); // Đen mờ 88%

        // 3. Khung Hộp Thoại Trung Tâm (Modal Box)
        modalBox = new GameObject("ModalBox", typeof(RectTransform), typeof(Image));
        modalBox.transform.SetParent(overlayObj.transform, false);
        RectTransform modalRect = modalBox.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(850, 520); // Kích thước khung đẹp chuẩn

        Image modalImg = modalBox.GetComponent<Image>();
        modalImg.color = new Color(0.11f, 0.13f, 0.18f, 0.98f); // Màu xanh đêm Dark Slate hiện đại

        // 4. Dải Tag cảnh báo (Header Tag)
        GameObject tagObj = new GameObject("HeaderTagText", typeof(RectTransform), typeof(Text));
        tagObj.transform.SetParent(modalBox.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0.5f, 0.88f);
        tagRect.anchorMax = new Vector2(0.5f, 0.88f);
        tagRect.sizeDelta = new Vector2(750, 50);

        headerTagText = tagObj.GetComponent<Text>();
        headerTagText.font = uiFont;
        headerTagText.fontSize = 28;
        headerTagText.fontStyle = FontStyle.Bold;
        headerTagText.alignment = TextAnchor.MiddleCenter;
        headerTagText.color = new Color(1f, 0.72f, 0.2f, 1f); // Màu cam vàng cảnh báo
        headerTagText.text = "[SERVER UNDER MAINTENANCE]";

        // 5. Tiêu đề đợt bảo trì (Title)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(modalBox.transform, false);
        RectTransform titleRectObj = titleObj.GetComponent<RectTransform>();
        titleRectObj.anchorMin = new Vector2(0.5f, 0.76f);
        titleRectObj.anchorMax = new Vector2(0.5f, 0.76f);
        titleRectObj.sizeDelta = new Vector2(750, 60);

        titleText = titleObj.GetComponent<Text>();
        titleText.font = uiFont;
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "Server Under Maintenance";

        // 6. Nội dung thông báo chi tiết (Message)
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(Text));
        msgObj.transform.SetParent(modalBox.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.5f, 0.52f);
        msgRect.anchorMax = new Vector2(0.5f, 0.52f);
        msgRect.sizeDelta = new Vector2(750, 140);

        messageText = msgObj.GetComponent<Text>();
        messageText.font = uiFont;
        messageText.fontSize = 22;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = new Color(0.85f, 0.88f, 0.93f, 1f);
        messageText.lineSpacing = 1.25f;
        messageText.text = "The server is currently undergoing maintenance for system updates and optimizations. Please check back later.";

        // 7. Thông tin thời gian dự kiến (Time Info)
        GameObject timeObj = new GameObject("TimeText", typeof(RectTransform), typeof(Text));
        timeObj.transform.SetParent(modalBox.transform, false);
        RectTransform timeRect = timeObj.GetComponent<RectTransform>();
        timeRect.anchorMin = new Vector2(0.5f, 0.30f);
        timeRect.anchorMax = new Vector2(0.5f, 0.30f);
        timeRect.sizeDelta = new Vector2(750, 70);

        timeText = timeObj.GetComponent<Text>();
        timeText.font = uiFont;
        timeText.fontSize = 20;
        timeText.fontStyle = FontStyle.Italic;
        timeText.alignment = TextAnchor.MiddleCenter;
        timeText.color = new Color(0.4f, 0.85f, 1f, 1f); // Màu xanh lơ dịu mát
        timeText.text = "Estimated remaining time: ~15 minutes";

        // 8. Nút Bấm "Retry" (Retry Button)
        GameObject retryBtnObj = new GameObject("RetryButton", typeof(RectTransform), typeof(Image), typeof(Button));
        retryBtnObj.transform.SetParent(modalBox.transform, false);
        RectTransform retryRect = retryBtnObj.GetComponent<RectTransform>();
        retryRect.anchorMin = new Vector2(0.33f, 0.12f);
        retryRect.anchorMax = new Vector2(0.33f, 0.12f);
        retryRect.sizeDelta = new Vector2(220, 60);

        Image retryImg = retryBtnObj.GetComponent<Image>();
        retryImg.color = new Color(0.18f, 0.58f, 0.68f, 1f); // Nút màu Cyan đậm hiện đại

        retryButton = retryBtnObj.GetComponent<Button>();
        retryButton.onClick.AddListener(OnPrimaryButtonClicked);

        GameObject retryTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        retryTextObj.transform.SetParent(retryBtnObj.transform, false);
        RectTransform retryTextRect = retryTextObj.GetComponent<RectTransform>();
        retryTextRect.anchorMin = Vector2.zero;
        retryTextRect.anchorMax = Vector2.one;
        retryTextRect.sizeDelta = Vector2.zero;

        retryBtnText = retryTextObj.GetComponent<Text>();
        retryBtnText.font = uiFont;
        retryBtnText.fontSize = 22;
        retryBtnText.fontStyle = FontStyle.Bold;
        retryBtnText.alignment = TextAnchor.MiddleCenter;
        retryBtnText.color = Color.white;
        retryBtnText.text = "Retry";

        // 9. Nút Bấm "Close" (Close Button)
        GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnObj.transform.SetParent(modalBox.transform, false);
        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.67f, 0.12f);
        closeRect.anchorMax = new Vector2(0.67f, 0.12f);
        closeRect.sizeDelta = new Vector2(220, 60);

        Image closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.color = new Color(0.32f, 0.36f, 0.42f, 1f); // Màu xám tối sang trọng

        closeButton = closeBtnObj.GetComponent<Button>();
        closeButton.onClick.AddListener(Hide);

        GameObject closeTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTextRect = closeTextObj.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.sizeDelta = Vector2.zero;

        Text closeText = closeTextObj.GetComponent<Text>();
        closeText.font = uiFont;
        closeText.fontSize = 22;
        closeText.fontStyle = FontStyle.Bold;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.text = "Close";

        // Khởi tạo trạng thái ẩn ban đầu
        popupCanvas.gameObject.SetActive(false);
    }
}
