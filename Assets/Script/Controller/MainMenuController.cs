using UnityEngine;
using UnityEngine.SceneManagement; // Dùng để chuyển sang Scene chơi game sau này

public class MainMenuController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Tabs")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject graphicsContent;

    private void Start()
    {
        // Khi bắt đầu chạy game, đảm bảo chỉ có Main Menu được hiện
        ShowMainMenu();
    }

    // --- LOGIC CHUYỂN ĐỔI GIỮA CÁC PANEL CHÍNH ---

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    public void OnPlayButtonPressed()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    public void OnSettingsButtonPressed()
    {
        // Settings Panel sẽ bật đè lên màn hình hiện tại
        settingsPanel.SetActive(true);

        // Mặc định khi mở Settings sẽ hiện Tab Audio trước
        OnAudioTabPressed();
    }

    public void OnCloseSettingsPressed()
    {
        settingsPanel.SetActive(false);
    }

    public void OnBackButtonPressed()
    {
        // Quay lại Main Menu từ màn hình chọn chế độ chơi
        ShowMainMenu();
    }

    public void OnQuitButtonPressed()
    {
        Debug.Log("Thoát Game!");
        Application.Quit();

        // Nếu đang chạy thử trong Unity Editor thì dừng chế độ Play
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- LOGIC CHUYỂN TAB TRONG BẢNG SETTINGS ---

    public void OnAudioTabPressed()
    {
        audioContent.SetActive(true);
        graphicsContent.SetActive(false);
    }

    public void OnGraphicsTabPressed()
    {
        audioContent.SetActive(false);
        graphicsContent.SetActive(true);
    }

    // --- PHẦN KẾT NỐI GAMEPLAY (SẼ PHÁT TRIỂN TIẾP) ---

    public void OnSingleplayerPressed()
    {
        Debug.Log("Chạy chế độ chơi đơn...");
        // Sau này load scene chơi đơn tại đây:
        // SceneManager.LoadScene("Gameplay_Scene_Name");
    }

    public void OnCoOpPressed()
    {
        Debug.Log("Chạy chế độ Multiplayer Co-op...");
        // Sau này sẽ kích hoạt UI nhập mã phòng và kết nối SignalR tại đây
    }
}