using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        ShowMainMenu();
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

    public void OnPlayButtonPressed()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        SetSelected(singleButton);
    }

    public void OnSingleplayerPressed()
    {
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
            LoadingScreenUI.Instance.ShowLoading("SẢNH CHỜ", "Đang di chuyển tới Sảnh Chờ...");
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