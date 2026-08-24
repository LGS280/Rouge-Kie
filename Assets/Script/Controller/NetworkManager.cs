using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading; // Thư viện quản lý luồng chính
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _instance;
    public static NetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<NetworkManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("NetworkManager");
                    _instance = go.AddComponent<NetworkManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Server Connection Settings")]
    [SerializeField] private string serverUrl = "http://localhost:5000/gamehub";

    [Header("Player Session Status (Debug)")]
    public bool IsLoggedIn = false;
    public string LoggedInUsername = "Guest";

    // BỔ SUNG: Thuộc tính lưu trữ mã phòng chơi hiện tại (Multiplayer-Ready)
    public string CurrentRoomId { get; private set; }

    private HubConnection hubConnection;
    private SynchronizationContext unityContext; // Đồng bộ luồng chính Unity

    public event System.Action<string, string, Vector3, Vector3> OnRemotePlayerShoot;
    public event System.Action<string, float> OnRemoteEnemyDamaged;
    public event System.Action<string, float, float, float> OnReceiveWeaponAngle;

    [System.Serializable]
    public class PublicRoomInfo
    {
        public string roomCode;
        public string hostName;
        public int currentPlayers;
        public int maxPlayers;
        public bool isGameStarted;
    }

    // --- CÁC SỰ KIỆN C# ĐỂ LỚP UI & SYNC MANAGER LẮNG NGHE ---
    public event Action<string> OnRoomCreated;
    public event Action<string, List<string>> OnJoinRoomSuccess;
    public event Action<string> OnJoinRoomFailed;
    public event Action<string, string> OnPlayerJoined;
    public event Action<string, string> OnPlayerDisconnected;
    public event Action<List<PublicRoomInfo>> OnReceivePublicRooms;

    // Sự kiện đồng bộ vị trí (Đồng đội gọi)
    public event Action<string, float, float> OnReceivePosition;

    // Sự kiện bắt đầu game
    public event Action OnGameStarted;

    // CÁC SỰ KIỆN ĐỒNG BỘ PHÒNG (ROOM COMBAT)
    public event Action<string, float, float> OnRoomCombatStarted; // roomId, centerX, centerY
    public event Action<string> OnRoomClearedFromServer;          // roomId
    public event Action<string, float, float> OnReceiveEnemyPosition; // enemyId, x, y

    // BỔ SUNG: Sự kiện đồng bộ chuyển tầng hầm ngục Co-op giữa các máy trong phòng
    public event Action<int> OnFloorTransitionSynced;

    // BỔ SUNG: Sự kiện đồng bộ loại súng chính và súng phụ Remote Player đang cầm (connId, activeWeaponName, secondaryWeaponName)
    public event Action<string, string, string> OnRemoteWeaponChanged;

    // BỔ SUNG: Sự kiện đồng bộ phòng đã mở trên Minimap cho đồng đội
    public event Action<string> OnRemoteRoomVisited;

    // BỔ SUNG: Sự kiện đồng bộ sát thương quái đánh trúng người chơi qua mạng (targetConnId, damage)
    public event Action<string, float> OnPlayerDamaged;

    // BỔ SUNG: Sự kiện đồng bộ khi đồng đội hy sinh (connId)
    public event Action<string> OnRemotePlayerDied;

    // BỔ SUNG: Sự kiện đồng bộ khi tất cả thành viên trong phòng Co-op đều đã hy sinh
    public event Action OnTeamDefeat;

    // BỔ SUNG: Sự kiện đồng bộ khi người chơi được hồi sinh (targetConnId, reviveHp)
    public event Action<string, int> OnPlayerRevived;

    // BỔ SUNG: Sự kiện đồng bộ khi Host ngắt kết nối/out game (hostName)
    public event Action<string> OnHostDisconnectedEndGame;

    public string MyConnectionId => hubConnection?.ConnectionId;

    // QUYỀN HẠN TRONG TRẬN: Sẽ được Server định đoạt khi tạo hoặc vào phòng thành công
    public string UserRole = "Guest";

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            unityContext = SynchronizationContext.Current;

            // Tự động khôi phục phiên đăng nhập từ PlayerPrefs khi khởi động game (Đặt trong Awake để chạy trước Start của các UI khác)
            string savedToken = PlayerPrefs.GetString("jwt_token", "");
            string savedUsername = PlayerPrefs.GetString("username", "Guest");
            if (!string.IsNullOrEmpty(savedToken))
            {
                IsLoggedIn = true;
                LoggedInUsername = savedUsername;
                UserRole = "Player";
                Debug.Log($"[NetworkManager] Tự động đăng nhập người dùng: {LoggedInUsername}");
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // BỔ SUNG: Tự động đồng bộ URL Server SignalR dựa trên cấu hình appsettings.json của GameConfigManager
        if (GameConfigManager.Instance != null && !string.IsNullOrEmpty(GameConfigManager.Instance.BaseUrl))
        {
            serverUrl = GameConfigManager.Instance.BaseUrl.Replace("/api", "/gamehub");
        }

        hubConnection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        // --- ĐĂNG KÝ LẮNG NGHE TỪ SERVER ---

        // CẬP NHẬT CÁCH 1: Hứng thêm biến bool isHost từ Server gửi về để phân quyền
        hubConnection.On<string, bool>("OnRoomCreated", (roomCode, isHost) =>
        {
            CurrentRoomId = roomCode;
            UserRole = isHost ? "Host" : "Client";
            Debug.Log($"Đã tạo phòng thành công! RoomCode: {roomCode} | Quyền của bạn: {UserRole}");

            unityContext.Post(_ => OnRoomCreated?.Invoke(roomCode), null);
        });

        // CẬP NHẬT CÁCH 1: Hứng thêm biến bool isHost từ Server gửi về khi vào phòng thành công
        hubConnection.On<string, List<string>, bool>("OnJoinRoomSuccess", (roomCode, players, isHost) =>
        {
            CurrentRoomId = roomCode;
            UserRole = isHost ? "Host" : "Client";
            Debug.Log($"Đã vào phòng thành công! RoomCode: {roomCode} | Quyền của bạn: {UserRole}");

            unityContext.Post(_ => OnJoinRoomSuccess?.Invoke(roomCode, players), null);
        });

        hubConnection.On<string>("OnJoinRoomFailed", (errorMessage) =>
        {
            unityContext.Post(_ => OnJoinRoomFailed?.Invoke(errorMessage), null);
        });

        hubConnection.On<string, string>("OnPlayerJoined", (username, connId) =>
        {
            unityContext.Post(_ => OnPlayerJoined?.Invoke(username, connId), null);
        });

        hubConnection.On<string, string>("OnPlayerDisconnected", (username, connId) =>
        {
            unityContext.Post(_ => OnPlayerDisconnected?.Invoke(username, connId), null);
        });

        // Đăng ký lắng nghe gói tin tọa độ của đồng đội từ Server gửi về
        hubConnection.On<string, float, float>("OnReceivePosition", (connId, x, y) =>
        {
            unityContext.Post(_ => OnReceivePosition?.Invoke(connId, x, y), null);
        });

        // Đăng ký lắng nghe tín hiệu bắt đầu game
        hubConnection.On("OnGameStarted", () =>
        {
            unityContext.Post(_ => OnGameStarted?.Invoke(), null);
        });

        // LẮNG NGHE LỆNH ROOM TỪ SERVER HUB GỬI VỀ
        hubConnection.On<string, float, float>("OnRoomCombatStarted", (roomId, centerX, centerY) =>
        {
            unityContext.Post(_ => OnRoomCombatStarted?.Invoke(roomId, centerX, centerY), null);
        });

        hubConnection.On<string>("OnRoomClearedFromServer", (roomId) =>
        {
            unityContext.Post(_ => OnRoomClearedFromServer?.Invoke(roomId), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đồng bộ chuyển tầng từ Server phát xuống
        hubConnection.On<int>("OnFloorTransitionSynced", (targetFloor) =>
        {
            unityContext.Post(_ => OnFloorTransitionSynced?.Invoke(targetFloor), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đổi súng của đồng đội từ Server phát xuống
        hubConnection.On<string, string, string>("OnRemoteWeaponChanged", (connId, activeName, secondaryName) =>
        {
            unityContext.Post(_ => OnRemoteWeaponChanged?.Invoke(connId, activeName, secondaryName), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đồng bộ phòng mở trên Minimap từ đồng đội
        hubConnection.On<string>("OnRemoteRoomVisited", (roomUniqueId) =>
        {
            unityContext.Post(_ => OnRemoteRoomVisited?.Invoke(roomUniqueId), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đồng bộ sát thương quái đánh trúng người chơi từ Server
        hubConnection.On<string, float>("OnPlayerDamaged", (targetConnId, damage) =>
        {
            unityContext.Post(_ => OnPlayerDamaged?.Invoke(targetConnId, damage), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đồng bộ người chơi hy sinh từ Server
        hubConnection.On<string>("OnRemotePlayerDied", (connId) =>
        {
            unityContext.Post(_ => OnRemotePlayerDied?.Invoke(connId), null);
        });

        hubConnection.On("OnTeamDefeat", () =>
        {
            unityContext.Post(_ => OnTeamDefeat?.Invoke(), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện đồng bộ người chơi được hồi sinh từ đồng đội
        hubConnection.On<string, int>("OnPlayerRevived", (connId, reviveHp) =>
        {
            unityContext.Post(_ => OnPlayerRevived?.Invoke(connId, reviveHp), null);
        });

        // BỔ SUNG: Lắng nghe sự kiện Host ngắt kết nối/out game
        hubConnection.On<string>("OnHostDisconnectedEndGame", (hostName) =>
        {
            unityContext.Post(_ => OnHostDisconnectedEndGame?.Invoke(hostName), null);
        });

        // BỔ SUNG: Lắng nghe danh sách phòng từ Server trả về
        hubConnection.On<List<PublicRoomInfo>>("OnReceivePublicRooms", (rooms) =>
        {
            unityContext.Post(_ => OnReceivePublicRooms?.Invoke(rooms), null);
        });

        // Gọi hàm đăng ký các sự kiện Combat mạng
        RegisterCombatCallbacks();

        try
        {
            await hubConnection.StartAsync();
            Debug.Log("Kết nối thành công tới Server SignalR!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Kết nối Server thất bại: {ex.Message}");
        }
    }

    // --- CÁC HÀM GỬI LỆNH LÊN SERVER ---

    public async void RequestGetPublicRooms()
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("GetPublicRooms");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] RequestGetPublicRooms gián đoạn: {ex.Message}");
        }
    }

    public async void RequestLeaveRoom()
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("LeaveRoom");
            }
            CurrentRoomId = null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] RequestLeaveRoom gián đoạn: {ex.Message}");
        }
    }

    public async void RequestCreateRoom(string username)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("CreateRoom", username);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] RequestCreateRoom gián đoạn: {ex.Message}");
        }
    }

    public async void RequestJoinRoom(string roomCode, string username)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("JoinRoom", roomCode, username);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] RequestJoinRoom gián đoạn: {ex.Message}");
        }
    }

    // Hàm gửi tọa độ di chuyển của người chơi cục bộ lên Server
    public async void SendPlayerPosition(float x, float y)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SyncPosition", x, y);
            }
        }
        catch (Exception ex)
        {
            // Bắt ngoại lệ khi ngắt kết nối/reconnect tạm thời, tránh văng lỗi lên Unity SynchronizationContext
            Debug.LogWarning($"[NetworkManager] SendPlayerPosition tạm thời bị gián đoạn: {ex.Message}");
        }
    }

    // Hàm gửi yêu cầu bắt đầu game lên Server (Chỉ Host gọi)
    public async void RequestStartGame()
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("StartGame");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] RequestStartGame gián đoạn: {ex.Message}");
        }
    }

    // CÁC PHƯƠNG THỨC GỬI SỰ KIỆN ROOM LÊN SERVER

    // Gọi khi có bất kỳ ai bước vào một phòng combat (Gửi vị trí người kích hoạt thay vì tâm phòng)
    public async void SendRoomCombatTrigger(string targetRoomId, Vector3 triggerPlayerPos)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                // Gửi lên Hub: Match Room Id hiện tại, Unique Room Id tự sinh, và tọa độ người kích hoạt ngay cửa
                await hubConnection.InvokeAsync("TriggerRoomCombat", CurrentRoomId, targetRoomId, triggerPlayerPos.x, triggerPlayerPos.y);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendRoomCombatTrigger gián đoạn: {ex.Message}");
        }
    }

    // Gọi khi một phòng đã hết sạch quái
    public async void SendRoomClearedEvent(string targetRoomId)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("RegisterRoomCleared", CurrentRoomId, targetRoomId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendRoomClearedEvent gián đoạn: {ex.Message}");
        }
    }

    private async void OnDestroy()
    {
        if (hubConnection != null)
        {
            await hubConnection.StopAsync();
            await hubConnection.DisposeAsync();
        }
    }

    // Đăng ký Listener trong hàm khởi tạo kết nối SignalR (ví dụ: RegisterHubCallbacks)
    private void RegisterCombatCallbacks()
    {
        // Đồng bộ bắn súng
        hubConnection.On<string, string, float, float, float, float>("OnPlayerShoot", (playerId, weaponId, px, py, dx, dy) =>
        {
            unityContext.Post(_ =>
            {
                OnRemotePlayerShoot?.Invoke(playerId, weaponId, new Vector3(px, py, 0), new Vector3(dx, dy, 0));
            }, null);
        });

        // Đồng bộ sát thương quái
        hubConnection.On<string, float>("OnEnemyDamaged", (enemyId, damage) =>
        {
            unityContext.Post(_ =>
            {
                OnRemoteEnemyDamaged?.Invoke(enemyId, damage);
            }, null);
        });

        // Đăng ký lắng nghe góc quay súng
        hubConnection.On<string, float, float, float>("OnReceiveShoot", (connId, angle, px, py) =>
        {
            unityContext.Post(_ =>
            {
                OnReceiveWeaponAngle?.Invoke(connId, angle, px, py);
            }, null);
        });

        // Đăng ký lắng nghe vị trí quái vật
        hubConnection.On<string, float, float>("OnReceiveEnemyPosition", (enemyId, x, y) =>
        {
            unityContext.Post(_ =>
            {
                OnReceiveEnemyPosition?.Invoke(enemyId, x, y);
            }, null);
        });
    }

    // --- PHƯƠNG THỨC GỬI LÊN SERVER (API KHÁCH GỌI) ---

    public async void SendShootEvent(string weaponId, Vector3 position, Vector3 direction)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SendShoot", CurrentRoomId, weaponId, position.x, position.y, direction.x, direction.y);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendShootEvent gián đoạn: {ex.Message}");
        }
    }

    public async void SendEnemyHitEvent(string roomId, string enemyId, float damage)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("RegisterEnemyHit", roomId, enemyId, damage);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendEnemyHitEvent gián đoạn: {ex.Message}");
        }
    }

    public async void SendWeaponAngle(float angle, float px, float py)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SyncShoot", angle, px, py);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendWeaponAngle gián đoạn: {ex.Message}");
        }
    }

    public async void SendEnemyPosition(string enemyId, float x, float y)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
            {
                await hubConnection.InvokeAsync("SyncEnemyPosition", CurrentRoomId, enemyId, x, y);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendEnemyPosition gián đoạn: {ex.Message}");
        }
    }

    // BỔ SUNG: Gửi yêu cầu chuyển tầng đồng bộ tới toàn bộ người chơi trong phòng Co-op
    public async void SendNextFloorRequest(int targetFloor)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("RequestNextFloor", CurrentRoomId, targetFloor);
                Debug.Log($"[NetworkManager] Đã gửi yêu cầu chuyển sang Tầng {targetFloor} lên Server.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendNextFloorRequest gián đoạn: {ex.Message}");
        }
    }

    // BỔ SUNG: Gửi thông báo đổi súng chính và súng phụ hiển thị qua mạng tới các người chơi khác trong phòng
    public async void SendEquippedWeapon(string activeWeaponName, string secondaryWeaponName)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("SyncEquippedWeapon", CurrentRoomId, activeWeaponName, secondaryWeaponName);
                Debug.Log($"[NetworkManager] Đã gửi thông báo đổi súng '{activeWeaponName}' & phụ '{secondaryWeaponName}' lên Server.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendEquippedWeapon gián đoạn: {ex.Message}");
        }
    }

    // BỔ SUNG: Gửi thông báo đã mở phòng trên Minimap tới các người chơi khác
    public async void SendRoomVisited(string roomUniqueId)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("SyncRoomVisited", CurrentRoomId, roomUniqueId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendRoomVisited gián đoạn: {ex.Message}");
        }
    }

    // BỔ SUNG: Gửi thông báo sát thương quái đánh trúng người chơi qua mạng
    public async void SendPlayerDamaged(string targetConnId, float damage)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("SyncPlayerDamaged", CurrentRoomId, targetConnId, damage);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendPlayerDamaged gián đoạn: {ex.Message}");
        }
    }

    // BỔ SUNG: Gửi thông báo người chơi hy sinh (Player Death) lên Server cho đồng đội
    public async void SendPlayerDeath()
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("SyncPlayerDeath", CurrentRoomId);
                Debug.Log("[NetworkManager] Đã gửi thông báo Player Death lên Server.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendPlayerDeath gián đoạn: {ex.Message}");
        }
    }

    // Thêm mới: Ngắt kết nối phòng chơi hiện tại và kết nối lại để reset trạng thái phòng nhưng giữ phiên đăng nhập
    public async System.Threading.Tasks.Task DisconnectAndReconnect()
    {
        CurrentRoomId = null;
        UserRole = "Guest";

        if (hubConnection != null)
        {
            try
            {
                await hubConnection.StopAsync();
                await hubConnection.StartAsync();
                Debug.Log("[NetworkManager] Đã ngắt kết nối phòng cũ và reconnect SignalR thành công.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Lỗi khi reconnect SignalR: {ex.Message}");
            }
        }
    }

    // BỔ SUNG: Gửi lệnh Hồi Sinh đồng đội (SendPlayerRevive) qua SignalR
    public async void SendPlayerRevive(string targetConnId, int reviveHp)
    {
        try
        {
            if (hubConnection != null && hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(CurrentRoomId))
            {
                await hubConnection.InvokeAsync("SyncPlayerRevive", CurrentRoomId, targetConnId, reviveHp);
                Debug.Log($"[NetworkManager] Đã gửi thông báo Hồi Sinh cho player: {targetConnId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] SendPlayerRevive gián đoạn: {ex.Message}");
        }
    }
}