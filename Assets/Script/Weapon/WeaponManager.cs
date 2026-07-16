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

    private bool isUsingSlot1 = true;
    private WeaponAim handWeaponAim;

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
    }

    void Update()
    {
        // 1. Kiểm tra nhặt vũ khí (Chuột trái hoặc nút X tay cầm khi có súng gần đó)
        CheckWeaponPickup();

        // 2. Logic đổi vũ khí (Swap) - Giữ nguyên hoàn toàn logic đổi súng hiện tại của bạn
        bool hasPressedSwapKey = false;

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        else if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        else if (Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.1f)
        {
            hasPressedSwapKey = true;
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

        // Bàn phím bấm Chuột trái
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            hasPressedPickupKey = true;
        }
        // Tay cầm bấm nút X
        else if (Gamepad.current != null && Gamepad.current.xButton.wasPressedThisFrame)
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

    void ResetWeaponsStatus()
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