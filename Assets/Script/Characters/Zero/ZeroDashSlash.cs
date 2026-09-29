using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DashSlashSkill : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key dashKey = Key.G;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 7f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float cooldown = 2f;
    [Tooltip("Tên Trigger trong Animator. Để trống nếu chưa có animation dash.")]
    [SerializeField] private string dashTrigger = "";

    [Header("Tắt script di chuyển khi lướt")]
    [SerializeField] private string movementScriptName = "PlayerController";
    [SerializeField] private Behaviour[] disableWhileDashing;

    [Header("Bất tử")]
    [SerializeField] private float invincibleDuration = 0.5f;

    [Header("Damage")]
    [SerializeField] private int damage = 60; // sát thương cố định
    [SerializeField] private float hitRadius = 7f;
    [SerializeField] private string enemyTag = "Enemy";
    [Tooltip("Tên hàm nhận sát thương trên script máu của quái (tham số int).")]
    [SerializeField] private string damageMethodName = "TakeDamage";

    [Header("Hiệu ứng chém lên quái")]
    [Tooltip("Có thể kéo MeleeSlashEffect vào để dùng tạm.")]
    [SerializeField] private GameObject slashVfxPrefab;
    [SerializeField] private float slashVfxLifetime = 1f;
    [Tooltip("Độ to của vệt chém. 2 = gấp đôi kích thước prefab.")]
    [SerializeField] private float slashVfxScale = 2f;

    [Header("SFX (âm thanh)")]
    [Tooltip("Kêu 1 lần lúc bắt đầu lướt. Để trống nếu không cần.")]
    [SerializeField] private AudioClip dashSfx;
    [Tooltip("Kêu mỗi lần chém trúng 1 quái. Để trống nếu không cần.")]
    [SerializeField] private AudioClip hitSfx;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Afterimage (bóng phía sau)")]
    [SerializeField] private float ghostInterval = 0.03f;
    [SerializeField] private float ghostLifetime = 0.25f;
    [SerializeField] private Color ghostColor = new Color(0.3f, 0.8f, 1f, 0.6f);

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator anim;
    private readonly List<Behaviour> toDisable = new List<Behaviour>();

    private bool isDashing;
    private float nextDashTime;
    private float invincibleUntil;
    private Vector2 lastMoveDir = Vector2.right;
    private Vector2 currentDashDir = Vector2.right;

    public bool IsDashing => isDashing;

    // Script máu của nhân vật kiểm tra biến này để bỏ qua sát thương
    public bool IsInvincible => Time.time < invincibleUntil;

    // NEW: các thuộc tính cho UI cooldown đọc
    public float CooldownDuration => cooldown;
    public float CooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
    public bool IsReady => !isDashing && CooldownRemaining <= 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        foreach (Behaviour b in disableWhileDashing)
        {
            if (b != null && !toDisable.Contains(b)) toDisable.Add(b);
        }

        if (!string.IsNullOrEmpty(movementScriptName))
        {
            Behaviour movement = GetComponent(movementScriptName) as Behaviour;
            if (movement != null)
            {
                if (!toDisable.Contains(movement)) toDisable.Add(movement);
            }
            else
            {
                Debug.LogWarning($"[Dash] Không tìm thấy script '{movementScriptName}' trên {name}.");
            }
        }
    }

    private void Update()
    {
        if (!isDashing && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            lastMoveDir = rb.linearVelocity.normalized;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb[dashKey].wasPressedThisFrame)
        {
            TryDash();
        }
    }

    // Chuột phải vào tên component trong Inspector (lúc đang Play) -> Test Dash
    [ContextMenu("Test Dash")]
    public void TryDash()
    {
        if (!Application.isPlaying) return;
        if (isDashing || Time.time < nextDashTime) return;
        StartCoroutine(DashRoutine());
    }

    private Vector2 GetDashDirection()
    {
        Camera cam = Camera.main;
        if (cam != null && Mouse.current != null)
        {
            Vector3 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 toMouse = (Vector2)mouseWorld - (Vector2)transform.position;
            if (toMouse.sqrMagnitude > 0.01f) return toMouse.normalized;
        }
        return lastMoveDir;
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        nextDashTime = Time.time + cooldown;
        invincibleUntil = Time.time + invincibleDuration;

        Vector2 dir = GetDashDirection();
        currentDashDir = dir;

        if (anim != null && !string.IsNullOrEmpty(dashTrigger)) anim.SetTrigger(dashTrigger);

        PlaySfx(dashSfx);

        foreach (Behaviour b in toDisable) b.enabled = false;

        float speed = dashDistance / dashDuration;

        // Mỗi kẻ địch chỉ bị trúng 1 lần trong 1 lần lướt (tính theo cả con quái,
        // không tính theo từng collider, để quái nhiều collider không bị chém nhiều lần)
        HashSet<GameObject> alreadyHit = new HashSet<GameObject>();
        float elapsed = 0f;
        float ghostTimer = 0f;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = dir * speed;

            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f)
            {
                SpawnGhost();
                ghostTimer = ghostInterval;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag(enemyTag)) continue;

                GameObject targetRoot = hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.gameObject
                    : hit.gameObject;
                if (!alreadyHit.Add(targetRoot)) continue;

                ApplyDamage(hit);
                SpawnSlashVfx(hit.bounds.center);
                PlaySfx(hitSfx);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        foreach (Behaviour b in toDisable) b.enabled = true;

        isDashing = false;
    }

    private void ApplyDamage(Collider2D target)
    {
        Debug.Log($"[Dash] Trúng: {target.name} - sát thương {damage}");
        target.SendMessageUpwards(damageMethodName, damage, SendMessageOptions.DontRequireReceiver);
    }

    // Phát âm thanh tại vị trí camera để nghe rõ đều, không bị nhỏ theo khoảng cách
    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, sfxVolume);
    }

    private void SpawnGhost()
    {
        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = sr.sprite;
        ghostSr.flipX = sr.flipX;
        ghostSr.flipY = sr.flipY;
        ghostSr.color = ghostColor;
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder = sr.sortingOrder - 1;

        ghost.AddComponent<DashGhostFade>().Init(ghostLifetime);
    }

    private void SpawnSlashVfx(Vector3 position)
    {
        if (slashVfxPrefab == null) return;

        // Vệt chém xoay theo hướng lướt, lệch ngẫu nhiên nhẹ cho mỗi nhát khác nhau
        float angle = Mathf.Atan2(currentDashDir.y, currentDashDir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle + Random.Range(-30f, 30f));

        GameObject vfx = Instantiate(slashVfxPrefab, position, rot);
        vfx.transform.localScale *= slashVfxScale;
        Destroy(vfx, slashVfxLifetime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}

// Làm bóng mờ dần rồi tự huỷ
public class DashGhostFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private float lifetime;
    private float timer;
    private Color startColor;

    public void Init(float life)
    {
        lifetime = life;
        sr = GetComponent<SpriteRenderer>();
        startColor = sr.color;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        sr.color = new Color(startColor.r, startColor.g, startColor.b,
            Mathf.Lerp(startColor.a, 0f, timer / lifetime));

        if (timer >= lifetime) Destroy(gameObject);
    }
}