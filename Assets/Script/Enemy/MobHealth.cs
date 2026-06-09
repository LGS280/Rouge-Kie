using UnityEngine;

public class MobHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    [HideInInspector] public bool isDead = false;

    private Animator animator;
    private Collider2D mobCollider;
    private Rigidbody2D rb;

    void Start()
    {
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        mobCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    // Hàm reset khi quái được lấy ra lại từ Object Pool
    void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
        if (mobCollider != null) mobCollider.enabled = true;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        // Kích hoạt Animation bị thương (giật lùi, chớp đỏ...)
        if (animator != null)
        {
            animator.SetTrigger("hurt");
        }
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;

        if (animator != null)
        {
            animator.SetTrigger("die");
        }

        // Tắt va chạm vật lý để người chơi/đạn đi xuyên qua xác quái khi đang chạy Anim chết
        if (mobCollider != null)
        {
            mobCollider.enabled = false;
        }

        // Dừng chuyển động vật lý ngay lập tức
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Trả quái về Object Pool sau một khoảng thời gian (ví dụ 1.2 giây)
        Invoke("RecycleMob", 1.2f);
    }

    void RecycleMob()
    {
        gameObject.SetActive(false);
    }
}