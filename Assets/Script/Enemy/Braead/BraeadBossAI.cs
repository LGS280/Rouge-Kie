using System.Collections;
using UnityEngine;

/// <summary>
/// Trí tuệ nhân tạo (AI) cho Boss Braead:
/// - Không đi tuần (No Wander), luôn khóa mục tiêu và ngắm vào Player
/// - Giữ khoảng cách chiến đấu (Kiting / Strafe), không bu sát người chơi
/// - Tự động kích hoạt chiêu nộ Laser 360 độ khi máu <= 50% (lướt về giữa phòng xả chiêu)
/// - Bước vào Phase 2 (Hóa nộ): Tăng 10% tốc độ đánh và xả đạn 10 viên tỏa tròn 360 độ
/// </summary>
public class BraeadBossAI : MonoBehaviour
{
    public enum BossState
    {
        Inactive,
        Combat,
        PreparingUltimate,
        CastingUltimate
    }

    [Header("Trạng thái hiện tại của Boss")]
    public BossState currentState = BossState.Combat;

    [Header("Cấu hình cự ly & Tốc độ")]
    [Tooltip("Khoảng cách chiến đấu lý tưởng để giữ cự ly với người chơi")]
    public float preferredDistance = 5.0f;
    public float moveSpeed = 2.2f;
    public float attackCooldown = 3.0f;
    public float detectRange = 14.0f;

    [Header("Chiêu nộ Laser (HP <= 50%)")]
    public float ultimateMoveSpeed = 4.0f;
    public float laserSweepDuration = 3.5f;

    [Header("Liên kết Component")]
    public Transform targetPlayer;
    public BraeadWeaponAim weaponAim;

    private Rigidbody2D rb;
    private Animator animator;
    private MobHealth mobHealth;
    private MobFlash mobFlash;
    private SpriteRenderer spriteRenderer;
    private MobNetworkIdentity mobNetworkIdentity;

    private float nextAttackTime = 0f;
    private bool isEnraged = false;
    private bool hasTriggeredUltimate = false;
    private bool hasShownBossBar = false;

    private Vector2 roomCenter;
    private float strafeDirection = 1f;
    private float strafeChangeTimer = 0f;
    private const float StrafeInterval = 2.5f;

    private bool isHost = true;
    [HideInInspector] public RoomController myRoom;

    public void SetRoom(RoomController room)
    {
        myRoom = room;
        if (myRoom != null)
        {
            roomCenter = myRoom.transform.position;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        mobHealth = GetComponent<MobHealth>();
        mobFlash = GetComponent<MobFlash>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mobNetworkIdentity = GetComponent<MobNetworkIdentity>();

        if (weaponAim == null)
        {
            weaponAim = GetComponentInChildren<BraeadWeaponAim>();
            if (weaponAim == null)
            {
                weaponAim = FindFirstObjectByType<BraeadWeaponAim>();
            }
        }

        roomCenter = transform.position; // Vị trí xuất phát mặc định là tâm
    }

    private void Start()
    {
        // 1. Đồng bộ chế độ nhiều người chơi
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        isHost = !isMultiplayer || (NetworkManager.Instance.UserRole == "Host");

        if (!isHost && rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 2. Nạp chỉ số từ Database (EnemyConfig của Braead)
        ApplyConfigFromDb();

        // 3. Khóa ngay mục tiêu người chơi
        FindNearestPlayer();
    }

    private void ApplyConfigFromDb()
    {
        if (GameConfigManager.Instance == null) return;

        EnemyConfig eConfig = GameConfigManager.Instance.GetEnemyConfig("Braead");
        if (eConfig == null) eConfig = GameConfigManager.Instance.GetEnemyConfig(gameObject.name);

        if (eConfig != null)
        {
            if (eConfig.moveSpeed > 0) moveSpeed = eConfig.moveSpeed;
            if (eConfig.attackSpeed > 0) attackCooldown = eConfig.attackSpeed;
        }
        else
        {
            // Fallback: Lấy FireRate từ vũ khí ID 22
            if (GameConfigManager.Instance.WeaponDb.TryGetValue(22, out WeaponConfig wConfig))
            {
                if (wConfig.fireRate > 0) attackCooldown = wConfig.fireRate;
            }
        }
    }

    private void Update()
    {
        // 1. Kiểm tra trạng thái chết
        if (mobHealth != null && mobHealth.isDead)
        {
            HandleDeath();
            return;
        }

        // 2. Luôn tìm và bám theo người chơi gần nhất
        FindNearestPlayer();

        // 3. Kích hoạt thanh máu Boss lớn trên màn hình khi chạm mặt người chơi
        if (!hasShownBossBar && targetPlayer != null && BossHealthBarUI.Instance != null && mobHealth != null)
        {
            BossHealthBarUI.Instance.ShowBossBar("BRAEAD", mobHealth);
            hasShownBossBar = true;
        }

        // 4. Kiểm tra ngưỡng máu <= 50% để kích hoạt Chiêu Nộ Laser đúng 1 lần
        if (!hasTriggeredUltimate && mobHealth != null && mobHealth.maxHealth > 0)
        {
            if (mobHealth.CurrentHealth <= mobHealth.maxHealth * 0.5f)
            {
                hasTriggeredUltimate = true;
                StartCoroutine(UltimateSequenceRoutine());
                return;
            }
        }

        // 5. Nếu đang trong chuỗi chiêu nộ, để Coroutine điều khiển
        if (currentState == BossState.PreparingUltimate || currentState == BossState.CastingUltimate)
        {
            return;
        }

        // 6. Xử lý quay mặt Boss và ngắm vũ khí luôn luôn hướng vào Player
        if (targetPlayer != null)
        {
            UpdateBossFacing(targetPlayer.position.x - transform.position.x);

            if (weaponAim != null)
            {
                weaponAim.AimAtTarget(targetPlayer);
            }
        }

        // 7. Xử lý di chuyển giữ khoảng cách (Kiting) và xả đạn
        if (isHost && currentState == BossState.Combat)
        {
            HandleCombatMovementAndAttack();
        }
    }

    /// <summary>
    /// Cơ chế di chuyển Giữ khoảng cách (Kiting / Strafe) và nhịp xả đạn
    /// </summary>
    private void HandleCombatMovementAndAttack()
    {
        if (targetPlayer == null)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isMoving", false);
            return;
        }

        float dist = Vector2.Distance(transform.position, targetPlayer.position);
        Vector2 toPlayer = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;

        // Cập nhật hướng bay lượn vòng cung (Strafe) theo chu kỳ
        strafeChangeTimer += Time.deltaTime;
        if (strafeChangeTimer >= StrafeInterval)
        {
            strafeDirection = (Random.value > 0.5f) ? 1f : -1f;
            strafeChangeTimer = 0f;
        }

        Vector2 orbitDir = new Vector2(-toPlayer.y, toPlayer.x) * strafeDirection;
        Vector2 finalMoveDir = Vector2.zero;

        if (dist > preferredDistance + 1.2f)
        {
            // Player ở quá xa -> Bay lại gần + lượn nhẹ
            finalMoveDir = (toPlayer * 0.75f + orbitDir * 0.25f).normalized;
        }
        else if (dist < preferredDistance - 1.0f)
        {
            // Player áp sát quá gần -> Bay lùi lại (Kite) + lượn nhẹ
            finalMoveDir = (-toPlayer * 0.8f + orbitDir * 0.2f).normalized;
        }
        else
        {
            // Trong cự ly lý tưởng -> Bay lượn vòng cung quanh Player
            finalMoveDir = orbitDir;
        }

        // Kiểm tra tránh đâm vào tường / chướng ngại vật
        Vector2 obstacleAvoidance = GetObstacleAvoidanceForce(finalMoveDir);
        finalMoveDir = (finalMoveDir + obstacleAvoidance).normalized;

        float currentSpeed = isEnraged ? moveSpeed * 1.1f : moveSpeed;
        if (rb != null)
        {
            rb.linearVelocity = finalMoveDir * currentSpeed;
        }

        if (animator != null)
        {
            animator.SetBool("isMoving", finalMoveDir.sqrMagnitude > 0.01f);
        }

        // Xử lý bắn đạn thường 10 viên
        if (Time.time >= nextAttackTime)
        {
            PerformNormalAttack();
        }
    }

    private void PerformNormalAttack()
    {
        if (targetPlayer == null) return;

        if (weaponAim != null)
        {
            weaponAim.ShootNormalBarrage(targetPlayer.position, isEnraged);
        }

        // Đồng bộ mạng trong chế độ Co-op
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer && isHost && mobNetworkIdentity != null && !string.IsNullOrEmpty(mobNetworkIdentity.networkId))
        {
            NetworkManager.Instance.SendBossAttack(mobNetworkIdentity.networkId, targetPlayer.position.x, targetPlayer.position.y);
        }

        float cooldown = isEnraged ? (attackCooldown * 0.9f) : attackCooldown;
        nextAttackTime = Time.time + cooldown;
    }

    /// <summary>
    /// Chuỗi kích hoạt Chiêu Nộ Laser 360 độ khi HP <= 50%
    /// </summary>
    private IEnumerator UltimateSequenceRoutine()
    {
        currentState = BossState.PreparingUltimate;

        // 1. Lướt nhanh về trung tâm phòng
        Vector2 center = (myRoom != null) ? (Vector2)myRoom.transform.position : roomCenter;
        float timeout = 2.5f;
        float timer = 0f;

        while (Vector2.Distance(transform.position, center) > 0.5f && timer < timeout)
        {
            timer += Time.deltaTime;
            Vector2 dirToCenter = (center - (Vector2)transform.position).normalized;
            if (rb != null) rb.linearVelocity = dirToCenter * ultimateMoveSpeed;
            if (animator != null) animator.SetBool("isMoving", true);
            yield return null;
        }

        // 2. Đến tâm phòng -> Dừng lại và bước vào trạng thái sạc chiêu
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (animator != null) animator.SetBool("isMoving", false);
        currentState = BossState.CastingUltimate;

        // 3. Thời gian báo hiệu (Wind-up / Telegraph): 1.2 giây
        yield return new WaitForSeconds(1.2f);

        // 4. Kích hoạt quét tia Laser 360 độ
        if (weaponAim != null)
        {
            yield return StartCoroutine(weaponAim.LaserSweepRoutine(laserSweepDuration));
        }
        else
        {
            yield return new WaitForSeconds(laserSweepDuration);
        }

        // 5. Kết thúc Laser -> Bước vào Phase 2 (Hóa nộ): Tăng 10% tốc độ đánh và di chuyển
        isEnraged = true;
        nextAttackTime = Time.time + 0.5f; // Bắn đợt đạn tỏa tròn ngay sau khi laser tắt
        currentState = BossState.Combat;
        Debug.Log("[Braead Boss] Kết thúc chiêu Laser! Kích hoạt Phase 2: Hóa Nộ (Enraged)");
    }

    private Vector2 GetObstacleAvoidanceForce(Vector2 currentDir)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, currentDir, 1.2f, LayerMask.GetMask("Obstacle", "Wall", "Door"));
        if (hit.collider != null)
        {
            return Vector2.Reflect(currentDir, hit.normal) * 0.6f;
        }
        return Vector2.zero;
    }

    private void UpdateBossFacing(float dirX)
    {
        if (Mathf.Abs(dirX) > 0.05f)
        {
            if (spriteRenderer != null)
            {
                // Lật sprite X mà không làm đảo lộn hệ toạ độ các object con
                spriteRenderer.flipX = (dirX < 0);
            }
            else
            {
                Vector3 scale = transform.localScale;
                scale.x = (dirX < 0) ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
    }

    private void FindNearestPlayer()
    {
        float shortestDistance = Mathf.Infinity;
        Transform nearestPlayer = null;

        GameObject localPlayer = GameObject.FindGameObjectWithTag("Player");
        if (localPlayer != null)
        {
            RookieHealth health = localPlayer.GetComponent<RookieHealth>();
            if (health == null || !health.isDead)
            {
                shortestDistance = Vector2.Distance(transform.position, localPlayer.transform.position);
                nearestPlayer = localPlayer.transform;
            }
        }

        RemotePlayerController[] remotePlayers = Object.FindObjectsByType<RemotePlayerController>(FindObjectsSortMode.None);
        foreach (var rpc in remotePlayers)
        {
            if (rpc != null && rpc.gameObject != null && !rpc.isDead)
            {
                float dist = Vector2.Distance(transform.position, rpc.transform.position);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    nearestPlayer = rpc.transform;
                }
            }
        }

        targetPlayer = nearestPlayer;
    }

    private void HandleDeath()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("isMoving", false);
            animator.SetTrigger("Die");
        }

        if (weaponAim != null)
        {
            weaponAim.gameObject.SetActive(false);
            weaponAim.enabled = false;
        }

        enabled = false;
    }
}
