using UnityEngine;

public class GroundWeapon : MonoBehaviour
{
    [Header("Prefab Vũ khí tương ứng")]
    public GameObject weaponPrefab;

    private bool isPlayerInside = false;
    private Transform nameTagTrans;

    // Hàm Static để khởi tạo GroundWeapon trực tiếp bằng code ở Runtime (không cần file Prefab)
    public static GameObject Create(GameObject weaponPrefab, Vector3 position)
    {
        if (weaponPrefab == null) return null;

        GameObject go = new GameObject("GroundWeapon");
        go.transform.position = position;

        // 1. TỰ ĐỘNG THIẾT LẬP KÍCH THƯỚC:
        // Lấy lossyScale của Player để súng trên đất có kích thước bằng súng khi cầm trên tay (không bị nhỏ nữa)
        Vector3 targetScale = new Vector3(2.5f, 2.5f, 1f); // Dự phòng mặc định
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            targetScale = player.transform.lossyScale;
            targetScale.z = 1f; // Tránh bóp méo trục z của 2D
        }
        go.transform.localScale = targetScale;

        // 2. Thêm SpriteRenderer để hiển thị súng
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        
        // 3. Thêm BoxCollider2D Trigger để nhận diện người chơi đi vào vùng nhặt
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.2f, 1.2f);

        // 4. Thêm Rigidbody2D Kinematic để đảm bảo va chạm trigger 2D hoạt động ổn định
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 5. Thêm script GroundWeapon và truyền prefab vũ khí tương ứng vào
        GroundWeapon gw = go.AddComponent<GroundWeapon>();
        gw.weaponPrefab = weaponPrefab;

        return go;
    }

    private void Start()
    {
        // Tự động lấy Sprite từ Weapon Prefab để hiển thị trên đất
        if (weaponPrefab != null)
        {
            SpriteRenderer prefabSR = weaponPrefab.GetComponent<SpriteRenderer>();
            SpriteRenderer mySR = GetComponent<SpriteRenderer>();
            if (prefabSR != null && mySR != null)
            {
                mySR.sprite = prefabSR.sprite;
                mySR.sortingOrder = 8; // Đặt lên 8 để cao hơn lòng rương (6) và Player (5), chống bị đè lấp
            }

            // Tạo text hiển thị tên súng bay lơ lửng phía trên súng
            CreateNameTag();
        }
        else
        {
            Debug.LogWarning("[GroundWeapon] weaponPrefab bị null khi khởi tạo!");
            // Đặt tên mặc định phòng hờ
            GameObject textObj = new GameObject("NameTag");
            textObj.transform.SetParent(transform);
            nameTagTrans = textObj.transform;
            
            // Triệt tiêu ảnh hưởng của scale cha lên TextMesh để chữ không bị phóng to quá đà
            Vector3 parentScale = transform.localScale;
            textObj.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
            textObj.transform.localPosition = new Vector3(0f, 0.22f, 0f); // Sát súng hơn khi bình thường

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = "Vũ Khí Vô Danh";
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.07f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            
            MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 9;
        }
    }

    private void CreateNameTag()
    {
        if (weaponPrefab == null) return;

        GameObject textObj = new GameObject("NameTag");
        textObj.transform.SetParent(transform);
        nameTagTrans = textObj.transform;
        
        // Triệt tiêu ảnh hưởng của scale cha lên TextMesh để chữ không bị phóng to quá đà
        Vector3 parentScale = transform.localScale;
        textObj.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
        textObj.transform.localPosition = new Vector3(0f, 0.42f, 0f);

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = ""; // Mặc định ẩn hoàn toàn tên súng khi ở xa
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.07f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.green;

        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 9; // Đặt lên 9 để cao hơn súng (8) và đè lên trên tất cả
        }
    }

    private void Update()
    {
        if (isPlayerInside)
        {
            UpdatePromptText();
        }
    }

    private void UpdatePromptText()
    {
        TextMesh tm = GetComponentInChildren<TextMesh>();
        if (tm != null)
        {
            tm.color = Color.green;
            string sName = (weaponPrefab != null) ? weaponPrefab.name : "Vũ Khí";
            string cleanName = sName.Replace("(Clone)", "").Replace("_", " ");
            
            if (InputDeviceHelper.IsGamepadActive())
            {
                tm.text = cleanName + "\n(Nút B)";
            }
            else
            {
                tm.text = cleanName + "\n(Bấm E)";
            }

            if (nameTagTrans != null)
            {
                nameTagTrans.localPosition = new Vector3(0f, 0.42f, 0f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            WeaponManager wm = collision.GetComponent<WeaponManager>();
            if (wm != null)
            {
                if (!wm.nearbyWeapons.Contains(this))
                {
                    wm.nearbyWeapons.Add(this);
                    isPlayerInside = true;
                    UpdatePromptText();
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            WeaponManager wm = collision.GetComponent<WeaponManager>();
            if (wm != null)
            {
                wm.nearbyWeapons.Remove(this);
                isPlayerInside = false;
                
                // Ẩn tên súng hoàn toàn khi người chơi đi xa
                TextMesh tm = GetComponentInChildren<TextMesh>();
                if (tm != null)
                {
                    tm.text = "";
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Đảm bảo dọn sạch tham chiếu khỏi người chơi tránh lỗi NullReferenceException
        WeaponManager wm = Object.FindFirstObjectByType<WeaponManager>();
        if (wm != null && wm.nearbyWeapons.Contains(this))
        {
            wm.nearbyWeapons.Remove(this);
        }
    }
}
