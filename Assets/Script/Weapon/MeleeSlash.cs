using UnityEngine;

public class MeleeSlash : MonoBehaviour
{
    [Header("THIẾT LẬP SÁT THƯƠNG GỐC")]
    public float delayTime = 0.1f;    // Thời gian vệt chém tồn tại trên màn hình
    public float damage = 15f;        // Sát thương cận chiến cơ bản

    [Header("THIẾT LẬP CHÍ MẠNG (CRITICAL)")]
    [Range(0f, 1f)]
    public float critChance = 0.2f;    // 0.2 nghĩa là 20% tỷ lệ ra đòn chí mạng
    public float critMultiplier = 2f;  // Nhân đôi sát thương khi chí mạng

    void Start()
    {
        // Vừa sinh ra là tự hủy liền sau delayTime giây cho nhẹ game, không bị dính hình
        Destroy(gameObject, delayTime);
    }

    // Hàm xử lý khi vệt chém quét trúng quái vật
    void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem có vả trúng quái vật mang Tag Enemy không
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