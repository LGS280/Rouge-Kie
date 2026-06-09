using UnityEngine;
using System.Collections.Generic;

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
    // Đánh dấu xem máy này có quyền điều khiển AI hay không (chỉ máy Host = true)
    public bool isHost = true;

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

    }

    void Update()
    {
        if (mobHealth.isDead) return;

        // Nếu không phải là Host, máy này chỉ nhận vị trí từ mạng, không tự chạy AI
        if (!isHost) return;

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
    }

    void FindNearestPlayer()
    {
        // Tìm tất cả các Player có trong phòng (Co-op)
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
        rb.linearVelocity = Vector2.zero; // Dừng di chuyển

        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectRange)
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
            // Di chuyển hướng về phía Player
            Vector2 direction = (targetPlayer.position - transform.position).normalized;
            rb.linearVelocity = direction * chaseSpeed;

            // Đồng bộ Animation di chuyển
            animator.SetBool("isMoving", true);

            // Lật mặt bằng flipX thay vì localScale (giả sử Sprite mặc định hướng sang bên Phải)
            if (direction.x > 0)
            {
                spriteRenderer.flipX = false; // Quay mặt sang phải
            }
            else if (direction.x < 0)
            {
                spriteRenderer.flipX = true;  // Quay mặt sang trái
            }

            //// Lật mặt quái (Flip Sprite) theo hướng di chuyển
            //if (direction.x > 0) transform.localScale = new Vector3(1, 1, 1);
            //else if (direction.x < 0) transform.localScale = new Vector3(-1, 1, 1);
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
        // Gây sát thương lên Player ở đây (gửi sự kiện qua Server)
    }
}