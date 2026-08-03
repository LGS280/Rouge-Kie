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
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (!isMultiplayer)
        {
            // Tự động tắt component này nếu đang chơi đơn (Solo) để tiết kiệm tài nguyên mạng và tránh gửi SignalR thừa
            enabled = false;
            return;
        }

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

            // ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG & SÚNG
            NetworkManager.Instance.OnRoomCombatStarted += HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer += HandleRoomClearedFromServer;
            NetworkManager.Instance.OnReceiveEnemyPosition += HandleRemoteEnemyPosition;
            NetworkManager.Instance.OnRemoteWeaponChanged += HandleRemoteWeaponChanged;
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

            // HỦY ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG & SÚNG
            NetworkManager.Instance.OnRoomCombatStarted -= HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer -= HandleRoomClearedFromServer;
            NetworkManager.Instance.OnReceiveEnemyPosition -= HandleRemoteEnemyPosition;
            NetworkManager.Instance.OnRemoteWeaponChanged -= HandleRemoteWeaponChanged;
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

            // Nạp cấu hình vị trí tay customHandPosition từ DB cho vũ khí Remote Player
            WeaponInfo remoteWeapon = newRemote.GetComponentInChildren<WeaponInfo>();
            if (remoteWeapon != null)
            {
                remoteWeapon.ApplyConfigFromDb();
            }

            remotePlayers.Add(connId, newRemote);

            // BỔ SUNG: Phát tín hiệu súng của chính mình lên mạng ngay khi xuất hiện đồng đội mới
            if (WeaponManager.Instance != null)
            {
                WeaponManager.Instance.SyncActiveWeaponToNetwork();
            }
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

    // BỔ SUNG: Xử lý đồng bộ loại súng hiển thị mà Remote Player (Player 2) đang cầm
    private void HandleRemoteWeaponChanged(string connId, string weaponName)
    {
        GameObject remoteObj = GetRemotePlayerById(connId);
        if (remoteObj == null || string.IsNullOrEmpty(weaponName)) return;

        Debug.Log($"[MultiplayerSyncManager] Đang đồng bộ súng mới '{weaponName}' cho Remote Player {connId}");

        // Tìm hoặc tự động tạo Hand_Position cho Remote Player
        Transform handPos = remoteObj.transform.Find("Hand_Position");
        if (handPos == null)
        {
            GameObject newHand = new GameObject("Hand_Position");
            newHand.transform.SetParent(remoteObj.transform, false);
            newHand.transform.localPosition = new Vector3(0.15f, -0.1f, 0f);
            handPos = newHand.transform;
        }

        // Xóa tất cả các súng cũ dưới Hand_Position
        foreach (Transform child in handPos)
        {
            Destroy(child.gameObject);
        }

        // Dò tìm Prefab súng dựa vào tên qua WeaponManager hoặc Resources
        GameObject weaponPrefab = null;
        if (WeaponManager.Instance != null)
        {
            weaponPrefab = WeaponManager.Instance.FindWeaponPrefabByName(weaponName);
        }

        if (weaponPrefab == null)
        {
            string cleanName = weaponName.Replace("(Clone)", "").Trim();
            weaponPrefab = Resources.Load<GameObject>($"Prefab/Weapons/{cleanName}");
            if (weaponPrefab == null)
            {
                weaponPrefab = Resources.Load<GameObject>($"Weapons/{cleanName}");
            }
        }

        if (weaponPrefab != null)
        {
            GameObject newWeapon = Instantiate(weaponPrefab, handPos);
            newWeapon.transform.localPosition = Vector3.zero;
            newWeapon.transform.localRotation = Quaternion.identity;
            newWeapon.transform.localScale = Vector3.one;

            // Xóa các script điều khiển local player (nếu có trên Prefab súng)
            MonoBehaviour[] scripts = newWeapon.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
                {
                    script.enabled = false;
                }
            }

            // Nạp thông số vị trí tay từ DB
            WeaponInfo info = newWeapon.GetComponent<WeaponInfo>();
            if (info != null)
            {
                info.ApplyConfigFromDb();
                if (info.customHandPosition != Vector3.zero)
                {
                    newWeapon.transform.localPosition = info.customHandPosition;
                }
            }

            Debug.Log($"[MultiplayerSyncManager] Đã gắn thành công súng '{weaponName}' cho Remote Player {connId}");
        }
        else
        {
            Debug.LogWarning($"[MultiplayerSyncManager] Không tìm thấy Prefab súng '{weaponName}' để gắn cho Remote Player!");
        }
    }

    // Cập nhật góc quay súng từ xa liên tục
    private void UpdateRemoteWeaponAngle(string connId, float angle, float px, float py)
    {
        GameObject remoteObj = GetRemotePlayerById(connId);
        if (remoteObj == null) return;

        // Tìm hoặc tự động tạo Hand_Position độc lập cho Remote Player để xoay súng (Không làm xoay thân nhân vật)
        Transform handPos = remoteObj.transform.Find("Hand_Position");
        if (handPos == null)
        {
            GameObject newHand = new GameObject("Hand_Position");
            newHand.transform.SetParent(remoteObj.transform, false);
            newHand.transform.localPosition = new Vector3(0.15f, -0.1f, 0f);
            handPos = newHand.transform;
        }

        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;

        // Xoay duy nhất Hand_Position quanh tâm tay nhân vật (TUYỆT ĐỐI KHÔNG xoay remoteObj.transform)
        handPos.rotation = Quaternion.Euler(0, 0, angle);

        // Lật Sprite thân nhân vật và lật trục Y của súng khi ngắm sang bên trái
        SpriteRenderer playerRenderer = remoteObj.GetComponent<SpriteRenderer>();
        bool isFacingLeft = (angle > 90f || angle < -90f);
        if (playerRenderer != null)
        {
            playerRenderer.flipX = isFacingLeft;
        }

        // Lật trục Y của Hand_Position để súng không bị ngửa bụng khi quay trái
        handPos.localScale = new Vector3(1f, isFacingLeft ? -1f : 1f, 1f);
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

                Transform pivotTransform = (remoteWeapon.transform.parent != null && remoteWeapon.transform.parent != remotePlayer.transform)
                    ? remoteWeapon.transform.parent
                    : remoteWeapon.transform;

                pivotTransform.rotation = Quaternion.Euler(0, 0, angle);

                SpriteRenderer playerRenderer = remotePlayer.GetComponent<SpriteRenderer>();
                if (playerRenderer != null)
                {
                    if (angle > 90f || angle < -90f)
                    {
                        playerRenderer.flipX = true;
                        pivotTransform.localScale = new Vector3(1f, -1f, 1f);
                    }
                    else
                    {
                        playerRenderer.flipX = false;
                        pivotTransform.localScale = new Vector3(1f, 1f, 1f);
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
            MobAI mobAI = enemy.GetComponent<MobAI>();
            if (mobAI != null)
            {
                mobAI.UpdateNetworkPosition(x, y);
            }
            else
            {
                enemy.transform.position = new Vector3(x, y, enemy.transform.position.z);
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