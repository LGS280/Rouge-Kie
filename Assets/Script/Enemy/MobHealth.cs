using UnityEngine;

public class MobHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    [HideInInspector] public bool isDead = false;

    public System.Action<MobHealth> OnDeath;

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

        Transform shadowObj = transform.Find("Shadow");
        if (shadowObj != null)
        {
            shadowObj.gameObject.SetActive(true);
        }
    }

    public void TakeDamage(int damage, bool isCrit = false)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        MobFlash flash = GetComponent<MobFlash>();
        if (flash != null) flash.TriggerFlash();
        DamageNumberSpawner.Instance.Spawn(transform.position, damage, isCrit); // ✅

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

    //void Die()
    //{
    //    isDead = true;

    //    if (animator != null)
    //    {
    //        animator.SetTrigger("die");
    //    }

    //    // Tắt va chạm vật lý để người chơi/đạn đi xuyên qua xác quái khi đang chạy Anim chết
    //    if (mobCollider != null)
    //    {
    //        mobCollider.enabled = false;
    //    }

    //    // Đóng băng vật lý của quái (Chuyển sang Static) để xác quái không bị trượt đi khi bị va chạm
    //    if (rb != null)
    //    {
    //        rb.bodyType = RigidbodyType2D.Static;
    //        rb.linearVelocity = Vector2.zero; // Dừng mọi lực quán tính còn lại
    //    }

    //    // Tắt hoàn toàn AI của quái để dừng mọi logic tìm đường/chạy Update
    //    MobAI ai = GetComponent<MobAI>();
    //    if (ai != null)
    //    {
    //        ai.enabled = false;
    //    }

    //    // Đẩy xác quái xuống lớp hiển thị phía sau (Dưới chân người chơi)
    //    SpriteRenderer sr = GetComponent<SpriteRenderer>();
    //    if (sr != null)
    //    {
    //        // Giả sử sortingOrder bình thường của bạn là 0 hoặc lớn hơn, đặt về -10 để nằm dưới chân nhân vật
    //        sr.sortingOrder = 2;
    //    }


    //    // Trả quái về Object Pool sau một khoảng thời gian (ví dụ 1.2 giây)
    //    //Invoke("RecycleMob", 1.2f);
    //}

    void Die()
    {
        isDead = true;

        if (animator != null)
        {
            animator.SetTrigger("die");
        }

        if (mobCollider != null)
        {
            mobCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.linearVelocity = Vector2.zero;
        }

        MobAI ai = GetComponent<MobAI>();
        if (ai != null)
        {
            ai.enabled = false;
        }

        Transform shadowObj = transform.Find("Shadow");
        if (shadowObj != null)
        {
            shadowObj.gameObject.SetActive(false);
        }

        // Đẩy xác quái xuống lớp hiển thị phía sau (Dưới chân người chơi)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 2;
        }

        OnDeath?.Invoke(this);
    }

    void RecycleMob()
    {
        gameObject.SetActive(false);
    }
}