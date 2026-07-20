using UnityEngine;

public class NormalBullet : MonoBehaviour
{
    [HideInInspector] public float speed;
    [HideInInspector] public float baseDamage;
    [HideInInspector] public float critChance;

    public float lifeTime = 3f;
    public float critMultiplier = 1.5f;

    // Hàm nhận dữ liệu từ DB truyền qua
    public void InitFromDb(int bulletId)
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            speed = config.flightSpeed;
            baseDamage = config.damage;
            critChance = config.critRate * 100f; // Đổi thập phân (0.2) thành phần trăm (20%)
        }
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle") || collision.CompareTag("Enemy") || collision.CompareTag("Door"))
        {
            if (collision.CompareTag("Enemy"))
            {
                CalculateAndApplyDamage(collision);
            }
            Destroy(gameObject);
        }
    }

    private void CalculateAndApplyDamage(Collider2D collision)
    {
        float finalDamage = baseDamage;
        bool isCrit = false;
        float roll = UnityEngine.Random.Range(0f, 100f);

        if (roll <= critChance)
        {
            finalDamage = baseDamage * critMultiplier;
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