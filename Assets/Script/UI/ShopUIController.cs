using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }

    [Header("UI Panels & Navigation")]
    public GameObject shopPanel;
    public Button closeShopButton;
    public Button gemTabButton;
    public Button coinTabButton;
    public Button skinTabButton;

    public GameObject gemTabContent;
    public GameObject coinTabContent;
    public GameObject skinTabContent;

    [Header("Feedback Status")]
    public TextMeshProUGUI statusText;

    [Serializable]
    public class ShopItemData
    {
        public int shopItemId;
        public string name;
        public string itemType;
        public string description;
        public int price;
        public string currencyType;
    }

    [Serializable]
    public class ShopItemListWrapper
    {
        public List<ShopItemData> items;
    }

    [Serializable]
    public class BuyItemResponseData
    {
        public bool success;
        public string message;
        public int shopItemId;
        public string itemName;
        public int remainingStandardCurrency;
        public int remainingPremiumCurrency;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (shopPanel == null)
        {
            BuildAutoShopUI();
        }

        if (closeShopButton != null)
        {
            closeShopButton.onClick.AddListener(CloseShop);
        }

        if (coinTabButton != null) coinTabButton.onClick.AddListener(() => SwitchTab(0));
        if (gemTabButton != null) gemTabButton.onClick.AddListener(() => SwitchTab(1));
        if (skinTabButton != null) skinTabButton.onClick.AddListener(() => SwitchTab(2));

        SwitchTab(0);
        FetchShopItems();
    }

    public void OpenShop()
    {
        if (shopPanel == null)
        {
            BuildAutoShopUI();
        }
        if (shopPanel != null) shopPanel.SetActive(true);
        FetchShopItems();
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    /// <summary>
    /// Kiểm tra xem giao diện Cửa Hàng có đang mở hay không để vô hiệu hóa di chuyển/xoay súng của nhân vật
    /// </summary>
    public bool IsShopOpen()
    {
        return shopPanel != null && shopPanel.activeSelf;
    }

    public void SwitchTab(int tabIndex)
    {
        if (coinTabContent != null) coinTabContent.SetActive(tabIndex == 0);
        if (gemTabContent != null) gemTabContent.SetActive(tabIndex == 1);
        if (skinTabContent != null) skinTabContent.SetActive(tabIndex == 2);
    }

    public void FetchShopItems()
    {
        if (statusText != null) statusText.text = "Loading Shop Items...";

        ApiClient.Instance.Get("/ShopItems", (json) =>
        {
            try
            {
                // Parse JSON array of shop items
                string wrappedJson = "{\"items\":" + json + "}";
                ShopItemListWrapper wrapper = JsonUtility.FromJson<ShopItemListWrapper>(wrappedJson);

                Debug.Log($"[ShopUIController] Tải thành công {wrapper.items.Count} vật phẩm từ Cửa Hàng!");
                if (statusText != null) statusText.text = "Shop Ready!";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShopUIController] Lỗi parse Shop Items: {ex.Message}");
                if (statusText != null) statusText.text = "Error loading shop items.";
            }
        }, (err) =>
        {
            Debug.LogWarning($"[ShopUIController] Không thể tải danh sách Shop: {err}");
            if (statusText != null) statusText.text = "Offline Shop Mode";
        });
    }

    /// <summary>
    /// Bấm Mua Súng trực tiếp bằng Tiền thật qua PayOS VietQR
    /// </summary>
    public void BuyGemPackage(int amountVnd, string description)
    {

        if (PaymentManager.Instance != null)
        {
            // Lưu lại thông tin súng mua bằng VietQR để sinh ra bàn khi thanh toán xong
            string prefabPath = GetPrefabPathByDescription(description);
            PaymentManager.Instance.pendingBoughtWeaponPrefab = prefabPath;

            PaymentManager.Instance.RequestPayment(amountVnd, description, "GEMS", (res) =>
            {
                if (statusText != null) statusText.text = "VietQR Code generated! Scan to pay.";
            }, (err) =>
            {
                if (statusText != null) statusText.text = "Payment request failed.";
            });
        }
        else
        {
            Debug.LogError("[ShopUIController] PaymentManager.Instance không tồn tại!");
        }
    }

    /// <summary>
    /// Bấm Mua vật phẩm Súng/Skin bằng Coins trong Game qua Backend API
    /// </summary>
    public void BuyShopItem(int shopItemId)
    {
        if (statusText != null) statusText.text = "Processing purchase...";

        string prefabPath = GetPrefabPathByItemId(shopItemId);

        ApiClient.Instance.Post($"/ShopItems/buy/{shopItemId}", "{}", (json) =>
        {
            try
            {
                BuyItemResponseData res = JsonUtility.FromJson<BuyItemResponseData>(json);
                if (res.success)
                {
                    Debug.Log($"[ShopUIController] {res.message}");
                    if (statusText != null) statusText.text = $"<color=green>{res.message}</color>";

                    // Làm mới giao diện hiển thị Coins/Gems của người chơi
                    if (PlayerProfileUI.Instance != null)
                    {
                        PlayerProfileUI.Instance.RefreshProfile();
                    }

                    // Tự động sinh súng vừa mua lên Bàn Trưng Bày
                    SpawnBoughtWeaponOnTable(prefabPath);
                }
                else
                {
                    if (statusText != null) statusText.text = $"<color=red>{res.message}</color>";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShopUIController] Parse error: {ex.Message}");
                // Offline fallback cho dev test
                SpawnBoughtWeaponOnTable(prefabPath);
            }
        }, (err) =>
        {
            Debug.LogWarning($"[ShopUIController] API Buy Item Error (chuyển chế độ Offline Test): {err}");
            if (statusText != null) statusText.text = "<color=green>Offline Purchase Success! Weapon spawned on table!</color>";
            // Offline / Dev Mode Fallback: Tự động sinh súng ngay lên bàn để test mượt mà
            SpawnBoughtWeaponOnTable(prefabPath);
        });
    }

    private GameObject currentSpawnedTableWeapon;

    /// <summary>
    /// Sinh súng vừa mua lên Bàn Trưng Bày (Display Table) xanh trong Lobby
    /// </summary>
    public void SpawnBoughtWeaponOnTable(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.Log("[ShopUIController] Vật phẩm vừa mua không có Prefab súng (Skin hoặc gói Gems).");
            return;
        }

        // 1. Tự động đóng giao diện Cửa Hàng
        CloseShop();

        // 2. Nạp Prefab súng từ WeaponManager hoặc Assets/Prefab/Weapons/ (Không dùng Resources)
        string cleanName = prefabPath.Replace("Weapons/", "").Trim();
        GameObject weaponPrefab = null;

        if (WeaponManager.Instance != null)
        {
            weaponPrefab = WeaponManager.Instance.FindWeaponPrefabByName(cleanName);
        }

#if UNITY_EDITOR
        if (weaponPrefab == null)
        {
            string editorPath = $"Assets/Prefab/Weapons/{cleanName}.prefab";
            weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
        }
#endif

        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[ShopUIController] Không tìm thấy Prefab súng tại đường dẫn: Assets/Prefab/Weapons/{cleanName}.prefab!");
            return;
        }

        // 3. Xóa súng cũ còn dở trên bàn nếu có
        if (currentSpawnedTableWeapon != null)
        {
            Destroy(currentSpawnedTableWeapon);
        }

        // 4. Tìm vị trí chiếc bàn xanh phía trước NPC Shop Merchant
        Vector3 tableSpawnPos = new Vector3(-13.0f, -1.8f, 0f); // Tọa độ mặc định chuẩn chiếc bàn xanh trong Lobby

        GameObject shopNPC = GameObject.Find("ShopMerchant_NPC");
        if (shopNPC != null)
        {
            // Đặt súng lên mặt bàn xanh (nhích sang phải 1.4 unit và nhích lên trên 0.3 unit từ NPC)
            tableSpawnPos = shopNPC.transform.position + new Vector3(1.4f, 0.3f, 0f);
        }

        // 5. Sinh đối tượng GroundWeapon để người chơi lại gần bấm [E] nhặt
        currentSpawnedTableWeapon = GroundWeapon.Create(weaponPrefab, tableSpawnPos);
        if (currentSpawnedTableWeapon != null)
        {
            Debug.Log($"[ShopUIController] Đã sinh súng '{weaponPrefab.name}' thành công tại Bàn Trưng Bày {tableSpawnPos}!");
        }
    }

    public string GetPrefabPathByItemId(int itemId)
    {
        switch (itemId)
        {
            case 1: return "AK_47A_Gold";
            case 2: return "Missile_Launcher";
            case 3: return "Rocket_Launcher";
            default: return "AK_47A_Gold";
        }
    }

    public string GetPrefabPathByDescription(string desc)
    {
        if (string.IsNullOrEmpty(desc)) return "AK_47A_Gold";
        if (desc.Contains("AK-47") || desc.Contains("Gold")) return "AK_47A_Gold";
        if (desc.Contains("Missile")) return "Missile_Launcher";
        if (desc.Contains("Rocket") || desc.Contains("Bazooka")) return "Rocket_Launcher";
        return "AK_47A_Gold";
    }

    /// <summary>
    /// Tìm và nạp Sprite hình ảnh của vũ khí từ thư mục Assets/Weapons/Player_Weapon/ hoặc từ Prefab súng
    /// </summary>
    public Sprite GetWeaponSpriteFromPrefab(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;

        string cleanName = weaponName.Replace("Weapons/", "").Replace("(Clone)", "").Trim();

#if UNITY_EDITOR
        // 1. Ưu tiên tìm trực tiếp file Sprite ảnh súng trong thư mục Assets/Weapons/Player_Weapon/{cleanName}/
        string spritePath = $"Assets/Weapons/Player_Weapon/{cleanName}/{cleanName}.png";
        Sprite directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (directSprite != null) return directSprite;

        // 2. Tìm file Sprite ảnh súng trong Assets/Weapons/
        spritePath = $"Assets/Weapons/{cleanName}.png";
        directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (directSprite != null) return directSprite;
#endif

        // 3. Dự phòng: Tìm từ SpriteRenderer của Prefab súng trong Assets/Prefab/Weapons/
        GameObject prefab = null;
        if (WeaponManager.Instance != null)
        {
            prefab = WeaponManager.Instance.FindWeaponPrefabByName(cleanName);
        }

#if UNITY_EDITOR
        if (prefab == null)
        {
            string editorPath = $"Assets/Prefab/Weapons/{cleanName}.prefab";
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
        }
#endif

        if (prefab != null)
        {
            SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
            if (sr == null) sr = prefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.sprite;
        }

        return null;
    }

    /// <summary>
    /// Tự động xây dựng giao diện Cửa Hàng mua Súng trực tiếp bằng Xu trong game hoặc Tiền thật VietQR
    /// </summary>
    private void BuildAutoShopUI()
    {
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        GameObject canvasObj;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            canvasObj = canvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("ShopUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // 1. Panel nền che mờ toàn màn hình
        shopPanel = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
        shopPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImg = shopPanel.GetComponent<Image>();
        panelImg.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);

        // 2. Khung cửa sổ chính (Shop Dialog)
        GameObject dialog = new GameObject("ShopDialog", typeof(RectTransform), typeof(Image));
        dialog.transform.SetParent(shopPanel.transform, false);

        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.sizeDelta = new Vector2(900, 620);
        dialogRect.anchoredPosition = Vector2.zero;

        Image dialogImg = dialog.GetComponent<Image>();
        dialogImg.color = new Color(0.1f, 0.14f, 0.22f, 0.98f);

        // 3. Tiêu đề Cửa Hàng (Header)
        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(dialog.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 260);
        headerRect.sizeDelta = new Vector2(600, 50);

        TextMeshProUGUI headerTxt = headerObj.GetComponent<TextMeshProUGUI>();
        headerTxt.text = "ARMORY & WEAPON SHOP";
        headerTxt.fontSize = 28;
        headerTxt.color = new Color(1f, 0.85f, 0.3f);
        headerTxt.alignment = TextAlignmentOptions.Center;
        headerTxt.fontStyle = FontStyles.Bold;

        // Nút Đóng [X]
        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(dialog.transform, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(410, 260);
        closeRect.sizeDelta = new Vector2(40, 40);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);

        GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform closeTxtRect = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 22;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        closeShopButton = closeObj.GetComponent<Button>();

        // 4. Thanh Nút Chuyển Tab (Tab Navigation Bar)
        GameObject tabBar = new GameObject("TabBar", typeof(RectTransform));
        tabBar.transform.SetParent(dialog.transform, false);
        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchoredPosition = new Vector2(0, 195);
        tabBarRect.sizeDelta = new Vector2(840, 50);

        coinTabButton = CreateTabButton(tabBar.transform, new Vector2(-280, 0), "COIN WEAPONS");
        gemTabButton = CreateTabButton(tabBar.transform, new Vector2(0, 0), "VIP WEAPONS (VietQR)");
        skinTabButton = CreateTabButton(tabBar.transform, new Vector2(280, 0), "SKINS");

        // 5. Khung chứa Nội dung 3 Tab (Tab Contents)
        coinTabContent = CreateTabContent(dialog.transform, "CoinTabContent");
        gemTabContent = CreateTabContent(dialog.transform, "GemTabContent");
        skinTabContent = CreateTabContent(dialog.transform, "SkinTabContent");

        // 5a. Súng mua bằng Xu trong Game (In-Game Coins)
        CreateShopCard(coinTabContent.transform, "AK-47 Gold", "500 Coins", "High-damage gold-plated assault rifle", "BUY (500 COINS)", "Weapons/AK_47A_Gold", () => BuyShopItem(1));
        CreateShopCard(coinTabContent.transform, "Missile Launcher", "800 Coins", "Long-range homing missile launcher", "BUY (800 COINS)", "Weapons/Missile_Launcher", () => BuyShopItem(2));
        CreateShopCard(coinTabContent.transform, "Rocket Launcher", "1,200 Coins", "Heavy rocket launcher with wide AoE", "BUY (1.2K COINS)", "Weapons/Rocket_Launcher", () => BuyShopItem(3));

        // 5b. Súng VIP mua trực tiếp bằng Tiền Thật qua VietQR (PayOS)
        CreateShopCard(gemTabContent.transform, "AK-47 Gold VIP", "2,000 VND", "Pay via VietQR to unlock AK-47 Gold VIP", "BUY NOW (2K VND)", "Weapons/AK_47A_Gold", () => BuyGemPackage(2000, "Buy AK-47 Gold VIP"));
        CreateShopCard(gemTabContent.transform, "Missile Launcher VIP", "2,000 VND", "Pay via VietQR to unlock Missile Launcher VIP", "BUY NOW (2K VND)", "Weapons/Missile_Launcher", () => BuyGemPackage(2000, "Buy Missile Launcher VIP"));
        CreateShopCard(gemTabContent.transform, "Rocket Launcher VIP", "2,000 VND", "Pay via VietQR to unlock Rocket Launcher VIP", "BUY NOW (2K VND)", "Weapons/Rocket_Launcher", () => BuyGemPackage(2000, "Buy Rocket Launcher VIP"));

        // 5c. Trang Phục Skins
        CreateShopCard(skinTabContent.transform, "Cyber Rookie", "100 Gems", "Rookie warrior battle suit", "BUY (100 GEMS)", "", () => BuyShopItem(4));
        CreateShopCard(skinTabContent.transform, "Hero Zero", "200 Gems", "Zero superhero battle suit", "BUY (200 GEMS)", "", () => BuyShopItem(5));

        // 6. Dòng trạng thái (Status Text)
        GameObject statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(dialog.transform, false);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, -270);
        statusRect.sizeDelta = new Vector2(800, 40);

        statusText = statusObj.GetComponent<TextMeshProUGUI>();
        statusText.text = "Shop Ready!";
        statusText.fontSize = 18;
        statusText.color = new Color(0.4f, 0.9f, 0.4f);
        statusText.alignment = TextAlignmentOptions.Center;

        shopPanel.SetActive(false);
    }

    private Button CreateTabButton(Transform parent, Vector2 pos, string label)
    {
        GameObject btnObj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(260, 45);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.18f, 0.24f, 0.35f);

        GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btnObj.GetComponent<Button>();
    }

    private GameObject CreateTabContent(Transform parent, string name)
    {
        GameObject content = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        content.transform.SetParent(parent, false);

        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, -30);
        rect.sizeDelta = new Vector2(840, 360);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        return content;
    }

    private void CreateShopCard(Transform parent, string title, string price, string desc, string buttonText, string spritePath, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject card = new GameObject("Card_" + title, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 320);

        Image img = card.GetComponent<Image>();
        img.color = new Color(0.15f, 0.19f, 0.28f);

        // Title
        GameObject tObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        tObj.transform.SetParent(card.transform, false);
        RectTransform tRect = tObj.GetComponent<RectTransform>();
        tRect.anchoredPosition = new Vector2(0, 125);
        tRect.sizeDelta = new Vector2(230, 30);
        TextMeshProUGUI tTxt = tObj.GetComponent<TextMeshProUGUI>();
        tTxt.text = title;
        tTxt.fontSize = 17;
        tTxt.color = new Color(1f, 0.9f, 0.4f);
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.fontStyle = FontStyles.Bold;

        // Icon súng
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(card.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(0, 55);
        iconRect.sizeDelta = new Vector2(130, 75);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;

        if (!string.IsNullOrEmpty(spritePath))
        {
            Sprite s = GetWeaponSpriteFromPrefab(spritePath);
            if (s != null)
            {
                iconImg.sprite = s;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = new Color(1f, 1f, 1f, 0.15f);
            }
        }
        else
        {
            iconImg.color = new Color(1f, 1f, 1f, 0.15f);
        }

        // Desc
        GameObject dObj = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        dObj.transform.SetParent(card.transform, false);
        RectTransform dRect = dObj.GetComponent<RectTransform>();
        dRect.anchoredPosition = new Vector2(0, -20);
        dRect.sizeDelta = new Vector2(220, 60);
        TextMeshProUGUI dTxt = dObj.GetComponent<TextMeshProUGUI>();
        dTxt.text = desc;
        dTxt.fontSize = 13;
        dTxt.color = new Color(0.8f, 0.85f, 0.9f);
        dTxt.alignment = TextAlignmentOptions.Center;

        // Price
        GameObject pObj = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
        pObj.transform.SetParent(card.transform, false);
        RectTransform pRect = pObj.GetComponent<RectTransform>();
        pRect.anchoredPosition = new Vector2(0, -65);
        pRect.sizeDelta = new Vector2(220, 25);
        TextMeshProUGUI pTxt = pObj.GetComponent<TextMeshProUGUI>();
        pTxt.text = price;
        pTxt.fontSize = 17;
        pTxt.color = new Color(0.4f, 0.9f, 0.5f);
        pTxt.alignment = TextAlignmentOptions.Center;
        pTxt.fontStyle = FontStyles.Bold;

        // Buy Button
        GameObject bObj = new GameObject("BuyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        bObj.transform.SetParent(card.transform, false);
        RectTransform bRect = bObj.GetComponent<RectTransform>();
        bRect.anchoredPosition = new Vector2(0, -115);
        bRect.sizeDelta = new Vector2(200, 45);
        Image bImg = bObj.GetComponent<Image>();
        bImg.color = new Color(0.2f, 0.65f, 0.35f);

        GameObject btObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        btObj.transform.SetParent(bObj.transform, false);
        RectTransform btRect = btObj.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI btTxt = btObj.GetComponent<TextMeshProUGUI>();
        btTxt.text = string.IsNullOrEmpty(buttonText) ? "BUY NOW" : buttonText;
        btTxt.fontSize = 15;
        btTxt.color = Color.white;
        btTxt.alignment = TextAlignmentOptions.Center;
        btTxt.fontStyle = FontStyles.Bold;

        Button btn = bObj.GetComponent<Button>();
        btn.onClick.AddListener(onClickAction);
    }
}
