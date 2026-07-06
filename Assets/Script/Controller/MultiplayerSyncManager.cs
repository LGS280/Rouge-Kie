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

    // BỔ SUNG: Cache danh sách các phòng trong Scene để truy xuất nhanh bằng ID
    private Dictionary<string, RoomController> roomCache = new Dictionary<string, RoomController>();

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

        // BỔ SUNG: Gán ID đồng bộ cho Phòng và Quái vật trước khi Cache
        AssignDeterministicRoomAndMobIds();

        // Cache toàn bộ RoomController có trong Scene
        CacheAllRooms();

        // Đăng ký lắng nghe gói tin mạng từ NetworkManager
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnReceivePosition += UpdateRemotePlayerPosition;
            NetworkManager.Instance.OnPlayerDisconnected += RemoveRemotePlayer;
            NetworkManager.Instance.OnRemotePlayerShoot += HandleRemotePlayerShoot;
            NetworkManager.Instance.OnRemoteEnemyDamaged += HandleRemoteEnemyDamaged;
            NetworkManager.Instance.OnReceiveWeaponAngle += UpdateRemoteWeaponAngle;

            // ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG
            NetworkManager.Instance.OnRoomCombatStarted += HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer += HandleRoomClearedFromServer;
            
            // ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ VỊ TRÍ QUÁI
            NetworkManager.Instance.OnReceiveEnemyPosition += HandleRemoteEnemyPosition;
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

            // HỦY ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG
            NetworkManager.Instance.OnRoomCombatStarted -= HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer -= HandleRoomClearedFromServer;
            
            // HỦY SỰ KIỆN ĐỒNG BỘ VỊ TRÍ QUÁI
            NetworkManager.Instance.OnReceiveEnemyPosition -= HandleRemoteEnemyPosition;
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

    // ==========================================
    // NEW: LOGIC ĐỒNG BỘ COMBAT ROOM
    // ==========================================

    private void AssignDeterministicRoomAndMobIds()
    {
        // 1. Đồng bộ Room ID
        RoomController[] allRooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        System.Array.Sort(allRooms, (a, b) => string.CompareOrdinal(GetGameObjectPath(a.gameObject), GetGameObjectPath(b.gameObject)));

        for (int i = 0; i < allRooms.Length; i++)
        {
            allRooms[i].roomUniqueId = $"room_{i}";
        }

        // 2. Đồng bộ Mob ID
        MobNetworkIdentity[] allMobs = Object.FindObjectsByType<MobNetworkIdentity>(FindObjectsSortMode.None);
        System.Array.Sort(allMobs, (a, b) => string.CompareOrdinal(GetGameObjectPath(a.gameObject), GetGameObjectPath(b.gameObject)));

        for (int i = 0; i < allMobs.Length; i++)
        {
            allMobs[i].networkId = $"mob_{i}";
        }
        
        Debug.Log($"Đã gán thành công {allRooms.Length} Room IDs và {allMobs.Length} Mob IDs đồng bộ.");
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name + "_" + obj.transform.GetSiblingIndex();
        Transform curr = obj.transform.parent;
        while (curr != null)
        {
            path = curr.name + "_" + curr.GetSiblingIndex() + "/" + path;
            curr = curr.parent;
        }
        return path;
    }

    private void CacheAllRooms()
    {
        RoomController[] allRooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        foreach (RoomController room in allRooms)
        {
            if (string.IsNullOrEmpty(room.roomUniqueId))
            {
                Debug.LogWarning($"Cảnh báo: Có một RoomController trên object {room.gameObject.name} chưa được gán roomUniqueId!");
                continue;
            }

            if (!roomCache.ContainsKey(room.roomUniqueId))
            {
                roomCache.Add(room.roomUniqueId, room);
            }
        }
    }

    // Khi Server báo một phòng đã bắt đầu đánh nhau
    private void HandleRoomCombatStarted(string targetRoomId, float centerX, float centerY)
    {
        Debug.Log($"NHẬN LỆNH TỪ SERVER: Bắt đầu combat tại phòng {targetRoomId}");

        // 1. Dịch chuyển Local Player vào tâm phòng
        if (localPlayer != null)
        {
            // Tùy biến một chút khoảng cách để 2 người không bị dính chùm vào nhau (offset ngẫu nhiên)
            Vector2 randomOffset = Random.insideUnitCircle * 1.5f;
            localPlayer.position = new Vector3(centerX + randomOffset.x, centerY + randomOffset.y, 0);
        }

        // 2. Dịch chuyển ngay lập tức tất cả Remote Players (Đồng đội)
        foreach (var remotePlayerKV in remotePlayers)
        {
            GameObject remoteObj = remotePlayerKV.Value;
            if (remoteObj != null)
            {
                RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
                Vector3 newPos = new Vector3(centerX, centerY, 0);

                if (rpc != null)
                {
                    rpc.targetPosition = newPos; // Báo RPC di chuyển mượt về vị trí này
                }

                remoteObj.transform.position = newPos; // Teleport lập tức để khỏi kẹt tường
            }
        }

        // 3. Tìm đúng căn phòng đó để ép cửa đóng lại + Kích hoạt quái (Logic nội bộ)
        if (roomCache.TryGetValue(targetRoomId, out RoomController room))
        {
            room.ExecuteStartCombatLocal();
        }
    }

    // Khi Server báo phòng đã dọn dẹp xong
    private void HandleRoomClearedFromServer(string targetRoomId)
    {
        Debug.Log($"NHẬN LỆNH TỪ SERVER: Phòng {targetRoomId} đã clear xong. Mở cửa!");

        if (roomCache.TryGetValue(targetRoomId, out RoomController room))
        {
            room.ExecuteClearRoomLocal();
        }
    }
    // ==========================================

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
        GameObject remotePlayer = GetRemotePlayerById(playerId);
        if (remotePlayer != null)
        {
            WeaponInfo remoteWeapon = remotePlayer.GetComponentInChildren<WeaponInfo>();
            if (remoteWeapon != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                remoteWeapon.transform.rotation = Quaternion.Euler(0, 0, angle);

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

                remoteWeapon.RemoteShoot(position, direction);
            }
        }
    }

    // Xử lý khi quái vật bị dính đòn (áp dụng cho tất cả Client)
    private void HandleRemoteEnemyDamaged(string enemyId, float healthFromServer)
    {
        Debug.Log($"[MultiplayerSyncManager] Nhận OnEnemyDamaged từ Server: enemyId={enemyId}, healthFromServer={healthFromServer}");
        GameObject enemy = FindEnemyByNetworkId(enemyId);
        if (enemy != null)
        {
            MobHealth health = enemy.GetComponent<MobHealth>();
            if (health != null)
            {
                // Truyền cục máu thật vào hàm đồng bộ, dẹp luôn TakeDamage qua mạng!
                health.SyncHealthFromNetwork(Mathf.RoundToInt(healthFromServer));
            }
            else
            {
                Debug.LogWarning($"[MultiplayerSyncManager] Không tìm thấy component MobHealth trên GameObject: {enemy.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[MultiplayerSyncManager] KHÔNG tìm thấy quái nào có networkId là: {enemyId} trong scene!");
        }
    }

    private void HandleRemoteEnemyPosition(string enemyId, float x, float y)
    {
        // Client nhận tọa độ từ Host và vẽ lại quái vật
        GameObject enemy = FindEnemyByNetworkId(enemyId);
        if (enemy != null)
        {
            enemy.transform.position = new Vector3(x, y, enemy.transform.position.z);
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