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
    private MobNetworkIdentity networkIdentity;

    void Start()
    {
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        mobCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        networkIdentity = GetComponent<MobNetworkIdentity>();
    }

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

    public void TakeDamage(int damage, bool isCrit = false, bool syncNetwork = true)
    {
        if (isDead) return;

        // CẢ HOST VÀ CLIENT ĐỀU TRỪ MÁU LOCAL ĐỂ CHƠI MƯỢT MÀ
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // SYNC DAMAGE: Sync all damage to other clients so health matches perfectly
        if (syncNetwork && NetworkManager.Instance != null && networkIdentity != null)
        {
            NetworkManager.Instance.SendEnemyHitEvent(NetworkManager.Instance.CurrentRoomId, networkIdentity.networkId, damage);
        }

        MobFlash flash = GetComponent<MobFlash>();
        if (flash != null) flash.TriggerFlash();
        DamageNumberSpawner.Instance.Spawn(transform.position, damage, isCrit);

        if (animator != null)
        {
            animator.SetTrigger("hurt");
        }

        if (currentHealth <= 0)
        {
            Die(syncNetwork);
        }
    }

    void Die(bool syncNetwork)
    {
        // 1. Cho quái chết tại máy hiện tại luôn
        ExecuteDieLocal();

        // 2. ĐỒNG BỘ HAI BÊN: Bất kể ai giết (Host hay Client), đều gửi một gói tin đặc biệt 
        // lên Server để báo cho máy đối phương khai tử con quái này theo.
        if (syncNetwork && NetworkManager.Instance != null && networkIdentity != null)
        {
            // Mượn hàm SendEnemyHitEvent gửi lượng dame 9999 để kích hoạt lệnh chết bên máy kia
            NetworkManager.Instance.SendEnemyHitEvent(NetworkManager.Instance.CurrentRoomId, networkIdentity.networkId, 9999f);
        }
    }

    // Ép quái chết lập tức (gọi cục bộ hoặc gọi từ máy khác qua mạng)
    public void ExecuteDieLocal()
    {
        if (isDead) return;
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