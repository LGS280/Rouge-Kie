using System.Collections.Generic;
using UnityEngine;

public class MultiplayerSyncManager : MonoBehaviour
{
    public static MultiplayerSyncManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject remotePlayerPrefab; // Kéo RemotePlayer_Prefab vào đây

    [Header("Local Player reference")]
    [SerializeField] private Transform localPlayer;
    [SerializeField] private float syncInterval = 0.05f; // Gửi tọa độ mỗi 50ms (Tần số 20Hz)
    private float lastSyncTime = 0f;

    // Quản lý danh sách các đồng đội đang có trong trận qua ConnectionId
    private Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // Tự động tìm nhân vật Rookie cục bộ của bạn trong Scene
        if (localPlayer == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) localPlayer = playerObj.transform;
        }

        // Đăng ký lắng nghe gói tin tọa độ mạng từ NetworkManager
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnReceivePosition += UpdateRemotePlayerPosition;
            NetworkManager.Instance.OnPlayerDisconnected += RemoveRemotePlayer;
            NetworkManager.Instance.OnRemotePlayerShoot += HandleRemotePlayerShoot;
            NetworkManager.Instance.OnRemoteEnemyDamaged += HandleRemoteEnemyDamaged;
            NetworkManager.Instance.OnReceiveWeaponAngle += UpdateRemoteWeaponAngle;
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký tất cả sự kiện để tránh lỗi rò rỉ bộ nhớ (Memory Leak)
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnReceivePosition -= UpdateRemotePlayerPosition;
            NetworkManager.Instance.OnPlayerDisconnected -= RemoveRemotePlayer;
            NetworkManager.Instance.OnRemotePlayerShoot -= HandleRemotePlayerShoot;
            NetworkManager.Instance.OnRemoteEnemyDamaged -= HandleRemoteEnemyDamaged;
            NetworkManager.Instance.OnReceiveWeaponAngle -= UpdateRemoteWeaponAngle;
        }
    }

    private void Update()
    {
        // Định kỳ gửi tọa độ của chính mình lên Server
        if (localPlayer != null && NetworkManager.Instance != null && Time.time - lastSyncTime >= syncInterval)
        {
            NetworkManager.Instance.SendPlayerPosition(localPlayer.position.x, localPlayer.position.y);
            
            // Gửi góc quay súng liên tục
            WeaponAim weaponAim = localPlayer.GetComponentInChildren<WeaponAim>();
            if (weaponAim != null)
            {
                float angle = weaponAim.transform.rotation.eulerAngles.z;
                NetworkManager.Instance.SendWeaponAngle(angle, localPlayer.position.x, localPlayer.position.y);
            }

            lastSyncTime = Time.time;
        }
    }

    // Xử lý đồng bộ tọa độ đồng đội
    private void UpdateRemotePlayerPosition(string connId, float x, float y)
    {
        // Bỏ qua nếu gói tin tọa độ đó là của chính mình
        if (NetworkManager.Instance != null && connId == NetworkManager.Instance.MyConnectionId) return;

        // Nếu ConnectionId này chưa có trong màn chơi -> Thực hiện sinh đồng đội động (Just-In-Time)
        if (!remotePlayers.ContainsKey(connId))
        {
            Debug.Log($"Sinh nhân vật đồng đội mới trong trận đấu! ID: {connId}");
            GameObject newRemote = Instantiate(remotePlayerPrefab, new Vector3(x, y, 0), Quaternion.identity);
            
            // Xóa các script của local player trên bản sao này để tránh xung đột
            Destroy(newRemote.GetComponent<PlayerController>());
            Destroy(newRemote.GetComponent<PlayerMovement>());
            Destroy(newRemote.GetComponent<UnityEngine.InputSystem.PlayerInput>());
            
            // Thêm script điều khiển từ xa
            newRemote.AddComponent<RemotePlayerController>();
            
            remotePlayers.Add(connId, newRemote);
        }
        else
        {
            // Cập nhật vị trí mục tiêu cho RemotePlayerController
            GameObject remote = remotePlayers[connId];
            if (remote != null)
            {
                RemotePlayerController rpc = remote.GetComponent<RemotePlayerController>();
                if (rpc != null)
                {
                    rpc.targetPosition = new Vector3(x, y, 0);
                }
                else
                {
                    remote.transform.position = new Vector3(x, y, 0);
                }
            }
        }
    }

    // Xóa đồng đội khỏi màn chơi khi họ out phòng chơi
    private void RemoveRemotePlayer(string username, string connId)
    {
        if (remotePlayers.TryGetValue(connId, out GameObject remote))
        {
            Destroy(remote);
            remotePlayers.Remove(connId);
            Debug.Log($"Đồng đội {username} đã ngắt kết nối, tiến hành xóa khỏi màn chơi.");
        }
    }

    // SỬA LỖI 1: Định nghĩa hàm tìm kiếm đồng đội theo ConnectionId
    private GameObject GetRemotePlayerById(string playerId)
    {
        if (remotePlayers.TryGetValue(playerId, out GameObject player))
        {
            return player;
        }
        return null;
    }

    // Cập nhật góc quay súng từ xa liên tục
    private void UpdateRemoteWeaponAngle(string connId, float angle, float px, float py)
    {
        GameObject remote = GetRemotePlayerById(connId);
        if (remote != null)
        {
            WeaponInfo remoteWeapon = remote.GetComponentInChildren<WeaponInfo>();
            if (remoteWeapon != null)
            {
                if (angle > 180f) angle -= 360f;
                if (angle < -180f) angle += 360f;

                remoteWeapon.transform.rotation = Quaternion.Euler(0, 0, angle);

                SpriteRenderer playerRenderer = remote.GetComponent<SpriteRenderer>();
                if (playerRenderer != null)
                {
                    if (angle > 90f || angle < -90f)
                    {
                        playerRenderer.flipX = true;
                        remoteWeapon.transform.localScale = new Vector3(1f, -1f, 1f);
                    }
                    else
                    {
                        playerRenderer.flipX = false;
                        remoteWeapon.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
            }
        }
    }

    // Xử lý vẽ đạn của người chơi khác
    private void HandleRemotePlayerShoot(string playerId, string weaponId, Vector3 position, Vector3 direction)
    {
        // Tìm đối tượng Remote Player
        GameObject remotePlayer = GetRemotePlayerById(playerId);
        if (remotePlayer != null)
        {
            // Lấy thành phần WeaponInfo nguyên bản trên tay của Remote Player
            WeaponInfo remoteWeapon = remotePlayer.GetComponentInChildren<WeaponInfo>();
            if (remoteWeapon != null)
            {
                // 1. Cập nhật góc xoay cho súng của Remote Player
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                remoteWeapon.transform.rotation = Quaternion.Euler(0, 0, angle);

                // 2. Lật hình ảnh nhân vật nếu súng hướng sang trái
                SpriteRenderer playerRenderer = remotePlayer.GetComponent<SpriteRenderer>();
                if (playerRenderer != null)
                {
                    if (angle > 90f || angle < -90f)
                    {
                        playerRenderer.flipX = true;
                        remoteWeapon.transform.localScale = new Vector3(1f, -1f, 1f);
                    }
                    else
                    {
                        playerRenderer.flipX = false;
                        remoteWeapon.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }

                // 3. Bắn đạn
                remoteWeapon.RemoteShoot(position, direction);
            }
        }
    }


    // Xử lý khi quái vật bị dính đòn (áp dụng cho tất cả Client)
    private void HandleRemoteEnemyDamaged(string enemyId, float damage)
    {
        GameObject enemy = FindEnemyByNetworkId(enemyId);
        if (enemy != null)
        {
            MobHealth health = enemy.GetComponent<MobHealth>();
            if (health != null)
            {
                // SỬA LỖI 2: Sử dụng Mathf.RoundToInt để ép kiểu an toàn từ float sang int
                health.TakeDamage(Mathf.RoundToInt(damage));
            }
        }
    }

    private GameObject FindEnemyByNetworkId(string networkId)
    {
        MobNetworkIdentity[] enemies = Object.FindObjectsByType<MobNetworkIdentity>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy.networkId == networkId)
                return enemy.gameObject;
        }
        return null;
    }
}