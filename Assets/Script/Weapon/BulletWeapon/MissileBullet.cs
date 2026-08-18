using UnityEngine;

public class MissileBullet : NormalBullet
{
    [Header("cấu hình đạn theo dõi")]
    public bool isHoming = true;
    public float detectRadius = 5.0f;
    public float turnSpeed = 260.0f;
    public float initialDirectTime = 0.05f;

    [Header("hiệu ứng đuôi lửa")]
    public bool hasFireTail = true;
    public GameObject fireTailObject;

    [Header("hiệu ứng nổ")]
    public GameObject explosionEffectPrefab;

    private Transform targetEnemy;
    private float spawnTimestamp;

    protected override void Start()
    {
        base.Start();
        spawnTimestamp = Time.time;

        if (fireTailObject == null)
        {
            Transform tailTrans = transform.Find("Fire_Tail");
            if (tailTrans != null)
            {
                fireTailObject = tailTrans.gameObject;
            }
        }

        if (fireTailObject != null)
        {
            fireTailObject.SetActive(hasFireTail);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle") || collision.CompareTag("Enemy") || collision.CompareTag("Door"))
        {
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        base.OnTriggerEnter2D(collision);
    }

    protected override void Update()
    {

        if (hasFireTail && fireTailObject != null && fireTailObject.activeSelf)
        {
            float scaleX = 0.9f + Random.Range(-0.1f, 0.1f);
            float scaleY = 0.9f + Random.Range(-0.15f, 0.15f);
            fireTailObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        if (isHoming && Time.time - spawnTimestamp >= initialDirectTime)
        {
            FindTargetEnemy();

            if (targetEnemy != null)
            {
                Vector2 direction = (targetEnemy.position - transform.position).normalized;
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }

        transform.Translate(Vector2.right * speed * Time.deltaTime, Space.Self);
    }

    private void FindTargetEnemy()
    {

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

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
