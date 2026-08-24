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
    [SerializeField] private Button copyRoomCodeButton;

    [Header("Public Room List UI (Optional)")]
    [SerializeField] private Transform roomListContainer; // Khung Content trong ScrollView chứa danh sách các phòng
    [SerializeField] private GameObject roomItemPrefab; // Prefab thanh thông tin phòng (Text + Nút Join)
    [SerializeField] private TMP_Text emptyRoomListText; // Text "Hiện không có phòng nào đang mở"
    [SerializeField] private Button refreshRoomsButton; // Nút làm mới danh sách phòng

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
            NetworkManager.Instance.OnReceivePublicRooms += HandleReceivePublicRooms;
        }

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.onClick.AddListener(OnCopyRoomCodePressed);
        }

        if (refreshRoomsButton != null)
        {
            refreshRoomsButton.onClick.AddListener(OnRefreshRoomsPressed);
        }

        // Xóa sạch các GameObject mẫu đặt sẵn trong Editor khi bắt đầu
        if (roomListContainer != null)
        {
            foreach (Transform child in roomListContainer)
            {
                Destroy(child.gameObject);
            }
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
            NetworkManager.Instance.OnReceivePublicRooms -= HandleReceivePublicRooms;
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

        // Tự động làm mới danh sách phòng khi mở sảnh
        OnRefreshRoomsPressed();

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
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RequestLeaveRoom();
        }

        lobbyMenuPanel.SetActive(true);
        playMenuPanel.SetActive(false);
        roomLobbyPanel.SetActive(false);
        ResetCopyButtonText();

        // Làm mới lại danh sách phòng sau khi vừa rời
        OnRefreshRoomsPressed();
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
        ResetCopyButtonText();

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
        ResetCopyButtonText();

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

    public void OnRefreshRoomsPressed()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RequestGetPublicRooms();
        }
    }

    private void HandleReceivePublicRooms(List<NetworkManager.PublicRoomInfo> rooms)
    {
        if (roomListContainer == null) return;

        // Dọn sạch các phòng cũ hiển thị trong ScrollView
        foreach (Transform child in roomListContainer)
        {
            Destroy(child.gameObject);
        }

        if (rooms == null || rooms.Count == 0)
        {
            if (emptyRoomListText != null)
            {
                emptyRoomListText.gameObject.SetActive(true);
                emptyRoomListText.text = "Chưa có phòng nào đang mở. Hãy tạo phòng mới!";
            }
            return;
        }

        if (emptyRoomListText != null) emptyRoomListText.gameObject.SetActive(false);

        foreach (var r in rooms)
        {
            if (roomItemPrefab != null)
            {
                GameObject itemObj = Instantiate(roomItemPrefab, roomListContainer);

                string host = !string.IsNullOrEmpty(r.hostName) ? r.hostName : "Host";
                int current = r.currentPlayers > 0 ? r.currentPlayers : 1;
                int max = r.maxPlayers > 0 ? r.maxPlayers : 4;

                // Tìm nút Join trước
                Button joinBtn = itemObj.GetComponentInChildren<Button>(true);

                // Tìm chính xác Text hiển thị thông tin phòng (loại trừ Text bên trong Button)
                TMP_Text[] allTexts = itemObj.GetComponentsInChildren<TMP_Text>(true);
                TMP_Text infoText = null;

                foreach (var txt in allTexts)
                {
                    if (joinBtn != null && txt.transform.IsChildOf(joinBtn.transform))
                    {
                        // Giữ nguyên hoặc đặt chữ của nút bấm là "JOIN"
                        txt.text = "JOIN";
                    }
                    else
                    {
                        infoText = txt;
                    }
                }

                if (infoText != null)
                {
                    string status = r.isGameStarted ? "<color=#FF4444>[IN-GAME]</color>" : "<color=#00FF66>[WAITING]</color>";
                    infoText.text = $"{status} <b>{host}</b> ({current}/{max})";
                }

                // Gán sự kiện cho Nút Join 1-Click
                if (joinBtn != null)
                {
                    if (r.isGameStarted || (max > 0 && current >= max))
                    {
                        joinBtn.interactable = false;
                    }
                    else
                    {
                        joinBtn.interactable = true;
                        string code = r.roomCode;
                        joinBtn.onClick.RemoveAllListeners();
                        joinBtn.onClick.AddListener(() =>
                        {
                            string username = GetValidUsername();
                            if (roomCodeInput != null) roomCodeInput.text = code;
                            NetworkManager.Instance.RequestJoinRoom(code, username);
                        });
                    }
                }
            }
        }
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
        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.ShowLoading("TẦNG 1 - 1", "Đang kết nối và khởi tạo phòng chơi Co-op...");
        }
        // Tải Scene chơi game thực tế của bạn
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
    }

    private Coroutine copyFeedbackCoroutine;

    public void ResetCopyButtonText()
    {
        if (copyFeedbackCoroutine != null)
        {
            StopCoroutine(copyFeedbackCoroutine);
            copyFeedbackCoroutine = null;
        }

        if (copyRoomCodeButton != null)
        {
            Text btnText = copyRoomCodeButton.GetComponentInChildren<Text>();
            TMP_Text tmpBtnText = copyRoomCodeButton.GetComponentInChildren<TMP_Text>();

            if (btnText != null) btnText.text = "Copy";
            if (tmpBtnText != null) tmpBtnText.text = "Copy";
        }
    }

    public void OnCopyRoomCodePressed()
    {
        string roomCode = NetworkManager.Instance != null ? NetworkManager.Instance.CurrentRoomId : "";
        if (string.IsNullOrEmpty(roomCode) && roomCodeText != null)
        {
            string fullText = roomCodeText.text;
            if (fullText.Contains(":"))
            {
                roomCode = fullText.Split(':')[1].Trim();
            }
        }

        if (!string.IsNullOrEmpty(roomCode))
        {
            GUIUtility.systemCopyBuffer = roomCode; // Lưu Mã Phòng vào Clipboard hệ thống
            Debug.Log($"[LobbyUIController] Đã sao chép Mã Phòng '{roomCode}' vào Clipboard!");

            if (copyFeedbackCoroutine != null)
            {
                StopCoroutine(copyFeedbackCoroutine);
            }
            copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedbackRoutine());
        }
    }

    private System.Collections.IEnumerator ShowCopyFeedbackRoutine()
    {
        if (copyRoomCodeButton != null)
        {
            Text btnText = copyRoomCodeButton.GetComponentInChildren<Text>();
            TMP_Text tmpBtnText = copyRoomCodeButton.GetComponentInChildren<TMP_Text>();

            if (btnText != null) btnText.text = "Copied";
            if (tmpBtnText != null) tmpBtnText.text = "Copied";

            yield return new WaitForSeconds(1.5f);

            if (btnText != null) btnText.text = "Copy";
            if (tmpBtnText != null) tmpBtnText.text = "Copy";
        }
        copyFeedbackCoroutine = null;
    }
}