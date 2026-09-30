using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện Bảng Chọn & Đổi Đặc Nhiệm (Agent Roster / Character Vault)
/// Tự động sinh UI đẹp mắt tương tự như WeaponVaultUIController.
/// </summary>
public class CharacterSelectUIController : MonoBehaviour
{
    public static CharacterSelectUIController Instance { get; private set; }

    [Header("UI References")]
    public GameObject rosterPanel;
    public Button closeButton;
    public Transform cardsContainer;
    public TextMeshProUGUI statusText;
    public Button openShopButton;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (rosterPanel == null)
        {
            BuildAutoRosterUI();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseRoster);
        }

        if (openShopButton != null)
        {
            openShopButton.onClick.RemoveAllListeners();
            openShopButton.onClick.AddListener(OnOpenShopClicked);
        }
    }

    private void Update()
    {
        if (IsRosterOpen() && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseRoster();
        }
    }

    public void OpenRoster()
    {
        if (rosterPanel == null)
        {
            BuildAutoRosterUI();
        }

        if (rosterPanel != null)
        {
            rosterPanel.SetActive(true);
            rosterPanel.transform.SetAsLastSibling();

            Transform dialogTrans = rosterPanel.transform.Find("Dialog");
            if (dialogTrans != null)
            {
                dialogTrans.localScale = new Vector3(1.2f, 1.2f, 1f);
            }
        }

        RefreshCardsDisplay();
        ShopUIController.SyncUnlockedCharactersFromServer(() => RefreshCardsDisplay());
    }

    public void CloseRoster()
    {
        if (rosterPanel != null)
        {
            rosterPanel.SetActive(false);
        }
    }

    public bool IsRosterOpen()
    {
        return rosterPanel != null && rosterPanel.activeSelf;
    }

    private void OnOpenShopClicked()
    {
        CloseRoster();
        if (ShopUIController.Instance != null)
        {
            ShopUIController.Instance.OpenShop();
            ShopUIController.Instance.SwitchTab(2); // Tab 3: CHARACTERS
        }
    }

    /// <summary>
    /// Làm mới danh sách thẻ nhân vật trong Roster
    /// </summary>
    public void RefreshCardsDisplay()
    {
        if (cardsContainer == null) return;

        foreach (Transform child in cardsContainer)
        {
            Destroy(child.gameObject);
        }

        string currentSelected = CharacterManager.Instance != null 
            ? CharacterManager.Instance.GetSelectedCharacter() 
            : "Rookie";

        // 1. Thẻ Rookie (Tân Binh Kie) - Luôn mở khóa mặc định
        CreateCharacterCard(
            charName: "Rookie",
            displayName: "Rookie",
            desc: "Brave standard tactical combat operative. Balanced stats and versatile weapon handling.",
            statsText: "HP: 100   |   Armor: 4   |   Mana: 200",
            specialtyText: "Specialty: All-Round Balanced Combat",
            isSelected: currentSelected.Equals("Rookie", StringComparison.OrdinalIgnoreCase)
        );

        // 2. Thẻ Hero Zero (Cyborg) - CHỈ hiển thị nếu người chơi ĐÃ MỞ KHÓA (tương tự như Kho Vũ Khí)
        bool isZeroUnlocked = ShopUIController.IsCharacterUnlocked("Zero");
        if (isZeroUnlocked)
        {
            CreateCharacterCard(
                charName: "Zero",
                displayName: "Hero Zero",
                desc: "Advanced high-tech cybernetic operative equipped with high mobility Overdrive booster.",
                statsText: "HP: 100   |   Armor: 4   |   Mana: 200",
                specialtyText: "Specialty: Speed Skill (+100% Sprint Boost)",
                isSelected: currentSelected.Equals("Zero", StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    private void CreateCharacterCard(string charName, string displayName, string desc, string statsText, string specialtyText, bool isSelected)
    {
        GameObject card = new GameObject("Card_" + charName, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(cardsContainer, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(340, 440);

        Image img = card.GetComponent<Image>();
        img.color = isSelected ? new Color(0.08f, 0.18f, 0.22f, 0.98f) : new Color(0.08f, 0.11f, 0.18f, 0.98f);

        // Viền thẻ
        GameObject border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(card.transform, false);
        RectTransform bRect = border.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        Image bImg = border.GetComponent<Image>();
        bImg.color = isSelected ? new Color(0.2f, 0.8f, 0.5f, 0.7f) : new Color(0.2f, 0.35f, 0.55f, 0.4f);

        // Khung nền bên trong
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(card.transform, false);
        RectTransform inRect = inner.GetComponent<RectTransform>();
        inRect.anchorMin = Vector2.zero;
        inRect.anchorMax = Vector2.one;
        inRect.sizeDelta = new Vector2(-6, -6);
        Image inImg = inner.GetComponent<Image>();
        inImg.color = isSelected ? new Color(0.06f, 0.15f, 0.18f, 0.98f) : new Color(0.07f, 0.10f, 0.16f, 0.98f);

        // Tên nhân vật (Đặt trên cùng cân đối)
        GameObject nameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(card.transform, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchoredPosition = new Vector2(0, 168);
        nameRect.sizeDelta = new Vector2(300, 36);
        TextMeshProUGUI nameTxt = nameObj.GetComponent<TextMeshProUGUI>();
        nameTxt.text = displayName;
        nameTxt.fontSize = 24;
        nameTxt.color = isSelected ? new Color(0.35f, 1f, 0.7f) : Color.white;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.fontStyle = FontStyles.Bold;

        // Khung Icon chân dung nhân vật
        GameObject iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(Image));
        iconFrame.transform.SetParent(card.transform, false);
        RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
        frameRect.anchoredPosition = new Vector2(0, 78);
        frameRect.sizeDelta = new Vector2(130, 130);
        Image frameImg = iconFrame.GetComponent<Image>();
        frameImg.color = new Color(0.04f, 0.06f, 0.10f, 0.95f);

        // Viền tinh tế quanh khung ảnh chân dung
        GameObject frameBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
        frameBorder.transform.SetParent(iconFrame.transform, false);
        RectTransform fbRect = frameBorder.GetComponent<RectTransform>();
        fbRect.anchorMin = Vector2.zero;
        fbRect.anchorMax = Vector2.one;
        fbRect.sizeDelta = new Vector2(2, 2);
        Image fbImg = frameBorder.GetComponent<Image>();
        fbImg.color = isSelected ? new Color(0.3f, 0.9f, 0.6f, 0.6f) : new Color(0.2f, 0.5f, 0.8f, 0.35f);
        frameBorder.transform.SetAsFirstSibling();

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(iconFrame.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = new Vector2(-12, -12);
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;

        Sprite charSprite = GetCharacterSprite(charName);
        if (charSprite != null)
        {
            iconImg.sprite = charSprite;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = Color.clear;
        }

        // Mô tả nhân vật
        GameObject descObj = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(card.transform, false);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchoredPosition = new Vector2(0, -20);
        descRect.sizeDelta = new Vector2(290, 48);
        TextMeshProUGUI descTxt = descObj.GetComponent<TextMeshProUGUI>();
        descTxt.text = desc;
        descTxt.fontSize = 12;
        descTxt.color = new Color(0.75f, 0.85f, 0.95f);
        descTxt.alignment = TextAlignmentOptions.Center;

        // Chỉ số Stats
        GameObject statsObj = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsObj.transform.SetParent(card.transform, false);
        RectTransform statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchoredPosition = new Vector2(0, -60);
        statsRect.sizeDelta = new Vector2(300, 24);
        TextMeshProUGUI statsTxt = statsObj.GetComponent<TextMeshProUGUI>();
        statsTxt.text = statsText;
        statsTxt.fontSize = 12;
        statsTxt.color = new Color(1f, 0.85f, 0.35f);
        statsTxt.alignment = TextAlignmentOptions.Center;
        statsTxt.fontStyle = FontStyles.Bold;

        // Kỹ năng đặc biệt
        GameObject specObj = new GameObject("Specialty", typeof(RectTransform), typeof(TextMeshProUGUI));
        specObj.transform.SetParent(card.transform, false);
        RectTransform specRect = specObj.GetComponent<RectTransform>();
        specRect.anchoredPosition = new Vector2(0, -90);
        specRect.sizeDelta = new Vector2(300, 24);
        TextMeshProUGUI specTxt = specObj.GetComponent<TextMeshProUGUI>();
        specTxt.text = specialtyText;
        specTxt.fontSize = 12;
        specTxt.color = new Color(0.4f, 0.9f, 1f);
        specTxt.alignment = TextAlignmentOptions.Center;

        // Nút bấm Hành động (Action Button)
        GameObject btnObj = new GameObject("BtnAction", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(card.transform, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(0, -155);
        btnRect.sizeDelta = new Vector2(270, 46);

        Image btnImg = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();

        GameObject btnTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTxtRect = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRect.anchorMin = Vector2.zero;
        btnTxtRect.anchorMax = Vector2.one;
        btnTxtRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI btnTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTxt.fontSize = 15;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.fontStyle = FontStyles.Bold;

        if (isSelected)
        {
            btnImg.color = new Color(0.18f, 0.45f, 0.25f, 0.85f);
            btnTxt.text = "DEPLOYED (CURRENT)";
            btnTxt.color = new Color(0.5f, 1f, 0.6f);
            btn.interactable = false;
        }
        else
        {
            btnImg.color = new Color(0.12f, 0.48f, 0.78f);
            btnTxt.text = "DEPLOY AGENT";
            btnTxt.color = Color.white;
            btn.interactable = true;
            btn.onClick.AddListener(() => EquipCharacter(charName, displayName));
        }
    }

    private void EquipCharacter(string charName, string displayName)
    {
        if (CharacterManager.Instance == null)
        {
            Debug.LogWarning("[CharacterSelectUI] CharacterManager.Instance không tồn tại!");
            return;
        }

        bool success = CharacterManager.Instance.SwitchCharacter(charName);
        if (success)
        {
            if (statusText != null)
            {
                statusText.text = $"<color=#55FF88>Operative '{displayName}' deployed successfully!</color>";
            }

            RefreshCardsDisplay();
            CloseRoster();
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = $"<color=red>Failed to deploy '{displayName}'!</color>";
            }
        }
    }

    /// <summary>
    /// Nạp Sprite hình ảnh chân dung nhân vật
    /// </summary>
    public static Sprite GetCharacterSprite(string charName)
    {
        return ShopUIController.GetCharacterSprite(charName);
    }

    /// <summary>
    /// Tự động khởi tạo toàn bộ giao diện Modal Agent Roster
    /// </summary>
    private void BuildAutoRosterUI()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        GameObject canvasObj;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            canvasObj = canvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("RosterUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 99;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // 1. Panel đen mờ nền
        rosterPanel = new GameObject("AgentRosterPanel", typeof(RectTransform), typeof(Image));
        rosterPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = rosterPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImg = rosterPanel.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.88f);

        // 2. Khung Dialog trung tâm
        GameObject dialog = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(rosterPanel.transform, false);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(860, 580);
        dialogRect.anchoredPosition = Vector2.zero;
        dialog.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        Image dialogImg = dialog.GetComponent<Image>();
        dialogImg.color = new Color(0.06f, 0.09f, 0.15f, 0.98f);

        // Viền phát sáng cho Dialog
        GameObject dialogBorder = new GameObject("Border", typeof(RectTransform), typeof(Image));
        dialogBorder.transform.SetParent(dialog.transform, false);
        RectTransform dbRect = dialogBorder.GetComponent<RectTransform>();
        dbRect.anchorMin = Vector2.zero;
        dbRect.anchorMax = Vector2.one;
        dbRect.sizeDelta = new Vector2(4, 4);
        Image dbImg = dialogBorder.GetComponent<Image>();
        dbImg.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        dialogBorder.transform.SetAsFirstSibling();

        // 3. Thanh tiêu đề trên cùng (Header Bar)
        GameObject header = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(dialog.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 255);
        headerRect.sizeDelta = new Vector2(860, 60);

        Image headerImg = header.GetComponent<Image>();
        headerImg.color = new Color(0.10f, 0.15f, 0.25f, 1f);

        // Chữ Tiêu đề
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 8);
        titleRect.sizeDelta = new Vector2(500, 32);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "AGENT ROSTER";
        titleText.fontSize = 24;
        titleText.color = new Color(0.3f, 0.85f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;

        // Chữ phụ đề
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(header.transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0, -14);
        subRect.sizeDelta = new Vector2(600, 20);

        TextMeshProUGUI subText = subObj.GetComponent<TextMeshProUGUI>();
        subText.text = "Select an operative to deploy in the battlefield";
        subText.fontSize = 12;
        subText.color = new Color(0.6f, 0.75f, 0.9f);
        subText.alignment = TextAlignmentOptions.Center;

        // Nút Đóng [X]
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(header.transform, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(400, 0);
        closeRect.sizeDelta = new Vector2(38, 38);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.color = new Color(0.8f, 0.22f, 0.22f);

        closeButton = closeObj.GetComponent<Button>();
        closeButton.onClick.AddListener(CloseRoster);

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 18;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        // 4. Khung chứa các thẻ nhân vật (Cards Container)
        GameObject cardsObj = new GameObject("CardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        cardsObj.transform.SetParent(dialog.transform, false);
        RectTransform cardsRect = cardsObj.GetComponent<RectTransform>();
        cardsRect.anchoredPosition = new Vector2(0, -10);
        cardsRect.sizeDelta = new Vector2(800, 450);

        HorizontalLayoutGroup layout = cardsObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 30;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        cardsContainer = cardsObj.transform;

        // 5. Thanh đáy: Status & Nút Mở Cửa Hàng
        GameObject footerObj = new GameObject("Footer", typeof(RectTransform));
        footerObj.transform.SetParent(dialog.transform, false);
        RectTransform footerRect = footerObj.GetComponent<RectTransform>();
        footerRect.anchoredPosition = new Vector2(0, -255);
        footerRect.sizeDelta = new Vector2(820, 40);

        GameObject statObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statObj.transform.SetParent(footerObj.transform, false);
        RectTransform statRect = statObj.GetComponent<RectTransform>();
        statRect.anchoredPosition = new Vector2(-150, 0);
        statRect.sizeDelta = new Vector2(500, 30);

        statusText = statObj.GetComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.fontSize = 14;
        statusText.alignment = TextAlignmentOptions.Left;

        GameObject shopBtnObj = new GameObject("OpenShopBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        shopBtnObj.transform.SetParent(footerObj.transform, false);
        RectTransform shopBtnRect = shopBtnObj.GetComponent<RectTransform>();
        shopBtnRect.anchoredPosition = new Vector2(330, 0);
        shopBtnRect.sizeDelta = new Vector2(140, 34);

        Image shopBtnImg = shopBtnObj.GetComponent<Image>();
        shopBtnImg.color = new Color(0.2f, 0.45f, 0.75f);

        openShopButton = shopBtnObj.GetComponent<Button>();
        openShopButton.onClick.AddListener(OnOpenShopClicked);

        GameObject shopBtnTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        shopBtnTxtObj.transform.SetParent(shopBtnObj.transform, false);
        RectTransform shopTxtRect = shopBtnTxtObj.GetComponent<RectTransform>();
        shopTxtRect.anchorMin = Vector2.zero;
        shopTxtRect.anchorMax = Vector2.one;
        shopTxtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI shopBtnTxt = shopBtnTxtObj.GetComponent<TextMeshProUGUI>();
        shopBtnTxt.text = "MORE AGENTS";
        shopBtnTxt.fontSize = 12;
        shopBtnTxt.color = Color.white;
        shopBtnTxt.alignment = TextAlignmentOptions.Center;
        shopBtnTxt.fontStyle = FontStyles.Bold;

        rosterPanel.SetActive(false);
    }
}
