using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("ĐIỂM NEO VỊ TRÍ")]
    public Transform handPosition;
    public Transform backPosition;

    [Header("HAI Ô VŨ KHÍ")]
    public GameObject weaponSlot1;
    public GameObject weaponSlot2;

    [Header("DANH SÁCH TẤT CẢ VŨ KHÍ (Dự phòng tự động nạp)")]
    public GameObject[] allWeaponPrefabs;

    [Header("NHẶT VŨ KHÍ")]
    [HideInInspector] public List<GroundWeapon> nearbyWeapons = new List<GroundWeapon>();

    public static WeaponManager Instance { get; private set; }

    private bool isUsingSlot1 = true;
    private WeaponAim handWeaponAim;
    private float nextScrollSwapTime = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        handWeaponAim = handPosition.GetComponent<WeaponAim>();

        if (weaponSlot1 != null)
        {
            // Tạo bản sao độc lập hoàn toàn trong Scene
            GameObject instance1 = Instantiate(weaponSlot1, handPosition.position, Quaternion.identity);
            Destroy(weaponSlot1); // Xóa bỏ cái xác Prefab bị lỗi cũ đi
            weaponSlot1 = instance1;
        }

        if (weaponSlot2 != null)
        {
            // Tạo bản sao độc lập hoàn toàn trong Scene
            GameObject instance2 = Instantiate(weaponSlot2, backPosition.position, Quaternion.identity);
            Destroy(weaponSlot2); // Xóa bỏ cái xác Prefab bị lỗi cũ đi
            weaponSlot2 = instance2;
        }

        ResetWeaponsStatus();

        // Tự động khôi phục súng đã nhặt/đổi ở Lobby khi vào Map chiến đấu
        RestoreSavedEquippedWeapons();
    }

    private static string savedSlot1PrefabName = "";
    private static string savedSlot2PrefabName = "";

    public static void ResetSavedWeapons()
    {
        savedSlot1PrefabName = "";
        savedSlot2PrefabName = "";
        PlayerPrefs.DeleteKey("Lobby_Slot1_Weapon");
        PlayerPrefs.DeleteKey("Lobby_Slot2_Weapon");
        PlayerPrefs.Save();
        Debug.Log("[WeaponManager] Đã đặt lại bộ nhớ lưu súng tạm thời.");
    }

    /// <summary>
    /// Lưu trữ thông tin tên Prefab vũ khí đang trang bị ở Lobby vào Bộ nhớ tĩnh / PlayerPrefs
    /// </summary>
    public void SaveEquippedWeapons()
    {
        string s1Name = GetCleanWeaponPrefabName(weaponSlot1);
        string s2Name = GetCleanWeaponPrefabName(weaponSlot2);

        savedSlot1PrefabName = s1Name;
        savedSlot2PrefabName = s2Name;

        if (!string.IsNullOrEmpty(s1Name)) PlayerPrefs.SetString("Lobby_Slot1_Weapon", s1Name);
        if (!string.IsNullOrEmpty(s2Name)) PlayerPrefs.SetString("Lobby_Slot2_Weapon", s2Name);
        PlayerPrefs.Save();

        Debug.Log($"[WeaponManager] Đã lưu súng trang bị từ Lobby: Slot1='{s1Name}', Slot2='{s2Name}'");
    }

    private string GetCleanWeaponPrefabName(GameObject weaponObj)
    {
        if (weaponObj == null) return "";

        WeaponInfo info = weaponObj.GetComponent<WeaponInfo>();
        if (info != null && info.weaponPrefab != null) return info.weaponPrefab.name;

        WeaponLaser laser = weaponObj.GetComponent<WeaponLaser>();
        if (laser != null && laser.weaponPrefab != null) return laser.weaponPrefab.name;

        return weaponObj.name.Replace("(Clone)", "").Trim();
    }

    /// <summary>
    /// Khôi phục súng người chơi đã nhặt ở Lobby khi vừa nạp vào Map chiến đấu (SampleScene)
    /// </summary>
    private void RestoreSavedEquippedWeapons()
    {
        string s1Name = !string.IsNullOrEmpty(savedSlot1PrefabName) ? savedSlot1PrefabName : PlayerPrefs.GetString("Lobby_Slot1_Weapon", "");
        string s2Name = !string.IsNullOrEmpty(savedSlot2PrefabName) ? savedSlot2PrefabName : PlayerPrefs.GetString("Lobby_Slot2_Weapon", "");

        if (string.IsNullOrEmpty(s1Name) && string.IsNullOrEmpty(s2Name)) return;

        Debug.Log($"[WeaponManager] Khôi phục súng từ Lobby sang Map: Slot1='{s1Name}', Slot2='{s2Name}'");

        if (!string.IsNullOrEmpty(s1Name))
        {
            GameObject p1 = FindWeaponPrefabByName(s1Name);
            if (p1 != null)
            {
                if (weaponSlot1 != null) Destroy(weaponSlot1);
                weaponSlot1 = Instantiate(p1, handPosition.position, Quaternion.identity);
                UpdateWeaponParent(weaponSlot1, handPosition, true);
            }
        }

        if (!string.IsNullOrEmpty(s2Name))
        {
            GameObject p2 = FindWeaponPrefabByName(s2Name);
            if (p2 != null)
            {
                if (weaponSlot2 != null) Destroy(weaponSlot2);
                weaponSlot2 = Instantiate(p2, backPosition.position, Quaternion.identity);
                UpdateWeaponParent(weaponSlot2, backPosition, false);
            }
        }

        isUsingSlot1 = true;
        ResetWeaponsStatus();
    }

    void Update()
    {
        // 1. Kiểm tra nhặt vũ khí (Phím E bàn phím hoặc Nút B tay cầm khi có súng gần đó)
        CheckWeaponPickup();

        // 2. Logic đổi vũ khí (Swap) - Hỗ trợ Rebind phím động từ Settings
        bool hasPressedSwapKey = false;

        InputAction switchAction = (InputLoader.Instance != null) ? InputLoader.Instance.GetAction("SwitchWeapon") : null;
        if (switchAction != null && (switchAction.triggered || switchAction.WasPressedThisFrame()))
        {
            hasPressedSwapKey = true;
        }
        else if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        else if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        else if (Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.1f)
        {
            if (Time.time >= nextScrollSwapTime)
            {
                hasPressedSwapKey = true;
                nextScrollSwapTime = Time.time + 1.0f; // Delay 1s giữa các lần cuộn chuột đổi súng
            }
        }

        if (hasPressedSwapKey)
        {
            SwapWeapon();
        }
    }

    private void CheckWeaponPickup()
    {
        if (nearbyWeapons.Count == 0) return;

        bool hasPressedPickupKey = false;

        InputAction interactAction = (InputLoader.Instance != null) ? InputLoader.Instance.GetAction("Interact") : null;
        if (interactAction != null && (interactAction.triggered || interactAction.WasPressedThisFrame()))
        {
            hasPressedPickupKey = true;
        }
        // Bàn phím bấm phím mặc định E (vừa mở rương vừa nhặt súng)
        else if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            hasPressedPickupKey = true;
        }
        // Tay cầm bấm Nút B (vừa mở rương vừa nhặt súng)
        else if (Gamepad.current != null && Gamepad.current.bButton.wasPressedThisFrame)
        {
            hasPressedPickupKey = true;
        }

        if (hasPressedPickupKey)
        {
            GroundWeapon closest = GetClosestGroundWeapon();
            if (closest != null)
            {
                PickupWeapon(closest);
            }
        }
    }

    private GroundWeapon GetClosestGroundWeapon()
    {
        GroundWeapon closest = null;
        float minDistance = Mathf.Infinity;
        foreach (var gw in nearbyWeapons)
        {
            if (gw == null) continue;
            float dist = Vector3.Distance(transform.position, gw.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = gw;
            }
        }
        return closest;
    }

    // Hàm dự phòng để tìm Prefab súng dựa vào tên GameObject của súng
    public GameObject FindWeaponPrefabByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;
        string cleanName = weaponName.Replace("(Clone)", "").Trim();
        Debug.Log($"[WeaponManager] Đang tìm Prefab bằng tên: '{cleanName}'");

        // Bước 1: Tìm trong allWeaponPrefabs của chính mình
        if (allWeaponPrefabs != null && allWeaponPrefabs.Length > 0)
        {
            foreach (var prefab in allWeaponPrefabs)
            {
                if (prefab != null && prefab.name == cleanName)
                {
                    Debug.Log($"[WeaponManager] Tìm thấy khớp trong allWeaponPrefabs: {prefab.name}");
                    return prefab;
                }
            }
        }

        // Bước 2: Dự phòng mở rộng - Dò tìm từ các WeaponChest trong Scene đang hoạt động
        WeaponChest[] chests = Object.FindObjectsByType<WeaponChest>(FindObjectsSortMode.None);
        foreach (var chest in chests)
        {
            if (chest != null && chest.weaponPrefabs != null)
            {
                foreach (var prefab in chest.weaponPrefabs)
                {
                    if (prefab != null && prefab.name == cleanName)
                    {
                        Debug.Log($"[WeaponManager] Tìm thấy khớp từ WeaponChest: {prefab.name}");
                        return prefab;
                    }
                }
            }
        }

        // Bước 3: Dò tìm trong Resources/Weapons/
        GameObject resPrefab = Resources.Load<GameObject>("Weapons/" + cleanName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(cleanName);
        if (resPrefab != null)
        {
            Debug.Log($"[WeaponManager] Tìm thấy khớp từ Resources/Weapons: {resPrefab.name}");
            return resPrefab;
        }

        return null;
    }

    public void PickupWeapon(GroundWeapon groundWeapon)
    {
        GameObject newWeaponPrefab = groundWeapon.weaponPrefab;
        if (newWeaponPrefab == null)
        {
            Debug.LogError($"[WeaponManager] Không thể nhặt vì groundWeapon.weaponPrefab bị NULL! Tên đối tượng trên đất: '{groundWeapon.gameObject.name}'");
            return;
        }

        // 1. Xác định vũ khí hiện tại ở Hand_Position
        GameObject currentHandWeapon = isUsingSlot1 ? weaponSlot1 : weaponSlot2;
        Debug.Log($"[WeaponManager] Bắt đầu nhặt: {newWeaponPrefab.name}. Đang cầm: {(currentHandWeapon != null ? currentHandWeapon.name : "Không có")}");

        // 2. Nếu đang cầm súng cũ, hãy vứt ra đất
        if (currentHandWeapon != null)
        {
            GameObject oldWeaponPrefab = null;
            
            WeaponInfo oldInfo = currentHandWeapon.GetComponent<WeaponInfo>();
            WeaponLaser oldLaser = currentHandWeapon.GetComponent<WeaponLaser>();
            if (oldInfo != null) oldWeaponPrefab = oldInfo.weaponPrefab;
            else if (oldLaser != null) oldWeaponPrefab = oldLaser.weaponPrefab;

            // QUAN TRỌNG: Nếu oldWeaponPrefab là đối tượng trong Scene (chứ không phải Prefab trong Project),
            // ta buộc phải ép về null để tìm kiếm file gốc từ Project, tránh việc tham chiếu bị hủy (Destroy) sau đó.
            if (oldWeaponPrefab != null && oldWeaponPrefab.scene.IsValid())
            {
                Debug.LogWarning($"[WeaponManager] Phát hiện oldWeaponPrefab '{oldWeaponPrefab.name}' là đối tượng trong Scene! Đang ép về null để tìm file gốc từ Project...");
                oldWeaponPrefab = null;
            }

            // DỰ PHÒNG 1: Dò tìm bằng Tên súng trong các danh sách nạp sẵn
            if (oldWeaponPrefab == null)
            {
                oldWeaponPrefab = FindWeaponPrefabByName(currentHandWeapon.name);
            }

            // DỰ PHÒNG 2 (CHỈ KHI CHẠY TRONG EDITOR): Tự động load trực tiếp từ Assets/Prefab/Weapons/ bằng AssetDatabase
#if UNITY_EDITOR
            if (oldWeaponPrefab == null)
            {
                string cleanName = currentHandWeapon.name.Replace("(Clone)", "").Trim();
                string path = $"Assets/Prefab/Weapons/{cleanName}.prefab";
                oldWeaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (oldWeaponPrefab != null)
                {
                    Debug.Log($"[WeaponManager] [Editor Mode] Tải thành công prefab súng bằng AssetDatabase tại đường dẫn '{path}': {oldWeaponPrefab.name}");
                }
                else
                {
                    Debug.LogError($"[WeaponManager] [Editor Mode] Thất bại tải prefab súng bằng AssetDatabase tại đường dẫn '{path}'");
                }
            }
#endif

            if (oldWeaponPrefab != null)
            {
                // Sinh súng rơi trên đất tại vị trí người chơi bằng C# code (ko cần kéo thả groundWeaponPrefab)
                GroundWeapon.Create(oldWeaponPrefab, transform.position);
                Debug.Log($"[WeaponManager] Đã vứt súng cũ ra đất: {oldWeaponPrefab.name}");
            }
            else
            {
                Debug.LogError($"[WeaponManager] THẤT BẠI HOÀN TOÀN: Không tìm thấy Prefab cho súng cũ '{currentHandWeapon.name}' để vứt ra đất!");
            }
            
            // Hủy súng cũ trên tay
            Destroy(currentHandWeapon);
        }

        // 3. Khởi tạo súng mới và gắn vào active slot
        GameObject newWeaponInstance = Instantiate(newWeaponPrefab, handPosition.position, Quaternion.identity);
        
        if (isUsingSlot1)
        {
            weaponSlot1 = newWeaponInstance;
            UpdateWeaponParent(weaponSlot1, handPosition, true);
        }
        else
        {
            weaponSlot2 = newWeaponInstance;
            UpdateWeaponParent(weaponSlot2, handPosition, true);
        }

        // 4. Xóa GroundWeapon cũ dưới đất khỏi danh sách nhặt và tiêu hủy
        if (nearbyWeapons.Contains(groundWeapon))
        {
            nearbyWeapons.Remove(groundWeapon);
        }
        Destroy(groundWeapon.gameObject);

        Debug.Log($"[WeaponManager] Đã nhặt súng mới thành công: {newWeaponPrefab.name}");
        SaveEquippedWeapons();
    }

    void SwapWeapon()
    {
        if (weaponSlot1 == null && weaponSlot2 != null)
        {
            weaponSlot1 = weaponSlot2;
            weaponSlot2 = null;
            isUsingSlot1 = true;
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            return;
        }

        if (weaponSlot1 != null && weaponSlot2 == null)
        {
            isUsingSlot1 = true;
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            return;
        }

        if (weaponSlot1 == null && weaponSlot2 == null) return;

        isUsingSlot1 = !isUsingSlot1;

        if (isUsingSlot1)
        {
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            UpdateWeaponParent(weaponSlot2, backPosition, false);
        }
        else
        {
            UpdateWeaponParent(weaponSlot2, handPosition, true);
            UpdateWeaponParent(weaponSlot1, backPosition, false);
        }

        // BỔ SUNG: Phát tín hiệu đổi súng lên mạng khi đổi vũ khí
        SyncActiveWeaponToNetwork();
    }

    void UpdateWeaponParent(GameObject weapon, Transform newParent, bool isTargetHand)
    {
        if (weapon == null) return;

        // Bây giờ đối tượng đã là bản sao độc lập, SetParent thoải mái không bao giờ lỗi nữa!
        weapon.transform.SetParent(newParent);
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        WeaponInfo info = weapon.GetComponent<WeaponInfo>();
        WeaponLaser laserScript = weapon.GetComponent<WeaponLaser>();

        if (isTargetHand)
        {
            if (info != null)
            {
                weapon.transform.localPosition = info.customHandPosition;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = info;
            }
            else if (laserScript != null)
            {
                weapon.transform.localPosition = laserScript.customHandPosition;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = null;
            }
            else
            {
                weapon.transform.localPosition = Vector3.zero;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = null;
            }
        }
        else
        {
            weapon.transform.localPosition = Vector3.zero;

            if (laserScript != null)
            {
                laserScript.StopLaser();
            }
        }

        SpriteRenderer weaponRenderer = weapon.GetComponent<SpriteRenderer>();
        SpriteRenderer playerRenderer = handPosition.parent.GetComponent<SpriteRenderer>();

        if (weaponRenderer != null && playerRenderer != null)
        {
            if (isTargetHand) weaponRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
            else weaponRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
        }
    }

    public void ResetWeaponsStatus()
    {
        if (weaponSlot1 == null && weaponSlot2 != null)
        {
            weaponSlot1 = weaponSlot2;
            weaponSlot2 = null;
        }

        if (weaponSlot1 != null)
        {
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            isUsingSlot1 = true;
        }

        if (weaponSlot2 != null)
        {
            UpdateWeaponParent(weaponSlot2, backPosition, false);
        }

        // BỔ SUNG: Đồng bộ loại súng đang cầm lên mạng cho các người chơi khác cùng thấy
        SyncActiveWeaponToNetwork();
    }

    /// <summary>
    /// Phát sóng loại súng chính và súng phụ đang cầm hiện tại lên Server SignalR
    /// </summary>
    public void SyncActiveWeaponToNetwork()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
        {
            GameObject activeWeapon = isUsingSlot1 ? weaponSlot1 : weaponSlot2;
            GameObject secondaryWeapon = isUsingSlot1 ? weaponSlot2 : weaponSlot1;

            string activeName = activeWeapon != null ? activeWeapon.name.Replace("(Clone)", "").Trim() : "";
            string secondaryName = secondaryWeapon != null ? secondaryWeapon.name.Replace("(Clone)", "").Trim() : "";

            NetworkManager.Instance.SendEquippedWeapon(activeName, secondaryName);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string folderPath = "Assets/Prefab/Weapons";
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] files = System.IO.Directory.GetFiles(folderPath, "*.prefab");
            System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
            foreach (string file in files)
            {
                if (file.Contains("GroundWeapon")) continue;
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }
            allWeaponPrefabs = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}