using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading; // Thư viện quản lý luồng chính
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

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

    // --- CÁC SỰ KIỆN C# ĐỂ LỚP UI & SYNC MANAGER LẮNG NGHE ---
    public event Action<string> OnRoomCreated;
    public event Action<string, List<string>> OnJoinRoomSuccess;
    public event Action<string> OnJoinRoomFailed;
    public event Action<string, string> OnPlayerJoined;
    public event Action<string, string> OnPlayerDisconnected;

    // Sự kiện đồng bộ vị trí (Đồng đội gọi)
    public event Action<string, float, float> OnReceivePosition;

    // Sự kiện bắt đầu game
    public event Action OnGameStarted;

    public string MyConnectionId => hubConnection?.ConnectionId;

    public string UserRole = "Guest";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            unityContext = SynchronizationContext.Current;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        // --- ĐĂNG KÝ LẮNG NGHE TỪ SERVER ---

        hubConnection.On<string>("OnRoomCreated", (roomCode) =>
        {
            CurrentRoomId = roomCode; // CẬP NHẬT: Lưu lại Room ID cục bộ
            unityContext.Post(_ => OnRoomCreated?.Invoke(roomCode), null);
        });

        hubConnection.On<string, List<string>>("OnJoinRoomSuccess", (roomCode, players) =>
        {
            CurrentRoomId = roomCode; // CẬP NHẬT: Lưu lại Room ID cục bộ
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

    public async void RequestCreateRoom(string username)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("CreateRoom", username);
        }
    }

    public async void RequestJoinRoom(string roomCode, string username)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("JoinRoom", roomCode, username);
        }
    }

    // Hàm gửi tọa độ di chuyển của người chơi cục bộ lên Server
    public async void SendPlayerPosition(float x, float y)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("SyncPosition", x, y);
        }
    }

    // Hàm gửi yêu cầu bắt đầu game lên Server (Chỉ Host gọi)
    public async void RequestStartGame()
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("StartGame");
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

        // Đồng bộ góc quay súng
        hubConnection.On<string, float, float, float>("OnReceiveShoot", (connId, angle, px, py) =>
        {
            unityContext.Post(_ =>
            {
                OnReceiveWeaponAngle?.Invoke(connId, angle, px, py);
            }, null);
        });
    }

    // --- PHƯƠNG THỨC GỬI LÊN SERVER (API KHÁCH GỌI) ---

    // TỐI ƯU HÓA: Vũ khí chỉ cần truyền tham số vũ khí và vị trí hướng bắn, 
    // hàm này tự lấy CurrentRoomId đã lưu để gửi lên server để giảm thiểu sai sót.
    public async void SendShootEvent(string weaponId, Vector3 position, Vector3 direction)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            // Sử dụng trực tiếp CurrentRoomId nội bộ tự động nhận diện từ phòng chơi
            await hubConnection.InvokeAsync("SendShoot", CurrentRoomId, weaponId, position.x, position.y, direction.x, direction.y);
        }
    }

    public async void SendEnemyHitEvent(string roomId, string enemyId, float damage)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("RegisterEnemyHit", roomId, enemyId, damage);
        }
    }

    public async void SendWeaponAngle(float angle, float px, float py)
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("SyncShoot", angle, px, py);
        }
    }
}