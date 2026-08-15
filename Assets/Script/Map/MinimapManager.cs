using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance { get; private set; }

    [Header("UI Containers")]
    public RectTransform container;        // Panel chứa các ô phòng Minimap (sẽ tự động căn giữa theo phòng hiện tại)
    public GameObject roomUiPrefab;        // Prefab ô phòng (Nếu để null, hệ thống sẽ tự động tạo dynamic bằng code)

    [Header("Minimap Assets")]
    public Sprite spriteRoom;              // Sprite nền phòng (Room.png)
    public Sprite spriteHome;              // Sprite nhà xuất phát (Home.png)
    public Sprite spriteBoss;              // Sprite phòng Boss (Boss.png)
    public Sprite spriteChest;             // Sprite phòng rương báu (Chest.png)
    public Sprite spritePortal;            // Sprite cổng dịch chuyển (portal.png)
    public Sprite spritePlayer;            // Sprite chấm tròn/mũi tên người chơi (nếu có)

    [Header("Layout Settings")]
    public float roomSpacing = 40f;        // Khoảng cách pixel giữa các ô phòng trên UI

    private Dictionary<Vector2Int, RoomController> roomControllers = new Dictionary<Vector2Int, RoomController>();
    private Dictionary<Vector2Int, MinimapRoomUI> roomUiDict = new Dictionary<Vector2Int, MinimapRoomUI>();
    private RoomController currentRoom;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Luôn nạp dữ liệu phòng thực tế từ DungeonGenerator (bỏ qua các room cũ còn lưu trong Scene Editor)
        var generator = Object.FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            var dict = generator.GetRoomControllers();
            if (dict != null && dict.Count > 0)
            {
                InitializeWithRooms(dict);
            }
        }

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRemoteRoomVisited += HandleRemoteRoomVisited;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnRemoteRoomVisited -= HandleRemoteRoomVisited;
        }
    }

    /// <summary>
    /// Tìm và trích xuất tọa độ gridPos của toàn bộ RoomController trong Scene dựa trên tên GameObject "Room_(X, Y)"
    /// </summary>
    private Dictionary<Vector2Int, RoomController> FindRoomsInScene()
    {
        var dict = new Dictionary<Vector2Int, RoomController>();
        var allControllers = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);

        foreach (var rc in allControllers)
        {
            if (rc == null) continue;
            string name = rc.gameObject.name; // Định dạng mặc định của DungeonGenerator tạo ra là "Room_(X, Y)"
            if (name.StartsWith("Room_("))
            {
                // Trích xuất phần chuỗi chứa tọa độ: "Room_(0, -1)" -> "0, -1"
                string coords = name.Substring(6, name.Length - 7);
                string[] split = coords.Split(',');
                if (split.Length == 2)
                {
                    if (int.TryParse(split[0].Trim(), out int x) && int.TryParse(split[1].Trim(), out int y))
                    {
                        var key = new Vector2Int(x, y);
                        if (!dict.ContainsKey(key))
                        {
                            dict.Add(key, rc);
                        }
                    }
                }
            }
        }
        return dict;
    }

    /// <summary>
    /// Hàm khởi tạo Minimap gọi trực tiếp từ DungeonGenerator sau khi tạo map xong
    /// </summary>
    public void InitializeMinimap()
    {
        var generator = Object.FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            InitializeWithRooms(generator.GetRoomControllers());
        }
        else
        {
            Debug.LogError("Không tìm thấy DungeonGenerator trong Scene để khởi tạo Minimap.");
        }
    }

    private HashSet<KeyValuePair<Vector2Int, Vector2Int>> validConnections = new HashSet<KeyValuePair<Vector2Int, Vector2Int>>();
    private Dictionary<string, MinimapCorridorUI> corridorUiDict = new Dictionary<string, MinimapCorridorUI>();

    public bool IsConnected(Vector2Int a, Vector2Int b)
    {
        if (validConnections != null && validConnections.Count > 0)
        {
            return validConnections.Contains(new KeyValuePair<Vector2Int, Vector2Int>(a, b)) ||
                   validConnections.Contains(new KeyValuePair<Vector2Int, Vector2Int>(b, a));
        }

        return roomControllers.ContainsKey(a) && roomControllers.ContainsKey(b);
    }

    /// <summary>
    /// Khởi tạo lưới phòng Minimap dựa trên danh sách RoomController
    /// </summary>
    public void InitializeWithRooms(Dictionary<Vector2Int, RoomController> rooms)
    {
        if (container == null)
        {
            Debug.LogError("Chưa gán RectTransform container cho MinimapManager.");
            return;
        }

        // Ép màu nền MinimapWindow về màu xanh mờ trong suốt nhẹ nhàng (Alpha = 0.22f)
        if (container.parent != null)
        {
            Image parentBg = container.parent.GetComponent<Image>();
            if (parentBg != null)
            {
                parentBg.color = new Color(0.05f, 0.08f, 0.15f, 0.22f);
            }
        }
        GameObject winObj = GameObject.Find("MinimapWindow");
        if (winObj != null)
        {
            Mask oldMask = winObj.GetComponent<Mask>();
            if (oldMask != null)
            {
                if (Application.isPlaying) Destroy(oldMask);
                else DestroyImmediate(oldMask);
            }
            if (winObj.GetComponent<RectMask2D>() == null)
            {
                winObj.AddComponent<RectMask2D>();
            }

            Image winBg = winObj.GetComponent<Image>();
            if (winBg != null)
            {
                winBg.color = new Color(0.05f, 0.08f, 0.15f, 0.22f);
            }
        }

        // Xoá sạch các ô Minimap cũ
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        roomControllers = new Dictionary<Vector2Int, RoomController>(rooms);
        roomUiDict.Clear();
        corridorUiDict.Clear();
        currentRoom = null;

        // Tải danh sách kết nối hành lang thực tế từ DungeonGenerator
        var generator = Object.FindAnyObjectByType<DungeonGenerator>();
        if (generator != null)
        {
            validConnections = generator.GetRoomConnections();
        }
        else
        {
            validConnections = BuildFallbackConnections();
        }

        // Chỉ phân loại lại phòng tại runtime nếu chưa được phân loại bởi DungeonGenerator
        bool hasCategorizedRooms = false;
        foreach (var kvp in roomControllers)
        {
            if (kvp.Value != null && kvp.Value.roomType != RoomType.Normal)
            {
                hasCategorizedRooms = true;
                break;
            }
        }
        if (!hasCategorizedRooms)
        {
            RecategorizeRoomsRuntime();
        }

        Debug.Log($"[MinimapManager] InitializeWithRooms count: {rooms.Count}, connections: {validConnections.Count}");

        // Tạo các thanh hành lang UI nối giữa các phòng kết nối với nhau
        HashSet<string> createdCorridorKeys = new HashSet<string>();
        foreach (var conn in validConnections)
        {
            Vector2Int posA = conn.Key;
            Vector2Int posB = conn.Value;

            string key = GetCorridorKey(posA, posB);
            if (createdCorridorKeys.Contains(key)) continue;
            createdCorridorKeys.Add(key);

            if (roomControllers.ContainsKey(posA) && roomControllers.ContainsKey(posB))
            {
                MinimapCorridorUI corridorUI = CreateDynamicCorridorUI(posA, posB);
                if (corridorUI != null)
                {
                    corridorUiDict.Add(key, corridorUI);
                }
            }
        }

        // Tạo giao diện ô phòng cho từng phòng trong map
        foreach (var kvp in roomControllers)
        {
            Vector2Int gridPos = kvp.Key;
            RoomController controller = kvp.Value;

            MinimapRoomUI roomUI = null;

            if (roomUiPrefab != null)
            {
                GameObject obj = Instantiate(roomUiPrefab, container);
                roomUI = obj.GetComponent<MinimapRoomUI>();
            }
            else
            {
                roomUI = CreateDynamicRoomUI(gridPos);
            }

            if (roomUI != null)
            {
                RectTransform rectTrans = roomUI.GetComponent<RectTransform>();
                rectTrans.anchoredPosition = new Vector2(gridPos.x * roomSpacing, gridPos.y * roomSpacing);

                Sprite iconSprite = null;
                switch (controller.roomType)
                {
                    case RoomType.Start:
                        iconSprite = spriteHome;
                        break;
                    case RoomType.Boss:
                        iconSprite = spriteBoss;
                        break;
                    case RoomType.Chest:
                        iconSprite = spriteChest;
                        break;
                    case RoomType.Portal:
                        iconSprite = (spritePortal != null) ? spritePortal : LoadSpriteSafely("Minimap/portal");
                        break;
                }

                roomUI.Setup(spriteRoom, iconSprite);
                roomUiDict.Add(gridPos, roomUI);

                if (controller.roomType == RoomType.Start || gridPos == Vector2Int.zero)
                {
                    currentRoom = controller;
                    controller.isVisited = true;
                }
            }
        }

        if (currentRoom == null && roomControllers.TryGetValue(Vector2Int.zero, out RoomController startRoom))
        {
            currentRoom = startRoom;
            startRoom.isVisited = true;
        }

        CenterMapOnCurrentRoom();
        UpdateMinimap();
    }

    /// <summary>
    /// Cập nhật hiển thị màu sắc, trạng thái các phòng và hành lang nối
    /// </summary>
    public void UpdateMinimap()
    {
        if (roomControllers.Count == 0) return;
        CenterMapOnCurrentRoom();

        HashSet<Vector2Int> visitedCoords = new HashSet<Vector2Int>();
        foreach (var kvp in roomControllers)
        {
            if (kvp.Value != null && kvp.Value.isVisited)
            {
                visitedCoords.Add(kvp.Key);
            }
        }

        // Cập nhật từng ô phòng UI
        foreach (var kvp in roomUiDict)
        {
            Vector2Int gridPos = kvp.Key;
            MinimapRoomUI ui = kvp.Value;

            if (ui == null) continue;

            if (!roomControllers.TryGetValue(gridPos, out RoomController controller) || controller == null)
            {
                continue;
            }

            bool isCurrent = (controller == currentRoom);
            bool isVisited = controller.isVisited;
            bool isCleared = controller.roomCleared;

            // CHỈ hiển thị ô kề cạnh nếu thực sự CÓ HÀNH LANG KẾT NỐI từ 1 phòng đã đi qua!
            bool isAdjacentToVisited = false;
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in directions)
            {
                Vector2Int vPos = gridPos + dir;
                if (visitedCoords.Contains(vPos) && IsConnected(gridPos, vPos))
                {
                    isAdjacentToVisited = true;
                    break;
                }
            }

            ui.SetState(isCurrent, isVisited, isCleared, isAdjacentToVisited);
        }

        // Cập nhật hiển thị từng thanh hành lang nối trên UI
        foreach (var kvp in corridorUiDict)
        {
            string key = kvp.Key;
            MinimapCorridorUI corridorUI = kvp.Value;
            if (corridorUI == null) continue;

            string[] parts = key.Split('_');
            if (parts.Length == 2)
            {
                Vector2Int posA = ParseVector2Int(parts[0]);
                Vector2Int posB = ParseVector2Int(parts[1]);

                bool isAVisited = visitedCoords.Contains(posA);
                bool isBVisited = visitedCoords.Contains(posB);

                bool isVisible = isAVisited || isBVisited;
                bool isBothVisited = isAVisited && isBVisited;

                corridorUI.SetState(isVisible, isBothVisited);
            }
        }
    }

    private string GetCorridorKey(Vector2Int a, Vector2Int b)
    {
        if (a.x < b.x || (a.x == b.x && a.y < b.y))
        {
            return $"{a.x},{a.y}_{b.x},{b.y}";
        }
        return $"{b.x},{b.y}_{a.x},{a.y}";
    }

    private Vector2Int ParseVector2Int(string s)
    {
        string[] split = s.Split(',');
        if (split.Length == 2 && int.TryParse(split[0], out int x) && int.TryParse(split[1], out int y))
        {
            return new Vector2Int(x, y);
        }
        return Vector2Int.zero;
    }

    private MinimapCorridorUI CreateDynamicCorridorUI(Vector2Int posA, Vector2Int posB)
    {
        string name = $"Corridor_{posA}_{posB}";
        GameObject corridorObj = new GameObject(name, typeof(RectTransform));
        corridorObj.transform.SetParent(container, false);
        corridorObj.transform.SetAsFirstSibling(); // Đưa hành lang xuống lớp nền phía dưới (Render phía sau các ô phòng)

        RectTransform rectTrans = corridorObj.GetComponent<RectTransform>();

        Vector2 centerPos = new Vector2(
            (posA.x + posB.x) * 0.5f * roomSpacing,
            (posA.y + posB.y) * 0.5f * roomSpacing
        );
        rectTrans.anchoredPosition = centerPos;

        bool isHorizontal = posA.y == posB.y;
        float corridorLengthUI = roomSpacing * 0.40f; // Chiều dài vừa vặn ẩn bên dưới viền phòng
        float corridorThicknessUI = 12f;              // Độ dày vừa vặn đẹp mắt (12px)

        if (isHorizontal)
        {
            rectTrans.sizeDelta = new Vector2(corridorLengthUI, corridorThicknessUI);
        }
        else
        {
            rectTrans.sizeDelta = new Vector2(corridorThicknessUI, corridorLengthUI);
        }

        Image img = corridorObj.AddComponent<Image>();
        img.type = Image.Type.Simple;
        img.color = new Color(0.35f, 0.4f, 0.5f, 0.65f);

        MinimapCorridorUI ui = corridorObj.AddComponent<MinimapCorridorUI>();
        ui.corridorImage = img;

        return ui;
    }

    private HashSet<KeyValuePair<Vector2Int, Vector2Int>> BuildFallbackConnections()
    {
        var result = new HashSet<KeyValuePair<Vector2Int, Vector2Int>>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var pos in roomControllers.Keys)
        {
            foreach (var dir in dirs)
            {
                Vector2Int nPos = pos + dir;
                if (roomControllers.ContainsKey(nPos))
                {
                    result.Add(new KeyValuePair<Vector2Int, Vector2Int>(pos, nPos));
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Dịch chuyển container ngược lại tọa độ phòng hiện tại để phòng của người chơi luôn nằm ở chính giữa HUD Minimap
    /// </summary>
    private void CenterMapOnCurrentRoom()
    {
        if (currentRoom == null || container == null) return;

        foreach (var kvp in roomControllers)
        {
            if (kvp.Value == currentRoom)
            {
                Vector2 targetPos = -new Vector2(kvp.Key.x * roomSpacing, kvp.Key.y * roomSpacing);
                container.anchoredPosition = targetPos;
                break;
            }
        }
    }

    /// <summary>
    /// Sự kiện khi người chơi đi vào phòng mới
    /// </summary>
    public void OnPlayerEnterRoom(RoomController room)
    {
        if (room == null) return;
        currentRoom = room;
        room.isVisited = true;
        CenterMapOnCurrentRoom();
        UpdateMinimap();

        // BỔ SUNG: Phát sóng phòng đã ghé thăm sang máy đồng đội qua SignalR
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
        {
            NetworkManager.Instance.SendRoomVisited(room.roomUniqueId);
        }
    }

    // BỔ SUNG: Nhận thông báo phòng mở từ đồng đội để cập nhật icon Minimap
    private void HandleRemoteRoomVisited(string roomUniqueId)
    {
        foreach (var kvp in roomControllers)
        {
            if (kvp.Value != null && kvp.Value.roomUniqueId == roomUniqueId)
            {
                kvp.Value.isVisited = true;
                UpdateMinimap();
                break;
            }
        }
    }

    /// <summary>
    /// Sự kiện khi dọn dẹp sạch quái trong phòng
    /// </summary>
    public void OnRoomCleared(RoomController room)
    {
        UpdateMinimap();
    }

    /// <summary>
    /// Khởi tạo động giao diện phòng Minimap khi không sử dụng Prefab bên ngoài
    /// </summary>
    private MinimapRoomUI CreateDynamicRoomUI(Vector2Int gridPos)
    {
        // 1. Tạo Node phòng chính
        GameObject roomObj = new GameObject("RoomUI_" + gridPos, typeof(RectTransform), typeof(CanvasGroup));
        roomObj.transform.SetParent(container, false);

        RectTransform rectTrans = roomObj.GetComponent<RectTransform>();
        rectTrans.sizeDelta = new Vector2(roomSpacing * 0.85f, roomSpacing * 0.85f);

        // 2. Tạo Image làm hình nền (Room.png)
        Image bgImg = roomObj.AddComponent<Image>();
        bgImg.type = Image.Type.Simple;

        // 3. Tạo Node con hiển thị Icon loại phòng (Boss/Chest/Home)
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(roomObj.transform, false);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.rectTransform.sizeDelta = new Vector2(roomSpacing * 0.85f, roomSpacing * 0.85f);
        iconImg.preserveAspect = true;

        // 4. Tạo Node con hiển thị chấm đỏ người chơi đứng
        GameObject playerObj = new GameObject("PlayerIndicator", typeof(RectTransform));
        playerObj.transform.SetParent(roomObj.transform, false);
        Image playerImg = playerObj.AddComponent<Image>();
        playerImg.rectTransform.sizeDelta = new Vector2(roomSpacing * 0.35f, roomSpacing * 0.35f);
        playerImg.color = Color.red;

        // Gán sprite cho player indicator nếu được cấu hình
        if (spritePlayer != null)
        {
            playerImg.sprite = spritePlayer;
        }
        else
        {
            // Tải sprite tròn mặc định của Unity UI (nếu có) để làm chấm tròn đỏ
            Sprite defaultSprite = Resources.Load<Sprite>("UI/Skin/Knob");
            if (defaultSprite != null)
            {
                playerImg.sprite = defaultSprite;
            }
        }

        playerObj.SetActive(false);

        // 5. Liên kết script điều khiển
        MinimapRoomUI ui = roomObj.AddComponent<MinimapRoomUI>();
        ui.roomBackground = bgImg;
        ui.roomIcon = iconImg;
        ui.playerIndicator = playerObj;

        return ui;
    }

    /// <summary>
    /// Đảm bảo Minimap UI luôn được khởi tạo và hiển thị trong Scene
    /// </summary>
    public static void EnsureMinimapExists()
    {
        if (Instance == null)
        {
            CreateAutoMinimapUI();
        }

        GameObject windowObj = GameObject.Find("MinimapWindow");
        if (windowObj != null)
        {
            RectTransform winRect = windowObj.GetComponent<RectTransform>();
            if (winRect != null)
            {
                winRect.anchorMin = new Vector2(1, 1);
                winRect.anchorMax = new Vector2(1, 1);
                winRect.pivot = new Vector2(1, 1);
                winRect.anchoredPosition = new Vector2(-45f, -55f);
            }

            Image bg = windowObj.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0.05f, 0.08f, 0.15f, 0.22f);
            }
        }

        if (Instance != null)
        {
            Instance.InitializeMinimap();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadCallback()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "SampleScene")
            {
                EnsureMinimapExists();
            }
        };
    }

    private static void CreateAutoMinimapUI()
    {
        if (Instance != null) return;

        Canvas canvas = FindUICanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[MinimapManager] Không tìm thấy Canvas nào trong Scene để tự động tạo Minimap.");
            return;
        }

        Transform parentTransform = canvas.transform;
        HUDManager hud = Object.FindFirstObjectByType<HUDManager>();
        if (hud != null)
        {
            parentTransform = hud.transform;
        }

        // Tự động ẩn nút Pause_Button trên màn hình chơi nếu tồn tại để nhường chỗ cho Minimap
        GameObject pauseBtn = GameObject.Find("Pause_Button");
        if (pauseBtn != null)
        {
            pauseBtn.SetActive(false);
            Debug.Log("[MinimapManager] Đã tự động ẩn Pause_Button để lấy chỗ trống.");
        }

        // 1. Tạo MinimapWindow làm khung chứa có Mask (Kích thước 240x240 để hiển thị rõ nét hơn)
        GameObject windowObj = new GameObject("MinimapWindow", typeof(RectTransform));
        windowObj.transform.SetParent(parentTransform, false);

        RectTransform windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(1, 1);
        windowRect.anchorMax = new Vector2(1, 1);
        windowRect.pivot = new Vector2(1, 1);
        windowRect.sizeDelta = new Vector2(240, 240);
        windowRect.anchoredPosition = new Vector2(-45f, -55f); // Hạ xuống và dời ra xa góc màn hình

        // Thêm nền xanh mờ nhẹ nhàng và trong suốt (Alpha = 0.22f) để xuyên thấu sàn nhà
        Image windowBg = windowObj.AddComponent<Image>();
        windowBg.color = new Color(0.05f, 0.08f, 0.15f, 0.22f);
        windowBg.raycastTarget = false;

        // Thêm RectMask2D để cắt các ô phòng tràn khung mượt mà không bị lỗi màu Stencil Shader của Mask cũ
        windowObj.AddComponent<RectMask2D>();

        // 2. Tạo MinimapContainer
        GameObject containerObj = new GameObject("MinimapContainer", typeof(RectTransform));
        containerObj.transform.SetParent(windowObj.transform, false);

        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(1000, 1000);
        containerRect.anchoredPosition = Vector2.zero;

        // 3. Tạo MinimapManager
        GameObject managerObj = new GameObject("MinimapManager");
        MinimapManager manager = managerObj.AddComponent<MinimapManager>();
        manager.container = containerRect;
        manager.roomSpacing = 55f; // Tăng khoảng cách ô phòng từ 35f lên 55f giúp các ô phòng to rõ ràng

        // Tải các sprite an toàn từ Resources/Minimap
        manager.spriteRoom = LoadSpriteSafely("Minimap/Room");
        manager.spriteHome = LoadSpriteSafely("Minimap/Home");
        manager.spriteBoss = LoadSpriteSafely("Minimap/Boss");
        manager.spriteChest = LoadSpriteSafely("Minimap/Chest");
        manager.spritePortal = LoadSpriteSafely("Minimap/portal");
        manager.spritePlayer = LoadSpriteSafely("UI/Skin/Knob");

        Debug.Log("[MinimapManager] Đã tự động tạo và cấu hình Minimap UI.");
        manager.InitializeMinimap();
    }

    private static Sprite LoadSpriteSafely(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;

        Sprite[] sprites = Resources.LoadAll<Sprite>(path);
        if (sprites != null && sprites.Length > 0)
        {
            return sprites[0];
        }

        return null;
    }

    /// <summary>
    /// Tìm Canvas giao diện UI chính (bỏ qua Canvas trong không gian 3D/World Space của quái)
    /// </summary>
    private static Canvas FindUICanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace && c.gameObject.activeInHierarchy)
            {
                return c;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    /// <summary>
    /// Phân loại lại phòng ở chế độ Runtime cho các RoomController hiện có để đảm bảo Minimap hoạt động chính xác
    /// </summary>
    private void RecategorizeRoomsRuntime()
    {
        if (roomControllers.Count == 0) return;

        // Nếu bất kỳ phòng nào đã được phân loại loại phòng (Start, Boss, Chest) từ trước, bảo vệ không ghi đè
        foreach (var kvp in roomControllers)
        {
            if (kvp.Value != null && kvp.Value.roomType != RoomType.Normal)
            {
                return;
            }
        }

        // 1. Đặt tất cả các phòng về Normal mặc định
        foreach (var kvp in roomControllers)
        {
            if (kvp.Value != null)
            {
                kvp.Value.roomType = RoomType.Normal;
            }
        }

        // 2. Gán phòng (0,0) làm Start (Home)
        if (roomControllers.TryGetValue(Vector2Int.zero, out RoomController startRc))
        {
            startRc.roomType = RoomType.Start;
            startRc.isVisited = true; // Phòng xuất phát mặc định đã đi qua
        }

        // 3. Tìm phòng Boss (tọa độ lưới cách xa (0,0) nhất)
        Vector2Int bossGrid = Vector2Int.zero;
        float maxDistance = -1f;
        foreach (var kvp in roomControllers)
        {
            if (kvp.Key == Vector2Int.zero) continue;
            float dist = Vector2Int.Distance(kvp.Key, Vector2Int.zero);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                bossGrid = kvp.Key;
            }
        }

        if (bossGrid != Vector2Int.zero && roomControllers.TryGetValue(bossGrid, out RoomController bossRc))
        {
            bossRc.roomType = RoomType.Boss;
            Debug.Log($"[MinimapManager] Đã nhận diện phòng Boss tại: {bossGrid}");
        }

        // 4. Tìm phòng Rương báu (Chest)
        // Tìm phòng cụt (chỉ có duy nhất 1 phòng kề cạnh trong lưới) và không phải Start/Boss
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        List<Vector2Int> otherCandidates = new List<Vector2Int>();

        foreach (var kvp in roomControllers)
        {
            Vector2Int pos = kvp.Key;
            if (pos == Vector2Int.zero || pos == bossGrid) continue;

            int neighbors = 0;
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                if (roomControllers.ContainsKey(pos + dir)) neighbors++;
            }

            if (neighbors == 1)
            {
                deadEnds.Add(pos);
            }
            else
            {
                otherCandidates.Add(pos);
            }
        }

        Vector2Int chestGrid = Vector2Int.zero;
        if (deadEnds.Count > 0)
        {
            chestGrid = deadEnds[Random.Range(0, deadEnds.Count)];
        }
        else if (otherCandidates.Count > 0)
        {
            chestGrid = otherCandidates[Random.Range(0, otherCandidates.Count)];
        }

        if (chestGrid != Vector2Int.zero && roomControllers.TryGetValue(chestGrid, out RoomController chestRc))
        {
            if (chestRc.roomType == RoomType.Normal)
            {
                chestRc.roomType = RoomType.Chest;
                Debug.Log($"[MinimapManager] Đã nhận diện phòng Rương báu tại: {chestGrid}");
            }
        }
    }
}
