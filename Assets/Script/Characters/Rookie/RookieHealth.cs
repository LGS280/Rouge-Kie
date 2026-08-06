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
        isDead = true;
        if (animator != null) animator.SetTrigger("die");
        if (playerCollider != null) playerCollider.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        Transform handPos = transform.Find("Hand_Position");
        Transform backPos = transform.Find("Back_Position");
        if (handPos != null) handPos.gameObject.SetActive(false);
        if (backPos != null) backPos.gameObject.SetActive(false);

        // Tắt bóng Shadow và vòng xanh Player_Ring để tạo cảm giác nhân vật nằm bệt xuống sàn
        Transform shadowPos = transform.Find("Shadow");
        if (shadowPos != null) shadowPos.gameObject.SetActive(false);

        Transform ringPos = transform.Find("Player_Ring");
        if (ringPos == null) ringPos = transform.Find("Ring");
        if (ringPos == null) ringPos = transform.Find("PlayerRing");
        if (ringPos != null) ringPos.gameObject.SetActive(false);

        WeaponAim weapon = GetComponentInChildren<WeaponAim>();
        if (weapon != null) weapon.enabled = false;

        transform.position += new Vector3(0, -0.3f, 0);
        StartCoroutine(FadeToGray());
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

        // Báo cho RunStatsTracker kết thúc trận với kết quả Thất bại
        if (RunStatsTracker.Instance != null)
        {
            RunStatsTracker.Instance.EndRun(false);
        }
    }
}
