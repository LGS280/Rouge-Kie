using UnityEngine;

public class PlayerMeleeSlash : MonoBehaviour
{
    [Header("xử lý cận chiến")]
    public GameObject meleeSlashPrefab;
    public float meleeRadius = 3f;
    public float meleeCooldown = 0.3f;
    public float slashOffset = 1.2f;
    public string enemyTag = "Enemy";

    private float cooldownTimer = 0f;

    void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    public bool TryMeleeAttack(Transform firePoint)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, meleeRadius);

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag(enemyTag))
            {
                Vector2 directionToEnemy = (hit.transform.position - transform.position).normalized;
                Vector2 weaponDirection = firePoint.right;

                float dotProduct = Vector2.Dot(weaponDirection, directionToEnemy);

                if (dotProduct > 0f)
                {
                    if (cooldownTimer <= 0f)
                    {
                        if (meleeSlashPrefab != null && firePoint != null)
                        {
                            Vector3 spawnPosition = transform.position + firePoint.right * slashOffset;

                            Instantiate(meleeSlashPrefab, spawnPosition, firePoint.rotation);
                            cooldownTimer = meleeCooldown;
                        }
                    }

                    return true;
                }
            }
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRadius);
    }
}
