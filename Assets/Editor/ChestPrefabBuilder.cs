#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ChestPrefabBuilder
{
    [MenuItem("Tools/Build Chest Prefab")]
    public static void BuildPrefab()
    {
        // 1. Tạo GameObject cha
        GameObject chestParent = new GameObject("NormalChest");
        RewardChest rewardChest = chestParent.AddComponent<RewardChest>();
        
        // Thêm BoxCollider2D và thiết lập làm Trigger tự mở
        BoxCollider2D collider = chestParent.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.2f, 1.0f); // Kích thước vùng va chạm vừa phải

        // Thêm Rigidbody2D ở chế độ Kinematic để nhận diện va chạm 2D Trigger chuẩn xác
        Rigidbody2D rb = chestParent.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 2. Load các Sprite đã cắt (Multiple sprites) từ sheet
        string bodyPath = "Assets/Item/Chest/Normal_Chest/Body_ChestV1.png";
        string topPath = "Assets/Item/Chest/Normal_Chest/Top_Chest1.png";
        string insidePath = "Assets/Item/Chest/Normal_Chest/Inside_Chest.png";

        Sprite bodySprite = LoadSpriteFromSheet(bodyPath, "Body_ChestV1_0");
        Sprite topSprite = LoadSpriteFromSheet(topPath, "Top_Chest1_0");
        Sprite insideSprite = LoadSpriteFromSheet(insidePath, "Inside_Chest_0");

        if (bodySprite == null || topSprite == null || insideSprite == null)
        {
            Debug.LogError("Không tìm thấy các Sprite cắt sẵn (Body_ChestV1_0, Top_Chest1_0, Inside_Chest_0) trong thư mục Assets/Item/Chest/Normal_Chest/. Vui lòng kiểm tra lại thiết lập Sprite Mode là Multiple và đã Slice Sprite.");
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
        bodyRenderer.sortingOrder = 5; // Layer hiển thị phía trên map

        // 4. Tạo GameObject con: Top (Nắp rương khi đóng)
        GameObject topObj = new GameObject("Top");
        topObj.transform.SetParent(chestParent.transform);
        // Định vị nắp rương nằm ngay phía trên thân rương (0.5625f unit - khớp dịch chuyển 9 pixel để đè chồng đúng 1 hàng của thân rương)
        topObj.transform.localPosition = new Vector3(0f, 0.5625f, 0f);
        SpriteRenderer topRenderer = topObj.AddComponent<SpriteRenderer>();
        topRenderer.sprite = topSprite;
        topRenderer.sortingOrder = 6; // Đè lên trên thân rương

        // 5. Tạo GameObject con: Inside (Lòng rương khi mở ra)
        GameObject insideObj = new GameObject("Inside");
        insideObj.transform.SetParent(chestParent.transform);
        // Định vị lòng rương trùng khớp
        insideObj.transform.localPosition = new Vector3(0f, 0.5625f, 0f);
        SpriteRenderer insideRenderer = insideObj.AddComponent<SpriteRenderer>();
        insideRenderer.sprite = insideSprite;
        insideRenderer.sortingOrder = 6;
        insideObj.SetActive(false); // Ban đầu ẩn đi

        // 6. Gán các trường tham chiếu vào Script cha
        rewardChest.body = bodyObj;
        rewardChest.top = topObj;
        rewardChest.inside = insideObj;

        // 7. Tạo thư mục Prefab/Map nếu chưa tồn tại
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/Map"))
        {
            // Kiểm tra xem Assets/Prefab đã có chưa
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefab");
            }
            AssetDatabase.CreateFolder("Assets/Prefab", "Map");
        }

        // 8. Xuất GameObject thành Prefab
        string localPath = "Assets/Prefab/Map/NormalChest.prefab";
        // Đảm bảo ghi đè trực tiếp để cập nhật
        PrefabUtility.SaveAsPrefabAssetAndConnect(chestParent, localPath, InteractionMode.UserAction);
        
        // Hủy Object tạm trong Scene hiện tại
        Object.DestroyImmediate(chestParent);

        Debug.Log("Đã dựng thành công Prefab Rương Thưởng (NormalChest) tại: " + localPath);
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
