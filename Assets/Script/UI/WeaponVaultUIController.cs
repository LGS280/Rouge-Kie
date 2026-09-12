using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponVaultUIController : MonoBehaviour
{
    public static WeaponVaultUIController Instance { get; private set; }

    [Header("UI Panels & References")]
    public GameObject vaultPanel;
    public Button closeVaultButton;
    public Transform weaponGridContent;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI emptyNoticeText;
    public Button openShopQuickButton;

    [Serializable]
    public class UnlockedWeaponDto
    {
        public int weaponConfigId;
        public string weaponName;
        public string prefabName;
        public string weaponType;
        public string rarity;
        public bool isUnlocked;
        public string unlockedAt;
    }

    [Serializable]
    public class UnlockedWeaponListWrapper
    {
        public List<UnlockedWeaponDto> items;
    }

    // Bộ nhớ cache các vũ khí đã mở khóa (lưu theo prefabName, ví dụ: "AK_47A_Gold")
    private HashSet<string> unlockedWeaponPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private const string PREFS_KEY_UNLOCKED = "unlocked_weapons";

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

        LoadUnlockedWeaponsFromPrefs();
    }

    private void Start()
    {
        if (vaultPanel == null)
        {
            BuildAutoVaultUI();
        }

        if (closeVaultButton != null)
        {
            closeVaultButton.onClick.RemoveAllListeners();
            closeVaultButton.onClick.AddListener(CloseVault);
        }

        if (openShopQuickButton != null)
        {
            openShopQuickButton.onClick.RemoveAllListeners();
            openShopQuickButton.onClick.AddListener(OnOpenShopClicked);
        }

        // Đồng bộ danh sách từ server khi bắt đầu
        FetchUnlockedWeapons();
    }

    public void OpenVault()
    {
        if (vaultPanel == null)
        {
            BuildAutoVaultUI();
        }

        if (vaultPanel != null)
        {
            vaultPanel.SetActive(true);
            vaultPanel.transform.SetAsLastSibling();
        }

        RefreshCardsDisplay();
        FetchUnlockedWeapons();
    }

    public void CloseVault()
    {
        if (vaultPanel != null)
        {
            vaultPanel.SetActive(false);
        }
    }

    public bool IsVaultOpen()
    {
        return vaultPanel != null && vaultPanel.activeSelf;
    }

    private void OnOpenShopClicked()
    {
        CloseVault();
        ShopUIController shop = ShopUIController.Instance;
        if (shop == null) shop = UnityEngine.Object.FindFirstObjectByType<ShopUIController>();
        if (shop != null)
        {
            shop.OpenShop();
        }
    }

    /// <summary>
    /// Nạp danh sách vũ khí mở khóa từ PlayerPrefs bộ nhớ tạm offline
    /// </summary>
    private void LoadUnlockedWeaponsFromPrefs()
    {
        string saved = PlayerPrefs.GetString(PREFS_KEY_UNLOCKED, "");
        if (!string.IsNullOrEmpty(saved))
        {
            string[] items = saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                string clean = CleanPrefabName(item);
                if (!string.IsNullOrEmpty(clean))
                {
                    unlockedWeaponPrefabs.Add(clean);
                }
            }
        }
    }

    /// <summary>
    /// Lưu cache danh sách vũ khí mở khóa vào PlayerPrefs
    /// </summary>
    private void SaveUnlockedWeaponsToPrefs()
    {
        string combined = string.Join(",", unlockedWeaponPrefabs);
        PlayerPrefs.SetString(PREFS_KEY_UNLOCKED, combined);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Kiểm tra một vũ khí đã được mở khóa chưa
    /// </summary>
    public bool IsWeaponUnlocked(string prefabName)
    {
        string clean = CleanPrefabName(prefabName);
        return unlockedWeaponPrefabs.Contains(clean);
    }

    /// <summary>
    /// Đăng ký mở khóa một vũ khí (gọi sau khi mua tại Shop hoặc thanh toán VietQR thành công)
    /// </summary>
    public void RegisterUnlockedWeapon(string prefabName)
    {
        string clean = CleanPrefabName(prefabName);
        if (string.IsNullOrEmpty(clean)) return;

        if (!unlockedWeaponPrefabs.Contains(clean))
        {
            unlockedWeaponPrefabs.Add(clean);
            SaveUnlockedWeaponsToPrefs();
            Debug.Log($"[WeaponVaultUI] Đã mở khóa vĩnh viễn vũ khí vào Kho: {clean}");
        }

        if (vaultPanel != null && vaultPanel.activeSelf)
        {
            RefreshCardsDisplay();
        }
    }

    /// <summary>
    /// Gọi API Backend để lấy danh sách vũ khí đã mở khóa của người chơi
    /// </summary>
    public void FetchUnlockedWeapons()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrEmpty(token) || ApiClient.Instance == null)
        {
            // Offline Mode: Chỉ hiển thị dữ liệu lưu trong PlayerPrefs
            RefreshCardsDisplay();
            return;
        }

        if (statusText != null) statusText.text = "Đang đồng bộ Kho Vũ Khí...";

        ApiClient.Instance.Get("/PlayerWeapons/my-weapons", (json) =>
        {
            try
            {
                string wrappedJson = "{\"items\":" + json + "}";
                UnlockedWeaponListWrapper wrapper = JsonUtility.FromJson<UnlockedWeaponListWrapper>(wrappedJson);

                if (wrapper != null && wrapper.items != null)
                {
                    unlockedWeaponPrefabs.Clear();
                    foreach (var item in wrapper.items)
                    {
                        if (item.isUnlocked && !string.IsNullOrEmpty(item.prefabName))
                        {
                            string clean = CleanPrefabName(item.prefabName);
                            unlockedWeaponPrefabs.Add(clean);
                        }
                    }

                    SaveUnlockedWeaponsToPrefs();
                    Debug.Log($"[WeaponVaultUI] Đã đồng bộ thành công {wrapper.items.Count} vũ khí từ Server.");
                }

                if (statusText != null) statusText.text = "Kho Vũ Khí sẵn sàng!";
                RefreshCardsDisplay();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponVaultUI] Lỗi parse dữ liệu vũ khí từ server: {ex.Message}");
                RefreshCardsDisplay();
            }
        }, (err) =>
        {
            Debug.LogWarning($"[WeaponVaultUI] Không thể kết nối tới server (chuyển sang Offline Cache): {err}");
            if (statusText != null) statusText.text = "Chế độ Kho Offline";
            RefreshCardsDisplay();
        });
    }

    /// <summary>
    /// Cập nhật lại giao diện danh sách thẻ vũ khí
    /// </summary>
    public void RefreshCardsDisplay()
    {
        if (weaponGridContent == null) return;

        // Xóa các card cũ
        foreach (Transform child in weaponGridContent)
        {
            Destroy(child.gameObject);
        }

        bool hasAny = unlockedWeaponPrefabs.Count > 0;

        if (emptyNoticeText != null) emptyNoticeText.gameObject.SetActive(!hasAny);
        if (openShopQuickButton != null) openShopQuickButton.gameObject.SetActive(!hasAny);

        if (!hasAny)
        {
            return;
        }

        foreach (string prefabName in unlockedWeaponPrefabs)
        {
            CreateWeaponVaultCard(prefabName);
        }
    }

    private void CreateWeaponVaultCard(string prefabName)
    {
        string clean = CleanPrefabName(prefabName);
        string displayName = FormatDisplayName(clean);
        Sprite weaponSprite = LoadWeaponSprite(clean);

        // Kiểm tra xem người chơi hiện tại có đang cầm súng này trên tay/lưng không
        bool isEquipped = IsCurrentlyEquipped(clean);

        GameObject card = new GameObject("Card_" + clean, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(weaponGridContent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(230, 310);

        Image img = card.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.24f, 0.98f);

        // Khung viền phát sáng nhẹ
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = isEquipped ? new Color(0.2f, 0.8f, 0.5f, 0.8f) : new Color(0.3f, 0.45f, 0.7f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Tiêu đề Tên Vũ Khí
        GameObject tObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        tObj.transform.SetParent(card.transform, false);
        RectTransform tRect = tObj.GetComponent<RectTransform>();
        tRect.anchoredPosition = new Vector2(0, 115);
        tRect.sizeDelta = new Vector2(210, 30);
        TextMeshProUGUI tTxt = tObj.GetComponent<TextMeshProUGUI>();
        tTxt.text = displayName;
        tTxt.fontSize = 16;
        tTxt.color = new Color(1f, 0.85f, 0.35f);
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.fontStyle = FontStyles.Bold;

        // Icon Vũ Khí
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(card.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(0, 45);
        iconRect.sizeDelta = new Vector2(130, 80);
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;
        if (weaponSprite != null)
        {
            iconImg.sprite = weaponSprite;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = new Color(1f, 1f, 1f, 0.2f);
        }

        // Nhãn phân loại / Đã mở khóa
        GameObject tagObj = new GameObject("Tag", typeof(RectTransform), typeof(TextMeshProUGUI));
        tagObj.transform.SetParent(card.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchoredPosition = new Vector2(0, -30);
        tagRect.sizeDelta = new Vector2(200, 25);
        TextMeshProUGUI tagTxt = tagObj.GetComponent<TextMeshProUGUI>();
        tagTxt.text = "<color=#40ff40>✓ ĐÃ MỞ KHÓA</color>";
        tagTxt.fontSize = 13;
        tagTxt.alignment = TextAlignmentOptions.Center;
        tagTxt.fontStyle = FontStyles.Bold;

        // Nút Trang Bị [EQUIP] / [EQUIPPED]
        GameObject btnObj = new GameObject("EquipBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(card.transform, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(0, -95);
        btnRect.sizeDelta = new Vector2(180, 42);

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

        if (isEquipped)
        {
            btnImg.color = new Color(0.25f, 0.35f, 0.3f, 0.8f);
            btnTxt.text = "EQUIPPED";
            btnTxt.color = new Color(0.5f, 0.9f, 0.6f);
            btn.interactable = false;
        }
        else
        {
            btnImg.color = new Color(0.18f, 0.62f, 0.32f);
            btnTxt.text = "EQUIP";
            btnTxt.color = Color.white;
            btn.interactable = true;
            btn.onClick.AddListener(() => EquipWeapon(clean, displayName));
        }
    }

    /// <summary>
    /// Thực hiện trang bị vũ khí đã mở khóa vào tay nhân vật
    /// </summary>
    private void EquipWeapon(string prefabName, string displayName)
    {
        if (WeaponManager.Instance == null)
        {
            Debug.LogWarning("[WeaponVaultUI] WeaponManager.Instance không tồn tại!");
            return;
        }

        GameObject prefab = WeaponManager.Instance.FindWeaponPrefabByName(prefabName);

#if UNITY_EDITOR
        if (prefab == null)
        {
            string editorPath = $"Assets/Prefab/Weapons/{prefabName}.prefab";
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
        }
#endif

        if (prefab == null)
        {
            Debug.LogError($"[WeaponVaultUI] Không tìm thấy Prefab cho súng: {prefabName}");
            if (statusText != null) statusText.text = $"<color=red>Lỗi: Không tìm thấy Prefab {prefabName}!</color>";
            return;
        }

        bool success = WeaponManager.Instance.EquipWeaponDirectly(prefab);
        if (success)
        {
            Debug.Log($"[WeaponVaultUI] Đã trang bị thành công '{displayName}' vào tay nhân vật!");
            if (statusText != null) statusText.text = $"<color=green>Đã trang bị {displayName}!</color>";

            // Làm mới các nút thẻ
            RefreshCardsDisplay();
        }
    }

    private bool IsCurrentlyEquipped(string cleanPrefabName)
    {
        if (WeaponManager.Instance == null) return false;

        string currentSlot1 = GetCleanName(WeaponManager.Instance.weaponSlot1);
        string currentSlot2 = GetCleanName(WeaponManager.Instance.weaponSlot2);

        return currentSlot1.Equals(cleanPrefabName, StringComparison.OrdinalIgnoreCase) ||
               currentSlot2.Equals(cleanPrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private string GetCleanName(GameObject weaponObj)
    {
        if (weaponObj == null) return "";
        WeaponInfo info = weaponObj.GetComponent<WeaponInfo>();
        if (info != null && info.weaponPrefab != null) return info.weaponPrefab.name;
        WeaponLaser laser = weaponObj.GetComponent<WeaponLaser>();
        if (laser != null && laser.weaponPrefab != null) return laser.weaponPrefab.name;
        return weaponObj.name.Replace("(Clone)", "").Trim();
    }

    private string CleanPrefabName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw.Replace("Weapons/", "").Replace("(Clone)", "").Trim();
    }

    private string FormatDisplayName(string cleanName)
    {
        switch (cleanName)
        {
            case "AK_47A_Gold": return "AK-47 Gold";
            case "Missile_Launcher": return "Missile Launcher";
            case "Rocket_Launcher": return "Rocket Launcher";
            case "AK47": return "AK-47 Classic";
            case "Assault_Shotgun": return "Assault Shotgun";
            case "Desert_Eagle": return "Desert Eagle";
            case "Laser_Gun_MK1": return "Laser Gun MK1";
            case "Gold_Katana": return "Gold Katana";
            case "Wooden_Bow": return "Wooden Bow";
            default: return cleanName.Replace("_", " ");
        }
    }

    private Sprite LoadWeaponSprite(string cleanName)
    {
        ShopUIController shop = ShopUIController.Instance;
        if (shop != null)
        {
            Sprite s = shop.GetWeaponSpriteFromPrefab(cleanName);
            if (s != null) return s;
        }

#if UNITY_EDITOR
        string spritePath = $"Assets/Weapons/Player_Weapon/{cleanName}/{cleanName}.png";
        Sprite directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (directSprite != null) return directSprite;

        spritePath = $"Assets/Weapons/{cleanName}.png";
        directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (directSprite != null) return directSprite;

        string prefabPath = $"Assets/Prefab/Weapons/{cleanName}.prefab";
        GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (p != null)
        {
            SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
            if (sr == null) sr = p.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.sprite;
        }
#endif
        return null;
    }

    /// <summary>
    /// Tự động khởi tạo toàn bộ giao diện Modal Kho Vũ Khí (Weapon Armory Vault)
    /// </summary>
    private void BuildAutoVaultUI()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        GameObject canvasObj;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            canvasObj = canvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("VaultUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 99;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // Panel đen mờ nền
        vaultPanel = new GameObject("VaultPanel", typeof(RectTransform), typeof(Image));
        vaultPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = vaultPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImg = vaultPanel.GetComponent<Image>();
        panelImg.color = new Color(0.02f, 0.04f, 0.07f, 0.88f);

        // Khung Dialog chính
        GameObject dialog = new GameObject("VaultDialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(vaultPanel.transform, false);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(880, 580);
        dialogRect.anchoredPosition = Vector2.zero;

        Image dialogImg = dialog.GetComponent<Image>();
        dialogImg.color = new Color(0.09f, 0.13f, 0.20f, 0.98f);

        Outline dlgOutline = dialog.AddComponent<Outline>();
        dlgOutline.effectColor = new Color(0.25f, 0.4f, 0.65f, 0.8f);
        dlgOutline.effectDistance = new Vector2(2, -2);

        // Tiêu đề Header
        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(dialog.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 245);
        headerRect.sizeDelta = new Vector2(650, 45);

        TextMeshProUGUI headerTxt = headerObj.GetComponent<TextMeshProUGUI>();
        headerTxt.text = "WEAPON ARMORY VAULT";
        headerTxt.fontSize = 26;
        headerTxt.color = new Color(1f, 0.85f, 0.35f);
        headerTxt.alignment = TextAlignmentOptions.Center;
        headerTxt.fontStyle = FontStyles.Bold;

        // Subtitle hướng dẫn
        GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(dialog.transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0, 210);
        subRect.sizeDelta = new Vector2(700, 30);
        TextMeshProUGUI subTxt = subObj.GetComponent<TextMeshProUGUI>();
        subTxt.text = "Chọn vũ khí đã mở khóa vĩnh viễn để trang bị cho chuyến thám hiểm!";
        subTxt.fontSize = 14;
        subTxt.color = new Color(0.75f, 0.82f, 0.9f);
        subTxt.alignment = TextAlignmentOptions.Center;

        // Nút Đóng [X]
        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(dialog.transform, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(400, 245);
        closeRect.sizeDelta = new Vector2(38, 38);
        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.color = new Color(0.75f, 0.2f, 0.2f);

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 20;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        closeVaultButton = closeObj.GetComponent<Button>();
        closeVaultButton.onClick.AddListener(CloseVault);

        // Khung cuộn / Grid chứa các Card súng
        GameObject gridObj = new GameObject("WeaponGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        gridObj.transform.SetParent(dialog.transform, false);
        RectTransform gridRect = gridObj.GetComponent<RectTransform>();
        gridRect.anchoredPosition = new Vector2(0, 0);
        gridRect.sizeDelta = new Vector2(820, 340);

        HorizontalLayoutGroup layout = gridObj.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        weaponGridContent = gridObj.transform;

        // Thông báo khi kho trống (Empty Notice)
        GameObject emptyObj = new GameObject("EmptyNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObj.transform.SetParent(dialog.transform, false);
        RectTransform emptyRect = emptyObj.GetComponent<RectTransform>();
        emptyRect.anchoredPosition = new Vector2(0, 30);
        emptyRect.sizeDelta = new Vector2(650, 70);
        emptyNoticeText = emptyObj.GetComponent<TextMeshProUGUI>();
        emptyNoticeText.text = "Kho Vũ Khí hiện đang trống!\nHãy ghé Cửa Hàng (Shop) mua súng để mở khóa vĩnh viễn.";
        emptyNoticeText.fontSize = 18;
        emptyNoticeText.color = new Color(0.9f, 0.7f, 0.3f);
        emptyNoticeText.alignment = TextAlignmentOptions.Center;
        emptyNoticeText.fontStyle = FontStyles.Bold;

        // Nút Mở Shop Nhanh
        GameObject shopBtnObj = new GameObject("QuickShopBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        shopBtnObj.transform.SetParent(dialog.transform, false);
        RectTransform shopBtnRect = shopBtnObj.GetComponent<RectTransform>();
        shopBtnRect.anchoredPosition = new Vector2(0, -50);
        shopBtnRect.sizeDelta = new Vector2(220, 48);
        Image shopBtnImg = shopBtnObj.GetComponent<Image>();
        shopBtnImg.color = new Color(0.2f, 0.5f, 0.8f);

        GameObject shopBtnTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        shopBtnTxtObj.transform.SetParent(shopBtnObj.transform, false);
        RectTransform shopBtnTxtRect = shopBtnTxtObj.GetComponent<RectTransform>();
        shopBtnTxtRect.anchorMin = Vector2.zero;
        shopBtnTxtRect.anchorMax = Vector2.one;
        shopBtnTxtRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI shopBtnTxt = shopBtnTxtObj.GetComponent<TextMeshProUGUI>();
        shopBtnTxt.text = "ĐẾN CỬA HÀNG SHOP";
        shopBtnTxt.fontSize = 15;
        shopBtnTxt.color = Color.white;
        shopBtnTxt.alignment = TextAlignmentOptions.Center;
        shopBtnTxt.fontStyle = FontStyles.Bold;

        openShopQuickButton = shopBtnObj.GetComponent<Button>();
        openShopQuickButton.onClick.AddListener(OnOpenShopClicked);

        vaultPanel.SetActive(false);
    }

    /// <summary>
    /// Xóa sạch dữ liệu Kho Vũ Khí cả trên Client lẫn Server (phục vụ Testing)
    /// </summary>
    public static void ResetArmoryTestData()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY_UNLOCKED);
        PlayerPrefs.Save();
        Debug.Log("[WeaponVaultUI] Đã xóa PlayerPrefs 'unlocked_weapons'!");

        if (Instance != null)
        {
            Instance.unlockedWeaponPrefabs.Clear();
            Instance.RefreshCardsDisplay();
        }

        string token = PlayerPrefs.GetString("jwt_token", "");
        if (!string.IsNullOrEmpty(token) && ApiClient.Instance != null)
        {
            ApiClient.Instance.Post("/PlayerWeapons/dev-reset-my-weapons", "{}", (json) =>
            {
                Debug.Log("[WeaponVaultUI] Đã xóa sạch dữ liệu Kho & Transactions trên Server!");
            }, (err) =>
            {
                Debug.LogWarning($"[WeaponVaultUI] Không thể gọi reset server: {err}");
            });
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Rogue-Kie/Reset Kho Vu Khi (Clear Cache & DB Test)")]
    public static void DevMenuItemResetArmory()
    {
        ResetArmoryTestData();
    }
#endif
}
