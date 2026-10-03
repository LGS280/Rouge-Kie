using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Canvas Group (Dùng để khóa tương tác phía sau)")]
    // THÊM MỚI: Quản lý tính chất tương tác của Menu chính
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;

    [Header("Settings Panels Content")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject graphicsContent;
    [SerializeField] private GameObject controlsContent;

    [Header("Tab Sprites")]
    [SerializeField] private Sprite activeTabSprite;
    [SerializeField] private Sprite inactiveTabSprite;

    [Header("Tab UI Images")]
    [SerializeField] private Image audioTabImage;
    [SerializeField] private Image graphicsTabImage;
    [SerializeField] private Image controlsTabImage;

    [Header("Tab UI Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button graphicsTabButton;
    [SerializeField] private Button controlsTabButton;

    [Header("First Selected Objects (For Gamepad/Keyboard)")]
    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject singleButton;
    [SerializeField] private GameObject firstSettingOption;
    [SerializeField] private GameObject firstControlsOption;

    private void Start()
    {
        // Nếu người chơi mở game lần đầu tiên (kể cả khi chạy trực tiếp từ Scene_Menu), chuyển sang Scene_Cutscene
        if (PlayerPrefs.GetInt(CutsceneManager.CUTSCENE_SEEN_KEY, 0) == 0)
        {
            SceneManager.LoadScene("Scene_Cutscene");
            return;
        }

        // Áp dụng ngay toàn bộ cấu hình âm thanh & đồ họa đã lưu khi vào Menu chính
        SettingsController.ApplyAllSavedSettings();

        EnsureRewatchCutsceneButton();
        ShowMainMenu();

        // Tự động kiểm tra trạng thái bảo trì hệ thống ngay khi vào game
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.CheckMaintenanceStatus(null, showPopupIfMaintenance: true);
        }

        // Tự động gắn bộ quản lý nút icon thông báo lịch bảo trì sắp tới (chấm than vàng kế bên nút Logout)
        if (GetComponent<UpcomingMaintenanceNoticeButton>() == null)
        {
            gameObject.AddComponent<UpcomingMaintenanceNoticeButton>();
        }
    }

    // --- LOGIC CHUYỂN ĐỔI GIỮA CÁC PANEL CHÍNH ---

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);

        // Mở khóa tương tác cho Menu chính khi quay lại màn hình chính
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
            mainMenuCanvasGroup.alpha = 1f; // Trả về độ sáng 100%
        }

        SetSelected(playButton);
    }

    /// <summary>
    /// Fetch lại toàn bộ API từ Backend khi bấm nút Play (kể cả chưa login hoặc đã login)
    /// </summary>
    public void FetchAllApis()
    {
        Debug.Log("[MainMenuController] Bắt đầu fetch lại toàn bộ API từ Backend...");

        // 1. Cấu hình game (súng, đạn, quái, nhân vật, màn chơi, buff, bảo trì...)
        if (GameConfigManager.Instance != null)
        {
            GameConfigManager.Instance.ReloadConfigs();
        }

        // 2. Trạng thái bảo trì máy chủ
        if (MaintenanceManager.Instance != null)
        {
            MaintenanceManager.Instance.CheckMaintenanceStatus(null, showPopupIfMaintenance: true);
        }

        // 3. Vật phẩm Shop & Mô tả nhân vật
        if (ShopUIController.Instance != null)
        {
            ShopUIController.Instance.FetchShopItems();
        }
        if (CharacterSelectUIController.Instance != null)
        {
            CharacterSelectUIController.Instance.FetchCharacterDescriptions();
        }

        // 4. Nếu đã đăng nhập, nạp lại toàn bộ dữ liệu tài khoản từ máy chủ
        bool loggedIn = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn;
        if (loggedIn)
        {
            PlayerProfileUI.Instance?.RefreshProfile();
            WeaponVaultUIController.Instance?.FetchUnlockedWeapons();
            ShopUIController.SyncUnlockedCharactersFromServer();
        }
    }

    public void OnPlayButtonPressed()
    {
        // Luôn fetch lại toàn bộ API từ Backend (kể cả chưa login hoặc đã login)
        FetchAllApis();

        // Kiểm tra xem người chơi đã đăng nhập hay chưa ngay khi bấm nút Play
        bool loggedIn = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn;
        if (!loggedIn)
        {
            Debug.Log("[MainMenuController] Chưa đăng nhập! Mở Scene Login/Register khi bấm nút Play...");
            LoginController.PendingActionAfterLogin = "PLAY_MENU";

            if (!SceneManager.GetSceneByName("LoginScrene").isLoaded)
            {
                SceneManager.LoadScene("LoginScrene", LoadSceneMode.Additive);
            }
            return;
        }

        OpenPlayMenu();
    }

    public void OpenPlayMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        SetSelected(singleButton);
    }

    public void OnSingleplayerPressed()
    {
        // Luôn fetch lại toàn bộ API trước khi vào chơi đơn
        FetchAllApis();

        // Chặn vào chơi nếu máy chủ đang bảo trì (ngoại trừ Developer và Admin)
        if (MaintenanceManager.Instance != null && MaintenanceManager.Instance.IsUnderMaintenance)
        {
            string role = NetworkManager.Instance != null ? NetworkManager.Instance.AccountRole : "Guest";
            if (role != "Developer" && role != "Admin")
            {
                if (MaintenancePopupUI.Instance != null && MaintenanceManager.Instance.CurrentStatus != null)
                {
                    MaintenancePopupUI.Instance.Show(MaintenanceManager.Instance.CurrentStatus);
                }
                return;
            }
        }

        // Kiểm tra xem người chơi đã đăng nhập hay chưa (giống như chế độ Co-op)
        bool loggedIn = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn;
        if (!loggedIn)
        {
            Debug.Log("[MainMenuController] Chưa đăng nhập! Đang gọi Scene Login/Register...");
            LoginController.PendingActionAfterLogin = "SINGLEPLAYER";

            if (!SceneManager.GetSceneByName("LoginScrene").isLoaded)
            {
                SceneManager.LoadScene("LoginScrene", LoadSceneMode.Additive);
            }
            return;
        }

        ProceedToSingleplayer();
    }

    public void ProceedToSingleplayer()
    {
        Debug.Log("Chạy chế độ chơi đơn...");

        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.ShowLoading("MAIN LOBBY", "Transitioning to Main Lobby...");
        }

        // Lệnh chuyển sang Sảnh Chờ (Lobby) trước khi vào trận đấu
        SceneManager.LoadScene("Lobby_Scene");
    }


    public void OnSettingsButtonPressed()
    {
        mainMenuCanvasGroup.interactable = false;
        mainMenuCanvasGroup.blocksRaycasts = false;
        mainMenuCanvasGroup.alpha = 0.5f;

        settingsPanel.SetActive(true); // Bảng Settings tự kích hoạt OnEnable và tự chuyển tab
    }


    public void OnCloseSettingsPressed()
    {
        settingsPanel.SetActive(false);

        // MỞ KHÓA tương tác lại cho Menu chính khi đóng Settings
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
            mainMenuCanvasGroup.alpha = 1f; // Trả về độ sáng 100%
        }

        SetSelected(playButton);
    }

    public void OnBackButtonPressed()
    {
        ShowMainMenu();
    }

    public void OnQuitButtonPressed()
    {
        Debug.Log("Thoát Game!");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnRewatchCutscenePressed()
    {
        Debug.Log("[MainMenuController] Xem lại Cutscene...");
        CutsceneManager.IsRewatching = true;
        SceneManager.LoadScene("Scene_Cutscene");
    }

    private void EnsureRewatchCutsceneButton()
    {
        if (mainMenuPanel == null) return;

        Transform container = null;
        if (playButton != null && playButton.transform.parent != null)
        {
            container = playButton.transform.parent;
        }
        else
        {
            container = mainMenuPanel.transform.Find("Button_Container ");
            if (container == null) container = mainMenuPanel.transform.Find("Button_Container");
        }

        if (container == null) return;

        Transform existing = container.Find("Rewatch_Button");
        if (existing == null) existing = container.Find("Rewatch_Button ");

        Button rewatchBtn = null;
        if (existing != null)
        {
            rewatchBtn = existing.GetComponent<Button>();
        }
        else
        {
            // Clone từ Settings_Button hoặc Play_Button để giữ nguyên toàn bộ style pixel art & UIButtonJuice
            Transform template = container.Find("Settings_Button ");
            if (template == null) template = container.Find("Settings_Button");
            if (template == null && playButton != null) template = playButton.transform;

            if (template != null)
            {
                GameObject clone = Instantiate(template.gameObject, container);
                clone.name = "Rewatch_Button";

                // Đặt vị trí nằm trước nút Quit_Button
                Transform quitTrans = container.Find("Quit_Button ");
                if (quitTrans == null) quitTrans = container.Find("Quit_Button");
                if (quitTrans != null)
                {
                    clone.transform.SetSiblingIndex(quitTrans.GetSiblingIndex());
                }

                RectTransform rt = clone.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(210f, 80f);
                }

                TextMeshProUGUI tmp = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    tmp.text = "Rewatch Cutscene";
                    tmp.fontSize = 18f;
                    tmp.enableAutoSizing = false;
                }

                rewatchBtn = clone.GetComponent<Button>();
            }
        }

        if (rewatchBtn != null)
        {
            // Xóa các persistent listener được clone từ nút mẫu bằng cách gán lại sự kiện runtime
            rewatchBtn.onClick = new Button.ButtonClickedEvent();
            rewatchBtn.onClick.AddListener(OnRewatchCutscenePressed);
        }

        // Căn chỉnh lại HorizontalLayoutGroup để 4 nút cân đối đẹp mắt ở giữa màn hình
        HorizontalLayoutGroup hlg = container.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.spacing = 55f;
        }
        RectTransform containerRt = container.GetComponent<RectTransform>();
        if (containerRt != null && Mathf.Abs(containerRt.anchoredPosition.x - (-300f)) < 5f)
        {
            containerRt.anchoredPosition = new Vector2(-415f, containerRt.anchoredPosition.y);
        }
    }

    // --- LOGIC CHUYỂN TAB TRONG BẢNG SETTINGS ---

    public void OnAudioTabPressed()
    {
        audioContent.SetActive(true);
        graphicsContent.SetActive(false);
        controlsContent.SetActive(false);

        audioTabImage.sprite = activeTabSprite;
        graphicsTabImage.sprite = inactiveTabSprite;
        controlsTabImage.sprite = inactiveTabSprite;

        audioTabButton.interactable = false;
        graphicsTabButton.interactable = true;
        controlsTabButton.interactable = true;

        SetSelected(firstSettingOption);
    }

    public void OnGraphicsTabPressed()
    {
        audioContent.SetActive(false);
        graphicsContent.SetActive(true);
        controlsContent.SetActive(false);

        audioTabImage.sprite = inactiveTabSprite;
        graphicsTabImage.sprite = activeTabSprite;
        controlsTabImage.sprite = inactiveTabSprite;

        audioTabButton.interactable = true;
        graphicsTabButton.interactable = false;
        controlsTabButton.interactable = true;

        Transform firstGraphicsOption = graphicsContent.transform.GetChild(0);
        if (firstGraphicsOption != null)
        {
            Transform toggleObj = firstGraphicsOption.Find("Toggle_Fullscreen");
            if (toggleObj != null)
                SetSelected(toggleObj.gameObject);
            else
                SetSelected(firstGraphicsOption.gameObject);
        }
    }

    public void OnControlsTabPressed()
    {
        audioContent.SetActive(false);
        graphicsContent.SetActive(false);
        controlsContent.SetActive(true);

        audioTabImage.sprite = inactiveTabSprite;
        graphicsTabImage.sprite = inactiveTabSprite;
        controlsTabImage.sprite = activeTabSprite;

        audioTabButton.interactable = true;
        graphicsTabButton.interactable = true;
        controlsTabButton.interactable = false;

        SetSelected(firstControlsOption);
    }

    private void Update()
    {
        // 1. Phím ESC / Nút Cancel (Gamepad) để điều hướng đóng/mở panel
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
        {
            if (settingsPanel.activeSelf)
            {
                OnCloseSettingsPressed();
            }
            else if (LeaderboardUI.Instance != null && LeaderboardUI.Instance.IsOpen)
            {
                LeaderboardUI.Instance.HideLeaderboard();
                SetSelected(playButton);
            }
            else if (playMenuPanel.activeSelf)
            {
                OnBackButtonPressed();
            }
            else if (mainMenuPanel.activeSelf)
            {
                OnSettingsButtonPressed();
            }
        }

        // 2. Phím tắt mở Leaderboards từ sảnh chính (Phím L hoặc nút Y trên tay cầm Gamepad)
        if (mainMenuPanel.activeSelf && !settingsPanel.activeSelf && !playMenuPanel.activeSelf)
        {
            bool isLeaderboardOpen = LeaderboardUI.Instance != null && LeaderboardUI.Instance.IsOpen;
            if (!isLeaderboardOpen)
            {
                if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton3))
                {
                    LeaderboardUI.Instance?.ShowLeaderboard();
                }

                // 3. Phím tắt Đăng xuất nhanh (Phím O hoặc nút Select/Share trên Gamepad)
                bool loggedIn = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn;
                if (loggedIn && (Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.JoystickButton6)))
                {
                    ApiClient.Instance?.Logout();
                }
            }
        }
    }

    private void SetSelected(GameObject obj)
    {
        if (obj != null && EventSystem.current != null)
        {
            StopAllCoroutines();
            StartCoroutine(SelectButtonDelayed(obj));
        }
    }

    private IEnumerator SelectButtonDelayed(GameObject obj)
    {
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        EventSystem.current.SetSelectedGameObject(obj);
    }
}