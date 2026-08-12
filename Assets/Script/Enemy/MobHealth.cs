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

    public void SyncHealthFromNetwork(int networkHealth)
    {
        if (isDead)
        {
            Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) received SyncHealthFromNetwork={networkHealth} nhưng đã chết.");
            return;
        }

        Debug.Log($"[MobHealth] {gameObject.name} (networkId={networkIdentity?.networkId}) SyncHealthFromNetwork: networkHealth={networkHealth}, currentHealth={currentHealth}");

        int damageTaken = currentHealth - networkHealth;
        currentHealth = networkHealth;

        if (damageTaken > 0)
        {
            ShowDamageUI(damageTaken, false);
        }

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

        MobWeaponAim weaponAim = GetComponentInChildren<MobWeaponAim>();
        if (weaponAim != null)
        {
            weaponAim.DestroyWeaponOnDeath();
            weaponAim.enabled = false;
        }

        OnDeath?.Invoke(this);
        SpawnLootOnDeath();
    }

    [Header("Cấu hình Rớt Loot Khi Chết")]
    public GameObject coinPrefabOverride;
    public GameObject manaPrefabOverride;
    [Range(0f, 1f)] public float lootDropChance = 0.2f; // 🎯 20% tỷ lệ rớt đồ, 80% rớt tay không

    private void SpawnLootOnDeath()
    {
        // 🎯 TỶ LỆ RỚT ĐỒ: Nếu không trúng tỷ lệ -> Không rớt đồ (tay không)
        if (Random.value > lootDropChance) return;

        // 1. Tìm Prefab Vàng & Mana
        GameObject coinPrefab = coinPrefabOverride;
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Prefab/Item/Coin/Coin");
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Coin");

        GameObject manaPrefab = manaPrefabOverride;
        if (manaPrefab == null) manaPrefab = Resources.Load<GameObject>("Prefab/Item/Mana");
        if (manaPrefab == null) manaPrefab = Resources.Load<GameObject>("Mana");

        // 2. CHỌN NGẪU NHIÊN 1 LOẠI (Vàng HOẶC Mana)
        bool dropCoin = Random.value > 0.5f;
        GameObject targetLootPrefab = (dropCoin && coinPrefab != null) ? coinPrefab : ((manaPrefab != null) ? manaPrefab : coinPrefab);

        if (targetLootPrefab == null) return;

        // 3. CHỈ RỚT SỐ LƯỢNG 1 HOẶC 2 VIÊN
        int dropAmount = Random.Range(1, 3);

        MobAI mobAI = GetComponent<MobAI>();
        Bounds roomBounds = (mobAI != null && mobAI.myRoom != null && mobAI.myRoom.RoomCollider != null) ? mobAI.myRoom.RoomCollider.bounds : default;

        for (int i = 0; i < dropAmount; i++)
        {
            // 🎯 RỚT TẠI CHỖ VỊ TRÍ QUÁI GỤC NGÃ (Nhích nhẹ 0.15m để không đè hình)
            Vector3 spawnPos = transform.position;
            Vector2 smallOffset = Random.insideUnitCircle * 0.15f;
            Vector3 finalPos = spawnPos + (Vector3)smallOffset;

            // Đảm bảo rớt trong ranh giới phòng
            if (roomBounds.size != Vector3.zero)
            {
                finalPos.x = Mathf.Clamp(finalPos.x, roomBounds.min.x + 0.5f, roomBounds.max.x - 0.5f);
                finalPos.y = Mathf.Clamp(finalPos.y, roomBounds.min.y + 0.5f, roomBounds.max.y - 0.5f);
            }

            GameObject lootObj = Instantiate(targetLootPrefab, finalPos, Quaternion.identity);

            // 🎯 KHÓA LỰC VĂNG: Giữ item đứng yên 100% tại chỗ không bị trượt bay ra ngoài map
            Rigidbody2D lootRb = lootObj.GetComponent<Rigidbody2D>();
            if (lootRb != null)
            {
                lootRb.linearVelocity = Vector2.zero;
                lootRb.linearDamping = 10f;
            }
        }
    }

    void RecycleMob()
    {
        gameObject.SetActive(false);
    }
}
