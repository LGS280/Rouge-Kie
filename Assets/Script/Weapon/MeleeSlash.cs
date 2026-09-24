using UnityEngine;

public class MeleeSlash : MonoBehaviour
{
    [Header("life time")]
    public float lifeTime = 0.1f;
    [HideInInspector] public float damage;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critMultiplier;

    public void InitFromDb(int bulletId)
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            damage = config.damage;
            critChance = config.critRate;
            critMultiplier = config.critMultiplier;
        }
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
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
                finalCritChance += PlayerBuffManager.Instance.critChanceOffset / 100f;
            }

            if (Random.value <= finalCritChance)
            {
                finalDamage *= critMultiplier;
                isCrit = true;
            }

            MobHealth enemyHealth = collision.GetComponent<MobHealth>();
            if (enemyHealth != null)
            {
                if (!gameObject.name.EndsWith("_Remote"))
                {
                    enemyHealth.TakeDamage(Mathf.RoundToInt(finalDamage), isCrit);
                }
            }
        }
    }
}
