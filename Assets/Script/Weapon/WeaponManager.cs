using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("điểm treo vũ khí")]
    public Transform handPosition;
    public Transform backPosition;

    [Header("2 ô vũ khí")]
    public GameObject weaponSlot1;
    public GameObject weaponSlot2;

    [Header("list các vũ khí của player (dự phòng chưa nạp)")]
    public GameObject[] allWeaponPrefabs;

    [Header("nhặt vũ khí")]
    [HideInInspector] public List<GroundWeapon> nearbyWeapons = new List<GroundWeapon>();

    public static WeaponManager Instance { get; private set; }

    private bool isUsingSlot1 = true;
    private WeaponAim handWeaponAim;
    private float nextScrollSwapTime = 0f;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        handWeaponAim = handPosition.GetComponent<WeaponAim>();

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Nếu ở Lobby_Scene: Luôn dùng 2 vũ khí mặc định được cấu hình trong Inspector cho run mới
        if (currentScene == "Lobby_Scene")
        {
            ResetSavedWeapons();

            if (weaponSlot1 != null)
            {
                GameObject instance1 = Instantiate(weaponSlot1, handPosition.position, Quaternion.identity);
                if (weaponSlot1.scene.IsValid()) Destroy(weaponSlot1);
                weaponSlot1 = instance1;
            }

            if (weaponSlot2 != null)
            {
                GameObject instance2 = Instantiate(weaponSlot2, backPosition.position, Quaternion.identity);
                if (weaponSlot2.scene.IsValid()) Destroy(weaponSlot2);
                weaponSlot2 = instance2;
            }

            ResetWeaponsStatus();
            SaveEquippedWeapons();
            return;
        }

        // Nếu ở Dungeon (SampleScene hoặc các map chiến đấu): Khôi phục súng mà người chơi mang từ Lobby sang
        bool restored = RestoreSavedEquippedWeapons();

        if (!restored)
        {
            // Dự phòng nếu không có dữ liệu lưu (ví dụ mở thẳng SampleScene trong Editor để test)
            if (weaponSlot1 != null)
            {
                GameObject instance1 = Instantiate(weaponSlot1, handPosition.position, Quaternion.identity);
                if (weaponSlot1.scene.IsValid()) Destroy(weaponSlot1);
                weaponSlot1 = instance1;
            }

            if (weaponSlot2 != null)
            {
                GameObject instance2 = Instantiate(weaponSlot2, backPosition.position, Quaternion.identity);
                if (weaponSlot2.scene.IsValid()) Destroy(weaponSlot2);
                weaponSlot2 = instance2;
            }

            ResetWeaponsStatus();
        }
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
        Debug.Log("[WeaponManager] Đã reset bộ nhớ vũ khí lưu cho lượt chơi mới.");
    }

    public void SaveEquippedWeapons()
    {
        // Luôn lưu khẩu súng đang cầm trên tay vào Slot 1, và súng đeo trên lưng vào Slot 2
        GameObject handWeapon = isUsingSlot1 ? weaponSlot1 : weaponSlot2;
        GameObject backWeapon = isUsingSlot1 ? weaponSlot2 : weaponSlot1;

        string s1Name = GetCleanWeaponPrefabName(handWeapon);
        string s2Name = GetCleanWeaponPrefabName(backWeapon);

        savedSlot1PrefabName = s1Name;
        savedSlot2PrefabName = s2Name;

        PlayerPrefs.SetString("Lobby_Slot1_Weapon", s1Name ?? "");
        PlayerPrefs.SetString("Lobby_Slot2_Weapon", s2Name ?? "");
        PlayerPrefs.Save();

        Debug.Log($"[WeaponManager] Đã lưu vũ khí: Cầm trên tay (Slot 1) = '{s1Name}', Đeo sau lưng (Slot 2) = '{s2Name}'");
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

    private bool RestoreSavedEquippedWeapons()
    {
        string s1Name = !string.IsNullOrEmpty(savedSlot1PrefabName) ? savedSlot1PrefabName : PlayerPrefs.GetString("Lobby_Slot1_Weapon", "");
        string s2Name = !string.IsNullOrEmpty(savedSlot2PrefabName) ? savedSlot2PrefabName : PlayerPrefs.GetString("Lobby_Slot2_Weapon", "");

        if (string.IsNullOrEmpty(s1Name) && string.IsNullOrEmpty(s2Name)) return false;

        Debug.Log($"[WeaponManager] Khôi phục vũ khí đã lưu từ Lobby: Slot 1 = '{s1Name}', Slot 2 = '{s2Name}'");

        // Dọn sạch các vũ khí mặc định cũ nếu có trong Scene
        if (weaponSlot1 != null && weaponSlot1.scene.IsValid())
        {
            Destroy(weaponSlot1);
            weaponSlot1 = null;
        }
        if (weaponSlot2 != null && weaponSlot2.scene.IsValid())
        {
            Destroy(weaponSlot2);
            weaponSlot2 = null;
        }

        if (handPosition != null)
        {
            foreach (Transform child in handPosition)
            {
                if (child != null && child.gameObject != null) Destroy(child.gameObject);
            }
        }
        if (backPosition != null)
        {
            foreach (Transform child in backPosition)
            {
                if (child != null && child.gameObject != null) Destroy(child.gameObject);
            }
        }

        bool restoredAny = false;

        if (!string.IsNullOrEmpty(s1Name))
        {
            GameObject p1 = FindWeaponPrefabByName(s1Name);
            if (p1 != null)
            {
                weaponSlot1 = Instantiate(p1, handPosition.position, Quaternion.identity);
                UpdateWeaponParent(weaponSlot1, handPosition, true);
                restoredAny = true;
            }
            else
            {
                Debug.LogWarning($"[WeaponManager] Không tìm thấy Prefab cho Slot 1: '{s1Name}'");
            }
        }
        else
        {
            weaponSlot1 = null;
        }

        if (!string.IsNullOrEmpty(s2Name))
        {
            GameObject p2 = FindWeaponPrefabByName(s2Name);
            if (p2 != null)
            {
                weaponSlot2 = Instantiate(p2, backPosition.position, Quaternion.identity);
                UpdateWeaponParent(weaponSlot2, backPosition, false);
                restoredAny = true;
            }
            else
            {
                Debug.LogWarning($"[WeaponManager] Không tìm thấy Prefab cho Slot 2: '{s2Name}'");
            }
        }
        else
        {
            weaponSlot2 = null;
        }

        if (restoredAny)
        {
            isUsingSlot1 = true;
            ResetWeaponsStatus();
            return true;
        }

        return false;
    }

    void Update()
    {

        CheckWeaponPickup();

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
                nextScrollSwapTime = Time.time + 1.0f;
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

        else if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            hasPressedPickupKey = true;
        }

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

    public GameObject FindWeaponPrefabByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return null;
        string cleanName = weaponName.Replace("Weapons/", "").Replace("(Clone)", "").Trim();

        if (allWeaponPrefabs != null && allWeaponPrefabs.Length > 0)
        {
            foreach (var prefab in allWeaponPrefabs)
            {
                if (prefab != null && prefab.name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return prefab;
                }
            }
        }

        WeaponChest[] chests = Object.FindObjectsByType<WeaponChest>(FindObjectsSortMode.None);
        foreach (var chest in chests)
        {
            if (chest != null && chest.weaponPrefabs != null)
            {
                foreach (var prefab in chest.weaponPrefabs)
                {
                    if (prefab != null && prefab.name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return prefab;
                    }
                }
            }
        }

        GameObject resPrefab = Resources.Load<GameObject>("Weapons/" + cleanName);
        if (resPrefab == null) resPrefab = Resources.Load<GameObject>(cleanName);
        if (resPrefab != null)
        {
            return resPrefab;
        }

#if UNITY_EDITOR
        string editorPath = $"Assets/Prefab/Weapons/{cleanName}.prefab";
        GameObject edPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
        if (edPrefab != null) return edPrefab;
#endif

        return null;
    }

    /// <summary>
    /// Trang bị trực tiếp vũ khí từ Kho Vũ Khí (Armory Vault) hoặc Shop vào tay người chơi
    /// </summary>
    public bool EquipWeaponDirectly(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return false;

        // Trang bị vũ khí trực tiếp vào vị trí tay cầm (Active Hand Weapon)
        if (isUsingSlot1)
        {
            if (weaponSlot1 != null) Destroy(weaponSlot1);
            weaponSlot1 = Instantiate(weaponPrefab, handPosition.position, Quaternion.identity);
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            if (weaponSlot2 != null) UpdateWeaponParent(weaponSlot2, backPosition, false);
        }
        else
        {
            if (weaponSlot2 != null) Destroy(weaponSlot2);
            weaponSlot2 = Instantiate(weaponPrefab, handPosition.position, Quaternion.identity);
            UpdateWeaponParent(weaponSlot2, handPosition, true);
            if (weaponSlot1 != null) UpdateWeaponParent(weaponSlot1, backPosition, false);
        }

        SaveEquippedWeapons();
        SyncActiveWeaponToNetwork();
        return true;
    }

    public void PickupWeapon(GroundWeapon groundWeapon)
    {
        GameObject newWeaponPrefab = groundWeapon.weaponPrefab;
        if (newWeaponPrefab == null)
        {

            return;
        }

        GameObject currentHandWeapon = isUsingSlot1 ? weaponSlot1 : weaponSlot2;
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);

        if (currentHandWeapon != null)
        {
            GameObject oldWeaponPrefab = null;

            WeaponInfo oldInfo = currentHandWeapon.GetComponent<WeaponInfo>();
            WeaponLaser oldLaser = currentHandWeapon.GetComponent<WeaponLaser>();
            if (oldInfo != null) oldWeaponPrefab = oldInfo.weaponPrefab;
            else if (oldLaser != null) oldWeaponPrefab = oldLaser.weaponPrefab;

            if (oldWeaponPrefab != null && oldWeaponPrefab.scene.IsValid())
            {

                oldWeaponPrefab = null;
            }

            if (oldWeaponPrefab == null)
            {
                oldWeaponPrefab = FindWeaponPrefabByName(currentHandWeapon.name);
            }

#if UNITY_EDITOR
            if (oldWeaponPrefab == null)
            {
                string cleanName = currentHandWeapon.name.Replace("(Clone)", "").Trim();
                string path = $"Assets/Prefab/Weapons/{cleanName}.prefab";
                oldWeaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (oldWeaponPrefab != null)
                {

                }
                else
                {

                }
            }
#endif

            if (oldWeaponPrefab != null)
            {
                string oldCleanName = oldWeaponPrefab.name.Replace("(Clone)", "").Trim();
                string dropNetworkId = $"drop_{oldCleanName}_{Mathf.RoundToInt(transform.position.x * 10)}_{Mathf.RoundToInt(transform.position.y * 10)}_{Random.Range(100, 999)}";
                GroundWeapon.Create(oldWeaponPrefab, transform.position, dropNetworkId);

                if (isMultiplayer)
                {
                    NetworkManager.Instance.SendDropWeapon(oldCleanName, transform.position.x, transform.position.y, dropNetworkId);
                }
            }
            else
            {

            }

            Destroy(currentHandWeapon);
        }

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

        if (nearbyWeapons.Contains(groundWeapon))
        {
            nearbyWeapons.Remove(groundWeapon);
        }

        // BỔ SUNG: Nếu đang trong phòng Co-op, gửi lệnh nhặt súng để xóa trên toàn bộ máy đồng đội
        if (isMultiplayer && groundWeapon != null && !string.IsNullOrEmpty(groundWeapon.networkId))
        {
            NetworkManager.Instance.SendPickupGroundWeapon(groundWeapon.networkId);
        }

        Destroy(groundWeapon.gameObject);

        SaveEquippedWeapons();

        // BỔ SUNG: Đồng bộ ngay lập tức súng mới trên tay mình cho toàn bộ đồng đội thấy
        SyncActiveWeaponToNetwork();
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

        SyncActiveWeaponToNetwork();
        SaveEquippedWeapons();
    }

    void UpdateWeaponParent(GameObject weapon, Transform newParent, bool isTargetHand)
    {
        if (weapon == null) return;

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

        SyncActiveWeaponToNetwork();
    }

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
        if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

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

            bool isChanged = false;
            if (allWeaponPrefabs == null || allWeaponPrefabs.Length != list.Count)
            {
                isChanged = true;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (allWeaponPrefabs[i] != list[i])
                    {
                        isChanged = true;
                        break;
                    }
                }
            }

            if (isChanged)
            {
                allWeaponPrefabs = list.ToArray();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
    }
#endif
}
