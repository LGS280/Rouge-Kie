using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LootType
{
    Coin,
    Mana
}

public class LootItem : MonoBehaviour
{
    [Header("Loot Configuration")]
    public LootType lootType = LootType.Coin;
    public int value = 1; // Giá trị cộng thêm (ví dụ: 1 coin hoặc 10 mana)

    [Header("Magnetic Attraction (Hút Nam Châm)")]
    public float detectRange = 4.0f;     // Khoảng cách bắt đầu hút người chơi
    public float baseAttractSpeed = 3.0f; // Tốc độ hút ban đầu
    public float acceleration = 5.0f;    // Gia tốc hút (càng gần bay càng nhanh)
    public float attractDelay = 0.5f;     // Trễ 0.5s sau khi sinh ra mới bắt đầu hút (để bay văng ra tự nhiên)
    public float pickupDelay = 0.5f;      // Trễ 0.5s trước khi cho phép người chơi nhặt (để tạo vụ nổ quà nhìn rõ hơn)

    private Transform playerTransform;
    private Rigidbody2D rb;
    private float spawnTime;
    private float currentSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time;
        currentSpeed = baseAttractSpeed;
    }

    private void Update()
    {
        // Chờ hết thời gian trễ văng ra ngoài
        if (Time.time - spawnTime < attractDelay) return;

        FindLocalPlayer();

        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            // Nếu người chơi nằm trong bán kính hút
            if (distance <= detectRange)
            {
                // Vô hiệu hóa lực cản vật lý để bay mượt mà về phía player
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic; // Chuyển sang Kinematic để tự điều khiển vị trí
                }

                // Gia tăng tốc độ hút khi lại gần
                currentSpeed += acceleration * Time.deltaTime;

                // Di chuyển tịnh tiến về phía player
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, currentSpeed * Time.deltaTime);
            }
        }
    }

    private void FindLocalPlayer()
    {
        if (playerTransform == null)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryCollect(collision);
    }

    private void TryCollect(Collider2D collision)
    {
        // Chỉ cho phép nhặt sau khi hết thời gian trễ văng ra ngoài để người chơi nhìn rõ quà
        if (Time.time - spawnTime < pickupDelay) return;

        // Khi chạm vào Player thì nhận thưởng
        if (collision.CompareTag("Player"))
        {
            RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
            if (playerHealth == null)
            {
                playerHealth = collision.GetComponentInParent<RookieHealth>();
            }

            if (playerHealth != null && !playerHealth.isDead)
            {
                CollectLoot(playerHealth);
            }
        }
    }

    private void CollectLoot(RookieHealth playerHealth)
    {
        if (lootType == LootType.Coin)
        {
            // Cộng vàng vào hệ thống theo dõi trận đấu (CurrencyEarned) đúng lượng value nhặt được
            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.AddCurrency(value);
            }
        }
        else if (lootType == LootType.Mana)
        {
            // Hồi phục mana cho người chơi
            playerHealth.RestoreMana(value);
        }

        // Tạo hiệu ứng nổ hạt bụi nhẹ tại đây nếu cần thiết

        // Hủy vật phẩm sau khi nhặt
        Destroy(gameObject);
    }
}
