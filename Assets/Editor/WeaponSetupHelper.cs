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

    [MenuItem("Tools/Make Explosion Prefab From Selected Sprites")]
    public static void MakeExplosionPrefabFromSelectedSprites()
    {
        Object[] selectedObjects = Selection.objects;
        System.Collections.Generic.List<Sprite> spriteList = new System.Collections.Generic.List<Sprite>();

        foreach (Object obj in selectedObjects)
        {
            if (obj is Sprite sp)
            {
                spriteList.Add(sp);
            }
            else if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is Sprite subSp)
                    {
                        spriteList.Add(subSp);
                    }
                }
            }
        }

        if (spriteList.Count == 0)
        {
            Debug.LogError("[WeaponSetupHelper] ❌ Vui lòng chọn các file ảnh Sprite (hoặc Sprite Sheet) vụ nổ trong cửa sổ Project!");
            return;
        }

        // Sắp xếp các sprite theo thứ tự tên (ví dụ: _1 -> _2 -> _3 -> _4 -> _5)
        spriteList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

        // Tự động đặt tên cho Prefab từ tên của Sprite
        string firstName = spriteList[0].name;
        string prefabName = firstName.Replace("_1", "").Replace("-1", "").Replace("_0", "").Trim();
        if (string.IsNullOrEmpty(prefabName)) prefabName = "Custom_Explosion_Effect";
        if (!prefabName.EndsWith("_Effect")) prefabName += "_Effect";

        if (!Directory.Exists("Assets/Prefab/Effects")) Directory.CreateDirectory("Assets/Prefab/Effects");

        GameObject expObj = new GameObject(prefabName);
        SpriteRenderer sr = expObj.AddComponent<SpriteRenderer>();
        sr.sprite = spriteList[0];
        sr.sortingOrder = 15;

        RogueKie.Effects.AutoDestroyEffect effectScript = expObj.AddComponent<RogueKie.Effects.AutoDestroyEffect>();
        effectScript.animationFrames = spriteList.ToArray();
        effectScript.frameDuration = 0.1f;

        string prefabPath = $"Assets/Prefab/Effects/{prefabName}.prefab";
        GameObject explosionPrefab = PrefabUtility.SaveAsPrefabAsset(expObj, prefabPath);
        Object.DestroyImmediate(expObj);

        Debug.Log($"[WeaponSetupHelper] 🎉 Đã tạo thành công Prefab Vụ Nổ mới từ {spriteList.Count} ảnh tại: {prefabPath}");
        AssetDatabase.Refresh();
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
