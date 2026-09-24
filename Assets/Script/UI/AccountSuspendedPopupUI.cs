using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the real-time Account Suspended / Banned notification dialog.
/// Automatically builds an in-game cyber-themed popup canvas via C# code (DontDestroyOnLoad).
/// All text and notifications are rendered in English.
/// </summary>
public class AccountSuspendedPopupUI : MonoBehaviour
{
    private static AccountSuspendedPopupUI _instance;
    public static AccountSuspendedPopupUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AccountSuspendedPopupUI>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("AccountSuspendedPopupUI");
                    _instance = obj.AddComponent<AccountSuspendedPopupUI>();
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
    private Button acknowledgeButton;
    private Text buttonText;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildPopupCanvas();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Displays the Account Suspended dialog with a custom or default English message.
    /// </summary>
    public void Show(string message = null)
    {
        if (popupCanvas == null)
        {
            BuildPopupCanvas();
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Your account has been suspended by an Administrator.\nYou have been disconnected from the session.";
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (popupCanvas != null)
        {
            popupCanvas.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Closes the popup dialog and ensures the player returns to the Main Menu.
    /// </summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (popupCanvas != null)
        {
            popupCanvas.gameObject.SetActive(false);
        }

        if (ApiClient.Instance != null)
        {
            ApiClient.Instance.ReturnToMainMenu();
        }
    }

    private void BuildPopupCanvas()
    {
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Root GameObject + Canvas Component
        GameObject canvasObj = new GameObject("AccountSuspendedCanvas");
        canvasObj.transform.SetParent(transform, false);

        popupCanvas = canvasObj.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 10005; // Placed above all other standard overlays

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 2. Dark semi-transparent background overlay
        GameObject overlayObj = new GameObject("OverlayBackground", typeof(RectTransform), typeof(Image));
        overlayObj.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        Image overlayImg = overlayObj.GetComponent<Image>();
        overlayImg.color = new Color(0.04f, 0.02f, 0.03f, 0.92f); // Deep dimming background

        // 3. Central Modal Box
        modalBox = new GameObject("ModalBox", typeof(RectTransform), typeof(Image));
        modalBox.transform.SetParent(overlayObj.transform, false);
        RectTransform modalRect = modalBox.GetComponent<RectTransform>();
        modalRect.anchorMin = new Vector2(0.5f, 0.5f);
        modalRect.anchorMax = new Vector2(0.5f, 0.5f);
        modalRect.sizeDelta = new Vector2(860, 430);

        Image modalImg = modalBox.GetComponent<Image>();
        modalImg.color = new Color(0.12f, 0.05f, 0.07f, 0.98f); // Dark red/crimson futuristic slate

        // 4. Header Tag
        GameObject tagObj = new GameObject("HeaderTagText", typeof(RectTransform), typeof(Text));
        tagObj.transform.SetParent(modalBox.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0.5f, 0.86f);
        tagRect.anchorMax = new Vector2(0.5f, 0.86f);
        tagRect.sizeDelta = new Vector2(760, 45);

        headerTagText = tagObj.GetComponent<Text>();
        headerTagText.font = uiFont;
        headerTagText.fontSize = 26;
        headerTagText.fontStyle = FontStyle.Bold;
        headerTagText.alignment = TextAnchor.MiddleCenter;
        headerTagText.color = new Color(0.95f, 0.25f, 0.35f, 1f); // Vibrant Crimson Alert
        headerTagText.text = "[SECURITY OVERRIDE - ACCOUNT SUSPENDED]";

        // 5. Title
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(modalBox.transform, false);
        RectTransform titleRectObj = titleObj.GetComponent<RectTransform>();
        titleRectObj.anchorMin = new Vector2(0.5f, 0.68f);
        titleRectObj.anchorMax = new Vector2(0.5f, 0.68f);
        titleRectObj.sizeDelta = new Vector2(760, 50);

        titleText = titleObj.GetComponent<Text>();
        titleText.font = uiFont;
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "Access Revoked";

        // 6. Message Body
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(Text));
        msgObj.transform.SetParent(modalBox.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.5f, 0.44f);
        msgRect.anchorMax = new Vector2(0.5f, 0.44f);
        msgRect.sizeDelta = new Vector2(760, 90);

        messageText = msgObj.GetComponent<Text>();
        messageText.font = uiFont;
        messageText.fontSize = 22;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = new Color(0.9f, 0.85f, 0.88f, 1f);
        messageText.text = "Your account has been suspended by an Administrator.\nYou have been disconnected from the session.";

        // 7. Acknowledge Button
        GameObject ackBtnObj = new GameObject("AcknowledgeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        ackBtnObj.transform.SetParent(modalBox.transform, false);
        RectTransform ackRect = ackBtnObj.GetComponent<RectTransform>();
        ackRect.anchorMin = new Vector2(0.5f, 0.18f);
        ackRect.anchorMax = new Vector2(0.5f, 0.18f);
        ackRect.sizeDelta = new Vector2(260, 56);

        Image ackImg = ackBtnObj.GetComponent<Image>();
        ackImg.color = new Color(0.85f, 0.2f, 0.28f, 1f); // Red accent button

        acknowledgeButton = ackBtnObj.GetComponent<Button>();
        acknowledgeButton.onClick.AddListener(Hide);

        GameObject ackTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        ackTextObj.transform.SetParent(ackBtnObj.transform, false);
        RectTransform ackTextRect = ackTextObj.GetComponent<RectTransform>();
        ackTextRect.anchorMin = Vector2.zero;
        ackTextRect.anchorMax = Vector2.one;
        ackTextRect.sizeDelta = Vector2.zero;

        buttonText = ackTextObj.GetComponent<Text>();
        buttonText.font = uiFont;
        buttonText.fontSize = 20;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.text = "Acknowledge";

        popupCanvas.gameObject.SetActive(false);
    }
}
