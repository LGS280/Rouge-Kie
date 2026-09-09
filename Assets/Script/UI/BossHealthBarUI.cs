using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    private static BossHealthBarUI _instance;
    public static BossHealthBarUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("UI Containers")]
    [Tooltip("GameObject cha chứa toàn bộ thanh máu Boss (Panel/Container) để bật/tắt")]
    [SerializeField] private GameObject bossBarContainer;

    [Header("UI Components")]
    [Tooltip("Thanh trượt Slider hiển thị lượng máu của Boss")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("Text hiển thị tên của Boss (VD: MELOG - THE GATLING WARLORD)")]
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Tooltip("Text hiển thị số máu cụ thể (VD: 500 / 500)")]
    [SerializeField] private TextMeshProUGUI healthNumberText;

    [Header("Cấu Hình Hiệu Ứng")]
    [Tooltip("Tốc độ trượt mượt của thanh máu khi nhận sát thương")]
    [SerializeField] private float smoothSpeed = 8f;

    private MobHealth trackedBossHealth;
    private bool isBarActive = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Tắt bắt buộc Whole Numbers để đảm bảo trượt mượt mà
        if (healthSlider != null)
        {
            healthSlider.wholeNumbers = false;
        }

        // Mặc định ẩn thanh máu khi vừa vào map nếu chưa bước vào phòng Boss
        if (bossBarContainer != null && !isBarActive)
        {
            bossBarContainer.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Khi bật lên, đảm bảo tắt Whole Numbers và kiểm tra kết nối Boss
        if (healthSlider != null)
        {
            healthSlider.wholeNumbers = false;
        }

        if (trackedBossHealth == null)
        {
            TryAutoBindBoss();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (trackedBossHealth != null)
        {
            trackedBossHealth.OnDeath -= HandleBossDeath;
        }
    }

    private void Update()
    {
        if (isBarActive && trackedBossHealth == null)
        {
            TryAutoBindBoss();
            return;
        }

        if (!isBarActive || trackedBossHealth == null) return;

        // Nếu Boss đã chết hoặc cạn máu -> ẩn thanh máu
        if (trackedBossHealth.isDead || trackedBossHealth.CurrentHealth <= 0)
        {
            HideBossBar();
            return;
        }

        // Cập nhật giá trị thanh Slider mượt mà
        if (healthSlider != null)
        {
            float maxH = trackedBossHealth.maxHealth > 0 ? (float)trackedBossHealth.maxHealth : 100f;
            if (healthSlider.maxValue != maxH)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxH;
            }
            healthSlider.wholeNumbers = false;

            float targetVal = Mathf.Clamp((float)trackedBossHealth.CurrentHealth, 0f, maxH);
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetVal, Time.deltaTime * smoothSpeed);
        }

        // Cập nhật số máu hiển thị
        if (healthNumberText != null)
        {
            healthNumberText.text = $"{trackedBossHealth.CurrentHealth} / {trackedBossHealth.maxHealth}";
        }
    }

    /// <summary>
    /// Kích hoạt hiển thị thanh máu Boss khi người chơi bước vào phòng chiến đấu
    /// </summary>
    public void ShowBossBar(string bossName, MobHealth bossMob)
    {
        if (bossMob == null) return;

        gameObject.SetActive(true);
        if (bossBarContainer != null)
        {
            bossBarContainer.SetActive(true);
        }

        // Hủy đăng ký Boss cũ nếu có
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

        float maxH = trackedBossHealth.maxHealth > 0 ? (float)trackedBossHealth.maxHealth : 100f;
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxH;
            healthSlider.wholeNumbers = false;
            healthSlider.value = trackedBossHealth.CurrentHealth;
        }

        if (healthNumberText != null)
        {
            healthNumberText.text = $"{trackedBossHealth.CurrentHealth} / {trackedBossHealth.maxHealth}";
        }

        isBarActive = true;
    }

    /// <summary>
    /// Tự động tìm kiếm Boss Melog trong Scene nếu chưa được gọi từ phòng
    /// </summary>
    public void TryAutoBindBoss()
    {
        MelogBossAI melog = FindFirstObjectByType<MelogBossAI>(FindObjectsInactive.Include);
        if (melog != null)
        {
            MobHealth mb = melog.GetComponent<MobHealth>();
            // Chỉ auto-bind nếu Boss đã được kích hoạt chiến đấu hoặc đang nhận sát thương
            if (mb != null && !mb.isDead && (melog.IsCombatActivated() || mb.CurrentHealth < mb.maxHealth))
            {
                string displayName = "MELOG - THE GATLING WARLORD";
                if (GameProgressionManager.Instance != null)
                {
                    int floor = GameProgressionManager.Instance.currentFloor;
                    if (floor >= 5) displayName = "ELITE BOSS - GOLIATH ROOT";
                    else displayName = $"MELOG - FLOOR {floor}";
                }

                ShowBossBar(displayName, mb);
            }
        }
    }

    /// <summary>
    /// Ẩn thanh máu Boss khi Boss bị tiêu diệt hoặc rời phòng
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
        if (healthSlider != null) healthSlider.value = 0f;
        if (healthNumberText != null && trackedBossHealth != null)
        {
            healthNumberText.text = $"0 / {trackedBossHealth.maxHealth}";
        }

        yield return new WaitForSeconds(delay);
        HideBossBar();
    }
}
