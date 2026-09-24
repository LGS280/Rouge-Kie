using UnityEngine;

public class MobHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    [HideInInspector] public bool isDead = false;

    public System.Action<MobHealth> OnDeath;

    private Animator animator;
    private Collider2D mobCollider;
    private Rigidbody2D rb;
    private MobNetworkIdentity networkIdentity;

    private int originalMaxHealth = 0;

    [HideInInspector]
    public string enemyConfigName = "";

    private void Awake()
    {
        originalMaxHealth = maxHealth;
        networkIdentity = GetComponent<MobNetworkIdentity>();
        if (networkIdentity == null)
        {
            networkIdentity = gameObject.AddComponent<MobNetworkIdentity>();
        }
    }

    private void ScaleHealthByProgression()
    {
        // 1. Final Boss (Braead hoặc Boss tầng cuối): Máu lấy BaseHealth từ Database, nhân hệ số Co-op nếu có nhiều người chơi
        if (GetComponent<BraeadBossAI>() != null || gameObject.name.ToUpper().Contains("BRAEAD"))
        {
            float bossMult = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.GetBossHPMultiplier() : 1.0f;
            maxHealth = Mathf.RoundToInt(originalMaxHealth * bossMult);
            return;
        }

        // 2. Mini-Boss (Melog): GIỮ NGUYÊN 100% CÔNG THỨC GỐC: (BaseHealth từ DB + ((floor - 1) * 100)) x Hệ số Co-op
        if (GetComponent<MelogBossAI>() != null || gameObject.name.ToUpper().Contains("MELOG") || gameObject.name.ToUpper().Contains("BOSS"))
        {
            int floor = 1;
            if (GameProgressionManager.Instance != null) floor = GameProgressionManager.Instance.currentFloor;
            int baseFloorHp = (floor > 1) ? (originalMaxHealth + ((floor - 1) * 100)) : originalMaxHealth;

            float bossMult = GameProgressionManager.Instance != null ? GameProgressionManager.Instance.GetBossHPMultiplier() : 1.0f;
            maxHealth = Mathf.RoundToInt(baseFloorHp * bossMult);
            return;
        }

        // 3. Quái thường: Nhân hệ số độ khó theo tầng
        if (GameProgressionManager.Instance != null)
        {
            float mult = GameProgressionManager.Instance.GetMonsterHPMultiplier();
            maxHealth = Mathf.RoundToInt(originalMaxHealth * mult);
        }
        else
        {
            maxHealth = originalMaxHealth;
        }
    }

    public void ApplyEnemyConfig()
    {
        if (GameConfigManager.Instance == null) return;
        string key = enemyConfigName;
        if (string.IsNullOrEmpty(key))
        {
            key = gameObject.name.Replace("(Clone)", "").Trim();

            // Tự động nhận diện nếu là Boss Braead hoặc Melog theo AI component hoặc name
            if (GetComponent<BraeadBossAI>() != null || key.ToUpper().Contains("BRAEAD"))
            {
                key = "Braead";
            }
            else if (GetComponent<MelogBossAI>() != null || key.ToUpper().Contains("MELOG") || key.ToUpper().Contains("BOSS"))
            {
                key = "Melog";
            }
        }

        EnemyConfig config = GameConfigManager.Instance.GetEnemyConfig(key);
        if (config == null)
        {
            if (GetComponent<BraeadBossAI>() != null)
            {
                config = GameConfigManager.Instance.GetEnemyConfig("Braead");
            }
            else if (GetComponent<MelogBossAI>() != null)
            {
                config = GameConfigManager.Instance.GetEnemyConfig("Melog");
            }
        }

        if (config != null && config.baseHealth > 0)
        {
            originalMaxHealth = config.baseHealth;
            ScaleHealthByProgression();
            currentHealth = maxHealth;
        }
    }

    void Start()
    {
        ApplyEnemyConfig();
        GameConfigManager.OnConfigLoaded += ApplyEnemyConfig;

        ScaleHealthByProgression();
        currentHealth = maxHealth;
        isDead = false;

        animator = GetComponent<Animator>();
        mobCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnDestroy()
    {
        GameConfigManager.OnConfigLoaded -= ApplyEnemyConfig;
    }

    void OnEnable()
    {
        ApplyEnemyConfig();
        ScaleHealthByProgression();
        currentHealth = maxHealth;
        isDead = false;
        if (mobCollider != null) mobCollider.enabled = true;

        Transform shadowObj = transform.Find("Shadow");
        if (shadowObj != null) shadowObj.gameObject.SetActive(true);
    }

    public void TakeDamage(int damage, bool isCrit = false)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.LogDamageDealt(damage);
        }

        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer && networkIdentity != null)
        {

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

            return;
        }

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

        }
    }

    public void ExecuteDieLocal()
    {
        if (isDead) return;

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

        MelogWeaponAim melogWeaponAim = GetComponent<MelogWeaponAim>();
        if (melogWeaponAim != null)
        {
            melogWeaponAim.DestroyWeaponsOnDeath();
            melogWeaponAim.enabled = false;
        }

        OnDeath?.Invoke(this);
        SpawnLootOnDeath();
    }

    [Header("Cấu hình Rớt Loot Khi Chết")]
    public GameObject coinPrefabOverride;
    public GameObject manaPrefabOverride;
    [Range(0f, 1f)] public float lootDropChance = 0.2f;

    private void SpawnLootOnDeath()
    {

        if (Random.value > lootDropChance) return;

        GameObject coinPrefab = coinPrefabOverride;
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Prefab/Item/Coin/Coin");
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Coin");

        GameObject manaPrefab = manaPrefabOverride;
        if (manaPrefab == null) manaPrefab = Resources.Load<GameObject>("Prefab/Item/Mana");
        if (manaPrefab == null) manaPrefab = Resources.Load<GameObject>("Mana");

        bool dropCoin = Random.value > 0.5f;
        GameObject targetLootPrefab = (dropCoin && coinPrefab != null) ? coinPrefab : ((manaPrefab != null) ? manaPrefab : coinPrefab);

        if (targetLootPrefab == null) return;

        int dropAmount = Random.Range(1, 3);

        MobAI mobAI = GetComponent<MobAI>();
        Bounds roomBounds = (mobAI != null && mobAI.myRoom != null && mobAI.myRoom.RoomCollider != null) ? mobAI.myRoom.RoomCollider.bounds : default;

        for (int i = 0; i < dropAmount; i++)
        {

            Vector3 spawnPos = transform.position;
            Vector2 smallOffset = Random.insideUnitCircle * 0.15f;
            Vector3 finalPos = spawnPos + (Vector3)smallOffset;

            if (roomBounds.size != Vector3.zero)
            {
                finalPos.x = Mathf.Clamp(finalPos.x, roomBounds.min.x + 0.5f, roomBounds.max.x - 0.5f);
                finalPos.y = Mathf.Clamp(finalPos.y, roomBounds.min.y + 0.5f, roomBounds.max.y - 0.5f);
            }

            GameObject lootObj = Instantiate(targetLootPrefab, finalPos, Quaternion.identity);

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
