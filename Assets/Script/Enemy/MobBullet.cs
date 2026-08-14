using UnityEngine;

/// <summary>
/// Script quản lý Đạn dành riêng cho Quái (Enemy Bullet).
/// Tự động nạp DB và CHỈ GÂY SÁT THƯƠNG CHO PLAYER (Bỏ qua Quái đồng đội).
/// </summary>
public class MobBullet : MonoBehaviour
{
    [Header("Thời gian tồn tại đạn (Bảng Inspector)")]
    public float lifeTime = 4f;

    [HideInInspector] public float speed = 10f;
    [HideInInspector] public int damage = 10;

    private bool hasHit = false;

    /// <summary>
    /// Nạp thông số đạn tự động từ DB BulletConfig
    /// </summary>
    public void InitFromDb(int bulletId)
    {
        if (bulletId <= 0) return;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            speed = config.flightSpeed > 0 ? config.flightSpeed : 10f;
            damage = config.damage > 0 ? config.damage : 10;
            Debug.Log($"[MobBullet] Nạp thành công bulletId={bulletId}: Speed={speed}, Damage={damage}");
        }
    }

    private void Start()
    {
        // Gán Layer EnemyBullet
        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            gameObject.layer = enemyBulletLayer;
        }

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        // 🛡️ BỎ QUA VẬT LÝ VÀ SÁT THƯƠNG NẾU VA CHẠM VỚI QUÁI ĐỒNG ĐỘI (Tag "Enemy" hoặc Layer "Enemy")
        if (collision.CompareTag("Enemy") || collision.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return; // Không nổ, không gây sát thương cho quái đồng đội
        }

        // 🎯 GÂY SÁT THƯƠNG NẾU BẮN TRÚNG LOCAL PLAYER HOẶC REMOTE PLAYER
        RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
        if (playerHealth == null) playerHealth = collision.GetComponentInParent<RookieHealth>();

        RemotePlayerController rpc = collision.GetComponent<RemotePlayerController>();
        if (rpc == null) rpc = collision.GetComponentInParent<RemotePlayerController>();

        if (playerHealth != null || rpc != null)
        {
            hasHit = true;
            if (playerHealth != null && !playerHealth.isDead)
            {
                playerHealth.TakeDamage(damage);
            }
            else if (rpc != null && !rpc.isDead && NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendPlayerDamaged(rpc.connectionId, damage);
                Debug.Log($"[MobBullet] Bắn trúng Remote Player {rpc.connectionId}, gửi {damage} sát thương qua mạng.");
            }

            Destroy(gameObject);
            return;
        }

        // 🧱 NỔ/TỰ HỦY NẾU BẮN TRÚNG TƯỜNG / RÀO CHẮN
        if (collision.CompareTag("Obstacle") || collision.CompareTag("Door"))
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}
