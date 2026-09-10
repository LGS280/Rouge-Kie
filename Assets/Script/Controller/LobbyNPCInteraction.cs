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
    public string promptText = "Press [E] to Open Armory Shop";
    public LobbyInteractionType interactionType = LobbyInteractionType.ShopMerchant;

    [Header("UI References")]
    public GameObject floatingCanvas;
    public TextMeshProUGUI promptTextUI;

    private bool isPlayerInRange = false;
    private Transform portalVisualTransform;

    private bool hasTriggeredDungeonPortal = false;

    private void Start()
    {
        if (interactionType == LobbyInteractionType.DungeonPortal)
        {
            EnsurePortalVisual();
            EnsureInvisibleWall();
        }
        else
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
    }

    /// <summary>
    /// Tự động sinh Tường Vô Hình (Solid Invisible Wall) phía sau Cổng Teleport để ngăn người chơi chạy lọt ra ngoài bản đồ
    /// </summary>
    private void EnsureInvisibleWall()
    {
        Transform existingWall = transform.Find("InvisibleWall");
        if (existingWall != null) return;

        GameObject wallObj = new GameObject("InvisibleWall");
        wallObj.transform.SetParent(transform, false);
        wallObj.transform.localPosition = new Vector3(0f, 0.8f, 0f); // Nằm ngay mép trên phía sau Cổng

        BoxCollider2D wallCol = wallObj.AddComponent<BoxCollider2D>();
        wallCol.isTrigger = false; // Tường vật lý cứng chặn 100% nhân vật không thể đi qua
        wallCol.size = new Vector2(10f, 1.5f); // Bề rộng 10 unit bịt kín khe rào
    }

    /// <summary>
    /// Tự động sinh đồ họa Cổng Teleport ma thuật xanh Cyan phát sáng xoay mượt mà chuẩn 1:1 như trong Map
    /// </summary>
    private void EnsurePortalVisual()
    {
        // Reset scale của parent về (1,1,1) để tránh bị dẹt ngang do Inspector
        transform.localScale = Vector3.one;

        SpriteRenderer existingRenderer = GetComponent<SpriteRenderer>();
        if (existingRenderer != null && existingRenderer.sprite != null)
        {
            portalVisualTransform = transform;
            return;
        }

        Transform existingChild = transform.Find("PortalVisual");
        if (existingChild != null)
        {
            portalVisualTransform = existingChild;
            portalVisualTransform.localScale = new Vector3(5.6f, 5.6f, 1f);
            return;
        }

        GameObject visualObj = new GameObject("PortalVisual");
        visualObj.transform.SetParent(transform, false);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localScale = new Vector3(5.6f, 5.6f, 1f); // Phóng to 5.6x5.6 giữ nguyên hình tròn 1:1

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();

        // Tạo Texture ma thuật Cổng Xanh Cyan phát sáng 64x64 chuẩn hình tròn
        Texture2D portalTex = new Texture2D(64, 64);
        Color cyanCore = new Color(0f, 1f, 1f, 0.95f);
        Color cyanEdge = new Color(0f, 0.5f, 0.9f, 0.3f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                if (dist <= 30f)
                {
                    float alpha = Mathf.Clamp01(1f - (dist / 30f));
                    portalTex.SetPixel(x, y, Color.Lerp(cyanCore, cyanEdge, dist / 30f) * alpha);
                }
                else
                {
                    portalTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        portalTex.Apply();

        sr.sprite = Sprite.Create(portalTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        sr.sortingOrder = 25; // Hiển thị nổi bật trên sàn
        portalVisualTransform = visualObj.transform;
    }

    /// <summary>
    /// Tự động tạo Canvas World Space chữ nổi trên đầu NPC chuẩn tỷ lệ 100% không cần chỉnh tay Inspector
    /// </summary>
    private void CreateAutoFloatingCanvas()
    {
        if (interactionType == LobbyInteractionType.DungeonPortal) return; // Bỏ hoàn toàn chữ nổi ở Cổng Teleport

        GameObject canvasObj = new GameObject("AutoPromptCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(300, 60);
        canvasRect.localScale = new Vector3(0.005f, 0.005f, 1f); // Tỷ lệ chuẩn nét căng không bị méo chữ
        canvasRect.anchoredPosition = new Vector2(0f, 0.65f); // Vị trí chuẩn sát ngay trên đầu NPC Shop Merchant

        GameObject textObj = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(promptText) ? "Press [E] to Open Shop" : promptText;
        tmp.fontSize = 28; // Tăng cỡ chữ to nổi bật hơn chút xíu
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;

        promptTextUI = tmp;
        floatingCanvas = canvasObj;
    }

    private void Update()
    {
        // Hiệu ứng xoay cổng ma thuật mượt mà nếu là DungeonPortal
        if (portalVisualTransform != null && interactionType == LobbyInteractionType.DungeonPortal)
        {
            portalVisualTransform.Rotate(0f, 0f, -90f * Time.deltaTime);
        }

        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E) && interactionType != LobbyInteractionType.DungeonPortal)
        {
            ExecuteInteraction();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null || other.name.Contains("Player") || other.name.Contains("Rookie"))
        {
            isPlayerInRange = true;

            // Nếu là Cổng DungeonPortal: Người chơi bước chân chạm cổng ➔ Tự động vào map chiến đấu luôn y hệt trong map
            if (interactionType == LobbyInteractionType.DungeonPortal)
            {
                if (hasTriggeredDungeonPortal) return;
                hasTriggeredDungeonPortal = true;
                Debug.Log("[LobbyNPCInteraction] Người chơi bước chân vào cổng Teleport ➔ Tự động chuyển tới Dungeon (SampleScene)...");
                ExecuteInteraction();
                return;
            }

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

                if (WeaponManager.Instance != null)
                {
                    WeaponManager.Instance.SaveEquippedWeapons();
                }

                if (LoadingScreenUI.Instance != null)
                {
                    LoadingScreenUI.Instance.ShowLoading("TẦNG 1 - 1", "Đang kết nối và khởi tạo hầm ngục mới...");
                }

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
