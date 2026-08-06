using UnityEngine;
using UnityEngine.Events;

public class RookieHealth : MonoBehaviour
{
    [Header("THI?T L?P M�U PLAYER")]
    public int maxHealth = 5;
    private int currentHealth;
    [HideInInspector] public bool isDead = false;
    private Animator animator;
    private Collider2D playerCollider;
    private Rigidbody2D rb;

    [Header("THI?T L?P GI�P")]
    public int maxArmor = 4;
    private int currentArmor;
    private float armorRegenDelayTimer = 0f;
    private float armorRegenTickTimer = 0f;
    private bool armorRegenStarted = false;
    public float armorRegenDelay = 2f;
    public float armorRegenTick = 1f;

    [Header("THI?T L?P MANA")]
    public int maxMana = 200;
    private int currentMana;

    public UnityEvent onHealthChanged;

    void Start()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;
        currentMana = maxMana;
        isDead = false;
        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        onHealthChanged?.Invoke();
    }

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.T))
        {
            TakeDamage(1);
        }
        if (currentArmor < maxArmor && !isDead)
        {
            if (!armorRegenStarted)
            {
                armorRegenDelayTimer -= Time.deltaTime;
                if (armorRegenDelayTimer <= 0f)
                {
                    armorRegenStarted = true;
                    armorRegenTickTimer = armorRegenTick;
                }
            }
            else
            {
                armorRegenTickTimer -= Time.deltaTime;
                if (armorRegenTickTimer <= 0f)
                {
                    currentArmor++;
                    onHealthChanged?.Invoke();
                    armorRegenTickTimer = armorRegenTick;
                    if (currentArmor >= maxArmor)
                        armorRegenStarted = false;
                }
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        int originalDamage = damage; // Lưu lại lượng sát thương thực tế để hiển thị chữ số bay

        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, damage);
            currentArmor -= absorbed;
            damage -= absorbed;
        }

        armorRegenDelayTimer = armorRegenDelay;
        armorRegenStarted = false;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke();

        // Hiệu ứng chớp đỏ báo hiệu chịu sát thương
        StartCoroutine(HurtFlashRoutine());

        // BỔ SUNG: Gửi thông báo chịu sát thương lên Server cho đồng đội hiển thị
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
        {
            NetworkManager.Instance.SendPlayerDamaged(NetworkManager.Instance.MyConnectionId, originalDamage);
        }

        // Hiển thị số sát thương màu cam nổi bật và có dấu trừ bay lên đầu nhân vật
        if (DamageNumberSpawner.Instance != null && originalDamage > 0)
        {
            DamageNumber dn = DamageNumberSpawner.Instance.Spawn(transform.position, originalDamage, false);
            if (dn != null)
            {
                dn.SetColor(new Color(1f, 0.4f, 0f)); // Màu cam sáng nổi bật để phân biệt với sát thương quái
                dn.SetText("-" + originalDamage);      // Thêm dấu trừ
            }
        }

        if (animator != null && HasParameter("hurt", animator)) animator.SetTrigger("hurt");
        if (currentHealth <= 0) Die();
    }

    /// <summary>
    /// Nhận sát thương đồng bộ từ mạng (do Host/Server gửi về cho Player 2).
    /// Trừ máu/giáp cục bộ nhưng KHÔNG phát tín hiệu SendPlayerDamaged ngược lại SignalR.
    /// </summary>
    public void TakeDamageFromNetwork(int damage)
    {
        if (isDead) return;

        int originalDamage = damage;

        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, damage);
            currentArmor -= absorbed;
            damage -= absorbed;
        }

        armorRegenDelayTimer = armorRegenDelay;
        armorRegenStarted = false;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        onHealthChanged?.Invoke();

        StartCoroutine(HurtFlashRoutine());

        if (DamageNumberSpawner.Instance != null && originalDamage > 0)
        {
            DamageNumber dn = DamageNumberSpawner.Instance.Spawn(transform.position, originalDamage, false);
            if (dn != null)
            {
                dn.SetColor(new Color(1f, 0.4f, 0f));
                dn.SetText("-" + originalDamage);
            }
        }

        if (animator != null && HasParameter("hurt", animator)) animator.SetTrigger("hurt");
        if (currentHealth <= 0) Die();
    }

    private bool HasParameter(string paramName, Animator anim)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    private System.Collections.IEnumerator HurtFlashRoutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(1f, 0.4f, 0.4f); // Chớp đỏ nhạt
            yield return new WaitForSeconds(0.15f);
            sr.color = Color.white; // Trả lại màu gốc
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        onHealthChanged?.Invoke();
    }

    public bool UseMana(int amount)
    {
        if (currentMana < amount) return false;
        currentMana -= amount;
        onHealthChanged?.Invoke();
        return true;
    }

    public void RestoreMana(int amount)
    {
        currentMana = Mathf.Clamp(currentMana + amount, 0, maxMana);
        onHealthChanged?.Invoke();
    }

    public void ApplyUpgradeStats(int hpBonus, int armorBonus, int manaBonus)
    {
        maxHealth += hpBonus;
        currentHealth += hpBonus;

        maxArmor += armorBonus;
        currentArmor += armorBonus;

        maxMana += manaBonus;
        currentMana += manaBonus;

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        currentArmor = Mathf.Clamp(currentArmor, 0, maxArmor);
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);

        onHealthChanged?.Invoke();
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public int GetCurrentArmor() => currentArmor;
    public int GetMaxArmor() => maxArmor;
    public int GetCurrentMana() => currentMana;
    public int GetMaxMana() => maxMana;

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. Tắt di chuyển và điều khiển
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
            {
                script.enabled = false;
            }
        }

        // 2. Tắt súng hiển thị trên tay và lưng
        Transform handPos = transform.Find("Hand_Position");
        Transform backPos = transform.Find("Back_Position");
        if (handPos != null) handPos.gameObject.SetActive(false);
        if (backPos != null) backPos.gameObject.SetActive(false);

        // 3. Khóa vật lý để nằm yên cố định tại chỗ, không bị đẩy trượt
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 4. Vô hiệu hóa Collider để không nhặt được Buff/Rương khi hy sinh
        if (playerCollider != null) playerCollider.enabled = false;

        // 5. Phát hoạt ảnh nằm xuống và chớp xám
        if (animator != null && HasParameter("die", animator)) animator.SetTrigger("die");
        StartCoroutine(FadeToGray());

        // 6. Gửi thông báo hy sinh lên Server nếu đang trong chế độ Co-op
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
        {
            NetworkManager.Instance.SendPlayerDeath();
        }
        else
        {
            // Trong chế độ Solo -> Kết thúc Thất bại
            if (RunStatsTracker.Instance != null)
            {
                RunStatsTracker.Instance.EndRun(false);
            }
        }
    }

    System.Collections.IEnumerator FadeToGray()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color startColor = sr.color;
        Color targetColor = new Color(0.3f, 0.3f, 0.3f, 1f); // x�m t?i
        float duration = 0.5f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            sr.color = Color.Lerp(startColor, targetColor, t / duration);
            yield return null;
        }

        sr.color = targetColor;

        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        // Trong chế độ Solo -> Kết thúc trận với kết quả Thất bại (Trong Co-op chỉ kết thúc khi cả 2 cùng chết)
        if (!isMultiplayer && RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.EndRun(false);
        }
    }

    /// <summary>
    /// Hồi sinh người chơi khi được đồng đội giữ [E] 2.5s trong Co-op
    /// </summary>
    public void Revive(int healthAmount)
    {
        if (!isDead) return;
        isDead = false;

        currentHealth = Mathf.Clamp(healthAmount, 1, maxHealth);
        currentArmor = maxArmor / 2; // Phục hồi 50% Giáp
        onHealthChanged?.Invoke();

        // 1. Kích hoạt lại di chuyển và điều khiển
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = true;

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = true;

        MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && (script.GetType().Name == "WeaponAim" || script.GetType().Name == "WeaponLaser"))
            {
                script.enabled = true;
            }
        }

        // 2. Kích hoạt lại súng hiển thị trên tay và lưng
        Transform handPos = transform.Find("Hand_Position");
        Transform backPos = transform.Find("Back_Position");
        if (handPos != null) handPos.gameObject.SetActive(true);
        if (backPos != null) backPos.gameObject.SetActive(true);

        // 3. Khôi phục Rigidbody2D vật lý động
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
        }

        // 4. Kích hoạt lại Collider2D
        if (playerCollider != null) playerCollider.enabled = true;

        // 5. Khôi phục màu sắc hiển thị
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;

        Debug.Log($"[RookieHealth] Người chơi đã được HỒI SINH với {currentHealth} Máu và {currentArmor} Giáp!");
    }
}
