using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Giao diện chọn nâng cấp Buff giữa các tầng (Minimalist Slate Theme, Font Symtext SDF, Icon 2D Sprite)
/// </summary>
public class UpgradeSelectionUI : MonoBehaviour
{
    private static UpgradeSelectionUI _instance;
    public static UpgradeSelectionUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<UpgradeSelectionUI>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("UpgradeSelectionUI");
                    _instance = obj.AddComponent<UpgradeSelectionUI>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private bool isMenuOpen = false;

    // Font Symtext SDF dùng đồng bộ cho toàn bộ UI
    private static TMP_FontAsset cachedFont;
    public static TMP_FontAsset GetSymtextFont()
    {
        if (cachedFont == null)
        {
            cachedFont = Resources.Load<TMP_FontAsset>("Fonts/Symtext SDF");
        }
        return cachedFont;
    }

    /// <summary>
    /// Tìm và nạp Sprite icon tương ứng cho Buff (ưu tiên iconPath trong Database, fallback về buffType)
    /// </summary>
    public static Sprite GetBuffSprite(BuffConfig buff)
    {
        if (buff == null) return null;
        Sprite sprite = null;

        if (!string.IsNullOrEmpty(buff.iconPath))
        {
            string path = buff.iconPath.Replace(".png", "").Replace(".jpg", "");
            if (!path.StartsWith("BuffIcons/"))
            {
                path = "BuffIcons/" + path;
            }
            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>(buff.iconPath.Replace(".png", ""));
            }
        }

        if (sprite == null && !string.IsNullOrEmpty(buff.buffType))
        {
            sprite = Resources.Load<Sprite>("BuffIcons/" + buff.buffType);
        }

        return sprite;
    }

    /// <summary>
    /// Tạo nhãn hiển thị chỉ số ngắn gọn (Stat Chip badge)
    /// </summary>
    public static string GetStatChipText(BuffConfig buff)
    {
        if (buff == null) return string.Empty;

        switch (buff.buffType)
        {
            case "MaxHP":
            case "HP":
            case "Health":
                return $"+{Mathf.RoundToInt(buff.value)} MAX HP";

            case "MaxArmor":
            case "Armor":
                return $"+{Mathf.RoundToInt(buff.value)} MAX ARMOR";

            case "MaxMana":
            case "Mana":
            case "Energy":
                return $"+{Mathf.RoundToInt(buff.value)} MAX MANA";

            case "Damage":
                int dmgPercent = Mathf.RoundToInt((buff.value - 1f) * 100f);
                return dmgPercent > 0 ? $"+{dmgPercent}% DAMAGE" : $"+{Mathf.RoundToInt(buff.value * 100f)}% DAMAGE";

            case "CritChance":
                return $"+{Mathf.RoundToInt(buff.value)}% CRIT CHANCE";

            case "FireRate":
                int ratePercent = buff.value < 1f ? Mathf.RoundToInt((1f - buff.value) * 100f) : Mathf.RoundToInt(buff.value * 100f);
                return $"+{ratePercent}% ATK SPEED";

            case "CoinMultiplier":
                int coinPercent = Mathf.RoundToInt((buff.value - 1f) * 100f);
                return coinPercent > 0 ? $"+{coinPercent}% COINS" : $"+{Mathf.RoundToInt(buff.value * 100f)}% COINS";

            default:
                return $"+{buff.value}";
        }
    }

    public void OpenUpgradeMenu(Action onComplete)
    {
        if (isMenuOpen || GameObject.Find("UpgradeSelectionCanvas") != null)
        {
            Debug.LogWarning("[UpgradeSelectionUI] Bảng chọn Buff đã đang mở, bỏ qua yêu cầu mở trùng lặp.");
            return;
        }

        if (GameConfigManager.Instance != null && (GameConfigManager.Instance.BuffDb == null || GameConfigManager.Instance.BuffDb.Count == 0))
        {
            Debug.LogWarning("[UpgradeSelectionUI] BuffDb trống trên Client! Tự động nạp dữ liệu Buff dự phòng.");
            GameConfigManager.Instance.PopulateDefaultBuffsFallback();
        }

        if (GameConfigManager.Instance == null || GameConfigManager.Instance.BuffDb == null || GameConfigManager.Instance.BuffDb.Count == 0)
        {
            Debug.LogWarning("[UpgradeSelectionUI] BuffDb trống hoặc GameConfigManager chưa sẵn sàng. Chuyển tầng trực tiếp.");
            onComplete?.Invoke();
            return;
        }

        isMenuOpen = true;

        // Tạm dừng thời gian trò chơi khi mở bảng nâng cấp
        Time.timeScale = 0f;

        // Lọc danh sách Buff: Tuyệt đối loại bỏ MoveSpeed và chọn ngẫu nhiên 3 Buff
        List<BuffConfig> availableBuffs = new List<BuffConfig>();
        foreach (var b in GameConfigManager.Instance.BuffDb)
        {
            if (b != null && !string.Equals(b.buffType, "MoveSpeed", StringComparison.OrdinalIgnoreCase))
            {
                availableBuffs.Add(b);
            }
        }

        List<BuffConfig> selectedBuffs = new List<BuffConfig>();
        int countToSelect = Mathf.Min(3, availableBuffs.Count);
        for (int i = 0; i < countToSelect; i++)
        {
            int index = UnityEngine.Random.Range(0, availableBuffs.Count);
            selectedBuffs.Add(availableBuffs[index]);
            availableBuffs.RemoveAt(index);
        }

        TMP_FontAsset fontAsset = GetSymtextFont();

        // 1. Tạo Canvas hiển thị
        GameObject canvasObj = new GameObject("UpgradeSelectionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Đảm bảo EventSystem tồn tại
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 2. Tạo Background Overlay mờ tối (Sleek dark vignette)
        GameObject bgObj = new GameObject("BackgroundOverlay");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.06f, 0.09f, 0.90f);

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Header Text & Subtitle
        GameObject headerContainer = new GameObject("HeaderContainer");
        headerContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRect = headerContainer.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.85f);
        headerRect.anchorMax = new Vector2(0.5f, 0.85f);
        headerRect.sizeDelta = new Vector2(800, 110);
        headerRect.anchoredPosition = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerContainer.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "SELECT AN UPGRADE";
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.fontSize = 38;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.95f, 0.96f, 0.98f, 1f); // #F1F5F9
        titleText.characterSpacing = 2f;

        RectTransform titleTextRect = titleObj.GetComponent<RectTransform>();
        titleTextRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleTextRect.anchorMax = new Vector2(0.5f, 0.7f);
        titleTextRect.sizeDelta = new Vector2(800, 50);
        titleTextRect.anchoredPosition = Vector2.zero;

        // Subtitle
        GameObject subtitleObj = new GameObject("SubtitleText");
        subtitleObj.transform.SetParent(headerContainer.transform, false);
        TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "Choose 1 enhancement to strengthen your operative";
        if (fontAsset != null) subtitleText.font = fontAsset;
        subtitleText.fontSize = 16;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.58f, 0.64f, 0.72f, 1f); // #94A3B8

        RectTransform subTextRect = subtitleObj.GetComponent<RectTransform>();
        subTextRect.anchorMin = new Vector2(0.5f, 0.2f);
        subTextRect.anchorMax = new Vector2(0.5f, 0.2f);
        subTextRect.sizeDelta = new Vector2(800, 30);
        subTextRect.anchoredPosition = Vector2.zero;

        // 4. Container chứa 3 thẻ
        GameObject containerObj = new GameObject("CardsContainer");
        containerObj.transform.SetParent(canvasObj.transform, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.44f);
        containerRect.anchorMax = new Vector2(0.5f, 0.44f);
        containerRect.sizeDelta = new Vector2(1040, 500);
        containerRect.anchoredPosition = Vector2.zero;

        // Thông số Card rộng rãi và chữ to rõ nét hơn
        float cardWidth = 290f;
        float cardHeight = 440f;
        float spacing = 36f;
        float startX = -((cardWidth * selectedBuffs.Count) + (spacing * (selectedBuffs.Count - 1))) / 2f + cardWidth / 2f;

        List<GameObject> spawnedCards = new List<GameObject>();

        for (int i = 0; i < selectedBuffs.Count; i++)
        {
            var buff = selectedBuffs[i];
            float posX = startX + i * (cardWidth + spacing);

            // Thẻ Card Root
            GameObject cardObj = new GameObject($"Card_{i}_{buff.buffType}");
            cardObj.transform.SetParent(containerObj.transform, false);
            spawnedCards.Add(cardObj);

            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRect.anchoredPosition = new Vector2(posX, 0f);

            // Nền Slate Card (Dark Slate #0F172A)
            Image cardImage = cardObj.AddComponent<Image>();
            cardImage.color = new Color(0.06f, 0.09f, 0.16f, 0.98f);

            // Viền Slate tối giản (#334155, không màu mè, không neon glow)
            Outline outline = cardObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.32f, 0.42f, 1f); // #3F516B
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            // Button click chọn thẻ
            Button cardButton = cardObj.AddComponent<Button>();
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(() =>
            {
                SelectBuff(buff, canvasObj, onComplete);
            });

            // Hiệu ứng Hover tối giản (Nâng nhẹ thẻ 8px và sáng viền Slate)
            cardObj.AddComponent<CardHoverEffect>();

            // --- 4.1 Rarity Header Tag ---
            GameObject rarityBadge = new GameObject("RarityBadge");
            rarityBadge.transform.SetParent(cardObj.transform, false);
            RectTransform rarityRect = rarityBadge.AddComponent<RectTransform>();
            rarityRect.anchorMin = new Vector2(0.5f, 0.92f);
            rarityRect.anchorMax = new Vector2(0.5f, 0.92f);
            rarityRect.sizeDelta = new Vector2(140, 26);
            rarityRect.anchoredPosition = Vector2.zero;

            Image rarityBg = rarityBadge.AddComponent<Image>();
            rarityBg.color = new Color(0.14f, 0.19f, 0.28f, 1f); // #1E293B

            Outline rarityOutline = rarityBadge.AddComponent<Outline>();
            rarityOutline.effectColor = new Color(0.35f, 0.45f, 0.58f, 0.8f);
            rarityOutline.effectDistance = new Vector2(1, 1);

            GameObject rarityTextObj = new GameObject("RarityText");
            rarityTextObj.transform.SetParent(rarityBadge.transform, false);
            TextMeshProUGUI rarityText = rarityTextObj.AddComponent<TextMeshProUGUI>();
            rarityText.text = string.IsNullOrEmpty(buff.rarity) ? "COMMON" : buff.rarity.ToUpper();
            if (fontAsset != null) rarityText.font = fontAsset;
            rarityText.fontSize = 13;
            rarityText.fontStyle = FontStyles.Bold;
            rarityText.alignment = TextAlignmentOptions.Center;
            rarityText.color = new Color(0.92f, 0.95f, 0.98f, 1f); // #EBF0F7

            RectTransform rarityTextRect = rarityTextObj.GetComponent<RectTransform>();
            rarityTextRect.anchorMin = Vector2.zero;
            rarityTextRect.anchorMax = Vector2.one;
            rarityTextRect.sizeDelta = Vector2.zero;

            // --- 4.2 Icon Container (Khung chứa Icon 2D Sprite) ---
            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(cardObj.transform, false);
            RectTransform iconContainerRect = iconContainer.AddComponent<RectTransform>();
            iconContainerRect.anchorMin = new Vector2(0.5f, 0.72f);
            iconContainerRect.anchorMax = new Vector2(0.5f, 0.72f);
            iconContainerRect.sizeDelta = new Vector2(84, 84);
            iconContainerRect.anchoredPosition = Vector2.zero;

            Image iconContainerBg = iconContainer.AddComponent<Image>();
            iconContainerBg.color = new Color(0.12f, 0.17f, 0.26f, 0.9f); // #1E293B

            Outline iconBorder = iconContainer.AddComponent<Outline>();
            iconBorder.effectColor = new Color(0.30f, 0.40f, 0.55f, 0.85f);
            iconBorder.effectDistance = new Vector2(1, 1);

            // Sprite Icon bên trong
            Sprite buffSprite = GetBuffSprite(buff);
            if (buffSprite != null)
            {
                GameObject iconImgObj = new GameObject("SpriteIcon");
                iconImgObj.transform.SetParent(iconContainer.transform, false);
                Image spriteImg = iconImgObj.AddComponent<Image>();
                spriteImg.sprite = buffSprite;
                spriteImg.preserveAspect = true;

                RectTransform spriteRect = iconImgObj.GetComponent<RectTransform>();
                spriteRect.anchorMin = new Vector2(0.5f, 0.5f);
                spriteRect.anchorMax = new Vector2(0.5f, 0.5f);
                spriteRect.sizeDelta = new Vector2(64, 64);
                spriteRect.anchoredPosition = Vector2.zero;
            }

            // --- 4.3 Buff Name ---
            GameObject nameObj = new GameObject("BuffName");
            nameObj.transform.SetParent(cardObj.transform, false);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = buff.buffName;
            if (fontAsset != null) nameText.font = fontAsset;
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white; // #FFFFFF sắc nét
            nameText.characterSpacing = 1.5f;

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.54f);
            nameRect.anchorMax = new Vector2(0.5f, 0.54f);
            nameRect.sizeDelta = new Vector2(270, 34);
            nameRect.anchoredPosition = Vector2.zero;

            // --- 4.4 Stat Chip Badge (+20% DAMAGE, +2 MAX HP...) ---
            GameObject chipBadge = new GameObject("StatChipBadge");
            chipBadge.transform.SetParent(cardObj.transform, false);
            RectTransform chipRect = chipBadge.AddComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(0.5f, 0.44f);
            chipRect.anchorMax = new Vector2(0.5f, 0.44f);
            chipRect.sizeDelta = new Vector2(240, 32);
            chipRect.anchoredPosition = Vector2.zero;

            Image chipBg = chipBadge.AddComponent<Image>();
            chipBg.color = new Color(0.10f, 0.16f, 0.26f, 1f);

            Outline chipOutline = chipBadge.AddComponent<Outline>();
            chipOutline.effectColor = new Color(0.32f, 0.48f, 0.65f, 0.9f);
            chipOutline.effectDistance = new Vector2(1, 1);

            GameObject chipTextObj = new GameObject("StatChipText");
            chipTextObj.transform.SetParent(chipBadge.transform, false);
            TextMeshProUGUI chipText = chipTextObj.AddComponent<TextMeshProUGUI>();
            chipText.text = GetStatChipText(buff);
            if (fontAsset != null) chipText.font = fontAsset;
            chipText.fontSize = 15;
            chipText.fontStyle = FontStyles.Bold;
            chipText.alignment = TextAlignmentOptions.Center;
            chipText.color = new Color(0.35f, 0.90f, 1f, 1f); // #59E6FF Cyan sáng nổi bật
            chipText.characterSpacing = 1f;

            RectTransform chipTextRect = chipTextObj.GetComponent<RectTransform>();
            chipTextRect.anchorMin = Vector2.zero;
            chipTextRect.anchorMax = Vector2.one;
            chipTextRect.sizeDelta = Vector2.zero;

            // --- 4.5 Description ---
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(cardObj.transform, false);
            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = buff.description;
            if (fontAsset != null) descText.font = fontAsset;
            descText.fontSize = 15;
            descText.fontStyle = FontStyles.Bold; // Bolding giúp font pixel không bị mờ ở độ phân giải nhỏ
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = new Color(0.96f, 0.97f, 0.99f, 1f); // Trắng sáng rõ nét
            descText.lineSpacing = 6f;
            descText.characterSpacing = 0.5f;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0.27f);
            descRect.anchorMax = new Vector2(0.5f, 0.27f);
            descRect.sizeDelta = new Vector2(265, 80);
            descRect.anchoredPosition = Vector2.zero;

            // --- 4.6 Bottom [ SELECT ] Button ---
            GameObject selectBtnObj = new GameObject("SelectButton");
            selectBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform selectBtnRect = selectBtnObj.AddComponent<RectTransform>();
            selectBtnRect.anchorMin = new Vector2(0.5f, 0.10f);
            selectBtnRect.anchorMax = new Vector2(0.5f, 0.10f);
            selectBtnRect.sizeDelta = new Vector2(240, 44);
            selectBtnRect.anchoredPosition = Vector2.zero;

            Image selectBtnImg = selectBtnObj.AddComponent<Image>();
            selectBtnImg.color = new Color(0.15f, 0.22f, 0.33f, 1f); // #1E293B

            Outline selectBtnBorder = selectBtnObj.AddComponent<Outline>();
            selectBtnBorder.effectColor = new Color(0.35f, 0.48f, 0.65f, 1f);
            selectBtnBorder.effectDistance = new Vector2(1.5f, 1.5f);

            Button selectBtn = selectBtnObj.AddComponent<Button>();
            ColorBlock cb = selectBtn.colors;
            cb.normalColor = new Color(0.15f, 0.22f, 0.33f, 1f);
            cb.highlightedColor = new Color(0.24f, 0.33f, 0.48f, 1f);
            cb.pressedColor = new Color(0.05f, 0.65f, 0.91f, 1f);
            selectBtn.colors = cb;

            selectBtn.onClick.AddListener(() =>
            {
                SelectBuff(buff, canvasObj, onComplete);
            });

            GameObject selectBtnTextObj = new GameObject("BtnText");
            selectBtnTextObj.transform.SetParent(selectBtnObj.transform, false);
            TextMeshProUGUI selectBtnText = selectBtnTextObj.AddComponent<TextMeshProUGUI>();
            selectBtnText.text = "SELECT";
            if (fontAsset != null) selectBtnText.font = fontAsset;
            selectBtnText.fontSize = 16;
            selectBtnText.fontStyle = FontStyles.Bold;
            selectBtnText.alignment = TextAlignmentOptions.Center;
            selectBtnText.color = Color.white;
            selectBtnText.characterSpacing = 2f;

            RectTransform selectBtnTextRect = selectBtnTextObj.GetComponent<RectTransform>();
            selectBtnTextRect.anchorMin = Vector2.zero;
            selectBtnTextRect.anchorMax = Vector2.one;
            selectBtnTextRect.sizeDelta = Vector2.zero;
        }

        // Kích hoạt hiệu ứng xuất hiện nhẹ nhàng của 3 thẻ
        StartCoroutine(AnimateCardsIntro(spawnedCards));
    }

    private void SelectBuff(BuffConfig buff, GameObject canvasObj, Action onComplete)
    {
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.PlayClickSound();
        }

        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.ApplyBuff(buff);
        }

        // BỔ SUNG: Nếu đang trong phòng Co-op, gửi xác nhận đã chọn xong Buff lên Server (Co-op Buff Barrier)
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer)
        {
            NetworkManager.Instance.SendPlayerBuffSelected(NetworkManager.Instance.CurrentRoomId);
        }

        isMenuOpen = false;
        if (canvasObj != null)
        {
            Destroy(canvasObj);
        }
        Time.timeScale = 1f;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateCardsIntro(List<GameObject> cards)
    {
        foreach (var card in cards)
        {
            if (card != null) card.transform.localScale = Vector3.one * 0.88f;
        }

        float elapsed = 0f;
        float duration = 0.22f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(0.88f, 1f, Mathf.Sin(t * Mathf.PI * 0.5f));

            foreach (var card in cards)
            {
                if (card != null) card.transform.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        foreach (var card in cards)
        {
            if (card != null) card.transform.localScale = Vector3.one;
        }
    }
}

/// <summary>
/// Hiệu ứng nâng nhẹ thẻ lên 8px và tăng sáng viền Slate khi di chuột (Hoạt động mượt ngay cả khi Time.timeScale = 0)
/// </summary>
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Outline outline;
    private Vector2 originalPos;
    private Color originalBorderColor;
    private Color hoverBorderColor = new Color(0.39f, 0.45f, 0.55f, 1f); // #64748B
    private Coroutine animRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        outline = GetComponent<Outline>();
        if (outline != null) originalBorderColor = outline.effectColor;
    }

    private void Start()
    {
        if (rectTransform != null) originalPos = rectTransform.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.PlayHoverSound();
        }

        if (outline != null) outline.effectColor = hoverBorderColor;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateTo(originalPos + new Vector2(0f, 8f)));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (outline != null) outline.effectColor = originalBorderColor;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateTo(originalPos));
    }

    private IEnumerator AnimateTo(Vector2 targetPos)
    {
        if (rectTransform == null) yield break;
        float elapsed = 0f;
        float duration = 0.12f;
        Vector2 startPos = rectTransform.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        rectTransform.anchoredPosition = targetPos;
    }

    private void OnDisable()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        if (rectTransform != null && originalPos != Vector2.zero)
        {
            rectTransform.anchoredPosition = originalPos;
        }
        if (outline != null) outline.effectColor = originalBorderColor;
    }
}
