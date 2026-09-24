using UnityEngine;

public class RemotePlayerController : MonoBehaviour
{
    public string connectionId;
    public Vector3 targetPosition;
    public float targetWeaponAngle;

    private Vector3 currentVelocity;
    private Vector3 lastTargetPos;
    private float lastPacketTime;
    private Vector3 estimatedVelocity;

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        targetPosition = transform.position;
        lastTargetPos = transform.position;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Cập nhật vị trí mục tiêu từ mạng SignalR với tính toán vận tốc ước tính (Velocity Extrapolation)
    /// </summary>
    public void SetNewTargetPosition(Vector3 newPos)
    {
        float dt = Time.time - lastPacketTime;
        if (dt > 0.001f && dt < 0.3f)
        {
            estimatedVelocity = (newPos - lastTargetPos) / dt;
        }
        else
        {
            estimatedVelocity = Vector3.zero;
        }

        lastTargetPos = newPos;
        targetPosition = newPos;
        lastPacketTime = Time.time;
    }

    public bool isDead = false;
    public Color assignedRingColor = new Color(0f, 0.75f, 1f, 1f); // Mặc định Xanh Dương cho Player 2

    public void DieRemotePlayer()
    {
        isDead = true;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        Transform shadowPos = transform.Find("Shadow");
        if (shadowPos != null) shadowPos.gameObject.SetActive(false);

        Transform ringPos = transform.Find("Player_Ring");
        if (ringPos == null) ringPos = transform.Find("Ring");
        if (ringPos == null) ringPos = transform.Find("PlayerRing");
        if (ringPos != null) ringPos.gameObject.SetActive(false);

        if (animator != null) animator.SetTrigger("die");
    }

    public void ReviveRemotePlayer()
    {
        isDead = false;
        StopAllCoroutines();

        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr != null) sr.color = Color.white;
        }

        Transform shadowPos = transform.Find("Shadow");
        if (shadowPos != null) shadowPos.gameObject.SetActive(true);

        Transform ringPos = transform.Find("Player_Ring");
        if (ringPos == null) ringPos = transform.Find("Ring");
        if (ringPos == null) ringPos = transform.Find("PlayerRing");
        if (ringPos != null)
        {
            ringPos.gameObject.SetActive(true);
            SpriteRenderer ringSr = ringPos.GetComponent<SpriteRenderer>();
            if (ringSr != null) ringSr.color = assignedRingColor; // Khôi phục đúng màu ban đầu của đồng đội
        }

        if (animator != null)
        {
            animator.ResetTrigger("die");
            animator.Rebind();
            animator.Update(0f);
            animator.SetFloat("Speed", 0f);
        }
    }

    void Update()
    {
        if (isDead) return;

        // 🎯 1. DỰ ĐOÁN VỊ TRÍ VÀ NỘI SUY MƯỢT MÀ 60 FPS (Extrapolation + SmoothDamp)
        Vector3 predictedTarget = targetPosition + (estimatedVelocity * 0.033f);
        transform.position = Vector3.SmoothDamp(transform.position, predictedTarget, ref currentVelocity, 0.04f);

        // Teleport tức thì nếu đứt đoạn quá xa (> 4.0m)
        if (Vector3.Distance(transform.position, targetPosition) > 4.0f)
        {
            transform.position = targetPosition;
            currentVelocity = Vector3.zero;
            estimatedVelocity = Vector3.zero;
        }

        // Cập nhật hoạt ảnh di chuyển
        if (animator != null)
        {
            float speedParam = (currentVelocity.sqrMagnitude > 0.01f || estimatedVelocity.sqrMagnitude > 0.01f) ? 1f : 0f;
            animator.SetFloat("Speed", speedParam);
        }

        // 🎯 2. XOAY SÚNG VÀ LẬT SPRITE REMOTE PLAYER MƯỢT MÀ 60 FPS (Quaternion.Slerp 30f)
        Transform handPos = transform.Find("Hand_Position");
        if (handPos != null)
        {
            float normAngle = targetWeaponAngle;
            if (normAngle > 180f) normAngle -= 360f;
            if (normAngle < -180f) normAngle += 360f;

            Quaternion targetRot = Quaternion.Euler(0, 0, normAngle);
            handPos.rotation = Quaternion.Slerp(handPos.rotation, targetRot, Time.deltaTime * 30f);

            bool isFacingLeft = (normAngle > 90f || normAngle < -90f);
            if (spriteRenderer != null) spriteRenderer.flipX = isFacingLeft;
            handPos.localScale = new Vector3(1f, isFacingLeft ? -1f : 1f, 1f);
        }
    }
}
