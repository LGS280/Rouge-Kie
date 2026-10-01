using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý HUD thông số người chơi (Máu, Giáp, Mana) và danh sách Buff đang sở hữu
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Status Bars")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider armorBar;
    [SerializeField] private Slider manaBar;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI manaText;

    [Header("Active Buffs Display")]
    [SerializeField] private Transform buffContainer;

    private void OnEnable()
    {
        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.OnBuffsChanged += RefreshBuffIcons;
        }
        RefreshBuffIcons();
        if (currentTarget != null)
        {
            UpdateHUDVisuals();
        }
    }

    private void OnDisable()
    {
        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.OnBuffsChanged -= RefreshBuffIcons;
        }
    }

    private RookieHealth currentTarget;

    private void Awake()
    {
        AlignHUDLayout();
    }

    private void Start()
    {
        AlignHUDLayout();
        RefreshBuffIcons();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            RookieHealth rh = p.GetComponent<RookieHealth>();
            if (rh != null && rh != currentTarget)
            {
                SetTarget(rh);
            }
        }
    }

    public void SetTarget(RookieHealth target)
    {
        if (target == null) return;

        if (currentTarget != null)
        {
            currentTarget.onHealthChanged.RemoveListener(UpdateHUDVisuals);
        }

        currentTarget = target;
        currentTarget.onHealthChanged.AddListener(UpdateHUDVisuals);

        // Cập nhật ngay lập tức các thanh trạng thái mà không cần chờ đợi khung hình
        UpdateHUDVisuals();
    }

    public void UpdateHUDVisuals()
    {
        if (currentTarget == null) return;

        int curHp = currentTarget.GetCurrentHealth();
        int maxHp = currentTarget.GetMaxHealth();
        int curArmor = currentTarget.GetCurrentArmor();
        int maxArmor = currentTarget.GetMaxArmor();
        int curMana = currentTarget.GetCurrentMana();
        int maxMana = currentTarget.GetMaxMana();

        if (hpBar != null) hpBar.value = maxHp > 0 ? (float)curHp / maxHp : 1f;
        if (hpText != null) hpText.text = $"{curHp}/{maxHp}";

        if (armorBar != null) armorBar.value = maxArmor > 0 ? (float)curArmor / maxArmor : 1f;
        if (armorText != null) armorText.text = $"{curArmor}/{maxArmor}";

        if (manaBar != null) manaBar.value = maxMana > 0 ? (float)curMana / maxMana : 1f;
        if (manaText != null) manaText.text = $"{curMana}/{maxMana}";
    }

    private void OnDestroy()
    {
        if (currentTarget != null)
        {
            currentTarget.onHealthChanged.RemoveListener(UpdateHUDVisuals);
        }
    }

    /// <summary>
    /// Làm mới danh sách Icon các Buff người chơi đang sở hữu
    /// </summary>
    public void RefreshBuffIcons()
    {
        if (PlayerBuffManager.Instance == null) return;
        EnsureBuffContainer();
        if (buffContainer == null) return;

        // Xóa các badge cũ
        for (int i = buffContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(buffContainer.GetChild(i).gameObject);
        }

        List<BuffConfig> buffs = PlayerBuffManager.Instance.activeBuffs;
        if (buffs == null || buffs.Count == 0) return;

        foreach (var buff in buffs)
        {
            CreateBuffIconBadge(buff);
        }
    }

    private void EnsureBuffContainer()
    {
        if (buffContainer != null)
        {
            ApplyBuffContainerLayout(buffContainer.GetComponent<RectTransform>());
            return;
        }

        Transform existing = transform.Find("BuffHUDContainer");
        if (existing != null)
        {
            buffContainer = existing;
            ApplyBuffContainerLayout(buffContainer.GetComponent<RectTransform>());
            return;
        }

        // Tạo Container linh hoạt nằm HOÀN TOÀN BÊN DƯỚI khung HUD (Panel_PlayerInfo)
        GameObject containerObj = new GameObject("BuffHUDContainer");
        containerObj.transform.SetParent(transform, false);

        RectTransform rect = containerObj.AddComponent<RectTransform>();
        ApplyBuffContainerLayout(rect);

        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        buffContainer = containerObj.transform;
    }

    private void ApplyBuffContainerLayout(RectTransform rect)
    {
        if (rect == null) return;
        // Neo tại cạnh đáy dưới cùng của Panel_PlayerInfo (anchorMin/Max = 0, 0)
        // và đẩy xuống phía dưới thêm 12px để nằm hoàn toàn bên ngoài khung HUD gỗ
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -12f);
        rect.sizeDelta = new Vector2(420f, 36f);
    }

    private void CreateBuffIconBadge(BuffConfig buff)
    {
        if (buff == null || buffContainer == null) return;

        GameObject badgeObj = new GameObject($"BuffBadge_{buff.buffType}");
        badgeObj.transform.SetParent(buffContainer, false);

        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(32f, 32f);

        // Nền tối Slate rõ nét cho badge
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.08f, 0.11f, 0.18f, 0.95f); // #0F172A

        Outline outline = badgeObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.42f, 0.52f, 0.95f); // #475569 viền sáng rõ nét
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Icon Sprite hiển thị bên trong badge
        Sprite iconSprite = UpgradeSelectionUI.GetBuffSprite(buff);
        if (iconSprite != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(badgeObj.transform, false);

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(24f, 24f);
            iconRect.anchoredPosition = Vector2.zero;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitHUD()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "Lobby_Scene" || scene.name == "SampleScene")
            {
                EnsureHUDExists();
            }
        };

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "Lobby_Scene" || currentScene == "SampleScene")
        {
            EnsureHUDExists();
        }
    }

    /// <summary>
    /// Đảm bảo HUD người chơi luôn tồn tại và hiển thị (kể cả trong sảnh chờ Lobby_Scene và chiến đấu)
    /// </summary>
    public static PlayerHUD EnsureHUDExists()
    {
        PlayerHUD existing = UnityEngine.Object.FindFirstObjectByType<PlayerHUD>();
        if (existing != null)
        {
            existing.AlignHUDLayout();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                RookieHealth rh = player.GetComponent<RookieHealth>();
                if (rh != null) existing.SetTarget(rh);
            }
            return existing;
        }

        return BuildAutoHUD();
    }

    private static PlayerHUD BuildAutoHUD()
    {
        GameObject canvasObj = GameObject.Find("HUD_Canvas");
        Canvas canvas = canvasObj != null ? canvasObj.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            canvasObj = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Tạo Panel_PlayerInfo
        GameObject panelObj = new GameObject("Panel_PlayerInfo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObj.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -34.21f);
        panelRect.sizeDelta = new Vector2(480f, 176f);

        Image panelImg = panelObj.GetComponent<Image>();
        Sprite woodSprite = GetWoodFrameSprite();
        if (woodSprite != null)
        {
            panelImg.sprite = woodSprite;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color(0.12f, 0.08f, 0.06f, 0.95f);
        }

        // Tạo 3 thanh chỉ số: Máu, Giáp, Mana căn chuẩn xác từng pixel theo khe khắc khung gỗ (PlayerHUD_Frame_Wood: 240x88 -> UI: 480x176)
        CreateStatSlider(panelObj.transform, "HPBar", new Vector2(32f, -28f), new Vector2(382f, 36f), new Color(0.75f, 0.12f, 0.12f), out Slider hpS, out TextMeshProUGUI hpT);
        CreateStatSlider(panelObj.transform, "ArmorBar", new Vector2(32f, -76f), new Vector2(358f, 36f), new Color(0.58f, 0.65f, 0.72f), out Slider armS, out TextMeshProUGUI armT);
        CreateStatSlider(panelObj.transform, "ManaBar", new Vector2(32f, -124f), new Vector2(334f, 36f), new Color(0.08f, 0.55f, 0.85f), out Slider manaS, out TextMeshProUGUI manaT);

        PlayerHUD hud = panelObj.AddComponent<PlayerHUD>();
        hud.hpBar = hpS;
        hud.hpText = hpT;
        hud.armorBar = armS;
        hud.armorText = armT;
        hud.manaBar = manaS;
        hud.manaText = manaT;

        // Thêm DashCooldownUI để hiển thị hồi chiêu phím F khi chơi Zero
        if (canvasObj.GetComponent<DashCooldownUI>() == null)
        {
            canvasObj.AddComponent<DashCooldownUI>();
        }

        GameObject curPlayer = GameObject.FindGameObjectWithTag("Player");
        if (curPlayer != null)
        {
            RookieHealth rh = curPlayer.GetComponent<RookieHealth>();
            if (rh != null) hud.SetTarget(rh);
        }

        Debug.Log("[PlayerHUD] Đã tự động khởi tạo HUD người chơi trong Scene!");
        return hud;
    }

    private static Sprite GetWoodFrameSprite()
    {
        Sprite s = Resources.Load<Sprite>("PlayerHUD_Frame_Wood");
        if (s != null) return s;

        Sprite[] all = Resources.LoadAll<Sprite>("PlayerHUD_Frame_Wood");
        if (all != null && all.Length > 0) return all[0];

#if UNITY_EDITOR
        Sprite edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/PlayerHUD_Frame_Wood.png");
        if (edSprite != null) return edSprite;

        Sprite[] edAll = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Images/PlayerHUD_Frame_Wood.png")
            .OfType<Sprite>().ToArray();
        if (edAll != null && edAll.Length > 0) return edAll[0];
#endif
        return null;
    }

    private static void CreateStatSlider(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor, out Slider outSlider, out TextMeshProUGUI outText)
    {
        GameObject sliderObj = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(parent, false);

        RectTransform sRect = sliderObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0f, 1f);
        sRect.anchorMax = new Vector2(0f, 1f);
        sRect.pivot = new Vector2(0f, 1f);
        sRect.anchoredPosition = pos;
        sRect.sizeDelta = size;

        // Background
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.055f, 0.04f, 1f); // Màu nền tối đồng nhất với khe khắc gỗ (#140E0A)

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.sizeDelta = Vector2.zero;

        // Fill
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fRect = fillObj.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = fillColor;

        // Text (TMP)
        GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(sliderObj.transform, false);
        RectTransform tRect = txtObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "0/0";
        tmp.fontSize = 17;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.targetGraphic = null;
        slider.fillRect = fRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        outSlider = slider;
        outText = tmp;
    }

    /// <summary>
    /// Căn chỉnh tỉ lệ và vị trí các thanh chỉ số khớp hoàn hảo từng pixel với khung gỗ
    /// </summary>
    public void AlignHUDLayout()
    {
        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -34.21f);
            panelRect.sizeDelta = new Vector2(480f, 176f);
            panelRect.localScale = Vector3.one;
        }

        AlignSlider(hpBar, new Vector2(32f, -28f), new Vector2(382f, 36f));
        AlignSlider(armorBar, new Vector2(32f, -76f), new Vector2(358f, 36f));
        AlignSlider(manaBar, new Vector2(32f, -124f), new Vector2(334f, 36f));
    }

    private static void AlignSlider(Slider slider, Vector2 pos, Vector2 size)
    {
        if (slider == null) return;
        RectTransform sRect = slider.GetComponent<RectTransform>();
        if (sRect != null)
        {
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(0f, 1f);
            sRect.pivot = new Vector2(0f, 1f);
            sRect.anchoredPosition = pos;
            sRect.sizeDelta = size;
            sRect.localScale = Vector3.one;
        }

        if (slider.fillRect != null)
        {
            Transform parent = slider.fillRect.parent;
            if (parent != null && parent != slider.transform)
            {
                RectTransform parentRect = parent.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    parentRect.anchorMin = Vector2.zero;
                    parentRect.anchorMax = Vector2.one;
                    parentRect.anchoredPosition = Vector2.zero;
                    parentRect.sizeDelta = Vector2.zero;
                    parentRect.localScale = Vector3.one;
                }
            }

            slider.fillRect.localScale = Vector3.one;
        }
    }
}