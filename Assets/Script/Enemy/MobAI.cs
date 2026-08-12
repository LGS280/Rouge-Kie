using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script quản lý AI Quái (FSM State Machine) - Nâng cấp 5 Trạng Thái Hoàn Hảo:
/// 1. Quái đi dạo tuần tra (Wander/Patrol) thong thả trong phòng khi chưa thấy Player.
/// 2. Quái bắn xa Kiting (Tự động đi lùi giữ cự cự 4.5m).
/// 3. Flocking Avoidance (Tự động đẩy nhau giàn hàng bao vây, không đè hình).
/// 4. Giữ nguyên 100% logic lật mặt flipX theo hướng nhìn.
/// 5. BẢO TOÀN 100% ĐỒNG BỘ MULTIPLAYER.
/// </summary>
public class MobAI : MonoBehaviour
{
    public enum EnemyState { Idle, Wander, Chase, Attack, Retreat }

    [Header("Trạng Thái AI")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Thông Số Di Chuyển")]
    public float chaseSpeed = 3.0f;
    public float detectRange = 7.0f;
    public float attackRange = 4.5f;
    public float attackCooldown = 2.0f;
    public int attackDamage = 10;
    public float wallPadding = 0.5f; // Khoảng cách đệm an toàn với tường

    [Header("Network Sync (Co-op Multiplayer)")]
    public bool isHost = true;

    // Lerp Variables cho Client
    private Vector2 networkTargetPos;
    private bool hasFirstNetworkPos = false;
    private Vector3 mobNetworkVelocity;
    private Vector2 lastNetworkTargetPos;
    private float lastMobPacketTime;
    private Vector2 estimatedMobVelocity;

    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public RoomController myRoom;
    private Rigidbody2D rb;
    private Animator animator;
    private MobHealth mobHealth;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private bool isRoomActivated = false;
    private Vector2 lastPos;
    private float nextAttackTime;
    private float retreatEndTime;

    // Các biến hỗ trợ đi dạo tuần tra (Wander)
    private Vector2 wanderTargetPos;
    private float nextWanderTimer;

    private MobWeaponAim mobWeaponAim;

    private MobNetworkIdentity mobNetworkIdentity;
    private float lastNetworkSyncTime = 0f;
    private float networkSyncInterval = 0.05f; // Gửi tọa độ quái mỗi 50ms (20Hz)

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mobHealth = GetComponent<MobHealth>();
        mobNetworkIdentity = GetComponent<MobNetworkIdentity>();
        originalScale = transform.localScale;

        mobWeaponAim = GetComponentInChildren<MobWeaponAim>();

        // 🎯 TỰ ĐỘNG ĐIỀU CHỈNH TẦM NHÌN, TẦM ĐÁNH VÀ ĐỌC FIRERATE TỪ DB WEAPONCONFIGS
        if (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null)
        {
            bool isMelee = mobWeaponAim.currentWeaponInfo.IsMelee;
            detectRange = isMelee ? 5.0f : 7.0f;
            attackRange = isMelee ? 0.9f : 4.0f; // 🗡️ Cận chiến đo từ mũi giáo tới Player (0.9m)

            WeaponConfig wConfig = mobWeaponAim.currentWeaponInfo.GetWeaponConfig();
            if (wConfig != null && wConfig.fireRate > 0)
            {
                attackCooldown = wConfig.fireRate;
            }
        }
        else
        {
            detectRange = 7.0f;
            attackRange = 4.0f;
        }

        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer)
        {
            isHost = (NetworkManager.Instance.UserRole == "Host");
        }
        if (!isHost && rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        lastPos = transform.position;
        wanderTargetPos = transform.position;
    }

    public void SetRoom(RoomController room)
    {
        myRoom = room;
    }

    void Update()
    {
        if (mobHealth != null && mobHealth.isDead) return;

        if (isHost)
        {
            // BỔ SUNG: Gửi đồng bộ tọa độ quái từ Host sang Client (Player 2)
            bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
            if (isMultiplayer && isRoomActivated && mobNetworkIdentity != null && !string.IsNullOrEmpty(mobNetworkIdentity.networkId))
            {
                if (Time.time - lastNetworkSyncTime >= networkSyncInterval)
                {
                    NetworkManager.Instance.SendEnemyPosition(mobNetworkIdentity.networkId, transform.position.x, transform.position.y);
                    lastNetworkSyncTime = Time.time;
                }
            }

            if (!isRoomActivated)
            {
                targetPlayer = null;
                currentState = EnemyState.Idle;
                if (animator != null) animator.SetBool("isMoving", false);
                return;
            }

            FindNearestPlayer();

            switch (currentState)
            {
                case EnemyState.Idle:
                    MonitorIdleState();
                    break;
                case EnemyState.Wander:
                    MonitorWanderState();
                    break;
                case EnemyState.Chase:
                    MonitorChaseState();
                    break;
                case EnemyState.Attack:
                    MonitorAttackState();
                    break;
                case EnemyState.Retreat:
                    MonitorRetreatState();
                    break;
            }
        }
        else
        {
            // Client: Dò tìm người chơi gần nhất để xoay súng/giáo ngắm bắn hiển thị trên màn hình Player 2
            FindNearestPlayer();

            // Client: Kích hoạt hiển thị hoạt ảnh tấn công & đạn/vệt chém khi quái áp sát Player 2
            if (targetPlayer != null && mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null)
            {
                Vector3 clientAttackOrigin = (mobWeaponAim.currentWeaponInfo.firePoint != null) ? mobWeaponAim.currentWeaponInfo.firePoint.position : transform.position;
                float dist = Vector2.Distance(clientAttackOrigin, targetPlayer.position);

                if (dist <= attackRange && Time.time >= nextAttackTime)
                {
                    if (animator != null) animator.SetTrigger("attack");
                    mobWeaponAim.Fire(targetPlayer.position, attackDamage);
                    nextAttackTime = Time.time + attackCooldown;
                }
            }

            // Client: Nhận vị trí nội suy mượt mà 60 FPS từ Host (Extrapolation + SmoothDamp)
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
                if (moveX > 0.05f) spriteRenderer.flipX = false;
                else if (moveX < -0.05f) spriteRenderer.flipX = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (mobHealth != null && mobHealth.isDead) return;

        if (isHost && isRoomActivated)
        {
            if (currentState == EnemyState.Idle)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }
    }

    void LateUpdate()
    {
        if (mobHealth != null && mobHealth.isDead) return;
        ClampPositionToRoom();
    }

    public void ActivateMob()
    {
        isRoomActivated = true;
    }

    public bool IsCombatActivated()
    {
        return isRoomActivated;
    }

    public Transform GetTargetPlayer()
    {
        return targetPlayer;
    }

    public void UpdateNetworkPosition(float x, float y)
    {
        Vector2 newPos = new Vector2(x, y);
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

    void ClampPositionToRoom()
    {
        if (myRoom == null || myRoom.RoomCollider == null) return;

        Bounds bounds = myRoom.RoomCollider.bounds;

        float minX = bounds.min.x + wallPadding;
        float maxX = bounds.max.x - wallPadding;
        float minY = bounds.min.y + wallPadding;
        float maxY = bounds.max.y - wallPadding;

        Vector3 pos = transform.position;
        if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY)
        {
            float clampedX = Mathf.Clamp(pos.x, minX, maxX);
            float clampedY = Mathf.Clamp(pos.y, minY, maxY);
            if (rb != null)
            {
                rb.position = new Vector2(clampedX, clampedY);
            }
            else
            {
                transform.position = new Vector3(clampedX, clampedY, pos.z);
            }
        }
    }

    void FindNearestPlayer()
    {
        float shortestDistance = Mathf.Infinity;
        Transform nearestPlayer = null;

        GameObject localPlayerObj = GameObject.FindGameObjectWithTag("Player");

        if (localPlayerObj == null)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 50f, 1 << playerLayer);
                if (colliders != null && colliders.Length > 0)
                {
                    localPlayerObj = colliders[0].gameObject;
                }
            }
        }

        if (localPlayerObj != null)
        {
            RookieHealth localHealth = localPlayerObj.GetComponent<RookieHealth>();
            if (localHealth == null) localHealth = localPlayerObj.GetComponentInParent<RookieHealth>();

            if (localHealth == null || !localHealth.isDead)
            {
                float dist = Vector2.Distance(transform.position, localPlayerObj.transform.position);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    nearestPlayer = localPlayerObj.transform;
                }
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

    void MonitorIdleState()
    {
        if (animator != null) animator.SetBool("isMoving", false);
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (isRoomActivated)
        {
            if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
            {
                currentState = EnemyState.Chase;
            }
            else
            {
                currentState = EnemyState.Wander;
            }
        }
    }

    void MonitorWanderState()
    {
        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (Time.time >= nextWanderTimer)
        {
            PickRandomWanderTarget();
            nextWanderTimer = Time.time + Random.Range(2.5f, 4.0f);
        }

        float distToWanderTarget = Vector2.Distance(transform.position, wanderTargetPos);
        if (distToWanderTarget > 0.3f)
        {
            Vector2 moveDir = (wanderTargetPos - (Vector2)transform.position).normalized;
            Vector2 separateForce = GetSeparationForce();
            Vector2 finalMoveDir = (moveDir + separateForce).normalized;

            rb.linearVelocity = finalMoveDir * (chaseSpeed * 0.35f);
            if (animator != null) animator.SetBool("isMoving", true);

            UpdateSpriteFacing(moveDir);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isMoving", false);
        }
    }

    private void PickRandomWanderTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * 2.5f;
        Vector3 potentialPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0);

        if (myRoom != null && myRoom.RoomCollider != null)
        {
            Bounds bounds = myRoom.RoomCollider.bounds;
            float clampedX = Mathf.Clamp(potentialPos.x, bounds.min.x + wallPadding, bounds.max.x - wallPadding);
            float clampedY = Mathf.Clamp(potentialPos.y, bounds.min.y + wallPadding, bounds.max.y - wallPadding);
            wanderTargetPos = new Vector2(clampedX, clampedY);
        }
        else
        {
            wanderTargetPos = potentialPos;
        }
    }

    void MonitorChaseState()
    {
        if (targetPlayer == null)
        {
            currentState = EnemyState.Wander;
            return;
        }

        // 🎯 ĐO KHOẢNG CÁCH TỪ MŨI GIÁO/SÚNG TỚI PLAYER
        Vector3 attackOrigin = (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && mobWeaponAim.currentWeaponInfo.firePoint != null)
            ? mobWeaponAim.currentWeaponInfo.firePoint.position
            : transform.position;

        float distanceToPlayer = Vector2.Distance(attackOrigin, targetPlayer.position);

        // 🎯 NẾU PLAYER DI CHUYỂN RA XA NGOÀI TẦM NHÌN: QUÁI BỎ CUỘC VÀ QUAY VỀ ĐI DẠO TUẦN TRA (Wander)
        if (distanceToPlayer > detectRange)
        {
            currentState = EnemyState.Wander;
            return;
        }

        bool isMelee = (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && mobWeaponAim.currentWeaponInfo.IsMelee);

        float targetAttackDistance = isMelee ? 0.9f : 4.0f; // Mũi giáo cách Player 0.9m

        // NẾU ĐÃ HẾT COOLDOWN BẮN/ĐÂM:
        if (Time.time >= nextAttackTime)
        {
            if (distanceToPlayer <= targetAttackDistance)
            {
                currentState = EnemyState.Attack;
                return;
            }

            // Tiếp tục di chuyển tiến lại gần nếu chưa tới cự ly
            Vector2 targetDir = (targetPlayer.position - transform.position).normalized;
            Vector2 separateForce = GetSeparationForce();
            Vector2 finalMoveDir = (targetDir + separateForce).normalized;

            rb.linearVelocity = finalMoveDir * chaseSpeed;
            if (animator != null) animator.SetBool("isMoving", true);
            UpdateSpriteFacing(targetPlayer.position - transform.position);
            return;
        }

        // NẾU ĐANG TRONG THỜI GIAN CHỜ COOLDOWN:
        if (isMelee)
        {
            // Tiến sát kè kè bên người Player (cách 1.1m) chờ cooldown
            if (distanceToPlayer > 1.1f)
            {
                Vector2 targetDir = (targetPlayer.position - transform.position).normalized;
                Vector2 separateForce = GetSeparationForce();
                Vector2 finalMoveDir = (targetDir + separateForce).normalized;

                rb.linearVelocity = finalMoveDir * chaseSpeed;
                if (animator != null) animator.SetBool("isMoving", true);
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool("isMoving", false);
            }
        }
        else
        {
            // Tầm xa: Dạt lùi ngắm bắn
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
        }

        UpdateSpriteFacing(targetPlayer.position - transform.position);
    }

    void MonitorAttackState()
    {
        if (targetPlayer == null)
        {
            currentState = EnemyState.Wander;
            return;
        }

        UpdateSpriteFacing(targetPlayer.position - transform.position);

        rb.linearVelocity = Vector2.zero;
        if (animator != null) animator.SetBool("isMoving", false);

        AttackTarget();
        nextAttackTime = Time.time + attackCooldown;

        currentState = EnemyState.Chase;
    }

    void MonitorRetreatState()
    {
        if (Time.time >= retreatEndTime || targetPlayer == null)
        {
            currentState = EnemyState.Chase;
            return;
        }

        Vector2 retreatDir = (transform.position - targetPlayer.position).normalized;
        Vector2 separateForce = GetSeparationForce();
        Vector2 finalMoveDir = (retreatDir + separateForce).normalized;

        rb.linearVelocity = finalMoveDir * (chaseSpeed * 0.8f);
        if (animator != null) animator.SetBool("isMoving", true);

        UpdateSpriteFacing(targetPlayer.position - transform.position);
    }

    private Vector2 GetSeparationForce()
    {
        Vector2 force = Vector2.zero;
        Collider2D[] nearbyMobs = Physics2D.OverlapCircleAll(transform.position, 0.8f, LayerMask.GetMask("Enemy"));
        int count = 0;

        foreach (var col in nearbyMobs)
        {
            if (col != null && col.gameObject != gameObject)
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

    private void UpdateSpriteFacing(Vector2 directionToPlayer)
    {
        if (spriteRenderer != null)
        {
            if (directionToPlayer.x > 0.05f)
            {
                spriteRenderer.flipX = false;
            }
            else if (directionToPlayer.x < -0.05f)
            {
                spriteRenderer.flipX = true;
            }
        }
    }

    void AttackTarget()
    {
        if (animator != null) animator.SetTrigger("attack");

        if (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && targetPlayer != null)
        {
            mobWeaponAim.Fire(targetPlayer.position, attackDamage);
        }
        else
        {
            StartCoroutine(DealDamageWithDelay());
        }
    }

    private System.Collections.IEnumerator DealDamageWithDelay()
    {
        yield return new WaitForSeconds(0.35f);

        if (targetPlayer != null && mobHealth != null && !mobHealth.isDead)
        {
            RookieHealth playerHealth = targetPlayer.GetComponent<RookieHealth>();
            RemotePlayerController rpc = targetPlayer.GetComponent<RemotePlayerController>();

            if ((playerHealth != null && playerHealth.isDead) || (rpc != null && rpc.isDead))
            {
                targetPlayer = null;
                currentState = EnemyState.Idle;
                yield break;
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
            else if (rpc != null)
            {
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SendPlayerDamaged(rpc.connectionId, attackDamage);
                }
            }
        }
    }
}