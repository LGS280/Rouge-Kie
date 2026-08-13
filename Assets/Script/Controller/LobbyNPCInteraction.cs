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
        if (promptTextUI != null)
        {
            promptTextUI.text = promptText;
        }

        if (floatingCanvas != null)
        {
            floatingCanvas.SetActive(false);
        }
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
                if (ShopUIController.Instance != null)
                {
                    ShopUIController.Instance.OpenShop();
                }
                else
                {
                    ShopUIController shop = Object.FindFirstObjectByType<ShopUIController>();
                    if (shop != null) shop.OpenShop();
                    else Debug.LogWarning("[LobbyNPCInteraction] Không tìm thấy ShopUIController!");
                }
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
