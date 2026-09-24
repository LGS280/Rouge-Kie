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

    [Header("Gem Currency Display")]
    public TextMeshProUGUI gemBalanceText;

    [Header("Purchase Confirmation Dialog")]
    public GameObject confirmationOverlay;
    public TextMeshProUGUI confirmTitleText;
    public TextMeshProUGUI confirmMessageText;
    public Image confirmItemIcon;
    public Button confirmYesButton;
    public Button confirmNoButton;
    public Button confirmCloseButton;
    private UnityEngine.Events.UnityAction pendingConfirmAction;

    [Header("Dynamic Shop Items")]
    public List<ShopItemData> dynamicShopItems = new List<ShopItemData>();

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

        if (confirmationOverlay == null && shopPanel != null)
        {
            BuildConfirmationDialog(shopPanel.transform);
        }

        if (closeShopButton != null)
        {
            closeShopButton.onClick.AddListener(CloseShop);
        }

        if (coinTabButton != null) coinTabButton.onClick.AddListener(() => SwitchTab(0));
        if (gemTabButton != null) gemTabButton.onClick.AddListener(() => SwitchTab(1));
        if (skinTabButton != null) skinTabButton.onClick.AddListener(() => SwitchTab(2));

        SwitchTab(0);
        UpdateGemDisplay();
        FetchShopItems();
    }

    public void OpenShop()
    {
        if (shopPanel == null)
        {
            BuildAutoShopUI();
        }
        if (confirmationOverlay == null && shopPanel != null)
        {
            BuildConfirmationDialog(shopPanel.transform);
        }
        HidePurchaseConfirmation();
        if (shopPanel != null) shopPanel.SetActive(true);
        UpdateGemDisplay();
        RefreshAllTabs();
        FetchShopItems();
    }

    /// <summary>
    /// Cập nhật hiển thị số lượng Gem hiện có của người chơi lên badge ở góc trên bên trái cửa sổ Shop
    /// </summary>
    public void UpdateGemDisplay()
    {
        if (gemBalanceText == null) return;

        if (PlayerProfileUI.CurrentProfile != null)
        {
            gemBalanceText.text = $"{PlayerProfileUI.CurrentProfile.standardCurrency:N0} Gem";
        }
        else
        {
            gemBalanceText.text = "... Gem";
        }

        if (ApiClient.Instance != null && NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn)
        {
            ApiClient.Instance.Get("/playerprofile", (json) =>
            {
                try
                {
                    var profile = JsonUtility.FromJson<ProfileResponseData>(json);
                    if (profile != null && gemBalanceText != null)
                    {
                        gemBalanceText.text = $"{profile.standardCurrency:N0} Gem";
                    }
                }
                catch { }
            }, null);
        }
    }

    public void CloseShop()
    {
        HidePurchaseConfirmation();
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
        HidePurchaseConfirmation();
        if (coinTabContent != null)
        {
            Transform t = coinTabContent.transform.parent != null && coinTabContent.transform.parent.name.EndsWith("_ScrollView") ? coinTabContent.transform.parent : coinTabContent.transform;
            t.gameObject.SetActive(tabIndex == 0);
        }
        if (gemTabContent != null)
        {
            Transform t = gemTabContent.transform.parent != null && gemTabContent.transform.parent.name.EndsWith("_ScrollView") ? gemTabContent.transform.parent : gemTabContent.transform;
            t.gameObject.SetActive(tabIndex == 1);
        }
        if (skinTabContent != null)
        {
            Transform t = skinTabContent.transform.parent != null && skinTabContent.transform.parent.name.EndsWith("_ScrollView") ? skinTabContent.transform.parent : skinTabContent.transform;
            t.gameObject.SetActive(tabIndex == 2);
        }

        SetTabButtonState(coinTabButton, tabIndex == 0);
        SetTabButtonState(gemTabButton, tabIndex == 1);
        SetTabButtonState(skinTabButton, tabIndex == 2);
    }

    private void SetTabButtonState(Button btn, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isActive ? new Color(0.28f, 0.42f, 0.68f) : new Color(0.18f, 0.24f, 0.35f);
        }
    }

    public void FetchShopItems()
    {
        if (statusText != null) statusText.text = "Loading Shop Items...";

        if (ApiClient.Instance == null)
        {
            if (statusText != null) statusText.text = "Offline Shop Mode";
            RefreshAllTabs();
            return;
        }

        ApiClient.Instance.Get("/ShopItems", (json) =>
        {
            try
            {
                // Parse JSON array of shop items
                string wrappedJson = "{\"items\":" + json + "}";
                ShopItemListWrapper wrapper = JsonUtility.FromJson<ShopItemListWrapper>(wrappedJson);

                if (wrapper != null && wrapper.items != null)
                {
                    dynamicShopItems = wrapper.items;
                    Debug.Log($"[ShopUIController] Tải thành công {dynamicShopItems.Count} vật phẩm từ Cửa Hàng!");
                    if (statusText != null) statusText.text = "Shop Ready!";
                    RefreshAllTabs();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShopUIController] Lỗi parse Shop Items: {ex.Message}");
                if (statusText != null) statusText.text = "Error loading shop items.";
                RefreshAllTabs();
            }
        }, (err) =>
        {
            Debug.LogWarning($"[ShopUIController] Không thể tải danh sách Shop: {err}");
            if (statusText != null) statusText.text = "Offline Shop Mode";
            RefreshAllTabs();
        });
    }

    public static void RegisterUnlockSafely(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath)) return;
        string clean = prefabPath.Replace("Weapons/", "").Replace(".prefab", "").Replace("(Clone)", "").Trim();

        WeaponVaultUIController vault = WeaponVaultUIController.Instance;
        if (vault == null) vault = UnityEngine.Object.FindFirstObjectByType<WeaponVaultUIController>();
        if (vault != null)
        {
            vault.RegisterUnlockedWeapon(clean);
        }
        else
        {
            string saved = PlayerPrefs.GetString("unlocked_weapons", "");
            if (string.IsNullOrEmpty(saved))
            {
                PlayerPrefs.SetString("unlocked_weapons", clean);
            }
            else
            {
                var set = new HashSet<string>(saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                if (!set.Contains(clean))
                {
                    set.Add(clean);
                    PlayerPrefs.SetString("unlocked_weapons", string.Join(",", set));
                }
            }
            PlayerPrefs.Save();
            Debug.Log($"[ShopUIController] Đã lưu offline fallback vũ khí '{clean}' vào PlayerPrefs!");
        }
    }

    /// <summary>
    /// Bấm Mua Súng trực tiếp bằng Tiền thật qua PayOS VietQR
    /// </summary>
    public void BuyGemPackage(int amountVnd, string description, int shopItemId = 0, string weaponPrefab = "")
    {
        if (PaymentManager.Instance != null)
        {
            // Lưu lại thông tin súng mua bằng VietQR để sinh ra bàn khi thanh toán xong
            string prefabPath = !string.IsNullOrEmpty(weaponPrefab) ? weaponPrefab : ResolveWeaponPrefabName("", description);
            PaymentManager.Instance.pendingBoughtWeaponPrefab = prefabPath;

            PaymentManager.Instance.RequestPayment(amountVnd, description, "GEMS", shopItemId, (res) =>
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
    public void BuyShopItem(int shopItemId, string weaponPrefab = "")
    {
        if (statusText != null) statusText.text = "Processing purchase...";

        string prefabPath = weaponPrefab;
        if (string.IsNullOrEmpty(prefabPath) && dynamicShopItems != null)
        {
            var found = dynamicShopItems.Find(x => x.shopItemId == shopItemId);
            if (found != null && found.itemType != "SKIN")
            {
                prefabPath = ResolveWeaponPrefabName(found.name, found.description);
            }
        }
        if (string.IsNullOrEmpty(prefabPath))
        {
            prefabPath = GetPrefabPathByItemId(shopItemId);
        }

        ApiClient.Instance.Post($"/ShopItems/buy/{shopItemId}", "{}", (json) =>
        {
            try
            {
                BuyItemResponseData res = JsonUtility.FromJson<BuyItemResponseData>(json);
                if (res.success)
                {
                    Debug.Log($"[ShopUIController] {res.message}");
                    if (statusText != null) statusText.text = $"<color=green>{res.message}</color>";

                    // Đăng ký mở khóa vĩnh viễn vào Kho Vũ Khí
                    RegisterUnlockSafely(prefabPath);

                    // Làm mới giao diện hiển thị Gems của người chơi
                    if (PlayerProfileUI.Instance != null)
                    {
                        PlayerProfileUI.Instance.RefreshProfile();
                    }

                    // Cập nhật ngay số dư Gem trên badge của Shop
                    if (gemBalanceText != null && res.remainingStandardCurrency >= 0)
                    {
                        gemBalanceText.text = $"{res.remainingStandardCurrency:N0} Gem";
                        if (PlayerProfileUI.CurrentProfile != null)
                        {
                            PlayerProfileUI.CurrentProfile.standardCurrency = res.remainingStandardCurrency;
                        }
                    }
                    else
                    {
                        UpdateGemDisplay();
                    }

                    // Tự động sinh súng vừa mua lên Bàn Trưng Bày
                    SpawnBoughtWeaponOnTable(prefabPath);

                    // Làm mới giao diện thẻ Shop (chuyển sang trạng thái ĐÃ MỞ KHÓA)
                    RefreshAllTabs();
                }
                else
                {
                    if (statusText != null) statusText.text = $"<color=red>{res.message}</color>";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShopUIController] Parse error: {ex.Message}");
                RegisterUnlockSafely(prefabPath);
                SpawnBoughtWeaponOnTable(prefabPath);
                RefreshAllTabs();
            }
        }, (err) =>
        {
            Debug.LogWarning($"[ShopUIController] API Buy Item Error (chuyển chế độ Offline Test): {err}");
            if (statusText != null) statusText.text = "<color=green>Offline Purchase Success! Weapon spawned on table!</color>";
            RegisterUnlockSafely(prefabPath);
            SpawnBoughtWeaponOnTable(prefabPath);
            RefreshAllTabs();
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
        if (dynamicShopItems != null)
        {
            var item = dynamicShopItems.Find(x => x.shopItemId == itemId);
            if (item != null)
            {
                return ResolveWeaponPrefabName(item.name, item.description);
            }
        }
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
        return ResolveWeaponPrefabName("", desc);
    }

    public string ResolveWeaponPrefabName(string name, string desc)
    {
        string n = name ?? "";
        string d = desc ?? "";
        string text = (n + " " + d).ToLowerInvariant();

        if (text.Contains("ak-47") || text.Contains("ak_47") || text.Contains("ak47") || text.Contains("gold ak"))
        {
            if (text.Contains("gold") || text.Contains("a_gold")) return "AK_47A_Gold";
            return "AK47";
        }
        if (text.Contains("missile")) return "Missile_Launcher";
        if (text.Contains("rocket") || text.Contains("bazooka")) return "Rocket_Launcher";
        if (text.Contains("shotgun")) return "Assault_Shotgun";
        if (text.Contains("desert") || text.Contains("eagle")) return "Desert_Eagle";
        if (text.Contains("katana")) return "Gold_Katana";
        if (text.Contains("laser")) return "Laser_Gun_MK1";
        if (text.Contains("m249")) return "M249";
        if (text.Contains("m4")) return "M4";
        if (text.Contains("odin")) return "Odin";
        if (text.Contains("snipe")) return "Snipe";
        if (text.Contains("uzi")) return "Uzi";
        if (text.Contains("bow")) return "Wooden_Bow";

        string clean = n.Replace(" ", "_").Trim();
        if (clean.EndsWith("_VIP", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - 4);
        }
        return clean;
    }

    private string FormatNumberShort(int num)
    {
        if (num >= 1000000) return (num / 1000000f).ToString("0.#") + "M";
        if (num >= 1000) return (num / 1000f).ToString("0.#") + "K";
        return num.ToString();
    }

    /// <summary>
    /// Nạp Sprite viên Gem từ Assets/Images/gemV1.png
    /// </summary>
    public Sprite GetGemSprite()
    {
#if UNITY_EDITOR
        Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/gemV1.png");
        if (s != null) return s;
#endif
        var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var sp in sprites)
        {
            if (sp != null && (sp.name == "gemV1" || sp.name.Equals("gemV1", StringComparison.OrdinalIgnoreCase)))
                return sp;
        }
        return null;
    }

    /// <summary>
    /// Tìm và nạp Sprite hình ảnh của vũ khí từ thư mục Assets/Weapons/Player_Weapon/ hoặc từ Prefab súng
    /// </summary>
    public Sprite GetWeaponSpriteFromPrefab(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;

        string cleanName = weaponName.Replace("Weapons/", "").Replace("(Clone)", "").Trim();

#if UNITY_EDITOR
        // 1. Ưu tiên tìm trực tiếp file Sprite ảnh súng trong thư mục Assets/Weapons/
        string[] candidatePaths = new string[]
        {
            $"Assets/Weapons/Player_Weapon/{cleanName}/{cleanName}.png",
            $"Assets/Weapons/Player_Weapon/{cleanName}.png",
            $"Assets/Weapons/Player_Weapon/AK/AK.png",
            $"Assets/Weapons/Player_Weapon/Bow/{cleanName}/{cleanName}.png",
            $"Assets/Weapons/{cleanName}.png"
        };

        foreach (var p in candidatePaths)
        {
            Sprite directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (directSprite != null) return directSprite;
        }
#endif

        // 2. Dự phòng: Tìm từ SpriteRenderer của Prefab súng trong Assets/Prefab/Weapons/
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
    /// Hiển thị hộp thoại Popup xác nhận (Double Check) trước khi thực hiện mua vật phẩm
    /// </summary>
    public void ShowPurchaseConfirmation(string itemName, string priceText, string spritePath, UnityEngine.Events.UnityAction onConfirmAction)
    {
        if (confirmationOverlay == null && shopPanel != null)
        {
            BuildConfirmationDialog(shopPanel.transform);
        }

        if (confirmationOverlay == null)
        {
            // Dự phòng nếu không khởi tạo được modal xác nhận thì thực thi trực tiếp
            onConfirmAction?.Invoke();
            return;
        }

        pendingConfirmAction = onConfirmAction;

        if (confirmTitleText != null)
        {
            confirmTitleText.text = "CONFIRM PURCHASE";
        }

        if (confirmMessageText != null)
        {
            confirmMessageText.text = $"Are you sure you want to purchase\n<color=#FFD700><b>{itemName}</b></color> for <color=#40FF40><b>{priceText}</b></color>?";
        }

        if (confirmItemIcon != null)
        {
            Sprite itemSprite = null;
            if (!string.IsNullOrEmpty(spritePath))
            {
                itemSprite = GetWeaponSpriteFromPrefab(spritePath);
            }
            if (itemSprite == null && (itemName.ToLower().Contains("gem") || priceText.ToLower().Contains("gem")))
            {
                itemSprite = GetGemSprite();
            }

            if (itemSprite != null)
            {
                confirmItemIcon.sprite = itemSprite;
                confirmItemIcon.color = Color.white;
                if (confirmItemIcon.transform.parent != null)
                {
                    confirmItemIcon.transform.parent.gameObject.SetActive(true);
                }
                else
                {
                    confirmItemIcon.gameObject.SetActive(true);
                }
            }
            else
            {
                if (confirmItemIcon.transform.parent != null && confirmItemIcon.transform.parent.name == "IconFrame")
                {
                    confirmItemIcon.transform.parent.gameObject.SetActive(false);
                }
                else
                {
                    confirmItemIcon.gameObject.SetActive(false);
                }
            }
        }

        confirmationOverlay.transform.SetAsLastSibling();
        confirmationOverlay.SetActive(true);
    }

    /// <summary>
    /// Đóng popup xác nhận mua hàng và hủy hành động đang chờ
    /// </summary>
    public void HidePurchaseConfirmation()
    {
        pendingConfirmAction = null;
        if (confirmationOverlay != null)
        {
            confirmationOverlay.SetActive(false);
        }
    }

    /// <summary>
    /// Tự động xây dựng Popup xác nhận mua hàng (Double Check Confirmation Dialog)
    /// </summary>
    private void BuildConfirmationDialog(Transform parent)
    {
        if (confirmationOverlay != null || parent == null) return;

        // 1. Overlay nền làm mờ và chặn tương tác với phần còn lại của Shop
        confirmationOverlay = new GameObject("ConfirmationOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        confirmationOverlay.transform.SetParent(parent, false);

        RectTransform overlayRect = confirmationOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        Image overlayImg = confirmationOverlay.GetComponent<Image>();
        overlayImg.color = new Color(0.01f, 0.02f, 0.05f, 0.88f);

        Button overlayBtn = confirmationOverlay.GetComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(HidePurchaseConfirmation);

        // 2. Khung hộp thoại chính (Confirm Box)
        GameObject box = new GameObject("ConfirmBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(confirmationOverlay.transform, false);

        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(520, 350);
        boxRect.anchoredPosition = Vector2.zero;

        Image boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(0.09f, 0.13f, 0.20f, 0.98f);

        // 3. Thanh tiêu đề trên cùng (Header Bar)
        GameObject headerBar = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerBar.transform.SetParent(box.transform, false);
        RectTransform headerBarRect = headerBar.GetComponent<RectTransform>();
        headerBarRect.anchoredPosition = new Vector2(0, 148);
        headerBarRect.sizeDelta = new Vector2(520, 54);
        Image headerBarImg = headerBar.GetComponent<Image>();
        headerBarImg.color = new Color(0.14f, 0.19f, 0.30f, 1f);

        // Tiêu đề chữ
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(headerBar.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(400, 40);

        confirmTitleText = titleObj.GetComponent<TextMeshProUGUI>();
        confirmTitleText.text = "CONFIRM PURCHASE";
        confirmTitleText.fontSize = 20;
        confirmTitleText.color = new Color(1f, 0.85f, 0.3f);
        confirmTitleText.alignment = TextAlignmentOptions.Center;
        confirmTitleText.fontStyle = FontStyles.Bold;

        // Nút Đóng [X] góc trên bên phải
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(headerBar.transform, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(230, 0);
        closeRect.sizeDelta = new Vector2(34, 34);

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
        closeTxt.fontSize = 18;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        confirmCloseButton = closeObj.GetComponent<Button>();
        confirmCloseButton.onClick.AddListener(HidePurchaseConfirmation);

        // 4. Khung và Icon hình ảnh xem trước của vật phẩm (Icon Preview Frame)
        GameObject iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(Image));
        iconFrame.transform.SetParent(box.transform, false);
        RectTransform iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchoredPosition = new Vector2(0, 60);
        iconFrameRect.sizeDelta = new Vector2(150, 85);

        Image iconFrameImg = iconFrame.GetComponent<Image>();
        iconFrameImg.color = new Color(0.06f, 0.08f, 0.13f, 0.85f);

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(iconFrame.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = new Vector2(-12, -12);

        confirmItemIcon = iconObj.GetComponent<Image>();
        confirmItemIcon.preserveAspect = true;

        // 5. Nội dung thông báo xác nhận (Message Text)
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgObj.transform.SetParent(box.transform, false);
        RectTransform msgRect = msgObj.GetComponent<RectTransform>();
        msgRect.anchoredPosition = new Vector2(0, -25);
        msgRect.sizeDelta = new Vector2(460, 65);

        confirmMessageText = msgObj.GetComponent<TextMeshProUGUI>();
        confirmMessageText.text = "Are you sure you want to purchase this item?";
        confirmMessageText.fontSize = 16;
        confirmMessageText.color = Color.white;
        confirmMessageText.alignment = TextAlignmentOptions.Center;

        // 6. Nút HỦY BỎ (CANCEL) - Bên trái màu đỏ/xám
        GameObject cancelObj = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObj.transform.SetParent(box.transform, false);
        RectTransform cancelRect = cancelObj.GetComponent<RectTransform>();
        cancelRect.anchoredPosition = new Vector2(-110, -115);
        cancelRect.sizeDelta = new Vector2(170, 46);

        Image cancelImg = cancelObj.GetComponent<Image>();
        cancelImg.color = new Color(0.55f, 0.22f, 0.22f);

        GameObject cancelTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelTxtObj.transform.SetParent(cancelObj.transform, false);
        RectTransform cancelTxtRect = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRect.anchorMin = Vector2.zero;
        cancelTxtRect.anchorMax = Vector2.one;
        cancelTxtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI cancelTxt = cancelTxtObj.GetComponent<TextMeshProUGUI>();
        cancelTxt.text = "CANCEL";
        cancelTxt.fontSize = 16;
        cancelTxt.color = Color.white;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.fontStyle = FontStyles.Bold;

        confirmNoButton = cancelObj.GetComponent<Button>();
        confirmNoButton.onClick.AddListener(HidePurchaseConfirmation);

        // 7. Nút ĐỒNG Ý MUA (CONFIRM) - Bên phải màu xanh lá cây
        GameObject yesObj = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        yesObj.transform.SetParent(box.transform, false);
        RectTransform yesRect = yesObj.GetComponent<RectTransform>();
        yesRect.anchoredPosition = new Vector2(110, -115);
        yesRect.sizeDelta = new Vector2(170, 46);

        Image yesImg = yesObj.GetComponent<Image>();
        yesImg.color = new Color(0.20f, 0.65f, 0.35f);

        GameObject yesTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        yesTxtObj.transform.SetParent(yesObj.transform, false);
        RectTransform yesTxtRect = yesTxtObj.GetComponent<RectTransform>();
        yesTxtRect.anchorMin = Vector2.zero;
        yesTxtRect.anchorMax = Vector2.one;
        yesTxtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI yesTxt = yesTxtObj.GetComponent<TextMeshProUGUI>();
        yesTxt.text = "CONFIRM";
        yesTxt.fontSize = 16;
        yesTxt.color = Color.white;
        yesTxt.alignment = TextAlignmentOptions.Center;
        yesTxt.fontStyle = FontStyles.Bold;

        confirmYesButton = yesObj.GetComponent<Button>();
        confirmYesButton.onClick.AddListener(() =>
        {
            var action = pendingConfirmAction;
            HidePurchaseConfirmation();
            action?.Invoke();
        });

        confirmationOverlay.SetActive(false);
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

        // 3. Tiêu đề Cửa Hàng (Header) - Nằm CHÍNH GIỮA CỬA SỔ
        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(dialog.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchoredPosition = new Vector2(0, 260);
        headerRect.sizeDelta = new Vector2(500, 50);

        TextMeshProUGUI headerTxt = headerObj.GetComponent<TextMeshProUGUI>();
        headerTxt.text = "ARMORY & WEAPON SHOP";
        headerTxt.fontSize = 28;
        headerTxt.color = new Color(1f, 0.85f, 0.3f);
        headerTxt.alignment = TextAlignmentOptions.Center;
        headerTxt.fontStyle = FontStyles.Bold;

        // Badge Hiển thị Gem (BÊN TRÁI - Đối xứng với Nút Đóng [X] bên phải)
        GameObject gemBadgeObj = new GameObject("GemBadge", typeof(RectTransform), typeof(Image));
        gemBadgeObj.transform.SetParent(dialog.transform, false);
        RectTransform gemBadgeRect = gemBadgeObj.GetComponent<RectTransform>();
        gemBadgeRect.anchoredPosition = new Vector2(-360, 260);
        gemBadgeRect.sizeDelta = new Vector2(145, 38);

        Image gemBadgeImg = gemBadgeObj.GetComponent<Image>();
        gemBadgeImg.color = new Color(0.06f, 0.10f, 0.16f, 0.95f);

        // Icon viên Gem (dùng Sprite thật từ Assets/Images/gemV1.png)
        GameObject gemIconObj = new GameObject("GemIcon", typeof(RectTransform), typeof(Image));
        gemIconObj.transform.SetParent(gemBadgeObj.transform, false);
        RectTransform gemIconRect = gemIconObj.GetComponent<RectTransform>();
        gemIconRect.anchoredPosition = new Vector2(-45, 0);
        gemIconRect.sizeDelta = new Vector2(24, 24);

        Image gemIconImg = gemIconObj.GetComponent<Image>();
        gemIconImg.preserveAspect = true;
        Sprite gemSp = GetGemSprite();
        if (gemSp != null)
        {
            gemIconImg.sprite = gemSp;
        }

        // Text số lượng Gem (Dùng chữ thường, không dùng emoji ký tự để tránh lỗi thiếu font atlas)
        GameObject gemTxtObj = new GameObject("GemText", typeof(RectTransform), typeof(TextMeshProUGUI));
        gemTxtObj.transform.SetParent(gemBadgeObj.transform, false);
        RectTransform gemTxtRect = gemTxtObj.GetComponent<RectTransform>();
        gemTxtRect.anchoredPosition = new Vector2(16, 0);
        gemTxtRect.sizeDelta = new Vector2(90, 32);

        gemBalanceText = gemTxtObj.GetComponent<TextMeshProUGUI>();
        gemBalanceText.text = "0 Gem";
        gemBalanceText.fontSize = 17;
        gemBalanceText.color = Color.white;
        gemBalanceText.alignment = TextAlignmentOptions.Left;
        gemBalanceText.fontStyle = FontStyles.Bold;

        // Nút Đóng [X] (BÊN PHẢI)
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

        coinTabButton = CreateTabButton(tabBar.transform, new Vector2(-280, 0), "GEM WEAPONS");
        gemTabButton = CreateTabButton(tabBar.transform, new Vector2(0, 0), "VIP WEAPONS (VietQR)");
        skinTabButton = CreateTabButton(tabBar.transform, new Vector2(280, 0), "SKINS");

        // 5. Khung chứa Nội dung 3 Tab (Tab Contents)
        coinTabContent = CreateTabContent(dialog.transform, "CoinTabContent");
        gemTabContent = CreateTabContent(dialog.transform, "GemTabContent");
        skinTabContent = CreateTabContent(dialog.transform, "SkinTabContent");

        RefreshAllTabs();

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

        // 7. Hộp thoại xác nhận mua hàng (Double Check Confirmation Dialog)
        BuildConfirmationDialog(shopPanel.transform);

        shopPanel.SetActive(false);
    }

    /// <summary>
    /// Làm mới nội dung các thẻ vật phẩm trong cả 3 Tab, tự động nhận biết vũ khí đã mở khóa
    /// </summary>
    public void RefreshAllTabs()
    {
        if (coinTabContent == null || gemTabContent == null || skinTabContent == null) return;

        // Dọn sạch các card cũ
        foreach (Transform child in coinTabContent.transform) Destroy(child.gameObject);
        foreach (Transform child in gemTabContent.transform) Destroy(child.gameObject);
        foreach (Transform child in skinTabContent.transform) Destroy(child.gameObject);

        if (dynamicShopItems != null && dynamicShopItems.Count > 0)
        {
            foreach (var item in dynamicShopItems)
            {
                if (item == null) continue;
                string itemType = (item.itemType ?? "").ToUpperInvariant().Trim();
                string currencyType = (item.currencyType ?? "").ToUpperInvariant().Trim();
                string prefabPath = ResolveWeaponPrefabName(item.name, item.description);

                if (itemType == "WEAPON_GEM" || (currencyType == "GEM" && itemType != "SKIN" && itemType != "WEAPON_VIP"))
                {
                    // Tab 1: GEM WEAPONS
                    string priceText = $"{item.price:N0} Gems";
                    string btnText = $"BUY ({FormatNumberShort(item.price)} GEMS)";
                    int itemId = item.shopItemId;
                    string itemName = item.name;
                    string targetPrefab = prefabPath;
                    CreateShopCard(coinTabContent.transform, itemName, priceText, item.description, btnText, targetPrefab, () =>
                    {
                        ShowPurchaseConfirmation(itemName, priceText, targetPrefab, () => BuyShopItem(itemId, targetPrefab));
                    });
                }
                else if (itemType == "WEAPON_VIP" || currencyType == "VND")
                {
                    // Tab 2: VIP WEAPONS (VietQR)
                    string priceText = $"{item.price:N0} VND";
                    string btnText = $"BUY NOW ({FormatNumberShort(item.price)} VND)";
                    int itemId = item.shopItemId;
                    int price = item.price;
                    string itemName = item.name;
                    string targetPrefab = prefabPath;
                    string desc = !string.IsNullOrEmpty(item.description) ? item.description : $"Buy {item.name}";
                    CreateShopCard(gemTabContent.transform, itemName, priceText, item.description, btnText, targetPrefab, () =>
                    {
                        ShowPurchaseConfirmation(itemName, priceText, targetPrefab, () => BuyGemPackage(price, desc, itemId, targetPrefab));
                    });
                }
                else if (itemType == "SKIN")
                {
                    // Tab 3: SKINS
                    string priceText = $"{item.price:N0} Gems";
                    string btnText = $"BUY ({FormatNumberShort(item.price)} GEMS)";
                    int itemId = item.shopItemId;
                    string itemName = item.name;
                    CreateShopCard(skinTabContent.transform, itemName, priceText, item.description, btnText, "", () =>
                    {
                        ShowPurchaseConfirmation(itemName, priceText, "", () => BuyShopItem(itemId, ""));
                    });
                }
            }
        }
        else
        {
            // Fallback tĩnh phòng khi chưa kết nối được Backend
            // 5a. Súng mua bằng Gem trong Game (In-Game Gems)
            CreateShopCard(coinTabContent.transform, "AK-47 Gold", "500 Gems", "High-damage gold-plated assault rifle", "BUY (500 GEMS)", "Weapons/AK_47A_Gold", () =>
                ShowPurchaseConfirmation("AK-47 Gold", "500 Gems", "Weapons/AK_47A_Gold", () => BuyShopItem(1, "AK_47A_Gold")));
            CreateShopCard(coinTabContent.transform, "Missile Launcher", "800 Gems", "Long-range homing missile launcher", "BUY (800 GEMS)", "Weapons/Missile_Launcher", () =>
                ShowPurchaseConfirmation("Missile Launcher", "800 Gems", "Weapons/Missile_Launcher", () => BuyShopItem(2, "Missile_Launcher")));
            CreateShopCard(coinTabContent.transform, "Rocket Launcher", "1,200 Gems", "Heavy rocket launcher with wide AoE", "BUY (1.2K GEMS)", "Weapons/Rocket_Launcher", () =>
                ShowPurchaseConfirmation("Rocket Launcher", "1,200 Gems", "Weapons/Rocket_Launcher", () => BuyShopItem(3, "Rocket_Launcher")));

            // 5b. Súng VIP mua trực tiếp bằng Tiền Thật qua VietQR (PayOS)
            CreateShopCard(gemTabContent.transform, "AK-47 Gold VIP", "2,000 VND", "Pay via VietQR to unlock AK-47 Gold VIP", "BUY NOW (2K VND)", "Weapons/AK_47A_Gold", () =>
                ShowPurchaseConfirmation("AK-47 Gold VIP", "2,000 VND", "Weapons/AK_47A_Gold", () => BuyGemPackage(2000, "Buy AK-47 Gold VIP", 1, "AK_47A_Gold")));
            CreateShopCard(gemTabContent.transform, "Missile Launcher VIP", "2,000 VND", "Pay via VietQR to unlock Missile Launcher VIP", "BUY NOW (2K VND)", "Weapons/Missile_Launcher", () =>
                ShowPurchaseConfirmation("Missile Launcher VIP", "2,000 VND", "Weapons/Missile_Launcher", () => BuyGemPackage(2000, "Buy Missile Launcher VIP", 2, "Missile_Launcher")));
            CreateShopCard(gemTabContent.transform, "Rocket Launcher VIP", "2,000 VND", "Pay via VietQR to unlock Rocket Launcher VIP", "BUY NOW (2K VND)", "Weapons/Rocket_Launcher", () =>
                ShowPurchaseConfirmation("Rocket Launcher VIP", "2,000 VND", "Weapons/Rocket_Launcher", () => BuyGemPackage(2000, "Buy Rocket Launcher VIP", 3, "Rocket_Launcher")));

            // 5c. Trang Phục Skins
            CreateShopCard(skinTabContent.transform, "Cyber Rookie", "100 Gems", "Rookie warrior battle suit", "BUY (100 GEMS)", "", () =>
                ShowPurchaseConfirmation("Cyber Rookie", "100 Gems", "", () => BuyShopItem(4, "")));
            CreateShopCard(skinTabContent.transform, "Hero Zero", "200 Gems", "Zero superhero battle suit", "BUY (200 GEMS)", "", () =>
                ShowPurchaseConfirmation("Hero Zero", "200 Gems", "", () => BuyShopItem(5, "")));
        }
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
        // 1. ScrollView Viewport Container
        GameObject scrollObj = new GameObject(name + "_ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollObj.transform.SetParent(parent, false);

        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector2(0, -30);
        scrollRect.sizeDelta = new Vector2(840, 360);

        Image maskImg = scrollObj.GetComponent<Image>();
        maskImg.color = new Color(0, 0, 0, 0.01f);
        Mask mask = scrollObj.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        // 2. Nội dung các thẻ nằm ngang có thể cuộn
        GameObject content = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollObj.transform, false);

        RectTransform cRect = content.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 0.5f);
        cRect.anchorMax = new Vector2(0, 0.5f);
        cRect.pivot = new Vector2(0, 0.5f);
        cRect.anchoredPosition = Vector2.zero;
        cRect.sizeDelta = new Vector2(840, 360);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.padding = new RectOffset(20, 20, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.content = cRect;
        sr.horizontal = true;
        sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;

        return content;
    }

    private void CreateShopCard(Transform parent, string title, string price, string desc, string buttonText, string spritePath, UnityEngine.Events.UnityAction onClickAction)
    {
        // Kiểm tra xem vũ khí này đã được người chơi mở khóa vĩnh viễn chưa
        bool isUnlocked = false;
        if (!string.IsNullOrEmpty(spritePath))
        {
            string clean = spritePath.Replace("Weapons/", "").Replace(".prefab", "").Trim();
            if (WeaponVaultUIController.Instance != null)
            {
                isUnlocked = WeaponVaultUIController.Instance.IsWeaponUnlocked(clean);
            }
            else
            {
                string saved = PlayerPrefs.GetString("unlocked_weapons", "");
                if (!string.IsNullOrEmpty(saved))
                {
                    var items = saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    isUnlocked = Array.Exists(items, item => item.Trim().Equals(clean, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        GameObject card = new GameObject("Card_" + title, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 320);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 250;
        le.preferredHeight = 320;
        le.minWidth = 250;
        le.minHeight = 320;

        Image img = card.GetComponent<Image>();
        img.color = isUnlocked ? new Color(0.11f, 0.14f, 0.20f, 0.95f) : new Color(0.15f, 0.19f, 0.28f);

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

        // Price / Status
        GameObject pObj = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
        pObj.transform.SetParent(card.transform, false);
        RectTransform pRect = pObj.GetComponent<RectTransform>();
        pRect.anchoredPosition = new Vector2(0, -65);
        pRect.sizeDelta = new Vector2(220, 25);
        TextMeshProUGUI pTxt = pObj.GetComponent<TextMeshProUGUI>();
        pTxt.text = isUnlocked ? "<color=#40ff40>[PURCHASED]</color>" : price;
        pTxt.fontSize = 17;
        pTxt.color = isUnlocked ? new Color(0.3f, 1f, 0.4f) : new Color(0.4f, 0.9f, 0.5f);
        pTxt.alignment = TextAlignmentOptions.Center;
        pTxt.fontStyle = FontStyles.Bold;

        // Buy / Equip Button
        GameObject bObj = new GameObject("BuyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        bObj.transform.SetParent(card.transform, false);
        RectTransform bRect = bObj.GetComponent<RectTransform>();
        bRect.anchoredPosition = new Vector2(0, -115);
        bRect.sizeDelta = new Vector2(200, 45);
        Image bImg = bObj.GetComponent<Image>();
        bImg.color = isUnlocked ? new Color(0.22f, 0.26f, 0.32f) : new Color(0.2f, 0.65f, 0.35f);

        GameObject btObj = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        btObj.transform.SetParent(bObj.transform, false);
        RectTransform btRect = btObj.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI btTxt = btObj.GetComponent<TextMeshProUGUI>();
        btTxt.text = isUnlocked ? "OWNED" : (string.IsNullOrEmpty(buttonText) ? "BUY NOW" : buttonText);
        btTxt.fontSize = 15;
        btTxt.color = isUnlocked ? new Color(0.6f, 0.65f, 0.7f) : Color.white;
        btTxt.alignment = TextAlignmentOptions.Center;
        btTxt.fontStyle = FontStyles.Bold;

        Button btn = bObj.GetComponent<Button>();
        if (isUnlocked)
        {
            // Đã mua: Khóa nút bấm (disable) để chống spam sinh hàng loạt súng ra bàn
            btn.interactable = false;
        }
        else
        {
            btn.interactable = true;
            btn.onClick.AddListener(onClickAction);
        }
    }
}
