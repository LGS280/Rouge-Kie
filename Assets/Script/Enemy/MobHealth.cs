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

    private int originalMaxHealth = 0;

    private void Awake()
    {
        originalMaxHealth = maxHealth; // Lưu trữ máu gốc
        networkIdentity = GetComponent<MobNetworkIdentity>();
        if (networkIdentity == null)
        {
            networkIdentity = gameObject.AddComponent<MobNetworkIdentity>();
        }
    }

    /// <summary>
    /// Tính toán và nhân tỉ lệ máu tối đa của quái theo tầng hiện tại từ GameProgressionManager
    /// </summary>
    private void ScaleHealthByProgression()
    {
        // Nếu quái này là Boss (tên chứa chữ BOSS), bỏ qua cơ chế tự động scale quái thường
        if (gameObject.name.Contains("BOSS"))
        {
            return;
        }

        if (GameProgressionManager.Instance != null)
        {
            float mult = GameProgressionManager.Instance.GetMonsterHPMultiplier();
            maxHealth = Mathf.RoundToInt(originalMaxHealth * mult);
        }
    }

    void Start()
    {
        ScaleHealthByProgression();
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        mobCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        ScaleHealthByProgression();
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

        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.LogDamageDealt(damage);
        }

        // BÍ QUYẾT: Gửi MÁU HIỆN TẠI (currentHealth) qua mạng thay vì gửi damage
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer && networkIdentity != null)
        {
            Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity.networkId}) TakeDamage: damage={damage}, remainingHealth={currentHealth}. Sending sync...");
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
        if (isDead)
        {
            Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) received SyncHealthFromNetwork={networkHealth} but is already dead.");
            return;
        }

        Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) SyncHealthFromNetwork: networkHealth={networkHealth}, currentHealth={currentHealth}");

        // CHỐNG TIẾNG VỌNG: Nếu máu mạng gửi về >= máu hiện tại -> Đây là gói tin cũ hoặc của chính mình dội lại -> BỎ QUA!
        if (networkHealth >= currentHealth)
        {
            Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) ignored sync: networkHealth={networkHealth} >= currentHealth={currentHealth}");
            return;
        }

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

        Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) ExecuteDieLocal() - Killing mob locally.");

        isDead = true;

        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.LogEnemyKilled();
        }
        currentHealth = 0;

        if (mobCollider != null) mobCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
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
