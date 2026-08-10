using UnityEngine;

/// <summary>
/// Script quản lý hiệu ứng đâm/chém cận chiến dành riêng cho Quái (Mob Melee Slash).
/// Tự động nạp sát thương từ DB BulletConfig (Không hiện trên Inspector).
/// CHỈ GÂY SÁT THƯƠNG CHO PLAYER và BỎ QUA QUÁI ĐỒNG ĐỘI.
/// </summary>
public class MobMeleeSlash : MonoBehaviour
{
    [Header("Thời gian tồn tại vệt sáng (Life Time)")]
    public float lifeTime = 0.15f;

    [HideInInspector] public int damage = 10;

    private bool hasHitPlayer = false;

    /// <summary>
    /// Nạp sát thương từ DB BulletConfig
    /// </summary>
    public void InitFromDb(int bulletId)
    {
        if (bulletId <= 0) return;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            damage = config.damage > 0 ? config.damage : 10;
            Debug.Log($"[MobMeleeSlash] Nạp sát thương cận chiến từ DB bulletId={bulletId}: Damage={damage}");
        }
    }

    private void Start()
    {
        // Gán Layer EnemyBullet để phân loại vật lý
        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            gameObject.layer = enemyBulletLayer;
        }

        // Tự động biến mất sau thời gian lifeTime (Lifetime)
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 🛡️ BỎ QUA NẾU VA CHẠM VỚI QUÁI ĐỒNG ĐỘI
        if (collision.CompareTag("Enemy") || collision.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        // 🎯 GÂY SÁT THƯƠNG NẾU ĐÂM TRÚNG PLAYER
        if (!hasHitPlayer && (collision.CompareTag("Player") || collision.gameObject.layer == LayerMask.NameToLayer("Player")))
        {
            hasHitPlayer = true;
            RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
            if (playerHealth == null) playerHealth = collision.GetComponentInParent<RookieHealth>();

            if (playerHealth != null && !playerHealth.isDead)
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }
}
