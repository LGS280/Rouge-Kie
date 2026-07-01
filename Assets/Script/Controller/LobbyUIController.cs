using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LobbyUIController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject playMenuPanel;
    [SerializeField] private GameObject lobbyMenuPanel;
    [SerializeField] private GameObject roomLobbyPanel;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playerListText;

    [Header("Lobby Action Buttons")]
    [SerializeField] private Button startGameButton;

    private List<string> activePlayers = new List<string>();

    private void Start()
    {
        // Đăng ký lắng nghe sự kiện mạng từ NetworkManager
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRoomCreated += HandleRoomCreated;
            NetworkManager.Instance.OnJoinRoomSuccess += HandleJoinRoomSuccess;
            NetworkManager.Instance.OnJoinRoomFailed += HandleJoinRoomFailed;
            NetworkManager.Instance.OnPlayerJoined += HandlePlayerJoined;
            NetworkManager.Instance.OnPlayerDisconnected += HandlePlayerDisconnected;
            NetworkManager.Instance.OnGameStarted += HandleGameStarted;

        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký để tránh rò rỉ bộ nhớ (Memory Leak)
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRoomCreated -= HandleRoomCreated;
            NetworkManager.Instance.OnJoinRoomSuccess -= HandleJoinRoomSuccess;
            NetworkManager.Instance.OnJoinRoomFailed -= HandleJoinRoomFailed;
            NetworkManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
            NetworkManager.Instance.OnPlayerDisconnected -= HandlePlayerDisconnected;
            NetworkManager.Instance.OnGameStarted -= HandleGameStarted;
        }
    }

    // --- HÀNH ĐỘNG CỦA CÁC NÚT BẤM ---

    public void OnCoOpButtonPressed()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("LỖI: Chưa có GameObject 'NetworkManager' trong Scene! Hãy kéo thả script NetworkManager vào một GameObject trống ngoài Hierarchy.");
            return; 
        }

        // TẠM THỜI: Tự động đăng nhập Guest nếu chưa đăng nhập khi test Co-op
        //if (!NetworkManager.Instance.IsLoggedIn)
        //{
        //    NetworkManager.Instance.IsLoggedIn = true; // Gán tạm bằng true để bypass
        //    NetworkManager.Instance.LoggedInUsername = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
        //    usernameInput.text = NetworkManager.Instance.LoggedInUsername;
        //}

        // --- BƯỚC KIỂM TRA ĐĂNG NHẬP THỰC TẾ ---
        if (!NetworkManager.Instance.IsLoggedIn)
        {
            Debug.Log("Chưa đăng nhập! Đang gọi Scene Login/Register...");

            // 1. Kiểm tra xem Scene Login đã được load chưa để tránh load trùng
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("LoginScrene").isLoaded)
            {
                // 2. Tải cộng dồn Scene Login đè lên Main Menu (nhớ đổi đúng tên Scene của bạn)
                UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScrene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            }
            return; // Dừng hàm tại đây, không cho vào Lobby Menu bên dưới
        }

        if (usernameInput != null && !string.IsNullOrEmpty(NetworkManager.Instance.LoggedInUsername))
        {
            usernameInput.text = NetworkManager.Instance.LoggedInUsername;
        }

        // Bỏ qua kiểm tra và cho phép mở sảnh chọn Co-op ngay lập tức
        playMenuPanel.SetActive(false);
        lobbyMenuPanel.SetActive(true);
        roomLobbyPanel.SetActive(false);

        //playMenuPanel.SetActive(false);
        //lobbyMenuPanel.SetActive(true);
        //roomLobbyPanel.SetActive(false);
    }

    public void OnCreateRoomPressed()
    {
        string username = GetValidUsername();
        NetworkManager.Instance.RequestCreateRoom(username);
    }

    public void OnJoinRoomPressed()
    {
        string username = GetValidUsername();
        string roomCode = roomCodeInput.text.Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogError("Mã phòng không được để trống!");
            return;
        }

        NetworkManager.Instance.RequestJoinRoom(roomCode, username);
    }

    public void OnBackPressedFromLobbyMenu()
    {
        lobbyMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
    }

    public void OnBackPressedFromRoomCode()
    {
        lobbyMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        roomLobbyPanel.SetActive(false);
    }

    public void OnStartGamePressed()
    {
        // Gửi lệnh yêu cầu bắt đầu game lên Server
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RequestStartGame();
        }
    }

    // --- XỬ LÝ SỰ KIỆN MẠNG TRẢ VỀ ---

    private void HandleRoomCreated(string roomCode)
    {
        lobbyMenuPanel.SetActive(false);
        roomLobbyPanel.SetActive(true);

        roomCodeText.text = $"ROOM CODE: {roomCode}";

        // Vì mình tạo phòng, mình là Host và là người chơi đầu tiên
        activePlayers.Clear();
        activePlayers.Add($"{usernameInput.text} (Host)");
        UpdatePlayerListUI();

        // Kích hoạt nút Start Game vì mình là Host
        startGameButton.gameObject.SetActive(true);
        startGameButton.interactable = true;
    }

    private void HandleJoinRoomSuccess(string roomCode, List<string> playersInRoom)
    {
        lobbyMenuPanel.SetActive(false);
        roomLobbyPanel.SetActive(true);

        roomCodeText.text = $"ROOM CODE: {roomCode}";

        // Cập nhật danh sách người chơi hiện có
        activePlayers = new List<string>(playersInRoom);
        UpdatePlayerListUI();

        // Mình không phải Host nên ẩn nút Start Game (chờ Host ấn)
        startGameButton.gameObject.SetActive(false);
    }

    private void HandleJoinRoomFailed(string error)
    {
        Debug.LogError($"Lỗi: {error}");
        // Có thể bổ sung UI Popup thông báo lỗi cho người chơi tại đây
    }

    private void HandlePlayerJoined(string username, string connId)
    {
        Debug.Log($"Người chơi mới vào: {username}");
        activePlayers.Add(username);
        UpdatePlayerListUI();
    }

    private void HandlePlayerDisconnected(string username, string connId)
    {
        Debug.Log($"Người chơi thoát: {username}");
        activePlayers.Remove(username);
        UpdatePlayerListUI();
    }

    // --- HÀM PHỤ TRỢ ---

    private string GetValidUsername()
    {
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            // Nếu không nhập tên, tự đặt tên mặc định kèm số ngẫu nhiên
            username = $"Player_{Random.Range(1000, 9999)}";
            usernameInput.text = username;
        }
        return username;
    }

    private void UpdatePlayerListUI()
    {
        playerListText.text = "PLAYERS IN ROOM:\n";
        for (int i = 0; i < activePlayers.Count; i++)
        {
            playerListText.text += $"{i + 1}. {activePlayers[i]}\n";
        }
    }

    private void HandleGameStarted()
    {
        Debug.Log("Trận đấu bắt đầu! Đang tải màn chơi...");
        // Tải Scene chơi game thực tế của bạn
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
    }
}