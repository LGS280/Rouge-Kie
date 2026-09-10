using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý Màn hình Chờ Tải Màn (Loading Screen UI) tự động khởi tạo (DontDestroyOnLoad)
/// Hiển thị hiệu ứng mờ mượt khi bắt đầu trận đấu từ Menu và khi chuyển tầng (Floor 1 -> 5).
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    private static LoadingScreenUI _instance;
    public static LoadingScreenUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<LoadingScreenUI>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("LoadingScreenUI");
                    _instance = obj.AddComponent<LoadingScreenUI>();
                }
            }
            return _instance;
        }
    }

    private Canvas loadingCanvas;
    private CanvasGroup canvasGroup;
    private Text titleText;
    private Text subText;
    private Text tipText;
    private RectTransform spinnerTransform;
    private bool isShowing = false;
    private Coroutine fadeCoroutine;
    private Coroutine safetyTimeoutCoroutine;

    private readonly string[] gameTips = new string[]
    {
        "Mẹo: Hãy di chuyển vòng quanh các vật cản để né đạn của quái vật!",
        "Mẹo: Vũ khí chất lượng cao hơn sẽ tiêu tốn nhiều Mana hơn cho mỗi phát bắn.",
        "Mẹo: Tiêu diệt Miniboss ở cuối mỗi tầng để nhận Rương Thưởng và mở Cổng Dịch Chuyển.",
        "Mẹo: Bấm Q hoặc lăn chuột để đổi linh hoạt giữa súng chính và súng phụ.",
        "Mẹo: Chọn Buff phù hợp sau mỗi tầng sẽ giúp bạn sống sót lâu hơn ở các tầng cao!"
    };

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildLoadingScreenCanvas();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Tự động tắt màn hình chờ mượt mà khi vào Sảnh Chờ (Lobby_Scene)
        if (scene.name == "Lobby_Scene" && isShowing)
        {
            StartCoroutine(AutoHideLobbyLoadingRoutine());
        }
    }

    private IEnumerator AutoHideLobbyLoadingRoutine()
    {
        // Chờ 0.5 giây để Scene nạp mượt mà, sau đó làm mờ dần trong 0.4s
        yield return new WaitForSecondsRealtime(0.5f);
        HideLoading(0.4f);
    }

    private void Update()
    {
        // Hiệu ứng xoay mượt cho icon Spinner khi đang hiện màn hình chờ
        if (isShowing && spinnerTransform != null)
        {
            spinnerTransform.Rotate(0f, 0f, -250f * Time.unscaledDeltaTime);
        }
    }

    /// <summary>
    /// Tự động dựng giao diện UI Canvas mờ nền tối cao cấp bằng C# code (Không cần kéo thả Editor)
    /// </summary>
    private void BuildLoadingScreenCanvas()
    {
        // 1. Root GameObject + Canvas Component
        GameObject canvasObj = new GameObject("LoadingScreenCanvas");
        canvasObj.transform.SetParent(transform, false);

        loadingCanvas = canvasObj.AddComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = 9999; // Cao nhất để che phủ tất cả UI khác

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 2. Nền tối Glassmorphism Dark Theme
        GameObject panelObj = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

        // 3. Tiêu đề (Title)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.6f);
        titleRect.anchorMax = new Vector2(0.5f, 0.6f);
        titleRect.sizeDelta = new Vector2(1200, 100);

        titleText = titleObj.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.3f, 1f); // Màu vàng hoàng kim phát sáng
        titleText.text = "ĐANG KHỞI TẠO HẦM NGỤC...";

        // 4. Phụ đề (Subtitle)
        GameObject subObj = new GameObject("SubText", typeof(RectTransform), typeof(Text));
        subObj.transform.SetParent(panelObj.transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.52f);
        subRect.anchorMax = new Vector2(0.5f, 0.52f);
        subRect.sizeDelta = new Vector2(1000, 60);

        subText = subObj.GetComponent<Text>();
        subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subText.fontSize = 26;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.8f, 0.85f, 0.9f, 0.9f);
        subText.text = "Vui lòng chờ trong giây lát...";

        // 5. Spinner Icon tải dữ liệu
        GameObject spinnerObj = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
        spinnerObj.transform.SetParent(panelObj.transform, false);
        spinnerTransform = spinnerObj.GetComponent<RectTransform>();
        spinnerTransform.anchorMin = new Vector2(0.5f, 0.4f);
        spinnerTransform.anchorMax = new Vector2(0.5f, 0.4f);
        spinnerTransform.sizeDelta = new Vector2(64, 64);

        Image spinnerImg = spinnerObj.GetComponent<Image>();
        Sprite spinnerSprite = Resources.Load<Sprite>("Minimap/Room");
        if (spinnerSprite != null) spinnerImg.sprite = spinnerSprite;
        spinnerImg.color = new Color(0f, 0.8f, 1f, 0.9f);

        // 6. Gợi ý mẹo chơi (Game Tips)
        GameObject tipObj = new GameObject("TipText", typeof(RectTransform), typeof(Text));
        tipObj.transform.SetParent(panelObj.transform, false);
        RectTransform tipRect = tipObj.GetComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0.5f, 0.15f);
        tipRect.anchorMax = new Vector2(0.5f, 0.15f);
        tipRect.sizeDelta = new Vector2(1400, 60);

        tipText = tipObj.GetComponent<Text>();
        tipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tipText.fontSize = 22;
        tipText.fontStyle = FontStyle.Italic;
        tipText.alignment = TextAnchor.MiddleCenter;
        tipText.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
        tipText.text = gameTips[0];
    }

    /// <summary>
    /// Hiển thị màn hình chờ mờ dần kèm tiêu đề và phụ đề tùy chỉnh
    /// </summary>
    public void ShowLoading(string title = "ĐANG TẢI DỮ LIỆU...", string subtitle = "", float duration = 0.3f)
    {
        gameObject.SetActive(true);
        if (loadingCanvas != null) loadingCanvas.gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        if (titleText != null) titleText.text = title;
        if (subText != null) subText.text = string.IsNullOrEmpty(subtitle) ? "Vui lòng chờ trong giây lát..." : subtitle;
        
        if (tipText != null && gameTips.Length > 0)
        {
            tipText.text = gameTips[Random.Range(0, gameTips.Length)];
        }

        isShowing = true;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f, duration));

        // BỔ SUNG: Bộ đếm thời gian an toàn (Safety Timeout 5s) tự động ẩn Loading Screen đề phòng bị kẹt
        if (safetyTimeoutCoroutine != null) StopCoroutine(safetyTimeoutCoroutine);
        safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutRoutine(5f));
    }

    private IEnumerator SafetyTimeoutRoutine(float timeoutSeconds)
    {
        yield return new WaitForSecondsRealtime(timeoutSeconds);
        if (isShowing)
        {
            Debug.LogWarning("[LoadingScreenUI] Kích hoạt Safety Timeout tự động ẩn Loading Screen!");
            HideLoading();
        }
    }

    /// <summary>
    /// Mờ dần và ẩn màn hình chờ khi quá trình tải bản đồ hoàn tất
    /// </summary>
    public void HideLoading(float duration = 0.4f)
    {
        isShowing = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        if (safetyTimeoutCoroutine != null) StopCoroutine(safetyTimeoutCoroutine);

        if (!gameObject.activeInHierarchy)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            return;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            if (targetAlpha <= 0f && loadingCanvas != null) loadingCanvas.gameObject.SetActive(false);
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        if (targetAlpha <= 0f && loadingCanvas != null)
        {
            loadingCanvas.gameObject.SetActive(false);
        }
    }
}
