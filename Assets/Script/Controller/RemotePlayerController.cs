using UnityEngine;

public class RemotePlayerController : MonoBehaviour
{
    public string connectionId;
    public Vector3 targetPosition;
    private Animator animator;

    // We can also store SpriteRenderer here if we need to flipX based on movement, 
    // but the flipX will be handled via weapon angle from MultiplayerSyncManager.
    // However, if the player moves without shooting/aiming, maybe they don't flip? 
    // Usually weapon aiming overrides movement direction for flipX in top-down shooters.
    
    void Start()
    {
        targetPosition = transform.position;
        animator = GetComponent<Animator>();
    }

    public bool isDead = false;

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

        if (animator != null)
        {
            animator.SetTrigger("die");
        }
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
            if (ringSr != null) ringSr.color = Color.green;
        }

        if (animator != null)
        {
            animator.ResetTrigger("die");
            animator.Rebind();
            animator.Update(0f);
            animator.SetFloat("Speed", 0f);
        }
    }

    private Vector3 currentVelocity;
    private float smoothTime = 0.05f; // 50ms smooth damp bọc lót thời gian giữa các gói tin SignalR

    void Update()
    {
        if (isDead) return;

        // Nội suy mượt mà 60 FPS bằng SmoothDamp không bị khựng giữa các gói tin mạng
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

        // Teleport tức thì nếu khoảng cách bị đứt đoạn quá xa (> 4.0m)
        if (Vector3.Distance(transform.position, targetPosition) > 4.0f)
        {
            transform.position = targetPosition;
            currentVelocity = Vector3.zero;
        }

        if (animator != null)
        {
            float speedParam = currentVelocity.sqrMagnitude > 0.01f ? 1f : 0f;
            animator.SetFloat("Speed", speedParam);
        }
    }
}
