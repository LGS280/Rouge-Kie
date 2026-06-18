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