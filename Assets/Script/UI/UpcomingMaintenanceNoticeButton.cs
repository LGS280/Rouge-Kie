using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Nút icon chấm than màu vàng hiển thị ở góc trên bên phải (kế bên nút Logout) trong Scene Menu
/// khi có lịch bảo trì sắp diễn ra.
/// Khi người chơi bấm vào nút, sẽ mở lại Hộp thoại thông báo lịch bảo trì (Upcoming Notice).
/// </summary>
public class UpcomingMaintenanceNoticeButton : MonoBehaviour
{
    private static UpcomingMaintenanceNoticeButton instance;

    private GameObject buttonObj;
    private RectTransform buttonRect;
    private Button buttonComponent;
    private Text iconText;

    private Transform lastParentTransform;
    private float checkInterval = 0.5f;
    private float lastCheckTime = 0f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        MaintenanceManager.OnMaintenanceChecked += OnMaintenanceChecked;
        MaintenancePopupUI.OnPopupClosed += OnPopupClosed;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MaintenanceManager.OnMaintenanceChecked -= OnMaintenanceChecked;
        MaintenancePopupUI.OnPopupClosed -= OnPopupClosed;
    }

    private void Start()
    {
        RefreshVisibility();
    }

    private void Update()
    {
        // Hiệu ứng nhịp đập (Pulsing) nhẹ nhàng để thu hút sự chú ý vào cảnh báo
        if (buttonObj != null && buttonObj.activeSelf)
        {
            float scale = 1.0f + 0.08f * Mathf.Sin(Time.unscaledTime * 4.5f);
            buttonObj.transform.localScale = new Vector3(scale, scale, 1f);

            // Định kỳ kiểm tra vị trí để luôn bám sát nút Logout nếu người chơi Login/Logout
            if (Time.unscaledTime - lastCheckTime > checkInterval)
            {
                lastCheckTime = Time.unscaledTime;
                EnsureCorrectPosition();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshVisibility();
    }

    private void OnMaintenanceChecked(CurrentMaintenanceStatus status)
    {
        RefreshVisibility();
    }

    private void OnPopupClosed()
    {
        RefreshVisibility();
    }

    private bool IsMenuScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Equals("Scene_Menu", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("Menu");
    }

    /// <summary>
    /// Cập nhật trạng thái ẩn/hiện của nút icon chấm than vàng
    /// </summary>
    public void RefreshVisibility()
    {
        // Chỉ hiển thị trong Scene Menu
        if (!IsMenuScene())
        {
            if (buttonObj != null) buttonObj.SetActive(false);
            return;
        }

        if (MaintenanceManager.Instance == null) return;

        bool hasUpcoming = MaintenanceManager.Instance.HasUpcomingMaintenance;
        bool isUnderMaintenance = MaintenanceManager.Instance.IsUnderMaintenance;

        // Chỉ hiện khi có lịch sắp diễn ra và máy chủ chưa bước vào bảo trì thực tế
        bool shouldShow = hasUpcoming && !isUnderMaintenance;

        if (shouldShow)
        {
            EnsureButtonCreated();
            EnsureCorrectPosition();
            if (buttonObj != null) buttonObj.SetActive(true);
        }
        else
        {
            if (buttonObj != null) buttonObj.SetActive(false);
        }
    }

    private void EnsureButtonCreated()
    {
        if (buttonObj != null) return;

        // 1. Tạo GameObject nút
        buttonObj = new GameObject("Button_UpcomingMaintenanceNotice", typeof(RectTransform), typeof(Image), typeof(Button));

        buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(42, 42);

        // 2. Cấu hình hình ảnh nút nền vàng hổ phách cảnh báo (#FFBA08 / #FFC726)
        Image bgImg = buttonObj.GetComponent<Image>();
        bgImg.color = new Color(1f, 0.78f, 0.12f, 1f); // Màu vàng cảnh báo tươi sáng

        // Thêm viền tối bao quanh nút
        Outline outline = buttonObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.15f, 0.05f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Cấu hình Button
        buttonComponent = buttonObj.GetComponent<Button>();
        buttonComponent.onClick.AddListener(OnNoticeButtonClicked);

        // 4. Tạo icon chữ chấm than [ ! ]
        GameObject textObj = new GameObject("ExclamationText", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        iconText = textObj.GetComponent<Text>();
        iconText.font = uiFont;
        iconText.fontSize = 28;
        iconText.fontStyle = FontStyle.Bold;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = new Color(0.12f, 0.12f, 0.15f, 1f); // Màu đen tím sẫm tương phản cao với nền vàng
        iconText.text = "!";

        // Thêm LayoutElement để khi nằm trong HorizontalLayoutGroup sẽ chiếm đúng kích thước
        LayoutElement le = buttonObj.AddComponent<LayoutElement>();
        le.minWidth = 42;
        le.minHeight = 42;
        le.preferredWidth = 42;
        le.preferredHeight = 42;
    }

    /// <summary>
    /// Đảm bảo nút được neo đúng vị trí: Kế bên nút Logout ở góc trên bên phải
    /// </summary>
    private void EnsureCorrectPosition()
    {
        if (buttonObj == null) return;

        // Tìm nút Logout trong scene
        GameObject logoutBtn = GameObject.Find("Button_Logout ");
        if (logoutBtn == null) logoutBtn = GameObject.Find("Button_Logout");

        if (logoutBtn != null && logoutBtn.transform.parent != null && logoutBtn.activeInHierarchy)
        {
            Transform parentRow = logoutBtn.transform.parent;

            if (buttonObj.transform.parent != parentRow)
            {
                buttonObj.transform.SetParent(parentRow, false);
                // Đặt vị trí ngay trước nút Logout để nằm cạnh bên trái của Logout
                int logoutIndex = logoutBtn.transform.GetSiblingIndex();
                buttonObj.transform.SetSiblingIndex(logoutIndex);
            }
        }
        else
        {
            // Fallback nếu nút Logout đang bị ẩn (chưa đăng nhập): Neo trực tiếp ở góc trên bên phải của Canvas Menu
            Canvas menuCanvas = FindFirstObjectByType<Canvas>();
            if (menuCanvas != null && buttonObj.transform.parent != menuCanvas.transform)
            {
                buttonObj.transform.SetParent(menuCanvas.transform, false);

                buttonRect.anchorMin = new Vector2(1f, 1f);
                buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(1f, 1f);
                buttonRect.anchoredPosition = new Vector2(-25f, -25f);
            }
        }
    }

    private void OnNoticeButtonClicked()
    {
        if (MaintenanceManager.Instance != null && MaintenanceManager.Instance.CurrentStatus != null)
        {
            if (MaintenanceManager.Instance.CurrentStatus.upcomingMaintenance != null)
            {
                MaintenancePopupUI.Instance.ShowUpcomingNotice(MaintenanceManager.Instance.CurrentStatus.upcomingMaintenance);
            }
        }
    }
}
