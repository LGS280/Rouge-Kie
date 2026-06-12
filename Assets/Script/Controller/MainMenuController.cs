using UnityEngine;
using UnityEngine.EventSystems; // Bắt buộc phải có để sử dụng EventSystem
using System.Collections;
public class MainMenuController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Tabs")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject graphicsContent;

    [Header("First Selected Objects (For Gamepad/Keyboard)")]
    [SerializeField] private GameObject playButton;      // Nút đầu tiên được chọn ở Main Menu
    [SerializeField] private GameObject singleButton;    // Nút đầu tiên được chọn ở Play Menu
    [SerializeField] private GameObject tabAudioBtn;     // Nút đầu tiên được chọn ở Settings Panel

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

        // Kích hoạt tiêu điểm vào nút Play khi ở Main Menu
        SetSelected(playButton);
    }

    public void OnPlayButtonPressed()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        // Kích hoạt tiêu điểm vào nút Single khi vào Play Menu
        SetSelected(singleButton);
    }

    public void OnSettingsButtonPressed()
    {
        settingsPanel.SetActive(true);
        OnAudioTabPressed();

        // Kích hoạt tiêu điểm vào nút Tab Audio khi mở bảng Settings
        SetSelected(tabAudioBtn);
    }

    public void OnCloseSettingsPressed()
    {
        settingsPanel.SetActive(false);

        // Khi đóng Settings, trả tiêu điểm về nút Play ở Main Menu
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
    }

    public void OnGraphicsTabPressed()
    {
        audioContent.SetActive(false);
        graphicsContent.SetActive(true);
    }

    // --- PHẦN KẾT NỐI GAMEPLAY ---

    public void OnSingleplayerPressed()
    {
        Debug.Log("Chạy chế độ chơi đơn...");
    }

    public void OnCoOpPressed()
    {
        Debug.Log("Chạy chế độ Multiplayer Co-op...");
    }

    // Hàm phụ trợ giúp chọn nút an toàn cho tay cầm, tránh lỗi NullReferenceException
    private void SetSelected(GameObject obj)
    {
        if (obj != null && EventSystem.current != null)
        {
            // Dừng các tiến trình chờ cũ đang chạy để tránh xung đột
            StopAllCoroutines();
            // Chạy tiến trình chờ 1 khung hình rồi mới chọn nút
            StartCoroutine(SelectButtonDelayed(obj));
        }
    }

    // Tiến trình chờ Canvas cập nhật hoàn tất trước khi chọn nút
    private IEnumerator SelectButtonDelayed(GameObject obj)
    {
        // Xóa tiêu điểm cũ để tránh lỗi bám dính tiêu điểm
        EventSystem.current.SetSelectedGameObject(null);

        // Chờ đúng 1 khung hình (để Panel mới kịp kích hoạt hoàn toàn trên màn hình)
        yield return null;

        // Tiến hành chọn nút mới
        EventSystem.current.SetSelectedGameObject(obj);
    }

}