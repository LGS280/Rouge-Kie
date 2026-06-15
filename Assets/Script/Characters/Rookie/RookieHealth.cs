using UnityEngine;

public class RookieHealth : MonoBehaviour
{
    [Header("THIẾT LẬP MÁU PLAYER")]
    public int maxHealth = 100;
    private int currentHealth;
    [HideInInspector] public bool isDead = false;

    private Animator animator;
    private Collider2D playerCollider;
    private Rigidbody2D rb;

    void Start()
    {
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);


        if (animator != null)
        {
            animator.SetTrigger("hurt");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Hàm xử lý khi Player hết máu
    void Die()
    {
        isDead = true;

        if (animator != null)
        {
            animator.SetTrigger("die");
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }


        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.linearVelocity = Vector2.zero;
        }

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        WeaponAim weapon = GetComponentInChildren<WeaponAim>();
        if (weapon != null)
        {
            weapon.enabled = false;
        }
    }
}