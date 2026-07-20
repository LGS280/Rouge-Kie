using UnityEngine;

public class MeleeSlash : MonoBehaviour
{
    public float delayTime = 0.1f;
    [HideInInspector] public float damage;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critMultiplier;

    public void InitFromDb(int bulletId)
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            damage = config.damage;
            critChance = config.critRate; // Script cận chiến của bạn dùng [Range(0,1)] nên giữ nguyên hệ thập phân
            critMultiplier = config.critMultiplier;
        }
    }

    void Start()
    {
        Destroy(gameObject, delayTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            float finalDamage = damage;
            if (PlayerBuffManager.Instance != null)
            {
                finalDamage *= PlayerBuffManager.Instance.damageMultiplier;
            }

            bool isCrit = false;

            float finalCritChance = critChance;
            if (PlayerBuffManager.Instance != null)
            {
                finalCritChance += PlayerBuffManager.Instance.critChanceOffset / 100f; // Chia 100 vì critChance ở dạng 0-1
            }

            if (Random.value <= finalCritChance)
            {
                finalDamage *= critMultiplier; // Nhân critMultiplier của cận chiến
                isCrit = true;
            }

            MobHealth enemyHealth = collision.GetComponent<MobHealth>();
            if (enemyHealth != null)
            {
                if (!gameObject.name.EndsWith("_Remote"))
                {
                    enemyHealth.TakeDamage(Mathf.RoundToInt(finalDamage), isCrit); // Truyền isCrit để hiển thị màu text crit nếu cần
                }
            }
        }
    }
}