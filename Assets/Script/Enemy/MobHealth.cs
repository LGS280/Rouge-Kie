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

    private void Awake()
    {
        networkIdentity = GetComponent<MobNetworkIdentity>();
        if (networkIdentity == null)
        {
            networkIdentity = gameObject.AddComponent<MobNetworkIdentity>();
        }
    }

    void Start()
    {
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        mobCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
        if (mobCollider != null) mobCollider.enabled = true;

        Transform shadowObj = transform.Find("Shadow");
        if (shadowObj != null) shadowObj.gameObject.SetActive(true);
    }

    // Đạn bắn trúng máy nào, máy đó gọi hàm này
    public void TakeDamage(int damage, bool isCrit = false)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // BÍ QUYẾT: Gửi MÁU HIỆN TẠI (currentHealth) qua mạng thay vì gửi damage
        if (NetworkManager.Instance != null && networkIdentity != null)
        {
            NetworkManager.Instance.SendEnemyHitEvent(NetworkManager.Instance.CurrentRoomId, networkIdentity.networkId, (float)currentHealth);
        }

        ShowDamageUI(damage, isCrit);

        if (currentHealth <= 0)
        {
            ExecuteDieLocal();
        }
    }

    // Hàm MỚI: Chỉ dành cho việc đồng bộ từ máy khác gửi sang
    public void SyncHealthFromNetwork(int networkHealth)
    {
        if (isDead) return;

        // CHỐNG TIẾNG VỌNG: Nếu máu mạng gửi về >= máu hiện tại -> Đây là gói tin cũ hoặc của chính mình dội lại -> BỎ QUA!
        if (networkHealth >= currentHealth) return;

        int damageTaken = currentHealth - networkHealth;
        currentHealth = networkHealth;

        ShowDamageUI(damageTaken, false);

        if (currentHealth <= 0)
        {
            ExecuteDieLocal();
        }
    }

    // Tách riêng phần hiển thị UI cho sạch code
    private void ShowDamageUI(int damageAmount, bool isCrit)
    {
        try
        {
            MobFlash flash = GetComponent<MobFlash>();
            if (flash != null) flash.TriggerFlash();

            if (DamageNumberSpawner.Instance != null && damageAmount > 0)
            {
                DamageNumberSpawner.Instance.Spawn(transform.position, damageAmount, isCrit);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Lỗi UI số dame: " + ex.Message);
        }
    }

    public void ExecuteDieLocal()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        if (mobCollider != null) mobCollider.enabled = false;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.linearVelocity = Vector2.zero;
        }

        MobAI ai = GetComponent<MobAI>();
        if (ai != null) ai.enabled = false;

        if (animator != null) animator.SetTrigger("die");

        Transform shadowObj = transform.Find("Shadow");
        if (shadowObj != null) shadowObj.gameObject.SetActive(false);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 2;

        OnDeath?.Invoke(this);
    }

    void RecycleMob()
    {
        gameObject.SetActive(false);
    }
}