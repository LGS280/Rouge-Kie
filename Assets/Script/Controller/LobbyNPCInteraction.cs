using UnityEngine;
using TMPro;

public enum LobbyInteractionType
{
    ShopMerchant,       // Shop bán đồ / vũ khí
    DungeonPortal,      // Cổng vào Dungeon / Bắt đầu trận
    LeaderboardBoard,   // Bảng xếp hạng
    PlayerProfile       // Xem trang phục / thông tin người chơi
}

public class LobbyNPCInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string entityName = "Armory Merchant";
    public string promptText = "Bấm [E] mở Shop Vũ Khí";
    public LobbyInteractionType interactionType = LobbyInteractionType.ShopMerchant;

    [Header("UI References")]
    public GameObject floatingCanvas;
    public TextMeshProUGUI promptTextUI;

    private bool isPlayerInRange = false;

    private void Start()
    {
        if (floatingCanvas == null)
        {
            CreateAutoFloatingCanvas();
        }

        if (promptTextUI != null)
        {
            promptTextUI.text = promptText;
        }

        if (floatingCanvas != null)
        {
            floatingCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Tự động tạo Canvas World Space chữ nổi trên đầu NPC chuẩn tỷ lệ 100% không cần chỉnh tay Inspector
    /// </summary>
    private void CreateAutoFloatingCanvas()
    {
        GameObject canvasObj = new GameObject("AutoPromptCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(250, 60);
        canvasRect.localScale = new Vector3(0.0065f, 0.0065f, 1f); // Kích thước vừa vặn cân đối
        canvasRect.anchoredPosition = new Vector2(0f, 0.68f); // Vị trí chuẩn sát đỉnh đầu NPC

        GameObject textObj = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(promptText) ? "Bấm [E] mở Shop" : promptText;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = 22; // Cỡ chữ max 22 hoàn hảo

        promptTextUI = tmp;
        floatingCanvas = canvasObj;
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            ExecuteInteraction();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null || other.name.Contains("Player") || other.name.Contains("Rookie"))
        {
            isPlayerInRange = true;
            if (floatingCanvas != null) floatingCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null || other.name.Contains("Player") || other.name.Contains("Rookie"))
        {
            isPlayerInRange = false;
            if (floatingCanvas != null) floatingCanvas.SetActive(false);
        }
    }

    public void ExecuteInteraction()
    {
        Debug.Log($"[LobbyNPCInteraction] Kích hoạt tương tác với {entityName} ({interactionType})");

        switch (interactionType)
        {
            case LobbyInteractionType.ShopMerchant:
                ShopUIController shop = ShopUIController.Instance;
                if (shop == null) shop = Object.FindFirstObjectByType<ShopUIController>();

                if (shop == null)
                {
                    GameObject shopObj = new GameObject("ShopUIController");
                    shop = shopObj.AddComponent<ShopUIController>();
                }
                shop.OpenShop();
                break;

            case LobbyInteractionType.DungeonPortal:
                Debug.Log("[LobbyNPCInteraction] Chuyển tới Dungeon (SampleScene)...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
                break;

            case LobbyInteractionType.LeaderboardBoard:
                LeaderboardUI leaderboard = Object.FindFirstObjectByType<LeaderboardUI>();
                if (leaderboard != null)
                {
                    leaderboard.ShowLeaderboard();
                }
                else
                {
                    Debug.Log("[LobbyNPCInteraction] Leaderboard UI Opened");
                }
                break;

            case LobbyInteractionType.PlayerProfile:
                PlayerProfileUI profile = Object.FindFirstObjectByType<PlayerProfileUI>();
                if (profile != null)
                {
                    profile.RefreshProfile();
                }
                break;
        }
    }
}
