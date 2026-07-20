using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
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

    [Header("First Selected Objects")]
    [SerializeField] private GameObject firstSettingOption;
    [SerializeField] private GameObject firstControlsOption;

    private void OnEnable()
    {
        // Mặc định luôn mở tab Audio khi bảng Settings được Active
        OnAudioTabPressed();
    }

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
        // Chỉ chạy logic điều hướng phím tắt khi panel cài đặt đang thực sự hiển thị và EventSystem hoạt động
        int currentTab = 0; // 0: Audio, 1: Graphics, 2: Controls
        if (graphicsContent.activeSelf) currentTab = 1;
        else if (controlsContent.activeSelf) currentTab = 2;

        // Chuyển đổi tab Settings bằng phím Tab hoặc Q/E (Keyboard) hoặc L1/R1 (Gamepad)
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton5))
        {
            int nextTab = (currentTab + 1) % 3;
            SwitchToTab(nextTab);
        }
        else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton4))
        {
            int nextTab = (currentTab - 1 + 3) % 3;
            SwitchToTab(nextTab);
        }

        // Phòng ngừa mất tiêu điểm (selection) khi dùng bàn phím/tay cầm
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            if (currentTab == 0) SetSelected(firstSettingOption);
            else if (currentTab == 1)
            {
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
            else if (currentTab == 2) SetSelected(firstControlsOption);
        }
    }

    private void SwitchToTab(int index)
    {
        if (index == 0) OnAudioTabPressed();
        else if (index == 1) OnGraphicsTabPressed();
        else if (index == 2) OnControlsTabPressed();
    }

    private void SetSelected(GameObject obj)
    {
        // KIỂM TRA AN TOÀN: Chỉ khởi chạy Coroutine khi GameObject này thực sự Active trong Scene
        if (gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(SelectButtonDelayed(obj));
        }
        else
        {
            // Nếu chưa hoàn toàn active (đang trong nhịp Enable), gán trực tiếp để tránh báo lỗi Console
            EventSystem.current.SetSelectedGameObject(obj);
        }
    }

    private IEnumerator SelectButtonDelayed(GameObject obj)
    {
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        EventSystem.current.SetSelectedGameObject(obj);
    }
}