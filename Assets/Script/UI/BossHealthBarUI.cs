using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("UI Containers")]
    [Tooltip("GameObject cha ch?a toàn b? thanh máu Boss (Panel/Container) d? b?t/t?t")]
    [SerializeField] private GameObject bossBarContainer;

    [Header("UI Components")]
    [Tooltip("Thanh tru?t Slider hi?n th? lu?ng máu c?a Boss")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("Text hi?n th? tên c?a Boss (VD: MELOG - THE GATLING WARLORD)")]
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Tooltip("Text hi?n th? s? máu c? th? (VD: 500 / 500)")]
    [SerializeField] private TextMeshProUGUI healthNumberText;

    [Header("C?u Hình Hi?u ?ng")]
    [Tooltip("T?c d? tru?t mu?t c?a thanh máu khi nh?n sát thuong")]
    [SerializeField] private float smoothSpeed = 8f;

    private MobHealth trackedBossHealth;
    private float targetFillRatio = 1f;
    private bool isBarActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // M?c d?nh ?n thanh máu khi chua vào phòng Boss
        if (bossBarContainer != null)
        {
            bossBarContainer.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (trackedBossHealth != null)
        {
            trackedBossHealth.OnDeath -= HandleBossDeath;
        }
    }

    private void Update()
    {
        if (!isBarActive || trackedBossHealth == null) return;

        // N?u Boss dã ch?t ho?c b? h?y -> ?n thanh máu
        if (trackedBossHealth.isDead || trackedBossHealth.CurrentHealth <= 0)
        {
            HideBossBar();
            return;
        }

        // C?p nh?t t? l? máu dích
        if (trackedBossHealth.maxHealth > 0)
        {
            targetFillRatio = Mathf.Clamp01((float)trackedBossHealth.CurrentHealth / trackedBossHealth.maxHealth);
        }

        // Tru?t thanh máu mu?t mà
        if (healthSlider != null)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetFillRatio, Time.deltaTime * smoothSpeed);
        }

        // C?p nh?t s? máu
        if (healthNumberText != null)
        {
            healthNumberText.text = $"{trackedBossHealth.CurrentHealth} / {trackedBossHealth.maxHealth}";
        }
    }

    /// <summary>
    /// Kích ho?t hi?n th? thanh máu Boss khi ngu?i choi bu?c vào phòng chi?n d?u
    /// </summary>
    public void ShowBossBar(string bossName, MobHealth bossMob)
    {
        if (bossMob == null) return;

        // H?y dang ký Boss cu n?u có
        if (trackedBossHealth != null)
        {
            trackedBossHealth.OnDeath -= HandleBossDeath;
        }

        trackedBossHealth = bossMob;
        trackedBossHealth.OnDeath += HandleBossDeath;

        if (bossNameText != null && !string.IsNullOrEmpty(bossName))
        {
            bossNameText.text = bossName;
        }

        if (trackedBossHealth.maxHealth > 0)
        {
            targetFillRatio = Mathf.Clamp01((float)trackedBossHealth.CurrentHealth / trackedBossHealth.maxHealth);
        }
        else
        {
            targetFillRatio = 1f;
        }

        if (healthSlider != null)
        {
            healthSlider.value = targetFillRatio;
        }

        if (healthNumberText != null)
        {
            healthNumberText.text = $"{trackedBossHealth.CurrentHealth} / {trackedBossHealth.maxHealth}";
        }

        if (bossBarContainer != null)
        {
            bossBarContainer.SetActive(true);
        }

        isBarActive = true;
    }

    /// <summary>
    /// ?n thanh máu Boss khi Boss b? tiêu di?t ho?c r?i phòng
    /// </summary>
    public void HideBossBar()
    {
        isBarActive = false;

        if (trackedBossHealth != null)
        {
            trackedBossHealth.OnDeath -= HandleBossDeath;
            trackedBossHealth = null;
        }

        if (bossBarContainer != null)
        {
            bossBarContainer.SetActive(false);
        }
    }

    private void HandleBossDeath(MobHealth deadBoss)
    {
        StartCoroutine(HideBossBarWithDelay(1.2f));
    }

    private IEnumerator HideBossBarWithDelay(float delay)
    {
        // Gi? thanh máu ? m?c 0 m?t chút d? ngu?i choi k?p nhìn th?y Boss c?n máu
        if (healthSlider != null) healthSlider.value = 0f;
        if (healthNumberText != null && trackedBossHealth != null)
        {
            healthNumberText.text = $"0 / {trackedBossHealth.maxHealth}";
        }

        yield return new WaitForSeconds(delay);
        HideBossBar();
    }
}
