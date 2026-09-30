using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CutsceneManager : MonoBehaviour
{
    public const string CUTSCENE_SEEN_KEY = "CutsceneSeen";

    /// <summary>
    /// Đặt bằng true khi người chơi bấm nút "Rewatch Cutscene" từ Menu để không bị tự động bỏ qua
    /// </summary>
    public static bool IsRewatching = false;

    [SerializeField] private Sprite[] pages;
    [SerializeField] private Image pageLeft;
    [SerializeField] private Image pageRight;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float delayBetweenClick = 0.2f;
    [SerializeField] private string nextSceneName = "Scene_Menu";

    private int currentIndex = 0;
    private bool isLeft = true;
    private bool isTransitioning = false;
    private bool isSkipPopupOpen = false;
    private bool isEnding = false;

    private CanvasGroup leftGroup;
    private CanvasGroup rightGroup;
    private RectTransform leftRect;
    private RectTransform rightRect;
    private Vector2 leftBasePos;
    private Vector2 rightBasePos;

    // UI trang trí & điều khiển tạo tự động
    private Canvas mainCanvas;
    private Text promptText;
    private Text pageCounterText;
    private readonly List<Image> pageDots = new List<Image>();
    private GameObject skipPopupOverlay;
    private RectTransform skipModalBox;
    private CanvasGroup skipPopupCanvasGroup;
    private CanvasGroup masterFadeGroup;

    // Hạt bụi ánh sáng nền (Ambient Motes)
    private class AmbientMote
    {
        public RectTransform rect;
        public Image img;
        public Vector2 velocity;
        public float baseAlpha;
        public float phase;
    }
    private readonly List<AmbientMote> ambientMotes = new List<AmbientMote>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoPopulateCutscenePages();
    }
#endif

    private void TryAutoPopulateCutscenePages()
    {
#if UNITY_EDITOR
        List<Sprite> loaded = new List<Sprite>();
        for (int i = 1; i <= 11; i++)
        {
            string path = $"Assets/Assets/Cutscenes/{i}.png";
            Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sp.name = i.ToString();
                }
            }
            if (sp != null)
            {
                loaded.Add(sp);
            }
        }
        if (loaded.Count == 11)
        {
            pages = loaded.ToArray();
        }
#endif
    }

    private void Awake()
    {
        TryAutoPopulateCutscenePages();

        // Lọc bỏ ảnh null hoặc ảnh meme gorilla (acc2f2506f2399aa46e87f97a2eb2095) nếu còn sót trong Inspector
        if (pages != null && pages.Length > 0)
        {
            List<Sprite> validPages = new List<Sprite>();
            foreach (var sp in pages)
            {
                if (sp == null) continue;
                if (sp.name.Contains("acc2f2506f2399aa46e87f97a2eb2095")) continue;
                validPages.Add(sp);
            }
            pages = validPages.ToArray();
        }

        // Nếu người chơi đã từng xem Cutscene ở lần mở game đầu tiên và không phải đang bấm Rewatch -> Chuyển thẳng vào Menu
        if (!IsRewatching && PlayerPrefs.GetInt(CUTSCENE_SEEN_KEY, 0) == 1)
        {
            SceneManager.LoadScene(nextSceneName);
            enabled = false;
            return;
        }

        // Đánh dấu đã mở Cutscene lần đầu ngay lập tức để đảm bảo chỉ tự động hiện duy nhất 1 lần khi mở game lần đầu (kể cả trên WebGL / itch.io)
        PlayerPrefs.SetInt(CUTSCENE_SEEN_KEY, 1);
        PlayerPrefs.Save();
    }

    private void Start()
    {
        if (!enabled) return;

        mainCanvas = FindFirstObjectByType<Canvas>();

        leftGroup = GetOrAddCanvasGroup(pageLeft);
        rightGroup = GetOrAddCanvasGroup(pageRight);

        leftRect = pageLeft.GetComponent<RectTransform>();
        rightRect = pageRight.GetComponent<RectTransform>();

        pageLeft.color = Color.white;
        pageRight.color = Color.white;

        leftGroup.alpha = 0f;
        rightGroup.alpha = 0f;

        BuildCinematicDecorations();

        if (leftRect != null) leftBasePos = leftRect.anchoredPosition;
        if (rightRect != null) rightBasePos = rightRect.anchoredPosition;

        UpdateProgressUI(0);

        // Tự động mở màn và hiển thị trang đầu tiên ngay khi vào Cutscene
        StartCoroutine(PlayOpeningAndFirstPage());
    }

    private void Update()
    {
        // 1. Chuyển động hạt bụi ánh sáng nền & hiệu ứng nhịp thở cho dòng nhắc click
        float time = Time.unscaledTime;

        if (promptText != null)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(time * 3.2f);
            Color c = promptText.color;
            c.a = isSkipPopupOpen ? 0.2f : pulse;
            promptText.color = c;
        }

        for (int i = 0; i < ambientMotes.Count; i++)
        {
            var m = ambientMotes[i];
            if (m.rect == null) continue;

            Vector2 pos = m.rect.anchoredPosition;
            pos += m.velocity * Time.unscaledDeltaTime;
            pos.x += Mathf.Sin(time * 0.8f + m.phase) * 8f * Time.unscaledDeltaTime;

            if (pos.y > 580f) pos.y = -580f;
            if (pos.x > 1000f) pos.x = -1000f;
            if (pos.x < -1000f) pos.x = 1000f;

            m.rect.anchoredPosition = pos;

            Color mc = m.img.color;
            mc.a = m.baseAlpha * (0.5f + 0.5f * Mathf.Sin(time * 1.8f + m.phase));
            m.img.color = mc;
        }

        // 2. Điều khiển bằng bàn phím
        if (isEnding) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSkipPopupOpen)
                CloseSkipConfirmPopup();
            else
                OpenSkipConfirmPopup();
            return;
        }

        if (!isSkipPopupOpen && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            OnScreenClicked();
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(Image img)
    {
        var cg = img.GetComponent<CanvasGroup>();
        if (cg == null) cg = img.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private IEnumerator PlayOpeningAndFirstPage()
    {
        isTransitioning = true;

        if (masterFadeGroup != null)
        {
            masterFadeGroup.alpha = 1f;
            float t = 0f;
            const float openDur = 0.45f;
            while (t < openDur)
            {
                t += Time.unscaledDeltaTime;
                masterFadeGroup.alpha = Mathf.Lerp(1f, 0f, t / openDur);
                yield return null;
            }
            masterFadeGroup.alpha = 0f;
        }

        isTransitioning = false;

        if (pages != null && pages.Length > 0)
        {
            StartCoroutine(ShowNextImage());
        }
    }

    public void OnScreenClicked()
    {
        if (isSkipPopupOpen || isTransitioning || isEnding) return;

        if (pages == null || currentIndex >= pages.Length)
        {
            StartCoroutine(EndCutscene());
            return;
        }

        StartCoroutine(ShowNextImage());
    }

    private IEnumerator ShowNextImage()
    {
        isTransitioning = true;

        if (isLeft)
        {
            // Khi bước sang trang Trái của cặp trang mới (trang 3, 5...), làm mờ cả 2 trang của cặp cũ cùng lúc
            if (currentIndex > 0)
            {
                yield return StartCoroutine(FadeOutSpreadSimultaneously(fadeDuration * 0.75f));
                yield return new WaitForSecondsRealtime(delayBetweenClick);
            }

            pageLeft.sprite = pages[currentIndex];
            UpdateProgressUI(currentIndex + 1);
            yield return StartCoroutine(AnimatePageIn(leftGroup, leftRect, leftBasePos, isLeftPage: true));
        }
        else
        {
            // Khi trang Phải xuất hiện, làm dịu nhẹ độ sáng của trang Trái xuống một chút để mắt tập trung vào trang Phải mới
            StartCoroutine(FadeCanvasGroup(leftGroup, leftGroup.alpha, 0.78f, fadeDuration * 0.6f));

            if (delayBetweenClick > 0f)
                yield return new WaitForSecondsRealtime(delayBetweenClick * 0.6f);

            pageRight.sprite = pages[currentIndex];
            UpdateProgressUI(currentIndex + 1);
            yield return StartCoroutine(AnimatePageIn(rightGroup, rightRect, rightBasePos, isLeftPage: false));

            // Đưa cả 2 trang về độ sáng 100% sau khi trang Phải hiện xong
            StartCoroutine(FadeCanvasGroup(leftGroup, leftGroup.alpha, 1f, 0.25f));
        }

        currentIndex++;
        isLeft = !isLeft;

        // Cập nhật dòng chữ hướng dẫn nếu đã hiện tới trang cuối cùng
        if (promptText != null && pages != null && currentIndex >= pages.Length)
        {
            promptText.text = "CLICK OR PRESS [SPACE] TO ENTER MAIN MENU  ▸▸";
            promptText.color = new Color(1f, 0.84f, 0.35f, 1f);
        }

        isTransitioning = false;
    }

    private IEnumerator FadeOutSpreadSimultaneously(float duration)
    {
        float startLeft = leftGroup != null ? leftGroup.alpha : 0f;
        float startRight = rightGroup != null ? rightGroup.alpha : 0f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p;
            if (leftGroup != null) leftGroup.alpha = Mathf.Lerp(startLeft, 0f, eased);
            if (rightGroup != null) rightGroup.alpha = Mathf.Lerp(startRight, 0f, eased);
            yield return null;
        }

        if (leftGroup != null) leftGroup.alpha = 0f;
        if (rightGroup != null) rightGroup.alpha = 0f;
    }

    private IEnumerator AnimatePageIn(CanvasGroup cg, RectTransform rect, Vector2 targetPos, bool isLeftPage)
    {
        if (cg == null) yield break;

        Vector2 startPos = targetPos + new Vector2(isLeftPage ? -28f : 28f, -12f);
        Vector3 startScale = new Vector3(0.95f, 0.95f, 1f);
        Vector3 endScale = Vector3.one;

        cg.alpha = 0f;
        if (rect != null)
        {
            rect.anchoredPosition = startPos;
            rect.localScale = startScale;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            // EaseOutCubic mượt mà
            float eased = 1f - Mathf.Pow(1f - p, 3f);

            cg.alpha = eased;
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
                rect.localScale = Vector3.Lerp(startScale, endScale, eased);
            }
            yield return null;
        }

        cg.alpha = 1f;
        if (rect != null)
        {
            rect.anchoredPosition = targetPos;
            rect.localScale = endScale;
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator EndCutscene()
    {
        if (isEnding) yield break;
        isEnding = true;
        isTransitioning = true;

        PlayerPrefs.SetInt(CUTSCENE_SEEN_KEY, 1);
        PlayerPrefs.Save();
        IsRewatching = false;

        yield return StartCoroutine(FadeOutSpreadSimultaneously(fadeDuration * 0.8f));

        if (masterFadeGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(masterFadeGroup, masterFadeGroup.alpha, 1f, 0.35f));
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // =========================================================================
    // SKIP CUTSCENE & CONFIRMATION POPUP
    // =========================================================================

    public void OpenSkipConfirmPopup()
    {
        if (isEnding || skipPopupOverlay == null) return;
        isSkipPopupOpen = true;
        skipPopupOverlay.SetActive(true);
        StopCoroutine(nameof(AnimateSkipModal));
        StartCoroutine(AnimateSkipModal(opening: true));
    }

    public void CloseSkipConfirmPopup()
    {
        if (!isSkipPopupOpen || skipPopupOverlay == null) return;
        StopCoroutine(nameof(AnimateSkipModal));
        StartCoroutine(AnimateSkipModal(opening: false));
    }

    public void ConfirmSkipCutscene()
    {
        if (isEnding) return;
        isSkipPopupOpen = false;
        if (skipPopupOverlay != null) skipPopupOverlay.SetActive(false);
        StopAllCoroutines();
        StartCoroutine(EndCutscene());
    }

    private IEnumerator AnimateSkipModal(bool opening)
    {
        float duration = 0.16f;
        float t = 0f;

        Vector3 fromScale = opening ? new Vector3(0.86f, 0.86f, 1f) : Vector3.one;
        Vector3 toScale = opening ? Vector3.one : new Vector3(0.88f, 0.88f, 1f);
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

        if (skipModalBox != null) skipModalBox.localScale = fromScale;
        if (skipPopupCanvasGroup != null) skipPopupCanvasGroup.alpha = fromAlpha;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = opening ? (1f - Mathf.Pow(1f - p, 3f)) : (p * p);

            if (skipModalBox != null) skipModalBox.localScale = Vector3.Lerp(fromScale, toScale, eased);
            if (skipPopupCanvasGroup != null) skipPopupCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            yield return null;
        }

        if (skipModalBox != null) skipModalBox.localScale = toScale;
        if (skipPopupCanvasGroup != null) skipPopupCanvasGroup.alpha = toAlpha;

        if (!opening)
        {
            isSkipPopupOpen = false;
            if (skipPopupOverlay != null) skipPopupOverlay.SetActive(false);
        }
    }

    // =========================================================================
    // PROCEDURAL CINEMATIC UI DECORATION ("ĐẸP TRONG MẮT ANTI")
    // =========================================================================

    private void BuildCinematicDecorations()
    {
        if (mainCanvas == null) return;

        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Transform canvasTransform = mainCanvas.transform;

        // 1. Nâng cấp Background thành hiệu ứng Radial Vignette sâu thẳm
        Transform bgTransform = canvasTransform.Find("Background");
        if (bgTransform != null)
        {
            Image bgImg = bgTransform.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = CreateRadialVignetteSprite(256, 256,
                    new Color(0.10f, 0.13f, 0.22f, 1f), // Tâm xanh tím than dịu
                    new Color(0.02f, 0.025f, 0.05f, 1f) // Viền đen tuyền điện ảnh
                );
                bgImg.color = Color.white;
                bgImg.raycastTarget = false;
            }

            // Tạo các hạt bụi ánh sáng lơ lửng phía sau 2 trang truyện
            CreateAmbientMotes(bgTransform);

            // Tạo đường kẻ trang trí tinh tế ngăn cách giữa trang Trái và trang Phải
            CreateCenterDivider(bgTransform);
        }

        // 2. Tinh chỉnh khung & đổ bóng chiều sâu cho pageLeft và pageRight
        PolishComicPageSlot(pageLeft, isLeftSlot: true);
        PolishComicPageSlot(pageRight, isLeftSlot: false);

        // 3. Thanh Letterbox Điện Ảnh Phía Trên (Top Bar) + Nút SKIP (Góc trên bên phải)
        GameObject topBar = new GameObject("CinematicTopBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(canvasTransform, false);
        RectTransform topBarRect = topBar.GetComponent<RectTransform>();
        topBarRect.anchorMin = new Vector2(0f, 1f);
        topBarRect.anchorMax = new Vector2(1f, 1f);
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.anchoredPosition = Vector2.zero;
        topBarRect.sizeDelta = new Vector2(0f, 76f);

        Image topBarImg = topBar.GetComponent<Image>();
        topBarImg.color = new Color(0.03f, 0.04f, 0.07f, 0.92f);
        topBarImg.raycastTarget = false;

        // Đường viền ánh kim mảnh dưới Top Bar
        CreateAccentLine(topBar.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1.5f),
            new Color(0.85f, 0.68f, 0.28f, 0.55f));

        // Nút SKIP CUTSCENE (Góc trên bên phải)
        CreateTopRightSkipButton(topBar.transform, uiFont);

        // 4. Thanh Letterbox Điện Ảnh Phía Dưới (Bottom Bar) + Tiến trình trang + Nhắc lệnh Click
        GameObject bottomBar = new GameObject("CinematicBottomBar", typeof(RectTransform), typeof(Image));
        bottomBar.transform.SetParent(canvasTransform, false);
        RectTransform bottomBarRect = bottomBar.GetComponent<RectTransform>();
        bottomBarRect.anchorMin = new Vector2(0f, 0f);
        bottomBarRect.anchorMax = new Vector2(1f, 0f);
        bottomBarRect.pivot = new Vector2(0.5f, 0f);
        bottomBarRect.anchoredPosition = Vector2.zero;
        bottomBarRect.sizeDelta = new Vector2(0f, 82f);

        Image bottomBarImg = bottomBar.GetComponent<Image>();
        bottomBarImg.color = new Color(0.03f, 0.04f, 0.07f, 0.92f);
        bottomBarImg.raycastTarget = false;

        // Đường viền ánh kim mảnh trên Bottom Bar
        CreateAccentLine(bottomBar.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1.5f),
            new Color(0.85f, 0.68f, 0.28f, 0.55f));

        // Bộ đếm trang & chấm tiến trình (Góc dưới bên trái)
        CreateBottomProgressIndicator(bottomBar.transform, uiFont);

        // Dòng chữ hướng dẫn nhấp nháy (Góc dưới bên phải)
        GameObject promptObj = new GameObject("ContinuePromptText", typeof(RectTransform), typeof(Text));
        promptObj.transform.SetParent(bottomBar.transform, false);
        RectTransform promptRect = promptObj.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.45f, 0f);
        promptRect.anchorMax = new Vector2(1f, 1f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = new Vector2(-36f, 0f);

        promptText = promptObj.GetComponent<Text>();
        promptText.font = uiFont;
        promptText.fontSize = 18;
        promptText.fontStyle = FontStyle.Bold;
        promptText.alignment = TextAnchor.MiddleRight;
        promptText.color = new Color(0.82f, 0.92f, 1f, 0.9f);
        promptText.text = "CLICK ANYWHERE OR PRESS [SPACE] TO CONTINUE  ▸";
        promptText.raycastTarget = false;

        // 5. Hộp thoại Xác Nhận Skip Cutscene (Confirm Popup)
        BuildSkipConfirmModal(canvasTransform, uiFont);

        // 6. Màn đen Fade-In / Fade-Out tổng thể trên cùng
        GameObject masterFadeObj = new GameObject("MasterFadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        masterFadeObj.transform.SetParent(canvasTransform, false);
        RectTransform fadeRect = masterFadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.sizeDelta = Vector2.zero;

        Image fadeImg = masterFadeObj.GetComponent<Image>();
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = false;

        masterFadeGroup = masterFadeObj.GetComponent<CanvasGroup>();
        masterFadeGroup.alpha = 1f;
        masterFadeGroup.blocksRaycasts = false;
        masterFadeGroup.interactable = false;
    }

    private void PolishComicPageSlot(Image pageImg, bool isLeftSlot)
    {
        if (pageImg == null) return;
        pageImg.preserveAspect = true;
        pageImg.raycastTarget = false;

        RectTransform rt = pageImg.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Thu gọn vùng hiển thị nằm gọn giữa Top Bar (76px) và Bottom Bar (82px) với khoảng đệm đẹp mắt
            rt.anchorMin = new Vector2(isLeftSlot ? 0.035f : 0.515f, 0.105f);
            rt.anchorMax = new Vector2(isLeftSlot ? 0.485f : 0.965f, 0.895f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        // Thêm bóng đổ sâu giúp trang truyện nổi bật khỏi nền
        Shadow dropShadow = pageImg.GetComponent<Shadow>();
        if (dropShadow == null) dropShadow = pageImg.gameObject.AddComponent<Shadow>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        dropShadow.effectDistance = new Vector2(8f, -10f);
    }

    private void CreateTopRightSkipButton(Transform parentBar, Font uiFont)
    {
        GameObject skipBtnObj = new GameObject("Button_SkipCutscene", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButtonJuice));
        skipBtnObj.transform.SetParent(parentBar, false);

        RectTransform btnRect = skipBtnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0.5f);
        btnRect.anchorMax = new Vector2(1f, 0.5f);
        btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.anchoredPosition = new Vector2(-34f, 0f);
        btnRect.sizeDelta = new Vector2(148f, 44f);

        Image btnBg = skipBtnObj.GetComponent<Image>();
        btnBg.color = new Color(0.12f, 0.15f, 0.25f, 0.95f);

        Outline btnOutline = skipBtnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.92f, 0.72f, 0.28f, 0.85f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = skipBtnObj.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
        cb.pressedColor = new Color(0.8f, 0.65f, 0.35f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(OpenSkipConfirmPopup);

        GameObject txtObj = new GameObject("SkipText", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(skipBtnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        Text txt = txtObj.GetComponent<Text>();
        txt.font = uiFont;
        txt.fontSize = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.98f, 0.86f, 0.52f, 1f);
        txt.text = "SKIP  ▸▸";
        txt.raycastTarget = false;
    }

    private void CreateBottomProgressIndicator(Transform bottomBar, Font uiFont)
    {
        GameObject container = new GameObject("ProgressContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        container.transform.SetParent(bottomBar, false);

        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.45f, 1f);
        rect.offsetMin = new Vector2(36f, 0f);
        rect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = container.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 10f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Nhãn số trang "PAGE 1 / 6"
        GameObject counterObj = new GameObject("PageCounterText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        counterObj.transform.SetParent(container.transform, false);
        LayoutElement counterLE = counterObj.GetComponent<LayoutElement>();
        counterLE.minWidth = 110f;
        counterLE.preferredWidth = 110f;
        counterLE.preferredHeight = 30f;

        pageCounterText = counterObj.GetComponent<Text>();
        pageCounterText.font = uiFont;
        pageCounterText.fontSize = 16;
        pageCounterText.fontStyle = FontStyle.Bold;
        pageCounterText.alignment = TextAnchor.MiddleLeft;
        pageCounterText.color = new Color(0.75f, 0.82f, 0.95f, 0.9f);
        pageCounterText.raycastTarget = false;

        // Các vạch chỉ báo từng trang
        int total = pages != null ? pages.Length : 0;
        pageDots.Clear();
        for (int i = 0; i < total; i++)
        {
            GameObject dotObj = new GameObject($"PageDot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dotObj.transform.SetParent(container.transform, false);

            LayoutElement le = dotObj.GetComponent<LayoutElement>();
            le.minWidth = 22f;
            le.preferredWidth = 22f;
            le.minHeight = 8f;
            le.preferredHeight = 8f;

            Image dotImg = dotObj.GetComponent<Image>();
            dotImg.color = new Color(0.25f, 0.30f, 0.42f, 0.7f);
            dotImg.raycastTarget = false;
            pageDots.Add(dotImg);
        }
    }

    private void UpdateProgressUI(int activePageOneBased)
    {
        int total = pages != null ? pages.Length : 0;
        if (pageCounterText != null)
        {
            int shown = Mathf.Clamp(activePageOneBased, 0, total);
            pageCounterText.text = $"PANEL  {shown} / {total}";
        }

        for (int i = 0; i < pageDots.Count; i++)
        {
            Image dot = pageDots[i];
            if (dot == null) continue;
            LayoutElement le = dot.GetComponent<LayoutElement>();

            if (i + 1 == activePageOneBased)
            {
                // Trang hiện tại: Sáng vàng hoàng kim & dài hơn
                dot.color = new Color(0.98f, 0.80f, 0.28f, 1f);
                if (le != null) { le.preferredWidth = 36f; le.minWidth = 36f; }
            }
            else if (i + 1 < activePageOneBased)
            {
                // Trang đã xem: Xanh cyan dịu
                dot.color = new Color(0.35f, 0.78f, 0.95f, 0.85f);
                if (le != null) { le.preferredWidth = 22f; le.minWidth = 22f; }
            }
            else
            {
                // Trang chưa mở: Xám tối
                dot.color = new Color(0.22f, 0.26f, 0.36f, 0.65f);
                if (le != null) { le.preferredWidth = 22f; le.minWidth = 22f; }
            }
        }
    }

    private void BuildSkipConfirmModal(Transform canvasTransform, Font uiFont)
    {
        // Lớp phủ tối toàn màn hình (chặn click xuống ClickArea bên dưới)
        skipPopupOverlay = new GameObject("SkipConfirmPopupOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        skipPopupOverlay.transform.SetParent(canvasTransform, false);

        RectTransform overlayRect = skipPopupOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        Image overlayImg = skipPopupOverlay.GetComponent<Image>();
        overlayImg.color = new Color(0.01f, 0.02f, 0.04f, 0.82f);
        overlayImg.raycastTarget = true;

        skipPopupCanvasGroup = skipPopupOverlay.GetComponent<CanvasGroup>();
        skipPopupCanvasGroup.alpha = 0f;
        skipPopupCanvasGroup.blocksRaycasts = true;
        skipPopupCanvasGroup.interactable = true;

        // Khung hộp thoại chính giữa màn hình
        GameObject modalObj = new GameObject("SkipModalBox", typeof(RectTransform), typeof(Image));
        modalObj.transform.SetParent(skipPopupOverlay.transform, false);

        skipModalBox = modalObj.GetComponent<RectTransform>();
        skipModalBox.anchorMin = new Vector2(0.5f, 0.5f);
        skipModalBox.anchorMax = new Vector2(0.5f, 0.5f);
        skipModalBox.pivot = new Vector2(0.5f, 0.5f);
        skipModalBox.anchoredPosition = Vector2.zero;
        skipModalBox.sizeDelta = new Vector2(520f, 270f);

        Image modalBg = modalObj.GetComponent<Image>();
        modalBg.color = new Color(0.07f, 0.09f, 0.16f, 0.98f);

        Outline modalBorder = modalObj.AddComponent<Outline>();
        modalBorder.effectColor = new Color(0.92f, 0.72f, 0.28f, 0.9f);
        modalBorder.effectDistance = new Vector2(2f, -2f);

        Shadow modalShadow = modalObj.AddComponent<Shadow>();
        modalShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        modalShadow.effectDistance = new Vector2(0f, -12f);

        // Thanh viền vàng trên đỉnh Modal
        CreateAccentLine(modalObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 4f),
            new Color(0.95f, 0.75f, 0.25f, 1f));

        // Tag tiêu đề phụ
        GameObject tagObj = new GameObject("HeaderTag", typeof(RectTransform), typeof(Text));
        tagObj.transform.SetParent(modalObj.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0f, 1f);
        tagRect.anchorMax = new Vector2(1f, 1f);
        tagRect.pivot = new Vector2(0.5f, 1f);
        tagRect.anchoredPosition = new Vector2(0f, -22f);
        tagRect.sizeDelta = new Vector2(-40f, 24f);

        Text tagText = tagObj.GetComponent<Text>();
        tagText.font = uiFont;
        tagText.fontSize = 14;
        tagText.fontStyle = FontStyle.Bold;
        tagText.alignment = TextAnchor.MiddleCenter;
        tagText.color = new Color(0.95f, 0.76f, 0.28f, 1f);
        tagText.text = "[ CONFIRMATION ]";

        // Tiêu đề chính
        GameObject titleObj = new GameObject("ModalTitle", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(modalObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -52f);
        titleRect.sizeDelta = new Vector2(-40f, 38f);

        Text titleText = titleObj.GetComponent<Text>();
        titleText.font = uiFont;
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "SKIP CUTSCENE?";

        // Nội dung hỏi xác nhận
        GameObject descObj = new GameObject("ModalDescription", typeof(RectTransform), typeof(Text));
        descObj.transform.SetParent(modalObj.transform, false);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.5f);
        descRect.anchorMax = new Vector2(1f, 0.5f);
        descRect.pivot = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, 8f);
        descRect.sizeDelta = new Vector2(-60f, 64f);

        Text descText = descObj.GetComponent<Text>();
        descText.font = uiFont;
        descText.fontSize = 17;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.color = new Color(0.78f, 0.83f, 0.92f, 1f);
        descText.text = "Are you sure you want to skip the story intro?\nYou can always rewatch it later from the Main Menu.";

        // Nút CANCEL (Bên trái)
        CreateModalActionButton(
            modalObj.transform,
            uiFont,
            "Button_CancelSkip",
            "RESUME",
            new Vector2(-110f, -82f),
            new Color(0.18f, 0.22f, 0.34f, 1f),
            new Color(0.45f, 0.55f, 0.75f, 0.8f),
            Color.white,
            CloseSkipConfirmPopup
        );

        // Nút SKIP (Bên phải)
        CreateModalActionButton(
            modalObj.transform,
            uiFont,
            "Button_ConfirmSkip",
            "SKIP NOW",
            new Vector2(110f, -82f),
            new Color(0.85f, 0.24f, 0.24f, 1f),
            new Color(1f, 0.75f, 0.35f, 0.9f),
            Color.white,
            ConfirmSkipCutscene
        );

        skipPopupOverlay.SetActive(false);
    }

    private void CreateModalActionButton(
        Transform parent,
        Font uiFont,
        string objName,
        string label,
        Vector2 anchoredPos,
        Color bgColor,
        Color borderColor,
        Color textColor,
        UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButtonJuice));
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(180f, 48f);

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        Text txt = txtObj.GetComponent<Text>();
        txt.font = uiFont;
        txt.fontSize = 17;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.text = label;
        txt.raycastTarget = false;
    }

    private void CreateAccentLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
    {
        GameObject lineObj = new GameObject("AccentLine", typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(parent, false);
        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;

        Image img = lineObj.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private void CreateCenterDivider(Transform bgTransform)
    {
        GameObject divider = new GameObject("CenterSpreadDivider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(bgTransform, false);
        divider.transform.SetAsFirstSibling();

        RectTransform rt = divider.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.12f);
        rt.anchorMax = new Vector2(0.5f, 0.88f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(2f, 0f);

        Image img = divider.GetComponent<Image>();
        img.color = new Color(0.85f, 0.72f, 0.35f, 0.14f);
        img.raycastTarget = false;
    }

    private void CreateAmbientMotes(Transform bgTransform)
    {
        ambientMotes.Clear();
        const int moteCount = 18;

        for (int i = 0; i < moteCount; i++)
        {
            GameObject moteObj = new GameObject($"AmbientMote_{i}", typeof(RectTransform), typeof(Image));
            moteObj.transform.SetParent(bgTransform, false);
            moteObj.transform.SetAsFirstSibling();

            RectTransform rt = moteObj.GetComponent<RectTransform>();
            float size = Random.Range(4f, 10f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Random.Range(-920f, 920f), Random.Range(-480f, 480f));

            Image img = moteObj.GetComponent<Image>();
            bool isGold = (i % 2 == 0);
            float baseAlpha = Random.Range(0.12f, 0.32f);
            img.color = isGold
                ? new Color(1f, 0.82f, 0.38f, baseAlpha)
                : new Color(0.38f, 0.82f, 1f, baseAlpha);
            img.raycastTarget = false;

            ambientMotes.Add(new AmbientMote
            {
                rect = rt,
                img = img,
                velocity = new Vector2(Random.Range(-6f, 6f), Random.Range(10f, 24f)),
                baseAlpha = baseAlpha,
                phase = Random.Range(0f, Mathf.PI * 2f)
            });
        }
    }

    private Sprite CreateRadialVignetteSprite(int width, int height, Color centerColor, Color edgeColor)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float maxDist = Vector2.Distance(Vector2.zero, center);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d));
                tex.SetPixel(x, y, Color.Lerp(centerColor, edgeColor, t));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}