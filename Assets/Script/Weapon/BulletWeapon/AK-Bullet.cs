using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;     // Tốc độ bay của đạn
    public float lifeTime = 3f;   // Sau 3 giây không chạm gì cũng tự hủy cho nhẹ game

    public float baseDamage = 10f;
    [Range(0f, 100f)]
    public float critChance = 20f;
    public float critMultiplier = 1.5f; // hệ số chỉ mạng (baseDamage * critMultipler = final Damage nếu crit)

    void Start()
    {
        // Vừa sinh ra là tự hủy sau lifeTime giây
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Đạn luôn luôn bay thẳng về phía trước theo trục X của chính nó
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    // Hàm xử lý khi đạn chạm vào tường hoặc quái vật
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

    // hàm tính xem phát đạn này là dame thường hay dame chí mạng
    private void CalculateAndApplyDamage(Collider2D collision)
    {
        float finalDamage = baseDamage;
        bool isCrit = false;

        float roll = UnityEngine.Random.Range(0f, 100f);

        if (roll <= critChance)
        {
            // nổ dame chí mạng
            finalDamage = baseDamage * critMultiplier;
            isCrit = true;
        }
        else
        {
            isCrit = false;
        }

        MobHealth enemyHealth = collision.GetComponent<MobHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(Mathf.RoundToInt(finalDamage), isCrit);
        }
    }
}
