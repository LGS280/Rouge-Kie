using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBuffManager : MonoBehaviour
{
    private static PlayerBuffManager _instance;
    public static PlayerBuffManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PlayerBuffManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("PlayerBuffManager");
                    _instance = obj.AddComponent<PlayerBuffManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Buff Modifiers")]
    public float damageMultiplier = 1f;
    public float critChanceOffset = 0f; // Cộng thẳng % chí mạng (ví dụ: +15f)
    public float fireRateMultiplier = 1f; // Cooldown multiplier (ví dụ: 0.8f)
    public float coinGainMultiplier = 1f;
    public float moveSpeedMultiplier = 1f;

    [Header("Bonus Stats")]
    public int bonusMaxHealth = 0;
    public int bonusMaxArmor = 0;
    public int bonusMaxMana = 0;

    // Danh sách ID các Buff đã chọn trong lượt chơi này
    public List<int> selectedBuffIds = new List<int>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void ResetBuffs()
    {
        damageMultiplier = 1f;
        critChanceOffset = 0f;
        fireRateMultiplier = 1f;
        coinGainMultiplier = 1f;
        moveSpeedMultiplier = 1f;

        bonusMaxHealth = 0;
        bonusMaxArmor = 0;
        bonusMaxMana = 0;

        selectedBuffIds.Clear();
        Debug.Log("[PlayerBuffManager] Đã reset toàn bộ Buff về mặc định.");
    }

    public void ApplyBuff(GameConfigManager.BuffConfig buff)
    {
        if (buff == null) return;

        selectedBuffIds.Add(buff.id);
        Debug.Log($"[PlayerBuffManager] Áp dụng Buff: {buff.buffName} (Loại: {buff.buffType}, Giá trị: {buff.value})");

        // Tìm Player trong Scene để tác động trực tiếp nếu cần thiết
        GameObject player = GameObject.FindWithTag("Player");
        RookieHealth health = player != null ? player.GetComponent<RookieHealth>() : null;

        switch (buff.buffType)
        {
            case "MaxHP":
                bonusMaxHealth += Mathf.RoundToInt(buff.value);
                if (health != null)
                {
                    health.ApplyUpgradeStats(Mathf.RoundToInt(buff.value), 0, 0);
                }
                break;

            case "MaxArmor":
                bonusMaxArmor += Mathf.RoundToInt(buff.value);
                if (health != null)
                {
                    health.ApplyUpgradeStats(0, Mathf.RoundToInt(buff.value), 0);
                }
                break;

            case "MaxMana":
                bonusMaxMana += Mathf.RoundToInt(buff.value);
                if (health != null)
                {
                    health.ApplyUpgradeStats(0, 0, Mathf.RoundToInt(buff.value));
                }
                break;

            case "MoveSpeed":
                moveSpeedMultiplier *= buff.value;
                break;

            case "Damage":
                damageMultiplier *= buff.value;
                break;

            case "CritChance":
                critChanceOffset += buff.value;
                break;

            case "FireRate":
                fireRateMultiplier *= buff.value; // Ví dụ: 0.8f để giảm cooldown bắn (bắn nhanh hơn)
                break;

            case "CoinMultiplier":
                coinGainMultiplier *= buff.value;
                break;

            default:
                Debug.LogWarning($"[PlayerBuffManager] Loại Buff không xác định: {buff.buffType}");
                break;
        }
    }
}
