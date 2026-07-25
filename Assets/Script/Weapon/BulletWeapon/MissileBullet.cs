using UnityEngine;

public class MissileBullet : NormalBullet
{
    [Header("Missile Homing Configuration")]
    public float detectRadius = 5.0f;     // Bán kính dò tìm kẻ địch
    public float turnSpeed = 260.0f;      // Tốc độ xoay lượn cong mềm mại (độ/giây)
    public float initialDirectTime = 0.05f; // Bắt đầu lượn bẻ lái cực nhanh sau 0.05s

    [Header("Visual Tail Effect")]
    public bool hasFireTail = true;      // Cờ bật/tắt đuôi lửa (ví dụ: Tên lửa = true, Mũi tên phép = false)
    public GameObject fireTailObject;   // Reference tới GameObject đuôi lửa

    private Transform targetEnemy;
    private float spawnTimestamp;

    protected override void Start()
    {
        base.Start();
        spawnTimestamp = Time.time;

        // Tự động tìm GameObject con "Fire_Tail" nếu chưa gán thủ công
        if (fireTailObject == null)
        {
            Transform tailTrans = transform.Find("Fire_Tail");
            if (tailTrans != null)
            {
                fireTailObject = tailTrans.gameObject;
            }
        }

        // Bật/tắt hiển thị đuôi lửa theo cờ hasFireTail
        if (fireTailObject != null)
        {
            fireTailObject.SetActive(hasFireTail);
        }
    }

    protected override void Update()
    {
        // 1. Hiệu ứng bùng nhấp nháy loa lửa ngay đít tên lửa (Nozzle Flame Cone)
        if (hasFireTail && fireTailObject != null && fireTailObject.activeSelf)
        {
            float scaleX = 0.9f + Random.Range(-0.1f, 0.1f);
            float scaleY = 0.9f + Random.Range(-0.15f, 0.15f);
            fireTailObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        // 2. Dò tìm và xoay hướng bay theo đuổi quái
        if (Time.time - spawnTimestamp >= initialDirectTime)
        {
            FindTargetEnemy();

            if (targetEnemy != null)
            {
                Vector2 direction = (targetEnemy.position - transform.position).normalized;
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                // Xoay từ từ hướng đạn về phía quái theo turnSpeed
                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        // 3. Tiến thẳng về phía trước theo hướng hiện tại (tính năng kế thừa speed từ NormalBullet)
        transform.Translate(Vector2.right * speed * Time.deltaTime, Space.Self);
    }

    private void FindTargetEnemy()
    {
        // Kiểm tra target hiện tại còn hợp lệ hay không
        if (targetEnemy != null)
        {
            if (!targetEnemy.gameObject.activeInHierarchy)
            {
                targetEnemy = null;
            }
            else
            {
                MobHealth mobHp = targetEnemy.GetComponent<MobHealth>();
                if (mobHp != null && mobHp.isDead)
                {
                    targetEnemy = null;
                }
            }
        }

        // Nếu chưa có target, quét tìm con quái gần nhất trong bán kính detectRadius
        if (targetEnemy == null)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectRadius);
            float minDistance = Mathf.Infinity;
            Transform closest = null;

            foreach (var col in colliders)
            {
                if (col.CompareTag("Enemy"))
                {
                    MobHealth hp = col.GetComponent<MobHealth>();
                    if (hp != null && hp.isDead) continue;

                    float dist = Vector2.Distance(transform.position, col.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = col.transform;
                    }
                }
            }

            targetEnemy = closest;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng tròn bán kính dò quái trong Unity Editor để xem trực quan
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
