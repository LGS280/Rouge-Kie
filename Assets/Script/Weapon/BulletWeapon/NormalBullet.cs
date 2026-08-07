using UnityEngine;

public class NormalBullet : MonoBehaviour
{
    [HideInInspector] public float speed;
    [HideInInspector] public float baseDamage;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critMultiplier;

    public float lifeTime = 3f;

    [Header("Impact Effect")]
    public GameObject explosionEffectPrefab; // Prefab hiệu ứng vụ nổ (tùy chọn)

    // Hàm nhận dữ liệu từ DB truyền qua
    public void InitFromDb(int bulletId)
    {
        if (bulletId <= 0) return;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            speed = config.flightSpeed;
            baseDamage = config.damage;
            critChance = config.critRate * 100f; // Đổi thập phân (0.2) thành phần trăm (20%)
            critMultiplier = config.critMultiplier;
            Debug.Log($"[NormalBullet] Nạp thành công bulletId={bulletId}: Speed={speed}, Damage={baseDamage}");
        }
    }

    protected virtual void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    protected virtual void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle") || collision.CompareTag("Enemy") || collision.CompareTag("Door"))
        {
            if (collision.CompareTag("Enemy"))
            {
                CalculateAndApplyDamage(collision);
            }

            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }

    private void CalculateAndApplyDamage(Collider2D collision)
    {
        float finalDamage = baseDamage;
        if (PlayerBuffManager.Instance != null)
        {
            finalDamage *= PlayerBuffManager.Instance.damageMultiplier;
        }

        bool isCrit = false;
        float roll = UnityEngine.Random.Range(0f, 100f);

        float finalCritChance = critChance;
        if (PlayerBuffManager.Instance != null)
        {
            finalCritChance += PlayerBuffManager.Instance.critChanceOffset;
        }

        if (roll <= finalCritChance)
        {
            finalDamage *= critMultiplier; // Nhân hệ số chí mạng của đạn
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