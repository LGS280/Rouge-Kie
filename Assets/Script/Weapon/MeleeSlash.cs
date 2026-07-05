using UnityEngine;

public class MeleeSlash : MonoBehaviour
{
    public float delayTime = 0.1f;
    [HideInInspector] public float damage;
    [HideInInspector] public float critChance;
    public float critMultiplier = 2f;

    public void InitFromDb(int bulletId)
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            damage = config.damage;
            critChance = config.critRate; // Script cận chiến của bạn dùng [Range(0,1)] nên giữ nguyên hệ thập phân
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
            bool isCrit = false;

            if (Random.value <= critChance)
            {
                finalDamage = damage * critMultiplier;
                isCrit = true;
            }

            MobHealth enemyHealth = collision.GetComponent<MobHealth>();
            if (enemyHealth != null)
            {
                if (!gameObject.name.EndsWith("_Remote"))
                {
                    enemyHealth.TakeDamage(Mathf.RoundToInt(finalDamage));
                }
            }
        }
    }
}