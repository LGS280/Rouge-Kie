using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance;

    [Header("Attack Speed")]
    public float attackSpeedMultiplier = 1f; // 1 = bình thường, 3 = nhanh gấp 3

    void Awake()
    {
        Instance = this;
    }

    public void ApplyAttackSpeedBuff(float multiplier, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(AttackSpeedBuffRoutine(multiplier, duration));
    }

    System.Collections.IEnumerator AttackSpeedBuffRoutine(float multiplier, float duration)
    {
        attackSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        attackSpeedMultiplier = 1f;
    }
}