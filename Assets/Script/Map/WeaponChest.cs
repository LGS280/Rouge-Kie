using System.Collections;
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
                string keyName = InputDeviceHelper.GetInteractKeyDisplayString();
                if (InputDeviceHelper.IsGamepadActive())
                {
                    promptText.text = "Press B";
                }
                else
                {
                    promptText.text = "Press " + keyName;
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

        // 5. Chạy animation nắp rương di chuyển nhẹ về phía sau rồi biến mất
        if (top != null)
        {
            StartCoroutine(AnimateTopChestOpen(top));
        }
    }

    private IEnumerator AnimateTopChestOpen(GameObject topObj)
    {
        SpriteRenderer sr = topObj.GetComponent<SpriteRenderer>();
        Vector3 startPos = topObj.transform.localPosition;
        Vector3 targetPos = startPos + new Vector3(0f, 0.4f, 0f); // Di chuyển nhẹ về phía sau/trên

        float duration = 0.55f; // Tăng từ 0.25s lên 0.55s để animation chậm lại mượt mà, dễ quan sát
        float elapsed = 0f;

        Color startColor = (sr != null) ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            topObj.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

            if (sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        topObj.SetActive(false);
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

        // Chọn súng ngẫu nhiên theo Trọng số Phẩm chất (Common: 50%, Rare: 30%, Epic: 15%, Legendary: 5%)
        GameObject randomWeaponPrefab = SelectWeaponByRarity(weaponPrefabs);
        
        // Sinh súng nằm yên trên sàn (không cần GroundWeapon prefab), lệch phải 0.8 unit để không đè lên rương
        Vector3 spawnPos = transform.position + new Vector3(0.8f, 0f, 0f);
        GroundWeapon.Create(randomWeaponPrefab, spawnPos);

        Debug.Log($"[WeaponChest] 🎉 Đã mở rương vũ khí! Sinh súng: {randomWeaponPrefab.name} tại {spawnPos}");
    }

    private GameObject SelectWeaponByRarity(GameObject[] prefabs)
    {
        float totalWeight = 0f;
        float[] weights = new float[prefabs.Length];

        for (int i = 0; i < prefabs.Length; i++)
        {
            float weight = 40f; // Trọng số mặc định nếu không có DB
            GameObject p = prefabs[i];
            
            if (p != null)
            {
                string pName = p.name.Replace("(Clone)", "").Trim();
                if (GameConfigManager.Instance != null && GameConfigManager.Instance.WeaponDbByName.TryGetValue(pName, out WeaponConfig config))
                {
                    if (!string.IsNullOrEmpty(config.rarity))
                    {
                        switch (config.rarity.Trim().ToLower())
                        {
                            case "common":
                                weight = 50f; // Tỷ lệ phổ thông
                                break;
                            case "rare":
                                weight = 30f; // Tỷ lệ súng hiếm (Shotgun, Laser, M249, Missile)
                                break;
                            case "epic":
                                weight = 15f; // Tỷ lệ súng xịn (Snipe, Gold Katana)
                                break;
                            case "legendary":
                                weight = 5f;  // Tỷ lệ súng siêu xịn huyền thoại
                                break;
                        }
                    }
                }
            }

            weights[i] = weight;
            totalWeight += weight;
        }

        float randomRoll = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        for (int i = 0; i < prefabs.Length; i++)
        {
            currentSum += weights[i];
            if (randomRoll <= currentSum)
            {
                return prefabs[i];
            }
        }

        return prefabs[Random.Range(0, prefabs.Length)];
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
