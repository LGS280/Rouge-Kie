using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] Slider hpBar;
    [SerializeField] Slider armorBar;
    [SerializeField] Slider manaBar;
    [SerializeField] TextMeshProUGUI hpText;
    [SerializeField] TextMeshProUGUI armorText;
    [SerializeField] TextMeshProUGUI manaText;

    public void SetTarget(RookieHealth target)
    {
        target.onHealthChanged.AddListener(() =>
        {
            if (hpBar != null)
                hpBar.value = (float)target.GetCurrentHealth() / target.GetMaxHealth();
            if (hpText != null)
                hpText.text = target.GetCurrentHealth() + "/" + target.GetMaxHealth();
            if (armorBar != null)
                armorBar.value = (float)target.GetCurrentArmor() / target.GetMaxArmor();
            if (armorText != null)
                armorText.text = target.GetCurrentArmor() + "/" + target.GetMaxArmor();
            if (manaBar != null)
                manaBar.value = (float)target.GetCurrentMana() / target.GetMaxMana();
            if (manaText != null)
                manaText.text = target.GetCurrentMana() + "/" + target.GetMaxMana();
        });

        StartCoroutine(InitHUD(target));
    }

    System.Collections.IEnumerator InitHUD(RookieHealth target)
    {
        yield return null;
        if (hpBar != null) hpBar.value = (float)target.GetCurrentHealth() / target.GetMaxHealth();
        if (hpText != null) hpText.text = target.GetCurrentHealth() + "/" + target.GetMaxHealth();
        if (armorBar != null) armorBar.value = (float)target.GetCurrentArmor() / target.GetMaxArmor();
        if (armorText != null) armorText.text = target.GetCurrentArmor() + "/" + target.GetMaxArmor();
        if (manaBar != null) manaBar.value = (float)target.GetCurrentMana() / target.GetMaxMana();
        if (manaText != null) manaText.text = target.GetCurrentMana() + "/" + target.GetMaxMana();
    }
}