using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponChest : MonoBehaviour
{
    [Header("Visual Components")]
    public GameObject body;      // Body (thân rương)
    public GameObject top;       // Top (nắp rương)
    public GameObject inside;    // Inside (lòng rương hiển thị khi mở)

    [Header("Loot Configuration")]
    public GameObject[] weaponPrefabs;      // Danh sách các súng để random khi mở

    private bool isOpened = false;
    private bool isPlayerInRange = false;
    private TextMesh promptText;

    private void Start()
    {
        // 1. Tự động sửa lỗi liên kết ở Runtime (Self-healing)
        if (body == null)
        {
            Transform bodyTrans = transform.Find("Body");
            if (bodyTrans != null) body = bodyTrans.gameObject;
        }

        if (top == null)
        {
            Transform topTrans = transform.Find("Top");
            if (topTrans != null) top = topTrans.gameObject;
        }

        if (inside == null)
        {
            Transform insideTrans = transform.Find("Inside");
            if (insideTrans != null) inside = insideTrans.gameObject;
        }

        // 2. Ép cứng Sorting Order để rương hiển thị đúng đè lớp
        EnforceSortingOrders();

        // 3. Tạo chữ hướng dẫn tương tác bay phía trên
        CreatePromptText();
    }

    private void EnforceSortingOrders()
    {
        if (body != null)
        {
            SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 5;
        }

        if (top != null)
        {
            SpriteRenderer sr = top.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }

        if (inside != null)
        {
            SpriteRenderer sr = inside.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6;
        }
    }

    private void CreatePromptText()
    {
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, 0.9f, 0f); // Phía trên nắp rương

        promptText = textObj.AddComponent<TextMesh>();
        promptText.text = ""; // Không hiện tiêu đề rương ban đầu như yêu cầu
        promptText.fontSize = 24;
        promptText.characterSize = 0.05f;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.color = Color.green;

        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 7;
        }
    }

    private void Update()
    {
        if (isOpened) return;

        if (isPlayerInRange)
        {
            // Cập nhật text động tùy theo thiết bị đang sử dụng
            if (promptText != null)
            {
                if (InputDeviceHelper.IsGamepadActive())
                {
                    promptText.text = "Nút B";
                }
                else
                {
                    promptText.text = "Bấm E";
                }
                promptText.color = Color.green;
            }

            bool hasPressedOpenKey = false;

            // Bàn phím bấm E
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                hasPressedOpenKey = true;
            }
            // Tay cầm bấm B (buttonEast)
            else if (Gamepad.current != null && Gamepad.current.bButton.wasPressedThisFrame)
            {
                hasPressedOpenKey = true;
            }

            if (hasPressedOpenKey)
            {
                OpenChest();
            }
        }
    }

    public void OpenChest()
    {
        if (isOpened) return;
        isOpened = true;

        // 1. Cập nhật hiển thị rương mở
        if (body != null) body.SetActive(true);
        if (top != null) top.SetActive(false);
        if (inside != null) inside.SetActive(true);

        // 2. Xóa chữ hướng dẫn
        if (promptText != null)
        {
            Destroy(promptText.gameObject);
        }

        // 3. Sinh vũ khí ngẫu nhiên nằm yên trên sàn (lệch sang phải 0.8 unit)
        SpawnWeaponLoot();

        // 4. Vô hiệu hóa vùng va chạm để không tương tác nữa
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    private void SpawnWeaponLoot()
    {
        // DỰ PHÒNG EDITOR: Tự động nạp súng nếu mảng trống khi đang chạy trong Editor
#if UNITY_EDITOR
        if (weaponPrefabs == null || weaponPrefabs.Length == 0)
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
                    if (prefab != null) list.Add(prefab);
                }
                weaponPrefabs = list.ToArray();
            }
        }
#endif

        if (weaponPrefabs == null || weaponPrefabs.Length == 0)
        {
            Debug.LogWarning("[WeaponChest] Thiếu cấu hình weaponPrefabs!");
            return;
        }

        // Chọn súng ngẫu nhiên
        GameObject randomWeaponPrefab = weaponPrefabs[Random.Range(0, weaponPrefabs.Length)];
        
        // Sinh súng nằm yên trên sàn (không cần GroundWeapon prefab), lệch phải 0.8 unit để không đè lên rương
        Vector3 spawnPos = transform.position + new Vector3(0.8f, 0f, 0f);
        GroundWeapon.Create(randomWeaponPrefab, spawnPos);

        Debug.Log($"[WeaponChest] Đã mở rương vũ khí! Sinh súng: {randomWeaponPrefab.name} tại {spawnPos}");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOpened) return;

        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (isOpened) return;

        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;

            // Xóa text tương tác khi đi xa
            if (promptText != null)
            {
                promptText.text = "";
            }
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
            weaponPrefabs = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
