using UnityEngine;
using UnityEngine.InputSystem;

public class SpeedSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    [SerializeField] float buffMultiplier = 2f;  // 200% tăng = x3 tốc độ
    [SerializeField] float buffDuration = 5f;    // buff kéo dài 5 giây
    [SerializeField] float cooldown = 15f;       // chờ 15 giây để dùng lại
    [SerializeField] KeyCode skillKey = KeyCode.Q;

    float cooldownTimer = 0f;
    bool isReady => cooldownTimer <= 0f;

    private PlayerController playerController;
    void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        bool isSkillKeyPressed = false;

        if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
        {
            if (Gamepad.current != null && Gamepad.current.yButton.wasPressedThisFrame)
            {
                isSkillKeyPressed = true;
            }
        }
        else
        {
            if (Input.GetKeyDown(skillKey))
            {
                isSkillKeyPressed = true;
            }
        }

        // Kích hoạt skill nếu thỏa mãn điều kiện bấm nút và skill đã hồi chiêu xong
        if (isSkillKeyPressed && isReady)
        {
            ActivateSkill();
        }
    }

    void ActivateSkill()
    {
        PlayerStats.Instance.ApplyAttackSpeedBuff(buffMultiplier, buffDuration);
        cooldownTimer = cooldown;
        Debug.Log("Skill activated! Attack speed x" + buffMultiplier + " for " + buffDuration + "s");
    }

    // Dùng để hiện UI cooldown nếu cần sau này
    public float GetCooldownPercent() => cooldownTimer / cooldown;
}