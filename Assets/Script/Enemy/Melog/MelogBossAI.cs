using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script trí tuệ nhân tạo (AI) dành riêng cho Mini Boss Melog (2 tay 2 súng Gatling).
/// Tự động xoay thân Rotation Y = 0 (bên phải) và Rotation Y = 180 (bên trái).
/// Tách biệt hoàn toàn 100% với quái thường MobAI.cs.
/// </summary>
public class MelogBossAI : MonoBehaviour
{
    public enum BossState { Idle, Wander, Chase, Attack }

    [Header("Trạng thái hiện tại của Mini Boss")]
    public BossState currentState = BossState.Idle;

    [Header("Chỉ số di chuyển & Tầm quét")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 2.8f;
    public float detectRange = 7.0f;
    public float attackRange = 5.0f;
    private float nextAttackTime = 0f;

    [Header("Liên kết Component")]
    public Transform targetPlayer;
    private Rigidbody2D rb;
    private Animator animator;
    private MobHealth mobHealth;
    private MelogWeaponAim melogWeaponAim;
    private MobNetworkIdentity mobNetworkIdentity;

    private Vector2 wanderTargetPos;
    private float wanderTimer = 0f;
    private float changeWanderDirInterval = 3f;

    private bool isRoomActivated = false;
    private bool isHost = true;

    // Multiplayer smoothing variables
    private Vector2 networkTargetPos;
    private Vector2 lastNetworkTargetPos;
    private Vector3 mobNetworkVelocity;
    private Vector2 estimatedMobVelocity;
    private float lastMobPacketTime;
    private bool hasFirstNetworkPos = false;
    private float lastNetworkSyncTime = 0f;
    private float networkSyncInterval = 0.05f; // 20 FPS

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        mobHealth = GetComponent<MobHealth>();
        melogWeaponAim = GetComponent<MelogWeaponAim>();
        mobNetworkIdentity = GetComponent<MobNetworkIdentity>();
    }

    private void Start()
    {
        if (melogWeaponAim != null)
        {
            melogWeaponAim.InitializeDualHandsAndWeapons();
        }

        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer)
        {
            isHost = (NetworkManager.Instance.UserRole == "Host");
        }
        else
        {
            isHost = true;
        }

        if (!isHost && rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        FindNearestPlayer();
        GetNewWanderTarget();
    }

    private void Update()
    {
        if (mobHealth != null && mobHealth.isDead)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isMoving", false);
            if (melogWeaponAim != null) melogWeaponAim.DestroyWeaponsOnDeath();
            return;
        }

        if (isHost)
        {
            // Host: Đồng bộ vị trí cho Client 20 FPS
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && mobNetworkIdentity != null && !string.IsNullOrEmpty(mobNetworkIdentity.networkId))
            {
                if (Time.time - lastNetworkSyncTime >= networkSyncInterval)
                {
                    NetworkManager.Instance.SendEnemyPosition(mobNetworkIdentity.networkId, transform.position.x, transform.position.y);
                    lastNetworkSyncTime = Time.time;
                }
            }

            FindNearestPlayer();

            if (targetPlayer != null)
            {
                // 🎯 LUÔN TỰ ĐỘNG QUAY MẶT THÂN (Rotation Y = 0 / 180) VỀ PHÍA PLAYER
                UpdateBossFacing(targetPlayer.position.x - transform.position.x);
            }

            if (!isRoomActivated)
            {
                // 🎯 TỰ ĐỘNG MỞ KHÓA KHI PLAYER LẠI GẦN: Hỗ trợ kéo Melog thủ công vào Scene test độc lập
                if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
                {
                    isRoomActivated = true;
                }
                else
                {
                    currentState = BossState.Idle;
                    if (animator != null) animator.SetBool("isMoving", false);
                    return;
                }
            }

            switch (currentState)
            {
                case BossState.Idle:
                    MonitorIdleState();
                    break;
                case BossState.Wander:
                    MonitorWanderState();
                    break;
                case BossState.Chase:
                    MonitorChaseState();
                    break;
                case BossState.Attack:
                    MonitorAttackState();
                    break;
            }
        }
        else
        {
            // Client: Tìm Player để ngắm bắn hiển thị mượt 60 FPS
            FindNearestPlayer();

            // Client: Kích hoạt hiển thị hoạt ảnh tấn công & bão đạn 2 súng khi áp sát Player 2
            if (targetPlayer != null && melogWeaponAim != null)
            {
                float dist = Vector2.Distance(transform.position, targetPlayer.position);
                if (dist <= attackRange && Time.time >= nextAttackTime)
                {
                    if (animator != null) animator.SetTrigger("attack");
                    melogWeaponAim.FireBothGuns(targetPlayer.position, 0);

                    float dbFireRate = GetWeaponFireRateFromDb();
                    nextAttackTime = Time.time + dbFireRate;
                }
            }

            // Client: Nhận vị trí nội suy mượt mà SmoothDamp từ Host
            if (hasFirstNetworkPos)
            {
                Vector3 predictedMobPos = (Vector3)networkTargetPos + ((Vector3)estimatedMobVelocity * 0.033f);
                transform.position = Vector3.SmoothDamp(transform.position, predictedMobPos, ref mobNetworkVelocity, 0.04f);

                if (Vector3.Distance(transform.position, networkTargetPos) > 3.5f)
                {
                    transform.position = networkTargetPos;
                    mobNetworkVelocity = Vector3.zero;
                    estimatedMobVelocity = Vector2.zero;
                }

                bool isMoving = mobNetworkVelocity.sqrMagnitude > 0.01f || estimatedMobVelocity.sqrMagnitude > 0.01f;
                if (animator != null) animator.SetBool("isMoving", isMoving);

                float moveX = (mobNetworkVelocity.sqrMagnitude > 0.01f) ? mobNetworkVelocity.x : estimatedMobVelocity.x;
                UpdateBossFacing(moveX);
            }
        }
    }

    public void UpdateNetworkPosition(Vector2 newPos)
    {
        if (!hasFirstNetworkPos)
        {
            transform.position = newPos;
            lastNetworkTargetPos = newPos;
            hasFirstNetworkPos = true;
            lastMobPacketTime = Time.time;
        }
        else
        {
            float dt = Time.time - lastMobPacketTime;
            if (dt > 0.001f && dt < 0.3f)
            {
                estimatedMobVelocity = (newPos - lastNetworkTargetPos) / dt;
            }
            else
            {
                estimatedMobVelocity = Vector2.zero;
            }

            lastNetworkTargetPos = newPos;
            lastMobPacketTime = Time.time;
        }
        networkTargetPos = newPos;
    }

    private void FindNearestPlayer()
    {
        float shortestDistance = Mathf.Infinity;
        Transform nearestPlayer = null;

        GameObject localPlayerObj = GameObject.FindGameObjectWithTag("Player");
        if (localPlayerObj != null)
        {
            RookieHealth localHealth = localPlayerObj.GetComponent<RookieHealth>();
            if (localHealth == null || !localHealth.isDead)
            {
                shortestDistance = Vector2.Distance(transform.position, localPlayerObj.transform.position);
                nearestPlayer = localPlayerObj.transform;
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

    private void MonitorIdleState()
    {
        if (animator != null) animator.SetBool("isMoving", false);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (isRoomActivated)
        {
            if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
            {
                currentState = BossState.Chase;
            }
            else
            {
                currentState = BossState.Wander;
            }
        }
    }

    private void MonitorWanderState()
    {
        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
        {
            currentState = BossState.Chase;
            return;
        }

        wanderTimer += Time.deltaTime;
        if (wanderTimer >= changeWanderDirInterval)
        {
            GetNewWanderTarget();
            wanderTimer = 0f;
        }

        Vector2 moveDir = (wanderTargetPos - (Vector2)transform.position).normalized;
        rb.linearVelocity = moveDir * wanderSpeed;

        if (animator != null) animator.SetBool("isMoving", true);
        UpdateBossFacing(moveDir.x);

        if (Vector2.Distance(transform.position, wanderTargetPos) < 0.3f)
        {
            GetNewWanderTarget();
        }
    }

    private void GetNewWanderTarget()
    {
        Vector2 randomDir = Random.insideUnitCircle.normalized * Random.Range(2f, 4f);
        wanderTargetPos = (Vector2)transform.position + randomDir;
    }

    private void MonitorChaseState()
    {
        if (targetPlayer == null)
        {
            currentState = BossState.Wander;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, targetPlayer.position);

        // 🎯 PLAYER DI CHUYỂN RA XA NGOÀI TẦM NHÌN: BỎ CUỘC VÀ CHUYỂN VỀ WANDER
        if (distanceToPlayer > detectRange)
        {
            currentState = BossState.Wander;
            return;
        }

        // NẾU ĐÃ HẾT COOLDOWN 4S HỒI CHIÊU:
        if (Time.time >= nextAttackTime)
        {
            if (distanceToPlayer <= attackRange)
            {
                currentState = BossState.Attack;
                return;
            }

            Vector2 targetDir = (targetPlayer.position - transform.position).normalized;
            Vector2 separateForce = GetSeparationForce();
            Vector2 finalMoveDir = (targetDir + separateForce).normalized;

            rb.linearVelocity = finalMoveDir * chaseSpeed;
            if (animator != null) animator.SetBool("isMoving", true);
            UpdateBossFacing(targetPlayer.position.x - transform.position.x);
            return;
        }

        // NẾU ĐANG TRONG 4S HỒI CHIÊU: Di chuyển lượn vòng hông ngắm súng ở cự ly 5.0m
        if (distanceToPlayer < 5.0f)
        {
            Vector2 targetDir = (targetPlayer.position - transform.position).normalized;
            Vector2 retreatDir = -targetDir;
            Vector2 orbitDir = new Vector2(-targetDir.y, targetDir.x);
            Vector2 separateForce = GetSeparationForce();
            Vector2 finalMoveDir = (retreatDir * 0.5f + orbitDir * 0.5f + separateForce).normalized;

            rb.linearVelocity = finalMoveDir * (chaseSpeed * 0.6f);
            if (animator != null) animator.SetBool("isMoving", true);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isMoving", false);
        }

        UpdateBossFacing(targetPlayer.position.x - transform.position.x);
    }

    private void MonitorAttackState()
    {
        if (targetPlayer == null)
        {
            currentState = BossState.Wander;
            return;
        }

        UpdateBossFacing(targetPlayer.position.x - transform.position.x);

        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (animator != null) animator.SetBool("isMoving", false);

        // KÍCH HOẠT XẢ BÃỎ ĐẠN 2 NÒNG GATLING DUAL BARRAGE
        if (animator != null) animator.SetTrigger("attack");

        if (melogWeaponAim != null)
        {
            // Sát thương (5 HP) được MobBullet tự động đọc trực tiếp từ cột Damage bảng BulletConfigs DB
            melogWeaponAim.FireBothGuns(targetPlayer.position, 0);
        }

        // Thời gian hồi chiêu (FireRate = 3.0s) được tự động đọc trực tiếp từ cột FireRate bảng WeaponConfigs DB
        nextAttackTime = Time.time + GetWeaponFireRateFromDb();
        currentState = BossState.Chase;
    }

    private float GetWeaponFireRateFromDb()
    {
        if (melogWeaponAim != null && melogWeaponAim.leftWeaponInfo != null)
        {
            WeaponConfig config = melogWeaponAim.leftWeaponInfo.GetWeaponConfig();
            if (config != null && config.fireRate > 0)
            {
                return config.fireRate;
            }
        }
        return 3.0f; // Dự phòng mặc định nếu DB chưa load
    }

    private Vector2 GetSeparationForce()
    {
        Vector2 force = Vector2.zero;
        Collider2D[] nearbyMobs = Physics2D.OverlapCircleAll(transform.position, 0.8f, LayerMask.GetMask("Enemy"));
        int count = 0;

        foreach (var col in nearbyMobs)
        {
            if (col.gameObject != gameObject)
            {
                Vector2 pushDir = (transform.position - col.transform.position);
                float dist = pushDir.magnitude;
                if (dist > 0.05f && dist < 0.8f)
                {
                    force += pushDir.normalized * ((0.8f - dist) / 0.8f);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            force /= count;
            return force.normalized * 0.2f;
        }

        return Vector2.zero;
    }

    /// <summary>
    /// 🔥 BÀI MẸO ĐỈNH CAO: LẬT TOÀN BỘ KHỐI 3D SANG TRÁI BẰNG ROTATION Y = 180!
    /// Dính 100% 2 báng súng vào tay cầm không bao giờ bị lệch!
    /// </summary>
    private void UpdateBossFacing(float dirX)
    {
        if (Mathf.Abs(dirX) > 0.05f)
        {
            if (dirX < 0)
            {
                // Xoay sang TRÁI bằng Rotation Y = 180
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                // Xoay sang PHẢI bằng Rotation Y = 0
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
    }

    public void ActivateMob()
    {
        isRoomActivated = true;
    }

    public bool IsCombatActivated()
    {
        return isRoomActivated;
    }
}
