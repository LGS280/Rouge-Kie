using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý HUD thông số người chơi (Máu, Giáp, Mana) và danh sách Buff đang sở hữu
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Status Bars")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider armorBar;
    [SerializeField] private Slider manaBar;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI manaText;

    [Header("Active Buffs Display")]
    [SerializeField] private Transform buffContainer;

    private void OnEnable()
    {
        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.OnBuffsChanged += RefreshBuffIcons;
        }
        RefreshBuffIcons();
    }

    private void OnDisable()
    {
        if (PlayerBuffManager.Instance != null)
        {
            PlayerBuffManager.Instance.OnBuffsChanged -= RefreshBuffIcons;
        }
    }

    private RookieHealth currentTarget;

    private void Start()
    {
        RefreshBuffIcons();
        if (currentTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                RookieHealth rh = p.GetComponent<RookieHealth>();
                if (rh != null) SetTarget(rh);
            }
        }
    }

    public void SetTarget(RookieHealth target)
    {
        if (target == null) return;
        currentTarget = target;
        target.onHealthChanged.AddListener(() =>
        {
            if (hpBar != null) hpBar.value = (float)target.GetCurrentHealth() / target.GetMaxHealth();
            if (hpText != null) hpText.text = target.GetCurrentHealth() + "/" + target.GetMaxHealth();
            if (armorBar != null) armorBar.value = (float)target.GetCurrentArmor() / target.GetMaxArmor();
            if (armorText != null) armorText.text = target.GetCurrentArmor() + "/" + target.GetMaxArmor();
            if (manaBar != null) manaBar.value = (float)target.GetCurrentMana() / target.GetMaxMana();
            if (manaText != null) manaText.text = target.GetCurrentMana() + "/" + target.GetMaxMana();
        });

        StartCoroutine(InitHUD(target));
    }

    private IEnumerator InitHUD(RookieHealth target)
    {
        yield return null;
        if (hpBar != null) hpBar.value = (float)target.GetCurrentHealth() / target.GetMaxHealth();
        if (hpText != null) hpText.text = target.GetCurrentHealth() + "/" + target.GetMaxHealth();
        if (armorBar != null) armorBar.value = (float)target.GetCurrentArmor() / target.GetMaxArmor();
        if (armorText != null) armorText.text = target.GetCurrentArmor() + "/" + target.GetMaxArmor();
        if (manaBar != null) manaBar.value = (float)target.GetCurrentMana() / target.GetMaxMana();
        if (manaText != null) manaText.text = target.GetCurrentMana() + "/" + target.GetMaxMana();
    }

    /// <summary>
    /// Làm mới danh sách Icon các Buff người chơi đang sở hữu
    /// </summary>
    public void RefreshBuffIcons()
    {
        if (PlayerBuffManager.Instance == null) return;
        EnsureBuffContainer();
        if (buffContainer == null) return;

        // Xóa các badge cũ
        for (int i = buffContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(buffContainer.GetChild(i).gameObject);
        }

        List<BuffConfig> buffs = PlayerBuffManager.Instance.activeBuffs;
        if (buffs == null || buffs.Count == 0) return;

        foreach (var buff in buffs)
        {
            CreateBuffIconBadge(buff);
        }
    }

    private void EnsureBuffContainer()
    {
        if (buffContainer != null)
        {
            ApplyBuffContainerLayout(buffContainer.GetComponent<RectTransform>());
            return;
        }

        Transform existing = transform.Find("BuffHUDContainer");
        if (existing != null)
        {
            buffContainer = existing;
            ApplyBuffContainerLayout(buffContainer.GetComponent<RectTransform>());
            return;
        }

        // Tạo Container linh hoạt nằm HOÀN TOÀN BÊN DƯỚI khung HUD (Panel_PlayerInfo)
        GameObject containerObj = new GameObject("BuffHUDContainer");
        containerObj.transform.SetParent(transform, false);

        RectTransform rect = containerObj.AddComponent<RectTransform>();
        ApplyBuffContainerLayout(rect);

        HorizontalLayoutGroup hlg = containerObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        buffContainer = containerObj.transform;
    }

    private void ApplyBuffContainerLayout(RectTransform rect)
    {
        if (rect == null) return;
        // Neo tại cạnh đáy dưới cùng của Panel_PlayerInfo (anchorMin/Max = 0, 0)
        // và đẩy xuống phía dưới thêm 12px để nằm hoàn toàn bên ngoài khung HUD gỗ
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -12f);
        rect.sizeDelta = new Vector2(420f, 36f);
    }

    private void CreateBuffIconBadge(BuffConfig buff)
    {
        if (buff == null || buffContainer == null) return;

        GameObject badgeObj = new GameObject($"BuffBadge_{buff.buffType}");
        badgeObj.transform.SetParent(buffContainer, false);

        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(32f, 32f);

        // Nền tối Slate rõ nét cho badge
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.08f, 0.11f, 0.18f, 0.95f); // #0F172A

        Outline outline = badgeObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.42f, 0.52f, 0.95f); // #475569 viền sáng rõ nét
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Icon Sprite hiển thị bên trong badge
        Sprite iconSprite = UpgradeSelectionUI.GetBuffSprite(buff);
        if (iconSprite != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(badgeObj.transform, false);

            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(24f, 24f);
            iconRect.anchoredPosition = Vector2.zero;
        }
    }
}