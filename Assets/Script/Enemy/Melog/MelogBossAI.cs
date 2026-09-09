using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MelogBossAI : MonoBehaviour
{
    public enum BossState { Idle, Wander, Chase, Attack } // 4 trạng thái của boss

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
    [HideInInspector] public RoomController myRoom;

    public void SetRoom(RoomController room)
    {
        myRoom = room;
    }

    private Vector2 networkTargetPos; // Vị trí mạng nhận được ở gói tin mới nhất và trước đó
    private Vector2 lastNetworkTargetPos; 
    private Vector3 mobNetworkVelocity; // Vận tốc mạng dùng cho nội suy vị trí mượt
    private Vector2 estimatedMobVelocity;
    private float lastMobPacketTime; // Thời điểm nhận gói tin mạng gần nhất để tính vận tốc ngoại suy
    private bool hasFirstNetworkPos = false;
    private float lastNetworkSyncTime = 0f;
    private float networkSyncInterval = 0.05f;

    private void Awake() // tự động lấy các component cần thiết gắn trên boss
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
            melogWeaponAim.InitializeDualHandsAndWeapons(); // khởi tạo 2 tay cầm 2 vũ khí
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

        FindNearestPlayer(); // tìm người chơi gần nhất
        GetNewWanderTarget(); // lấy 1 điểm để đi tuần tra
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

        FindNearestPlayer();

        if (isHost)
        {

            if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && mobNetworkIdentity != null && !string.IsNullOrEmpty(mobNetworkIdentity.networkId))
            {
                if (Time.time - lastNetworkSyncTime >= networkSyncInterval)
                {
                    NetworkManager.Instance.SendEnemyPosition(mobNetworkIdentity.networkId, transform.position.x, transform.position.y);
                    lastNetworkSyncTime = Time.time;
                }
            }

            if (isRoomActivated && targetPlayer != null)
            {

                UpdateBossFacing(targetPlayer.position.x - transform.position.x);
            }
            // gửi tọa độ X Y của boss lên sv để đồng bộ với các client khác
            // xoay mặt boss về phía người chơi nếu thấy player 

            if (!isRoomActivated)
            {
                if ((myRoom == null && targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
                    || (mobHealth != null && mobHealth.CurrentHealth < mobHealth.maxHealth))
                {
                    isRoomActivated = true;
                    NotifyBossHealthBar();
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
            FindNearestPlayer();

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

                if (isMoving)
                {
                    float moveX = (mobNetworkVelocity.sqrMagnitude > 0.01f) ? mobNetworkVelocity.x : estimatedMobVelocity.x;
                    UpdateBossFacing(moveX);
                }
                else if (targetPlayer != null)
                {
                    UpdateBossFacing(targetPlayer.position.x - transform.position.x);
                }
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

    private void FindNearestPlayer() // hàm tìm người chơi gần đó
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

        if (distanceToPlayer > detectRange)
        {
            currentState = BossState.Wander;
            return;
        }

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

        if (animator != null) animator.SetTrigger("attack");

        if (melogWeaponAim != null)
        {
            melogWeaponAim.FireBothGuns(targetPlayer.position, 0);
        }

        // BỔ SUNG: Nếu đang trong phòng Co-op và là Host, gửi sự kiện xả đạn cho các máy Client
        bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);
        if (isMultiplayer && isHost && mobNetworkIdentity != null && !string.IsNullOrEmpty(mobNetworkIdentity.networkId))
        {
            NetworkManager.Instance.SendBossAttack(mobNetworkIdentity.networkId, targetPlayer.position.x, targetPlayer.position.y);
        }

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
        return 3.0f;
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

    private void UpdateBossFacing(float dirX)
    {
        if (Mathf.Abs(dirX) > 0.05f)
        {
            if (dirX < 0)
            {

                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {

                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
    }

    public void ActivateMob()
    {
        isRoomActivated = true;
        NotifyBossHealthBar();
    }

    private void NotifyBossHealthBar()
    {
        if (BossHealthBarUI.Instance != null && mobHealth != null)
        {
            string displayName = "MELOG - THE GATLING WARLORD";
            int floor = 1;
            if (GameProgressionManager.Instance != null) floor = GameProgressionManager.Instance.currentFloor;
            if (floor >= 5) displayName = "ELITE BOSS - GOLIATH ROOT";
            else displayName = $"MELOG - FLOOR {floor}";

            BossHealthBarUI.Instance.ShowBossBar(displayName, mobHealth);
        }
    }

    public bool IsCombatActivated()
    {
        return isRoomActivated;
    }

    // BỔ SUNG: Nhận lệnh mạng từ Host để xả đạn đồng bộ
    public void ExecuteNetworkAttack(Vector2 targetPos)
    {
        UpdateBossFacing(targetPos.x - transform.position.x);

        if (animator != null) animator.SetTrigger("attack");

        if (melogWeaponAim != null)
        {
            melogWeaponAim.FireBothGuns(targetPos, 0);
        }
    }
}
