using UnityEngine;

public class MobMeleeSlash : MonoBehaviour
{
    [Header("Thời gian tồn tại vệt sáng (Life Time)")]
    public float lifeTime = 0.15f;

    [HideInInspector] public int damage = 10;

    private bool hasHitPlayer = false;

    public void InitFromDb(int bulletId)
    {
        if (bulletId <= 0) return;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            damage = config.damage > 0 ? config.damage : 10;

        }
    }

    private void Start()
    {

        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            gameObject.layer = enemyBulletLayer;
        }

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (collision.CompareTag("Enemy") || collision.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        if (!hasHitPlayer)
        {
            RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
            if (playerHealth == null) playerHealth = collision.GetComponentInParent<RookieHealth>();

            RemotePlayerController rpc = collision.GetComponent<RemotePlayerController>();
            if (rpc == null) rpc = collision.GetComponentInParent<RemotePlayerController>();

            if (playerHealth != null || rpc != null)
            {
                hasHitPlayer = true;
                if (playerHealth != null && !playerHealth.isDead)
                {
                    playerHealth.TakeDamage(damage);
                }
                else if (rpc != null && !rpc.isDead && NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SendPlayerDamaged(rpc.connectionId, damage);

                }
            }
        }
    }
}
