using System;
using System.Collections.Generic;
using System.Threading; // Yêu cầu thư viện này để quản lý luồng
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    public event Action OnGameStarted; // Sự kiện báo cho UI biết trận đấu đã bắt đầu

    [Header("Server Connection Settings")]
    [SerializeField] private string serverUrl = "http://localhost:5000/gamehub";

    private HubConnection hubConnection;
    private SynchronizationContext unityContext; // Lưu luồng chính của Unity

    // Các sự kiện C# để lớp UI lắng nghe
    public event Action<string> OnRoomCreated;
    public event Action<string, List<string>> OnJoinRoomSuccess;
    public event Action<string> OnJoinRoomFailed;
    public event Action<string, string> OnPlayerJoined;
    public event Action<string, string> OnPlayerDisconnected;

    [Header("Player Session Status (Debug)")]
    public bool IsLoggedIn = false;       // Mặc định ban đầu là chưa đăng nhập
    public string LoggedInUsername = "Guest"; // Tên hiển thị mặc định
    public string UserRole = "Guest"; // Vai trò mặc định là Guest khi chưa đăng nhập
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            unityContext = SynchronizationContext.Current; // Lấy luồng chính hiện tại
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

        // ĐĂNG KÝ LẮNG NGHE SỰ KIỆN TỪ SERVER (Sử dụng unityContext.Post để chạy trên Main Thread)

        hubConnection.On<string>("OnRoomCreated", (roomCode) =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnRoomCreated?.Invoke(roomCode), null);
        });

        hubConnection.On<string, List<string>>("OnJoinRoomSuccess", (roomCode, players) =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnJoinRoomSuccess?.Invoke(roomCode, players), null);
        });

        hubConnection.On<string>("OnJoinRoomFailed", (errorMessage) =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnJoinRoomFailed?.Invoke(errorMessage), null);
        });

        hubConnection.On<string, string>("OnPlayerJoined", (username, connId) =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnPlayerJoined?.Invoke(username, connId), null);
        });

        hubConnection.On<string, string>("OnPlayerDisconnected", (username, connId) =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnPlayerDisconnected?.Invoke(username, connId), null);
        });

        hubConnection.On("OnGameStarted", () =>
        {
            // Chuyển tiếp sự kiện về chạy an toàn trên luồng chính (Main Thread) của Unity
            unityContext.Post(_ => OnGameStarted?.Invoke(), null);
        });


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

    public async void RequestStartGame()
    {
        if (hubConnection != null && hubConnection.State == HubConnectionState.Connected)
        {
            await hubConnection.InvokeAsync("StartGame");
        }
    }
}