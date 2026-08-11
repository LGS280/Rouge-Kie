using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script quản lý AI Quái (FSM State Machine) - Nâng cấp 5 Trạng Thái Hoàn Hảo:
/// 1. Quái đi dạo tuần tra (Wander/Patrol) thong thả trong phòng khi chưa thấy Player.
/// 2. Quái bắn xa Kiting (Tự động đi lùi giữ cự cự 4.5m).
/// 3. Quái cận chiến Retreat (Đâm thương xong giật lùi 1.2m né đòn).
/// 4. Flocking Avoidance (Tự động đẩy nhau giàn hàng bao vây, không đè hình).
/// 5. Giữ nguyên 100% logic lật mặt flipX theo hướng nhìn.
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

        // Tự động điều chỉnh cự cự an toàn giữ khoảng cách cho Quái
        if (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null)
        {
            attackRange = mobWeaponAim.currentWeaponInfo.isMelee ? 2.2f : 4.5f;
        }
        else
        {
            attackRange = 4.0f; // Mặc định dừng từ xa xả đạn
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
                float dist = Vector2.Distance(transform.position, targetPlayer.position);
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

    // Được gọi trực tiếp bởi RoomController khi bắt đầu TryStartRoomCombat()
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

    // Client nhận tọa độ từ mạng
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

        // 1. Dò tìm Local Player bằng Tag "Player"
        GameObject localPlayerObj = GameObject.FindGameObjectWithTag("Player");

        // 2. Dự phòng: Dò tìm Player bằng Layer "Player" nếu Tag chưa tìm thấy
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

            // BỎ QUA LOCAL PLAYER ĐÃ CHẾT!
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

        // 3. Dò tìm tất cả Remote Player (Co-op Multiplayer)
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
                // Khi chưa phát hiện Player: Chuyển sang trạng thái Wander đi dạo tuần tra trong phòng
                currentState = EnemyState.Wander;
            }
        }
    }

    /// <summary>
    /// 🚶 XỬ LÝ TRẠNG THÁI ĐI DẠO TUẦN TRA (Wander State)
    /// </summary>
    void MonitorWanderState()
    {
        // 🎯 NẾU PLAYER BƯỚC VÀO TẦM QUÉT: LẬP TỨC CHUYỂN SANG CHASE ĐUỔI ĐÁNH
        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        // Mỗi 2.5s - 4.0s: Chọn 1 vị trí ngẫu nhiên để thong thả đi dạo
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

            rb.linearVelocity = finalMoveDir * (chaseSpeed * 0.35f); // Đi bộ thong thả
            if (animator != null) animator.SetBool("isMoving", true);

            UpdateSpriteFacing(moveDir);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isMoving", false);
        }
    }

    /// <summary>
    /// Chọn điểm đi dạo tuần tra ngẫu nhiên nằm gọn trong phòng
    /// </summary>
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

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        if (distance > detectRange + 1.5f)
        {
            currentState = EnemyState.Wander;
            return;
        }

        bool isMelee = (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && mobWeaponAim.currentWeaponInfo.isMelee);

        // 🏹 1. QUÁI BẮN XA KITING: Nếu Player lại quá gần (< 3.2m), Quái tự động ĐI LÙI giữ khoảng cách
        Vector2 targetDir = (targetPlayer.position - transform.position).normalized;
        if (!isMelee && distance < 3.2f)
        {
            targetDir = -targetDir; // Đảo ngược hướng để đi lùi
        }

        // 🎯 ĐIỀU KIỆN TẤN CÔNG
        if (distance <= attackRange)
        {
            currentState = EnemyState.Attack;
            return;
        }

        // 🛡️ 3. FLOCKING AVOIDANCE: Đẩy nhẹ các Quái đồng đội ra xa để giàn hàng bao vây, không chồng hình
        Vector2 separateForce = GetSeparationForce();
        Vector2 finalMoveDir = (targetDir + separateForce).normalized;

        rb.linearVelocity = finalMoveDir * chaseSpeed;
        if (animator != null) animator.SetBool("isMoving", true);

        // 🎯 GIỮ NGUYÊN 100% LOGIC FLIPX CHO QUÁI
        UpdateSpriteFacing(targetPlayer.position - transform.position);
    }

    void MonitorAttackState()
    {
        rb.linearVelocity = Vector2.zero;
        if (animator != null) animator.SetBool("isMoving", false);

        if (targetPlayer == null)
        {
            currentState = EnemyState.Wander;
            return;
        }

        UpdateSpriteFacing(targetPlayer.position - transform.position);

        float distance = Vector2.Distance(transform.position, targetPlayer.position);
        if (distance > attackRange + 1.0f)
        {
            currentState = EnemyState.Chase;
        }
        else if (Time.time >= nextAttackTime)
        {
            AttackTarget();
            nextAttackTime = Time.time + attackCooldown;

            // 🗡️ 2. QUÁI CẬN CHIẾN RETREAT: Đâm thương xong chuyển trạng thái giật lùi 0.8s né đòn
            bool isMelee = (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && mobWeaponAim.currentWeaponInfo.isMelee);
            if (isMelee)
            {
                currentState = EnemyState.Retreat;
                retreatEndTime = Time.time + 0.8f;
            }
        }
    }

    void MonitorRetreatState()
    {
        if (Time.time >= retreatEndTime || targetPlayer == null)
        {
            currentState = EnemyState.Chase;
            return;
        }

        // Đi lùi xa khỏi Player 1.2m
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

    /// <summary>
    /// 🎯 GIỮ NGUYÊN 100% LOGIC FLIPX CHO QUÁI
    /// </summary>
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

        // Nếu Quái có vũ khí -> Bắn đạn từ vũ khí (Sát thương do đạn bay va chạm gây ra)
        if (mobWeaponAim != null && mobWeaponAim.currentWeaponInfo != null && targetPlayer != null)
        {
            mobWeaponAim.Fire(targetPlayer.position, attackDamage);
        }
        else
        {
            // Chỉ chạy gây sát thương trực tiếp nếu Quái KHÔNG có vũ khí (Đánh tay không mặc định)
            StartCoroutine(DealDamageWithDelay());
        }
    }

    private System.Collections.IEnumerator DealDamageWithDelay()
    {
        // Đợi 0.35 giây để hoạt ảnh chém/vung tay của quái trùng khớp với thời điểm gây dame
        yield return new WaitForSeconds(0.35f);

        if (targetPlayer != null && mobHealth != null && !mobHealth.isDead)
        {
            RookieHealth playerHealth = targetPlayer.GetComponent<RookieHealth>();
            RemotePlayerController rpc = targetPlayer.GetComponent<RemotePlayerController>();

            // BỎ QUA GÂY SÁT THƯƠNG NẾU MỤC TIÊU ĐÃ CHẾT!
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