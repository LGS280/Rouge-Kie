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

    void Update()
    {
        if (isDead) return;

        // Tính khoảng cách thay đổi
        float dist = Vector3.Distance(transform.position, targetPosition);
        
        // Lerp mượt mà tới vị trí mục tiêu (Tốc độ nội suy 15f để đuổi kịp)
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
        
        // Nếu khoảng cách còn xa hơn ngưỡng nhỏ, coi như đang di chuyển
        if (animator != null)
        {
            // SetFloat "Speed" vì Animator sử dụng Speed > 0.01f để chuyển từ Idle sang Run
            float speedParam = dist > 0.02f ? 1f : 0f;
            animator.SetFloat("Speed", speedParam);
        }
    }
}
