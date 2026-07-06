using UnityEngine;

public class MobAI : MonoBehaviour
{
    public enum EnemyState { Idle, Chase, Attack, Die }
    public EnemyState currentState = EnemyState.Idle;

    [Header("Stats")]
    public float chaseSpeed = 3f;
    public float detectRange = 7f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    private float nextAttackTime = 0f;

    [Header("Network Status")]
    public bool isHost = true;

    [Header("Room Setup")]
    [Tooltip("Khoảng cách giữ thêm với rào chắn phòng (Mặc định bằng 0 vì rào chắn của DungeonGenerator đã tự cách tường 2 ô)")]
    public float wallPadding = 0f;
    [HideInInspector] public RoomController myRoom; // Tự động nhận diện từ RoomController khi map được sinh ra
    private bool isRoomActivated = false;          // Cờ kiểm soát kích hoạt AI

    private float syncTimer = 0f;
    private float syncInterval = 0.1f; // 100ms sync rate cho Mob
    private Vector3 lastPos;

    private Transform targetPlayer;
    private Rigidbody2D rb;
    private Animator animator;
    private MobHealth mobHealth;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mobHealth = GetComponent<MobHealth>();
        originalScale = transform.localScale;

        if (NetworkManager.Instance != null)
        {
            isHost = (NetworkManager.Instance.UserRole == "Host");
        }
        
        lastPos = transform.position;
    }

    void Update()
    {
        if (mobHealth.isDead) return;

        if (!isHost)
        {
            // Client: Tự động đoán animation dựa trên sự thay đổi vị trí (do Host gửi qua)
            Vector3 delta = transform.position - lastPos;
            if (animator != null)
            {
                animator.SetBool("isMoving", delta.magnitude > 0.01f);
            }
            if (delta.x > 0.01f) spriteRenderer.flipX = false;
            else if (delta.x < -0.01f) spriteRenderer.flipX = true;

            lastPos = transform.position;
            return;
        }

        // BẢO VỆ CHẶT CHẼ: Nếu người chơi chưa bước qua cửa kích hoạt phòng,
        // quái vật đứng yên hoàn toàn, KHÔNG nhận diện và KHÔNG tìm kiếm Player.
        if (!isRoomActivated)
        {
            targetPlayer = null;
            currentState = EnemyState.Idle;
            if (animator != null) animator.SetBool("isMoving", false);
            rb.linearVelocity = Vector2.zero;
            return; // Thoát hàm ngay lập tức
        }

        // CHỈ KHI cửa đóng và combat bắt đầu, quái mới bắt đầu mở giác quan tìm Player
        FindNearestPlayer();

        switch (currentState)
        {
            case EnemyState.Idle:
                MonitorIdleState();
                break;
            case EnemyState.Chase:
                MonitorChaseState();
                break;
            case EnemyState.Attack:
                MonitorAttackState();
                break;
        }

        // Host: Gửi vị trí quái vật liên tục cho Client
        if (NetworkManager.Instance != null)
        {
            syncTimer -= Time.deltaTime;
            if (syncTimer <= 0f)
            {
                MobNetworkIdentity identity = GetComponent<MobNetworkIdentity>();
                if (identity != null && !string.IsNullOrEmpty(identity.networkId))
                {
                    NetworkManager.Instance.SendEnemyPosition(identity.networkId, transform.position.x, transform.position.y);
                }
                syncTimer = syncInterval;
            }
        }
    }

    void LateUpdate()
    {
        if (mobHealth.isDead) return;
        ClampPositionToRoom();
    }

    // Được gọi trực tiếp bởi RoomController khi bắt đầu TryStartRoomCombat()
    public void ActivateMob()
    {
        isRoomActivated = true;
    }

    // Chặn không cho quái vật đi ra khỏi ranh giới phòng
    void ClampPositionToRoom()
    {
        if (myRoom == null || myRoom.RoomCollider == null) return;

        Bounds bounds = myRoom.RoomCollider.bounds;

        // Khống chế tọa độ của quái vật nằm gọn trong bounds của RoomCollider
        float minX = bounds.min.x + wallPadding;
        float maxX = bounds.max.x - wallPadding;
        float minY = bounds.min.y + wallPadding;
        float maxY = bounds.max.y - wallPadding;

        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        float clampedY = Mathf.Clamp(transform.position.y, minY, maxY);

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }

    void FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float shortestDistance = Mathf.Infinity;
        Transform nearestPlayer = null;

        foreach (GameObject player in players)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
            if (distanceToPlayer < shortestDistance)
            {
                shortestDistance = distanceToPlayer;
                nearestPlayer = player.transform;
            }
        }

        targetPlayer = nearestPlayer;
    }

    void MonitorIdleState()
    {
        animator.SetBool("isMoving", false);
        rb.linearVelocity = Vector2.zero;

        if (isRoomActivated && targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
        {
            currentState = EnemyState.Chase;
        }
    }

    void MonitorChaseState()
    {
        if (targetPlayer == null)
        {
            currentState = EnemyState.Idle;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        if (distance > detectRange)
        {
            currentState = EnemyState.Idle;
        }
        else if (distance <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else
        {
            Vector2 direction = (targetPlayer.position - transform.position).normalized;
            rb.linearVelocity = direction * chaseSpeed;

            animator.SetBool("isMoving", true);

            // Xử lý quay mặt Sprite
            if (direction.x > 0)
            {
                spriteRenderer.flipX = false;
            }
            else if (direction.x < 0)
            {
                spriteRenderer.flipX = true;
            }
        }
    }

    void MonitorAttackState()
    {
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("isMoving", false);

        if (targetPlayer == null)
        {
            currentState = EnemyState.Idle;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);
        if (distance > attackRange)
        {
            currentState = EnemyState.Chase;
        }
        else if (Time.time >= nextAttackTime)
        {
            AttackTarget();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void AttackTarget()
    {
        animator.SetTrigger("attack");
    }
}