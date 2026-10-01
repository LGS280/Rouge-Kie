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

    private const float MAX_ESTIMATED_SPEED = 9.5f; // Kẹp trần vận tốc ước tính (tốc độ chạy chuẩn ~8f)

    /// <summary>
    /// Cập nhật vị trí mục tiêu từ mạng SignalR với cơ chế kẹp trần vận tốc và làm mượt chống spike giật lag
    /// </summary>
    public void SetNewTargetPosition(Vector3 newPos)
    {
        float dt = Time.time - lastPacketTime;
        if (dt > 0.005f && dt < 0.4f)
        {
            Vector3 rawVel = (newPos - targetPosition) / dt;
            // 🎯 Kẹp trần vận tốc tối đa: Triệt tiêu hoàn toàn cú nhảy 30-50m/s khi 2 gói tin đến dồn cục
            rawVel = Vector3.ClampMagnitude(rawVel, MAX_ESTIMATED_SPEED);
            // 🎯 Làm mượt vận tốc qua bộ lọc Exponential Smoothing
            estimatedVelocity = Vector3.Lerp(estimatedVelocity, rawVel, 0.45f);
        }
        else if (dt >= 0.4f)
        {
            // Quá lâu không có gói tin mới -> người chơi đã dừng hẳn hoặc tạm ngưng
            estimatedVelocity = Vector3.zero;
        }

        lastTargetPos = targetPosition;
        targetPosition = newPos;
        lastPacketTime = Time.time;
    }

    public bool isDead = false;
    public Color assignedRingColor = new Color(0f, 0.75f, 1f, 1f); // Mặc định Xanh Dương cho Player 2

    public void DieRemotePlayer()
    {
        isDead = true;
        currentVelocity = Vector3.zero;
        estimatedVelocity = Vector3.zero;

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
        currentVelocity = Vector3.zero;
        estimatedVelocity = Vector3.zero;
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

        float dist = Vector3.Distance(transform.position, targetPosition);

        // 🎯 1. NỘI SUY VỊ TRÍ MƯỢT MÀ 60 FPS (Adaptive SmoothDamp + Deadzone chống rung)
        if (dist > 4.5f)
        {
            // Teleport tức thì nếu khoảng cách quá xa (chuyển phòng / respawn)
            transform.position = targetPosition;
            currentVelocity = Vector3.zero;
            estimatedVelocity = Vector3.zero;
        }
        else if (dist < 0.015f)
        {
            // Vùng chết tĩnh (Deadzone): Đứng yên hoàn toàn khi đã tới sát đích, chống rung vi mô pixel
            transform.position = targetPosition;
            currentVelocity = Vector3.zero;
        }
        else
        {
            // Giảm dần vận tốc nếu quá 80ms không có gói tin mới (người chơi dừng lại)
            if (Time.time - lastPacketTime > 0.08f)
            {
                estimatedVelocity = Vector3.Lerp(estimatedVelocity, Vector3.zero, Time.deltaTime * 12f);
            }

            // Chỉ bù trừ bước chạy ngắn nếu đang di chuyển thực sự, không phóng quá đà khi dừng lại
            float timeSincePacket = Mathf.Clamp(Time.time - lastPacketTime, 0f, 0.033f);
            Vector3 targetWithLookahead = targetPosition;
            if (estimatedVelocity.sqrMagnitude > 0.05f && dist > 0.05f)
            {
                targetWithLookahead += estimatedVelocity * timeSincePacket * 0.5f;
            }

            // Thời gian mượt thích nghi: gần đích êm ái (0.06s), xa hơn bắt kịp nhanh hơn (0.04s)
            float smoothTime = Mathf.Lerp(0.06f, 0.04f, Mathf.Clamp01(dist / 2.0f));
            transform.position = Vector3.SmoothDamp(transform.position, targetWithLookahead, ref currentVelocity, smoothTime, 14f, Time.deltaTime);
        }

        // Cập nhật hoạt ảnh di chuyển
        if (animator != null)
        {
            float speedParam = (currentVelocity.sqrMagnitude > 0.04f || (dist > 0.05f && estimatedVelocity.sqrMagnitude > 0.04f)) ? 1f : 0f;
            animator.SetFloat("Speed", speedParam);
        }

        // 🎯 2. XOAY SÚNG VÀ LẬT SPRITE REMOTE PLAYER MƯỢT MÀ 60 FPS (Quaternion.Slerp 25f)
        Transform handPos = transform.Find("Hand_Position");
        if (handPos != null)
        {
            float normAngle = targetWeaponAngle;
            if (normAngle > 180f) normAngle -= 360f;
            if (normAngle < -180f) normAngle += 360f;

            Quaternion targetRot = Quaternion.Euler(0, 0, normAngle);
            handPos.rotation = Quaternion.Slerp(handPos.rotation, targetRot, Time.deltaTime * 25f);

            bool isFacingLeft = (normAngle > 90f || normAngle < -90f);
            if (spriteRenderer != null) spriteRenderer.flipX = isFacingLeft;
            handPos.localScale = new Vector3(1f, isFacingLeft ? -1f : 1f, 1f);
        }
    }
}
