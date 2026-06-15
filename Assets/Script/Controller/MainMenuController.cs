using UnityEngine;
using UnityEngine.EventSystems; // Sử dụng EventSystem cho tay cầm/bàn phím
using System.Collections;
using UnityEngine.UI; // Yêu cầu có để sử dụng cấu phần Image và Button

public class MainMenuController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Tabs")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject graphicsContent;

    [Header("Tab Sprites")]
    [SerializeField] private Sprite activeTabSprite;    // Sprite tab đang chọn (sáng, không viền dưới)
    [SerializeField] private Sprite inactiveTabSprite;  // Sprite tab chưa chọn (tối, có viền dưới)

    [Header("Tab UI Components")]
    [SerializeField] private Image audioTabImage;
    [SerializeField] private Image graphicsTabImage;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button graphicsTabButton;

    [Header("First Selected Objects (For Gamepad/Keyboard)")]
    [SerializeField] private GameObject playButton;          // Nút đầu tiên ở Main Menu
    [SerializeField] private GameObject singleButton;        // Nút đầu tiên ở Play Menu
    [SerializeField] private GameObject firstSettingOption;  // Chọn Slider Master đầu tiên thay vì chọn Tab (do Tab đang active đã bị khóa click)

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

        // Luôn mở mặc định Tab Audio khi mở bảng Settings
        OnAudioTabPressed();
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

        // Hoán đổi sprite hiển thị thủ công để giữ nguyên trạng thái
        audioTabImage.sprite = activeTabSprite;
        graphicsTabImage.sprite = inactiveTabSprite;

        // Khóa không cho click lại vào Tab đang mở (giữ nguyên trạng thái)
        audioTabButton.interactable = false;
        graphicsTabButton.interactable = true;

        // Chuyển tiêu điểm tay cầm vào Option đầu tiên (ví dụ: Slider Master)
        SetSelected(firstSettingOption);
    }

    public void OnGraphicsTabPressed()
    {
        audioContent.SetActive(false);
        graphicsContent.SetActive(true);

        // Hoán đổi sprite hiển thị thủ công để giữ nguyên trạng thái
        audioTabImage.sprite = inactiveTabSprite;
        graphicsTabImage.sprite = activeTabSprite;

        // Khóa không cho click lại vào Tab đang mở (giữ nguyên trạng thái)
        audioTabButton.interactable = true;
        graphicsTabButton.interactable = false;

        // Khi chuyển sang Tab Graphics, chọn phần tử đầu tiên của tab Graphics (ví dụ: Toggle Fullscreen)
        Transform firstGraphicsOption = graphicsContent.transform.GetChild(0);
        if (firstGraphicsOption != null)
        {
            // Tìm nút Toggle con bên trong Row đầu tiên của Graphics_Content
            Transform toggleObj = firstGraphicsOption.Find("Toggle_Fullscreen");
            if (toggleObj != null)
                SetSelected(toggleObj.gameObject);
            else
                SetSelected(firstGraphicsOption.gameObject);
        }
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

    // Hàm phụ trợ giúp chọn nút an toàn cho tay cầm
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