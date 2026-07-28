using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardChest : MonoBehaviour
{
    [Header("Visual Components")]
    public GameObject body;      // Body (thân rương)
    public GameObject top;       // Top (nắp rương)
    public GameObject inside;    // Inside (lòng rương hiển thị khi mở)

    [Header("Loot Prefabs")]
    public GameObject coinPrefab;     // Prefab vàng để sinh ra
    public GameObject manaPrefab;     // Prefab mana để sinh ra

    [Header("Loot Settings")]
    public int minGold = 3;
    public int maxGold = 4;
    public int minMana = 3;
    public int maxMana = 4;
    [Range(0f, 1f)]
    public float weaponDropChance = 0.15f; // 15% tỷ lệ rơi súng

    private bool isOpened = false;

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

        // 2. Ép cứng Sorting Order để rương luôn vẽ trên sàn/xác quái và lòng rương đè lên thân gỗ
        EnforceSortingOrders();
    }

    private void EnforceSortingOrders()
    {
        if (body != null)
        {
            SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 5; // Thân rương gỗ hiển thị đè lên sàn (0) và xác quái (1-2)
        }

        if (top != null)
        {
            SpriteRenderer sr = top.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6; // Nắp rương đè lên thân gỗ
        }

        if (inside != null)
        {
            SpriteRenderer sr = inside.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 6; // Lòng rương đè lên thân gỗ khi mở
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOpened) return;

        // Người chơi chạm vào rương thì rương tự động mở
        if (collision.CompareTag("Player"))
        {
            // Kiểm tra xem có phải là người chơi chính (local player) hay không
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                OpenChest();
            }
        }
    }

    public void OpenChest()
    {
        if (isOpened) return;

        isOpened = true;

        // Cập nhật trạng thái hiển thị của rương sang trạng thái mở
        if (body != null) body.SetActive(true);     // Luôn hiển thị thân rương gỗ bên dưới
        if (inside != null) inside.SetActive(true); // Hiện lòng rương mở bên trên

        // Chạy animation nắp rương di chuyển nhẹ về phía sau rồi biến mất
        if (top != null)
        {
            StartCoroutine(AnimateTopChestOpen(top));
        }

        // Sinh phần thưởng
        SpawnRewards();
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

    private void SpawnRewards()
    {
        float difficultyMultiplier = 1f;

        // TODO: Nếu sau này cần lấy hệ số nhân độ khó của tầng, có thể lấy thông qua GameDataManager/LevelConfig
        // difficultyMultiplier = GameDataManager.Instance.CurrentDifficultyMultiplier;

        int goldToSpawn = Mathf.RoundToInt(Random.Range(minGold, maxGold + 1) * difficultyMultiplier);
        int manaToSpawn = Mathf.RoundToInt(Random.Range(minMana, maxMana + 1) * difficultyMultiplier);

        int totalItems = goldToSpawn + manaToSpawn;
        int index = 0;

        // Sinh Vàng
        for (int i = 0; i < goldToSpawn; i++)
        {
            SpawnLootItem(coinPrefab, index, totalItems);
            index++;
        }

        // Sinh Mana
        for (int i = 0; i < manaToSpawn; i++)
        {
            SpawnLootItem(manaPrefab, index, totalItems);
            index++;
        }

        // Kiểm tra tỷ lệ rơi vũ khí ngẫu nhiên
        if (Random.value <= weaponDropChance)
        {
            SpawnWeaponReward();
        }
    }

    private void SpawnLootItem(GameObject prefab, int index, int totalItems)
    {
        if (prefab == null) return;

        // Sinh vật phẩm ngay tâm rương
        Vector3 spawnPos = transform.position;
        GameObject lootObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        Rigidbody2D rb = lootObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Thiết lập ở dạng Dynamic để bay vật lý
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            // Lực cản không khí lớn để phanh giảm tốc cực nhanh và dừng im trên sàn
            rb.linearDamping = 6.0f;
            rb.linearVelocity = Vector2.zero;

            // Tính góc bay tỏa đều nhau dạng vòng hoa/ngôi sao quanh rương
            float angle = index * (360f / Mathf.Max(1, totalItems)) + Random.Range(-15f, 15f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            // Tác động lực văng ra ngoài
            float force = Random.Range(3.5f, 5.0f);
            rb.AddForce(direction * force, ForceMode2D.Impulse);
        }
    }

    private void SpawnWeaponReward()
    {
        // TODO: Sẽ tích hợp thêm khi làm cơ chế nhặt súng rơi trên đất
        Debug.Log("May mắn rơi ra vũ khí từ rương!");
    }
}
