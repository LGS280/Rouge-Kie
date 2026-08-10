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
        if (closeShopButton != null)
        {
            closeShopButton.onClick.AddListener(CloseShop);
        }

        if (gemTabButton != null) gemTabButton.onClick.AddListener(() => SwitchTab(0));
        if (coinTabButton != null) coinTabButton.onClick.AddListener(() => SwitchTab(1));
        if (skinTabButton != null) skinTabButton.onClick.AddListener(() => SwitchTab(2));

        SwitchTab(0);
        FetchShopItems();
    }

    public void OpenShop()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        FetchShopItems();
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    public void SwitchTab(int tabIndex)
    {
        if (gemTabContent != null) gemTabContent.SetActive(tabIndex == 0);
        if (coinTabContent != null) coinTabContent.SetActive(tabIndex == 1);
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
    /// Bấm Nạp Gem qua PayOS VietQR
    /// </summary>
    public void BuyGemPackage(int amountVnd, string description)
    {
        if (PaymentManager.Instance != null)
        {
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
    /// Bấm Mua vật phẩm Shop/Skin bằng Gem hoặc Vàng qua Backend API
    /// </summary>
    public void BuyShopItem(int shopItemId)
    {
        if (statusText != null) statusText.text = "Processing purchase...";

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
                }
                else
                {
                    if (statusText != null) statusText.text = $"<color=red>{res.message}</color>";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShopUIController] Parse error: {ex.Message}");
            }
        }, (err) =>
        {
            Debug.LogError($"[ShopUIController] API Buy Item Error: {err}");
            if (statusText != null) statusText.text = "<color=red>Purchase Failed!</color>";
        });
    }
}
