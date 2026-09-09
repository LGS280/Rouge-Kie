using System.Collections.Generic;
using UnityEngine;

public class MultiplayerSyncManager : MonoBehaviour
{
    public static MultiplayerSyncManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject remotePlayerPrefab; // Kéo RemotePlayer_Prefab vào đây

    [Header("Local Player reference")]
    [SerializeField] private Transform localPlayer;
    [SerializeField] private float syncInterval = 0.033f; // Gửi tọa độ mỗi 33ms (Tần số 30Hz mượt mà)
    private float lastSyncTime = 0f;

    // Quản lý danh sách các đồng đội đang có trong trận qua ConnectionId
    public Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();

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

        // Tự động gắn TeammateReviveArea để quản lý giữ phím [E] 2.5s hồi sinh đồng đội
        if (GetComponent<Assets.Script.Characters.Rookie.TeammateReviveArea>() == null)
        {
            gameObject.AddComponent<Assets.Script.Characters.Rookie.TeammateReviveArea>();
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

            // ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG & SÚNG & SÁT THƯƠNG
            NetworkManager.Instance.OnRoomCombatStarted += HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer += HandleRoomClearedFromServer;
            NetworkManager.Instance.OnReceiveEnemyPosition += HandleRemoteEnemyPosition;
            NetworkManager.Instance.OnRemoteWeaponChanged += HandleRemoteWeaponChanged;
            NetworkManager.Instance.OnPlayerDamaged += HandlePlayerDamaged;
            NetworkManager.Instance.OnRemotePlayerDied += HandleRemotePlayerDied;
            NetworkManager.Instance.OnTeamDefeat += HandleTeamDefeat;
            NetworkManager.Instance.OnPlayerRevived += HandlePlayerRevived;
            NetworkManager.Instance.OnHostDisconnectedEndGame += HandleHostDisconnectedEndGame;

            // BỔ SUNG: Đăng ký sự kiện mở rương, nhặt súng dùng chung và vứt súng
            NetworkManager.Instance.OnChestOpened += HandleRemoteChestOpened;
            NetworkManager.Instance.OnGroundWeaponPickedUp += HandleRemoteGroundWeaponPickedUp;
            NetworkManager.Instance.OnWeaponDropped += HandleRemoteWeaponDropped;

            // BỔ SUNG: Đăng ký sự kiện Boss tấn công từ máy Host
            NetworkManager.Instance.OnBossAttack += HandleRemoteBossAttack;
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

            // HỦY ĐĂNG KÝ SỰ KIỆN ĐỒNG BỘ PHÒNG & SÚNG & SÁT THƯƠNG
            NetworkManager.Instance.OnRoomCombatStarted -= HandleRoomCombatStarted;
            NetworkManager.Instance.OnRoomClearedFromServer -= HandleRoomClearedFromServer;
            NetworkManager.Instance.OnReceiveEnemyPosition -= HandleRemoteEnemyPosition;
            NetworkManager.Instance.OnRemoteWeaponChanged -= HandleRemoteWeaponChanged;
            NetworkManager.Instance.OnPlayerDamaged -= HandlePlayerDamaged;
            NetworkManager.Instance.OnRemotePlayerDied -= HandleRemotePlayerDied;
            NetworkManager.Instance.OnTeamDefeat -= HandleTeamDefeat;
            NetworkManager.Instance.OnPlayerRevived -= HandlePlayerRevived;
            NetworkManager.Instance.OnHostDisconnectedEndGame -= HandleHostDisconnectedEndGame;

            // BỔ SUNG: Hủy đăng ký sự kiện mở rương, nhặt súng dùng chung và vứt súng
            NetworkManager.Instance.OnChestOpened -= HandleRemoteChestOpened;
            NetworkManager.Instance.OnGroundWeaponPickedUp -= HandleRemoteGroundWeaponPickedUp;
            NetworkManager.Instance.OnWeaponDropped -= HandleRemoteWeaponDropped;

            // BỔ SUNG: Hủy đăng ký sự kiện Boss tấn công từ máy Host
            NetworkManager.Instance.OnBossAttack -= HandleRemoteBossAttack;
        }
    }

    private void Update()
    {
        // Nếu bản thân đang bị ngã/chết, ngưng gửi tọa độ di chuyển hay góc quay súng lên mạng
        if (localPlayer != null)
        {
            RookieHealth health = localPlayer.GetComponent<RookieHealth>();
            if (health != null && health.isDead) return;
        }

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

    /// <summary>
    /// BỔ SUNG: Làm mới Cache phòng và quái vật khi chuyển tầng hầm ngục mới
    /// </summary>
    public void RefreshRoomAndMobNetworkCache()
    {
        roomCache.Clear();
        AssignDeterministicRoomAndMobIds();
        CacheAllRooms();
        Debug.Log("[MultiplayerSyncManager] Đã làm mới cache phòng và quái vật cho tầng mới!");
    }

    // Khi Server báo một phòng đã bắt đầu đánh nhau
    private RoomController FindRoomById(string targetRoomId)
    {
        if (string.IsNullOrEmpty(targetRoomId)) return null;

        if (roomCache.TryGetValue(targetRoomId, out RoomController room) && room != null)
        {
            return room;
        }

        // Fallback 1: Quét lại tất cả RoomController trong Scene xem có roomUniqueId trùng khớp không
        RoomController[] allRooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        foreach (var r in allRooms)
        {
            if (r != null && r.roomUniqueId == targetRoomId)
            {
                if (!roomCache.ContainsKey(targetRoomId)) roomCache[targetRoomId] = r;
                return r;
            }
        }

        // Fallback 2: Lấy phòng duy nhất đang bị khóa cửa đánh quái (roomStarted == true)
        foreach (var r in allRooms)
        {
            if (r != null && r.roomStarted && !r.roomCleared)
            {
                return r;
            }
        }

        return null;
    }

    // Khi Server báo một phòng đã bắt đầu đánh nhau
    private void HandleRoomCombatStarted(string targetRoomId, float safeX, float safeY)
    {
        Debug.Log($"NHẬN LỆNH TỪ SERVER: Bắt đầu combat tại phòng {targetRoomId}");

        Vector3 targetPos = new Vector3(safeX, safeY, 0);
        RoomController room = FindRoomById(targetRoomId);

        if (room != null)
        {
            // 1. Chỉ dịch chuyển localPlayer nếu còn sống và đang ở NGOÀI phòng
            if (localPlayer != null)
            {
                RookieHealth localHealth = localPlayer.GetComponent<RookieHealth>();
                bool isLocalDead = localHealth != null && localHealth.isDead;
                if (!isLocalDead && room.RoomCollider != null && !room.RoomCollider.bounds.Contains(localPlayer.position))
                {
                    Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                    localPlayer.position = targetPos + new Vector3(randomOffset.x, randomOffset.y, 0);
                }
            }

            // 2. Chỉ dịch chuyển remotePlayers nếu còn sống và đang ở NGOÀI phòng
            foreach (var remotePlayerKV in remotePlayers)
            {
                GameObject remoteObj = remotePlayerKV.Value;
                if (remoteObj != null)
                {
                    RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
                    bool isRemoteDead = rpc != null && rpc.isDead;
                    if (!isRemoteDead && room.RoomCollider != null && !room.RoomCollider.bounds.Contains(remoteObj.transform.position))
                    {
                        if (rpc != null) rpc.targetPosition = targetPos;
                        remoteObj.transform.position = targetPos;
                    }
                }
            }

            // Ép cửa đóng lại + Kích hoạt quái (Logic nội bộ)
            room.ExecuteStartCombatLocal();
        }
    }

    // Khi Server báo phòng đã dọn dẹp xong
    private void HandleRoomClearedFromServer(string targetRoomId)
    {
        Debug.Log($"NHẬN LỆNH TỪ SERVER: Phòng {targetRoomId} đã clear xong. Mở cửa!");

        RoomController room = FindRoomById(targetRoomId);
        if (room != null)
        {
            room.ExecuteClearRoomLocal();
        }
    }

    private void HandleRemotePlayerDied(string connId)
    {
        Debug.Log($"[MultiplayerSyncManager] Đồng đội {connId} đã hy sinh trong Co-op!");
        if (remotePlayers.TryGetValue(connId, out GameObject remoteObj) && remoteObj != null)
        {
            RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
            if (rpc != null) rpc.DieRemotePlayer();
            Animator anim = remoteObj.GetComponent<Animator>();
            if (anim != null) anim.SetTrigger("die");
            SpriteRenderer sr = remoteObj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.35f, 0.35f, 0.35f, 1f);

            // Ẩn bóng Shadow và vòng xanh Player_Ring của đồng đội khi hy sinh
            Transform shadowPos = remoteObj.transform.Find("Shadow");
            if (shadowPos != null) shadowPos.gameObject.SetActive(false);

            Transform ringPos = remoteObj.transform.Find("Player_Ring");
            if (ringPos == null) ringPos = remoteObj.transform.Find("Ring");
            if (ringPos == null) ringPos = remoteObj.transform.Find("PlayerRing");
            if (ringPos != null) ringPos.gameObject.SetActive(false);
        }
    }

    private void HandlePlayerRevived(string connId, int reviveHp)
    {
        Debug.Log($"[MultiplayerSyncManager] Người chơi {connId} đã được HỒI SINH trong Co-op!");
        if (NetworkManager.Instance != null && connId == NetworkManager.Instance.MyConnectionId)
        {
            if (localPlayer != null)
            {
                RookieHealth health = localPlayer.GetComponent<RookieHealth>();
                if (health != null) health.Revive(reviveHp);
            }
        }
        else if (remotePlayers.TryGetValue(connId, out GameObject remoteObj) && remoteObj != null)
        {
            RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
            if (rpc != null) rpc.ReviveRemotePlayer();

            // Bật lại bóng Shadow và vòng xanh Player_Ring của đồng đội khi được hồi sinh
            Transform shadowPos = remoteObj.transform.Find("Shadow");
            if (shadowPos != null) shadowPos.gameObject.SetActive(true);

            Transform ringPos = remoteObj.transform.Find("Player_Ring");
            if (ringPos == null) ringPos = remoteObj.transform.Find("Ring");
            if (ringPos == null) ringPos = remoteObj.transform.Find("PlayerRing");
            if (ringPos != null)
            {
                ringPos.gameObject.SetActive(true);
                SpriteRenderer ringSr = ringPos.GetComponent<SpriteRenderer>();
                if (ringSr != null) ringSr.color = (rpc != null) ? rpc.assignedRingColor : new Color(0f, 0.75f, 1f, 1f);
            }
        }
    }

    private void HandleHostDisconnectedEndGame(string hostName)
    {
        Debug.LogWarning($"[MultiplayerSyncManager] Chủ phòng {hostName} đã thoát game! Trận đấu kết thúc.");
        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.ShowLoading("TRẬN ĐẤU KẾT THÚC", $"Chủ phòng {hostName} đã rời trận đấu.");
        }
        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.EndRun(false);
        }
    }

    private void HandleTeamDefeat()
    {
        Debug.Log("[MultiplayerSyncManager] Tất cả thành viên trong phòng Co-op đã hy sinh! Mở Bảng Defeat...");
        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.EndRun(false);
        }
    }
    // ==========================================

    // Xử lý đồng bộ tọa độ đồng đội
    private void UpdateRemotePlayerPosition(string connId, float x, float y)
    {
        // Bỏ qua nếu gói tin tọa độ đó là của chính mình
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.MyConnectionId))
        {
            if (string.Equals(connId, NetworkManager.Instance.MyConnectionId, System.StringComparison.OrdinalIgnoreCase)) return;
        }

        // Nếu ConnectionId này chưa có trong màn chơi -> Thực hiện sinh đồng đội động (Just-In-Time)
        if (!remotePlayers.ContainsKey(connId))
        {
            Debug.Log($"Sinh nhân vật đồng đội mới trong trận đấu! ID: {connId}");
            GameObject newRemote = Instantiate(remotePlayerPrefab, new Vector3(x, y, 0), Quaternion.identity);

            // Xóa các script của local player trên bản sao này để tránh xung đột
            Destroy(newRemote.GetComponent<PlayerController>());
            Destroy(newRemote.GetComponent<PlayerMovement>());
            Destroy(newRemote.GetComponent<UnityEngine.InputSystem.PlayerInput>());

            // Đảm bảo Collider của Remote Player không làm kẹt/chắn đường di chuyển của Local Player
            Collider2D col = newRemote.GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // BỔ SUNG: Xóa WeaponManager trên bản sao đồng đội để tránh chạy logic quản lý súng nội bộ
            WeaponManager remoteWm = newRemote.GetComponent<WeaponManager>();
            if (remoteWm != null)
            {
                Destroy(remoteWm);
            }

            // BỔ SUNG: Dọn dẹp sạch súng cũ nằm trên lưng Back_Position để không bị hiện súng nằm ngang thừa
            Transform backPos = newRemote.transform.Find("Back_Position");
            if (backPos != null)
            {
                foreach (Transform child in backPos)
                {
                    Destroy(child.gameObject);
                }
            }

            // Thêm script điều khiển từ xa
            RemotePlayerController rpc = newRemote.AddComponent<RemotePlayerController>();
            rpc.connectionId = connId;

            // Nạp cấu hình vị trí tay customHandPosition từ DB cho vũ khí Remote Player
            WeaponInfo remoteWeapon = newRemote.GetComponentInChildren<WeaponInfo>();
            if (remoteWeapon != null)
            {
                remoteWeapon.ApplyConfigFromDb();
            }

            remotePlayers.Add(connId, newRemote);

            // BỔ SUNG: Gán màu vòng chân phân biệt 4 người chơi (Player 2: Xanh Dương, Player 3: Vàng, Player 4: Tím)
            int playerIndex = remotePlayers.Count; // 1, 2, 3
            Color ringColor = new Color(0f, 0.75f, 1f, 1f); // Mặc định Xanh Dương (P2)
            if (playerIndex == 2) ringColor = new Color(1f, 0.85f, 0f, 1f); // Vàng (P3)
            else if (playerIndex >= 3) ringColor = new Color(0.8f, 0.2f, 1f, 1f); // Tím (P4)

            rpc.assignedRingColor = ringColor;

            Transform ringPos = newRemote.transform.Find("Player_Ring");
            if (ringPos == null) ringPos = newRemote.transform.Find("Ring");
            if (ringPos == null) ringPos = newRemote.transform.Find("PlayerRing");
            if (ringPos != null)
            {
                SpriteRenderer ringSr = ringPos.GetComponent<SpriteRenderer>();
                if (ringSr != null) ringSr.color = ringColor;
            }

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
                    rpc.SetNewTargetPosition(new Vector3(x, y, 0));
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

    // BỔ SUNG: Xử lý đồng bộ loại súng chính và súng phụ hiển thị mà Remote Player (Player 2) đang cầm
    private void HandleRemoteWeaponChanged(string connId, string activeWeaponName, string secondaryWeaponName)
    {
        GameObject remoteObj = GetRemotePlayerById(connId);
        if (remoteObj == null) return;

        Debug.Log($"[MultiplayerSyncManager] Đang đồng bộ súng chính '{activeWeaponName}' & phụ '{secondaryWeaponName}' cho Remote Player {connId}");

        // 1. XỬ LÝ SÚNG CHÍNH TRÊN TAY (Hand_Position)
        if (!string.IsNullOrEmpty(activeWeaponName))
        {
            Transform handPos = remoteObj.transform.Find("Hand_Position");
            if (handPos == null)
            {
                GameObject newHand = new GameObject("Hand_Position");
                newHand.transform.SetParent(remoteObj.transform, false);
                handPos = newHand.transform;
            }
            handPos.localPosition = new Vector3(0f, -0.29f, 0f); // Chuẩn Y = -0.29 khớp 100% với Rookie.prefab

            foreach (Transform child in handPos)
            {
                Destroy(child.gameObject);
            }

            GameObject activePrefab = FindWeaponPrefabByName(activeWeaponName);
            if (activePrefab != null)
            {
                GameObject newActiveWeapon = Instantiate(activePrefab, handPos);
                newActiveWeapon.transform.localPosition = Vector3.zero;
                newActiveWeapon.transform.localRotation = Quaternion.identity;
                newActiveWeapon.transform.localScale = Vector3.one;

                MonoBehaviour[] scripts = newActiveWeapon.GetComponents<MonoBehaviour>();
                foreach (var script in scripts)
                {
                    if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
                    {
                        script.enabled = false;
                    }
                }

                WeaponInfo info = newActiveWeapon.GetComponent<WeaponInfo>();
                if (info != null)
                {
                    info.ApplyConfigFromDb();
                    if (info.customHandPosition != Vector3.zero)
                    {
                        newActiveWeapon.transform.localPosition = info.customHandPosition;
                    }
                }
            }
        }

        // 2. XỬ LÝ SÚNG PHỤ ĐEO NGHIÊNG SAU LƯNG (Back_Position)
        Transform backPos = remoteObj.transform.Find("Back_Position");
        if (backPos == null)
        {
            GameObject newBack = new GameObject("Back_Position");
            newBack.transform.SetParent(remoteObj.transform, false);
            backPos = newBack.transform;
        }

        // Ép vị trí (-0.2, 0, 0) và góc xoay (-45 độ) của Remote Player khớp 100% với Rookie.prefab
        backPos.localPosition = new Vector3(-0.2f, 0f, 0f);
        backPos.localRotation = Quaternion.Euler(0, 0, -45f);

        foreach (Transform child in backPos)
        {
            Destroy(child.gameObject);
        }

        if (!string.IsNullOrEmpty(secondaryWeaponName))
        {
            GameObject secondaryPrefab = FindWeaponPrefabByName(secondaryWeaponName);
            if (secondaryPrefab != null)
            {
                GameObject newSecondaryWeapon = Instantiate(secondaryPrefab, backPos);
                newSecondaryWeapon.transform.localPosition = Vector3.zero;
                newSecondaryWeapon.transform.localRotation = Quaternion.identity; // Đồng bộ 100% góc xoay chuẩn của Back_Position trên nhân vật
                newSecondaryWeapon.transform.localScale = Vector3.one;

                MonoBehaviour[] scripts = newSecondaryWeapon.GetComponents<MonoBehaviour>();
                foreach (var script in scripts)
                {
                    if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
                    {
                        script.enabled = false;
                    }
                }

                SpriteRenderer secRenderer = newSecondaryWeapon.GetComponent<SpriteRenderer>();
                SpriteRenderer playerRenderer = remoteObj.GetComponent<SpriteRenderer>();
                if (secRenderer != null && playerRenderer != null)
                {
                    secRenderer.sortingOrder = playerRenderer.sortingOrder - 1; // Nằm sau thân nhân vật
                }
            }
        }
    }

    private GameObject FindWeaponPrefabByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;
        string cleanName = weaponName.Replace("(Clone)", "").Trim();

        GameObject weaponPrefab = null;
        if (WeaponManager.Instance != null)
        {
            weaponPrefab = WeaponManager.Instance.FindWeaponPrefabByName(cleanName);
        }

        if (weaponPrefab == null)
        {
#if UNITY_EDITOR
            string editorPath = $"Assets/Prefab/Weapons/{cleanName}.prefab";
            weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
            if (weaponPrefab != null) return weaponPrefab;
#endif
            weaponPrefab = Resources.Load<GameObject>($"Prefab/Weapons/{cleanName}");
            if (weaponPrefab == null)
            {
                weaponPrefab = Resources.Load<GameObject>($"Weapons/{cleanName}");
            }
        }
        return weaponPrefab;
    }

    // BỔ SUNG: Nhận đồng bộ sát thương quái đánh trúng người chơi qua mạng
    private void HandlePlayerDamaged(string targetConnId, float damage)
    {
        if (NetworkManager.Instance == null) return;

        // 🎯 CHỈ XỬ LÝ HIỂN THỊ CHO ĐỒNG ĐỘI (REMOTE PLAYER)
        // Bản thân (Local Player cả Host và Guest) đã tự trừ máu & hiển thị -9 cục bộ khi bị dính đạn rồi!
        if (targetConnId != NetworkManager.Instance.MyConnectionId)
        {
            Debug.Log($"[MultiplayerSyncManager] Đồng đội {targetConnId} nhận sát thương: {damage} HP");
            GameObject remoteObj = GetRemotePlayerById(targetConnId);
            if (remoteObj != null)
            {
                int dmgInt = Mathf.RoundToInt(damage);
                if (DamageNumberSpawner.Instance != null && dmgInt > 0)
                {
                    DamageNumber dn = DamageNumberSpawner.Instance.Spawn(remoteObj.transform.position, dmgInt, false);
                    if (dn != null)
                    {
                        dn.SetColor(new Color(1f, 0.4f, 0f));
                        dn.SetText("-" + dmgInt);
                    }
                }
                StartCoroutine(RemoteHurtFlashRoutine(remoteObj));
            }
        }
    }

    private System.Collections.IEnumerator RemoteHurtFlashRoutine(GameObject remoteObj)
    {
        if (remoteObj == null) yield break;
        SpriteRenderer sr = remoteObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(1f, 0.35f, 0.35f, 1f);
            yield return new WaitForSeconds(0.15f);
            if (sr != null)
            {
                // 🎯 RESET VỀ MÀU TRẮNG NGUYÊN BẢN (Color.white): Đảm bảo 100% nhân vật trả về màu gốc, không kẹt màu đỏ!
                sr.color = Color.white;
            }
        }
    }

    // Cập nhật góc quay súng từ xa liên tục
    private void UpdateRemoteWeaponAngle(string connId, float angle, float px, float py)
    {
        GameObject remoteObj = GetRemotePlayerById(connId);
        if (remoteObj == null) return;

        RemotePlayerController rpc = remoteObj.GetComponent<RemotePlayerController>();
        if (rpc != null)
        {
            rpc.targetWeaponAngle = angle;

            // Bọc lót cập nhật vị trí nếu nhận được cùng gói tin
            if (Vector3.Distance(remoteObj.transform.position, new Vector3(px, py, 0)) > 2.5f)
            {
                rpc.SetNewTargetPosition(new Vector3(px, py, 0));
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
                MelogBossAI melogBossAI = enemy.GetComponent<MelogBossAI>();
                if (melogBossAI != null)
                {
                    melogBossAI.UpdateNetworkPosition(new Vector2(x, y));
                }
                else
                {
                    enemy.transform.position = new Vector3(x, y, enemy.transform.position.z);
                }
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

    // BỔ SUNG: Xử lý khi đồng đội trong phòng mở rương vũ khí dùng chung
    private void HandleRemoteChestOpened(string chestId, string weaponName, float spawnX, float spawnY)
    {
        Debug.Log($"[MultiplayerSyncManager] Đồng đội mở rương '{chestId}' rớt súng '{weaponName}' tại ({spawnX}, {spawnY})");

        Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

        WeaponChest[] allChests = Object.FindObjectsByType<WeaponChest>(FindObjectsSortMode.None);
        WeaponChest targetChest = null;
        float minDistance = float.MaxValue;

        foreach (var chest in allChests)
        {
            if (chest != null)
            {
                if (chest.chestId == chestId)
                {
                    targetChest = chest;
                    break;
                }

                float dist = Vector3.Distance(chest.transform.position, spawnPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    targetChest = chest;
                }
            }
        }

        if (targetChest != null)
        {
            targetChest.OpenChestFromNetwork(weaponName, spawnPos);
        }
    }

    // BỔ SUNG: Xử lý khi có bất kỳ đồng đội nào nhặt súng rơi dưới sàn -> Xóa súng ngay lập tức
    private void HandleRemoteGroundWeaponPickedUp(string groundWeaponId)
    {
        Debug.Log($"[MultiplayerSyncManager] Đồng đội đã nhặt súng mạng '{groundWeaponId}', tiến hành xóa khỏi sàn.");

        GroundWeapon[] allGroundWeapons = Object.FindObjectsByType<GroundWeapon>(FindObjectsSortMode.None);
        foreach (var gw in allGroundWeapons)
        {
            if (gw != null && gw.networkId == groundWeaponId)
            {
                Destroy(gw.gameObject);
                break;
            }
        }
    }

    // BỔ SUNG: Xử lý khi đồng đội vứt vũ khí cũ ra sàn -> Hiển thị súng trên sàn với đúng networkId
    private void HandleRemoteWeaponDropped(string weaponName, float posX, float posY, string groundWeaponId)
    {
        Debug.Log($"[MultiplayerSyncManager] Đồng đội vứt súng '{weaponName}' (ID: {groundWeaponId}) tại ({posX}, {posY})");

        // Kiểm tra xem vũ khí này đã tồn tại trên sàn chưa để tránh trùng lặp
        GroundWeapon[] allGroundWeapons = Object.FindObjectsByType<GroundWeapon>(FindObjectsSortMode.None);
        foreach (var gw in allGroundWeapons)
        {
            if (gw != null && gw.networkId == groundWeaponId)
            {
                return;
            }
        }

        GameObject weaponPrefab = FindWeaponPrefabByName(weaponName);
        if (weaponPrefab != null)
        {
            Vector3 spawnPos = new Vector3(posX, posY, 0);
            GroundWeapon.Create(weaponPrefab, spawnPos, groundWeaponId);
        }
        else
        {
            Debug.LogWarning($"[MultiplayerSyncManager] Không tìm thấy prefab vũ khí cho '{weaponName}' khi đồng đội vứt súng!");
        }
    }

    // BỔ SUNG: Xử lý khi nhận sự kiện Boss tấn công từ máy Host
    private void HandleRemoteBossAttack(string bossId, float targetX, float targetY)
    {
        GameObject bossObj = FindEnemyByNetworkId(bossId);
        if (bossObj != null)
        {
            MelogBossAI melogAI = bossObj.GetComponent<MelogBossAI>();
            if (melogAI != null)
            {
                melogAI.ExecuteNetworkAttack(new Vector2(targetX, targetY));
            }
        }
    }
}