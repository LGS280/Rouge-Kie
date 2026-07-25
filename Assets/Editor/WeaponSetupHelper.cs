#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class WeaponSetupHelper
{
    [MenuItem("Tools/Setup Weapon Prefabs")]
    public static void SetupPrefabs()
    {
        string folderPath = "Assets/Prefab/Weapons";
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"Thư mục {folderPath} không tồn tại!");
            return;
        }

        string[] files = Directory.GetFiles(folderPath, "*.prefab");
        System.Collections.Generic.List<GameObject> weaponList = new System.Collections.Generic.List<GameObject>();

        foreach (string file in files)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
            if (prefab == null) continue;

            // Bỏ qua file GroundWeapon nếu nó lỡ tồn tại
            if (prefab.name == "GroundWeapon") continue;

            bool isDirty = false;

            // 1. Kiểm tra WeaponInfo
            WeaponInfo info = prefab.GetComponent<WeaponInfo>();
            if (info != null)
            {
                info.weaponPrefab = prefab;
                
                // Tự động tìm và gán âm thanh bắn súng từ thư mục cũ Assets/Audio/SFX/
                string sfxFolder = "Assets/Audio/SFX";
                string soundName = "";
                if (prefab.name.Contains("AK47")) soundName = "rifle";
                else if (prefab.name.Contains("Desert_Eagle")) soundName = "DE";
                else if (prefab.name.Contains("M249")) soundName = "rifle";
                else if (prefab.name.Contains("Snipe")) soundName = "rifle";

                if (!string.IsNullOrEmpty(soundName))
                {
                    string mp3Path = $"{sfxFolder}/{soundName}.mp3";
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(mp3Path);
                    if (clip == null)
                    {
                        string wavPath = $"{sfxFolder}/{soundName}.wav";
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath);
                    }
                    if (clip != null)
                    {
                        info.shootSoundClip = clip;
                        Debug.Log($"Đã tự động gán âm thanh '{soundName}' cho súng: {prefab.name}");
                    }
                }

                isDirty = true;
                weaponList.Add(prefab);
            }

            // 2. Kiểm tra WeaponLaser
            WeaponLaser laser = prefab.GetComponent<WeaponLaser>();
            if (laser != null)
            {
                laser.weaponPrefab = prefab;
                isDirty = true;
                weaponList.Add(prefab);
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(prefab);
                Debug.Log($"Đã cấu hình tự tham chiếu Prefab cho súng: {prefab.name}");
            }
        }

        // 3. Tự động tìm và cấu hình danh sách súng cho WeaponChest.prefab
        string chestPath = "Assets/Prefab/Map/WeaponChest.prefab";
        GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chestPath);
        if (chestPrefab != null)
        {
            WeaponChest chest = chestPrefab.GetComponent<WeaponChest>();
            if (chest != null)
            {
                chest.weaponPrefabs = weaponList.ToArray();
                EditorUtility.SetDirty(chestPrefab);
                Debug.Log($"Đã tự động nạp {weaponList.Count} loại súng vào mảng Weapon Prefabs của WeaponChest Prefab!");
            }
        }

        // 4. Tự động nạp danh sách súng vào WeaponManager của Rookie.prefab làm dữ liệu đối chiếu dự phòng
        string rookiePath = "Assets/Prefab/Rookie/Rookie.prefab";
        GameObject rookiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rookiePath);
        if (rookiePrefab != null)
        {
            WeaponManager wm = rookiePrefab.GetComponent<WeaponManager>();
            if (wm != null)
            {
                wm.allWeaponPrefabs = weaponList.ToArray();
                EditorUtility.SetDirty(rookiePrefab);
                Debug.Log($"Đã tự động nạp danh sách {weaponList.Count} súng vào WeaponManager của Player (Rookie) Prefab!");
            }
        }

        // 5. ĐỒNG BỘ CHO SCENE ĐANG MỞ: Cập nhật trực tiếp các đối tượng WeaponManager và WeaponChest trong Scene đang thiết lập
        WeaponManager[] sceneManagers = Object.FindObjectsByType<WeaponManager>(FindObjectsSortMode.None);
        foreach (var wm in sceneManagers)
        {
            wm.allWeaponPrefabs = weaponList.ToArray();
            EditorUtility.SetDirty(wm);
            Debug.Log($"Đã tự động đồng bộ danh sách súng vào Player (WeaponManager) trong Scene!");
        }

        WeaponChest[] sceneChests = Object.FindObjectsByType<WeaponChest>(FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            chest.weaponPrefabs = weaponList.ToArray();
            EditorUtility.SetDirty(chest);
            Debug.Log($"Đã tự động đồng bộ danh sách súng vào WeaponChest trong Scene!");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== [Tools] Đã hoàn thành cấu hình tự động toàn bộ vũ khí và rương! ===");
    }

    [MenuItem("Tools/Build Weapon Chest Prefab")]
    public static void BuildWeaponChestPrefab()
    {
        // 1. Tạo GameObject cha
        GameObject chestParent = new GameObject("WeaponChest");
        WeaponChest weaponChest = chestParent.AddComponent<WeaponChest>();
        
        // Thêm BoxCollider2D và thiết lập làm Trigger va chạm nhặt súng
        BoxCollider2D collider = chestParent.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.2f, 1.0f); // Kích thước va chạm

        // Thêm Rigidbody2D ở chế độ Kinematic để nhận diện va chạm 2D Trigger
        Rigidbody2D rb = chestParent.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 2. Load các Sprite đã cắt (Multiple sprites) từ sheet rương vũ khí
        string bodyPath = "Assets/Item/Chest/Weapon_Chest/Body_Weapon_Chest.png";
        string topPath = "Assets/Item/Chest/Weapon_Chest/Top_Weapon_Chest.png";
        string insidePath = "Assets/Item/Chest/Weapon_Chest/Inside_Weapon_Chest.png";

        Sprite bodySprite = LoadSpriteFromSheet(bodyPath, "Body_Weapon_Chest_0");
        Sprite topSprite = LoadSpriteFromSheet(topPath, "Top_Weapon_Chest_0");
        Sprite insideSprite = LoadSpriteFromSheet(insidePath, "Inside_Weapon_Chest_0");

        if (bodySprite == null || topSprite == null || insideSprite == null)
        {
            Debug.LogError("Không tìm thấy các Sprite cắt sẵn (Body_Weapon_Chest_0, Top_Weapon_Chest_0, Inside_Weapon_Chest_0). Vui lòng kiểm tra lại import mode.");
            Object.DestroyImmediate(chestParent);
            return;
        }

        // 3. Tạo GameObject con: Body (Thân rương)
        GameObject bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(chestParent.transform);
        // Dịch chuyển thân rương xuống -0.09375f (tương đương -1.5 pixel) để bù trừ phần bóng 3px ở đáy (tổng chiều cao từ 8px lên 11px), giúp nắp rương giữ nguyên 0.5f khớp khít
        bodyObj.transform.localPosition = new Vector3(0f, -0.09375f, 0f);
        SpriteRenderer bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        bodyRenderer.sortingOrder = 5;

        // 4. Tạo GameObject con: Top (Nắp rương khi đóng)
        GameObject topObj = new GameObject("Top");
        topObj.transform.SetParent(chestParent.transform);
        // Định vị nắp rương (0.5625f unit tương đương dịch chuyển 9 pixel để đè chồng đúng 1 hàng của thân rương)
        topObj.transform.localPosition = new Vector3(0f, 0.5625f, 0f);
        SpriteRenderer topRenderer = topObj.AddComponent<SpriteRenderer>();
        topRenderer.sprite = topSprite;
        topRenderer.sortingOrder = 6;

        // 5. Tạo GameObject con: Inside (Lòng rương khi mở)
        GameObject insideObj = new GameObject("Inside");
        insideObj.transform.SetParent(chestParent.transform);
        // Định vị lòng rương trùng khớp
        insideObj.transform.localPosition = new Vector3(0f, 0.5625f, 0f);
        SpriteRenderer insideRenderer = insideObj.AddComponent<SpriteRenderer>();
        insideRenderer.sprite = insideSprite;
        insideRenderer.sortingOrder = 6;
        insideObj.SetActive(false); // Ẩn ban đầu

        // 6. Gán các trường tham chiếu vào Script
        weaponChest.body = bodyObj;
        weaponChest.top = topObj;
        weaponChest.inside = insideObj;

        // 7. Tạo thư mục Prefab/Map nếu chưa tồn tại
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/Map"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefab");
            }
            AssetDatabase.CreateFolder("Assets/Prefab", "Map");
        }

        // 8. Xuất GameObject thành Prefab
        string localPath = "Assets/Prefab/Map/WeaponChest.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(chestParent, localPath, InteractionMode.UserAction);
        
        Object.DestroyImmediate(chestParent);

        Debug.Log("Đã dựng thành công Prefab Rương Vũ Khí (WeaponChest) tại: " + localPath);
        
        // Gọi SetupPrefabs luôn để nạp đầy đủ súng vào rương mới dựng xong
        SetupPrefabs();
        
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Build Missile Launcher Prefab")]
    public static void BuildMissileLauncherPrefab()
    {
        // 1. Đảm bảo thư mục lưu trữ Prefabs tồn tại
        if (!Directory.Exists("Assets/Prefab/Bullet")) Directory.CreateDirectory("Assets/Prefab/Bullet");
        if (!Directory.Exists("Assets/Prefab/Weapons")) Directory.CreateDirectory("Assets/Prefab/Weapons");

        // 2. Dựng Prefab Đạn Tên Lửa (Bullet_Missile / Bullet_Missle)
        string folderDir = "Assets/Weapons/Player_Weapon/Missile_Launcher";
        if (!Directory.Exists(folderDir)) folderDir = "Assets/Weapons/Player_Weapon/Missle_Launcher";

        string missleSpritePath = $"{folderDir}/Missile.png";
        if (!File.Exists(missleSpritePath)) missleSpritePath = $"{folderDir}/Missle.png";

        string fireTailSpritePath = $"{folderDir}/Fire_Tail.png";

        Sprite missleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(missleSpritePath);
        Sprite fireTailSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fireTailSpritePath);

        if (missleSprite == null)
        {
            Debug.LogError($"[WeaponSetupHelper] Không tìm thấy Sprite tên lửa tại: {missleSpritePath}");
            return;
        }

        GameObject bulletObj = new GameObject("Missile");
        
        // Sprite Renderer tên lửa chính
        SpriteRenderer bulletSr = bulletObj.AddComponent<SpriteRenderer>();
        bulletSr.sprite = missleSprite;
        bulletSr.sortingOrder = 12;

        // BoxCollider2D (Trigger)
        BoxCollider2D col = bulletObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(missleSprite.rect.width / missleSprite.pixelsPerUnit, missleSprite.rect.height / missleSprite.pixelsPerUnit);

        // Rigidbody2D (Kinematic)
        Rigidbody2D rb = bulletObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Script MissleBullet (Đuổi quái + Đuôi lửa)
        MissileBullet missleScript = bulletObj.AddComponent<MissileBullet>();
        missleScript.detectRadius = 10.0f;
        missleScript.turnSpeed = 260.0f;
        missleScript.initialDirectTime = 0.05f;
        missleScript.hasFireTail = true;

        // Tạo GameObject con: Fire_Tail (Loa lửa ngay đít tên lửa)
        if (fireTailSprite != null)
        {
            GameObject tailObj = new GameObject("Fire_Tail");
            tailObj.transform.SetParent(bulletObj.transform);
            
            // Định vị loa lửa nằm ở đít tên lửa
            float missileWidth = missleSprite.rect.width / missleSprite.pixelsPerUnit;
            float tailWidth = fireTailSprite.rect.width / fireTailSprite.pixelsPerUnit;
            tailObj.transform.localPosition = new Vector3(-(missileWidth / 2f + tailWidth * 0.25f), 0f, 0f);

            SpriteRenderer tailSr = tailObj.AddComponent<SpriteRenderer>();
            tailSr.sprite = fireTailSprite;
            tailSr.sortingOrder = 11; // Nằm sau quả tên lửa

            missleScript.fireTailObject = tailObj;
        }

        // Bổ sung TrailRenderer: Dải vệt khói lửa uốn lượn dài kéo theo sau đạn (chuẩn phong cách Soul Knight)
        TrailRenderer trail = bulletObj.AddComponent<TrailRenderer>();
        trail.time = 0.35f;             // Độ dài dải lửa kéo vệt sau đạn
        trail.startWidth = 0.14f;       // Độ rộng đầu dải lửa (ôm khít đít tên lửa)
        trail.endWidth = 0.01f;         // Độ rộng đuôi vệt khói lửa (vuốt nhọn dần)
        trail.minVertexDistance = 0.05f;// Mượt mà theo đường cong
        trail.sortingOrder = 10;        // Vẽ dưới tên lửa và loa lửa

        // Dải màu lửa chuyển tiếp từ Vàng rực -> Cam lửa -> Đỏ nhạt mờ dần
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(1.0f, 0.75f, 0.2f), 0.0f), 
                new GradientColorKey(new Color(1.0f, 0.35f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.8f, 0.15f, 0.05f), 1.0f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0.95f, 0.0f), 
                new GradientAlphaKey(0.5f, 0.6f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        trail.colorGradient = gradient;

        Material spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (spriteMat != null) trail.material = spriteMat;

        // Lưu Prefab Đạn (Tên Missile.prefab chuẩn 1-1 theo DB "prefabName": "Missile")
        string bulletPrefabPath = "Assets/Prefab/Bullet/Missile.prefab";
        GameObject bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletObj, bulletPrefabPath);
        Object.DestroyImmediate(bulletObj);
        Debug.Log($"[WeaponSetupHelper] Đã dựng thành công Prefab đạn tên lửa tại: {bulletPrefabPath}");

        // 3. Dựng Prefab Khẩu Súng (Missile_Launcher.prefab)
        string launcherSpritePath = $"{folderDir}/Missile_Launcher.png";
        if (!File.Exists(launcherSpritePath)) launcherSpritePath = $"{folderDir}/Missle_Launcher.png";
        Sprite launcherSprite = AssetDatabase.LoadAssetAtPath<Sprite>(launcherSpritePath);

        if (launcherSprite == null)
        {
            Debug.LogError($"[WeaponSetupHelper] Không tìm thấy Sprite súng tên lửa tại: {launcherSpritePath}");
            return;
        }

        GameObject launcherObj = new GameObject("Missile_Launcher");
        SpriteRenderer launcherSr = launcherObj.AddComponent<SpriteRenderer>();
        launcherSr.sprite = launcherSprite;
        launcherSr.sortingOrder = 11;

        // Script WeaponInfo
        WeaponInfo info = launcherObj.AddComponent<WeaponInfo>();
        info.weaponPrefab = launcherObj;
        info.bulletPrefab = bulletPrefab;

        // Tạo FirePoint ở đầu nòng súng
        GameObject firePointObj = new GameObject("FirePoint");
        firePointObj.transform.SetParent(launcherObj.transform);
        float launcherWidth = launcherSprite.rect.width / launcherSprite.pixelsPerUnit;
        firePointObj.transform.localPosition = new Vector3(launcherWidth / 2f + 0.1f, 0.05f, 0f);
        info.firePoint = firePointObj.transform;

        // Lưu Prefab Súng chuẩn tên Missile_Launcher.prefab theo DB
        string launcherPrefabPath = "Assets/Prefab/Weapons/Missile_Launcher.prefab";
        GameObject launcherPrefab = PrefabUtility.SaveAsPrefabAsset(launcherObj, launcherPrefabPath);
        
        // Cập nhật lại tự tham chiếu cho Prefab súng vừa tạo
        WeaponInfo prefabInfo = launcherPrefab.GetComponent<WeaponInfo>();
        if (prefabInfo != null)
        {
            prefabInfo.weaponPrefab = launcherPrefab;
            EditorUtility.SetDirty(launcherPrefab);
        }

        Object.DestroyImmediate(launcherObj);
        Debug.Log($"[WeaponSetupHelper] Đã dựng thành công Prefab khẩu Missile Launcher tại: {launcherPrefabPath}");

        // Gọi đồng bộ lại toàn bộ súng
        SetupPrefabs();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Make Selected Prefab A Missile Bullet")]
    public static void MakeSelectedMissileBullet()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null)
        {
            Debug.LogError("Vui lòng chọn 1 Prefab đạn hoặc GameObject đạn trong Unity!");
            return;
        }

        // Thêm BoxCollider2D Trigger
        BoxCollider2D col = selectedObj.GetComponent<BoxCollider2D>();
        if (col == null) col = selectedObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // Thêm Rigidbody2D Kinematic
        Rigidbody2D rb = selectedObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = selectedObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Thêm Script MissileBullet (Tự đuổi quái)
        MissileBullet missleScript = selectedObj.GetComponent<MissileBullet>();
        if (missleScript == null) missleScript = selectedObj.AddComponent<MissileBullet>();
        missleScript.detectRadius = 8.0f;
        missleScript.turnSpeed = 360.0f;

        Debug.Log($"[WeaponSetupHelper] Đã cấu hình thành công đạn đuổi quái (MissileBullet) cho: {selectedObj.name}");
    }

    private static Sprite LoadSpriteFromSheet(string path, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite && sprite.name == spriteName)
            {
                return sprite;
            }
        }
        return null;
    }
}
#endif
