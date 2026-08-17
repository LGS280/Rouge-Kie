using UnityEngine;

public class MobBullet : MonoBehaviour
{
    [Header("Thời gian tồn tại đạn (Bảng Inspector)")]
    public float lifeTime = 4f;

    [HideInInspector] public float speed = 10f;
    [HideInInspector] public int damage = 10;

    private bool hasHit = false;

    public void InitFromDb(int bulletId)
    {
        if (bulletId <= 0) return;

        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bulletId, out BulletConfig config))
        {
            speed = config.flightSpeed > 0 ? config.flightSpeed : 10f;
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

    private void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        if (collision.CompareTag("Enemy") || collision.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        RookieHealth playerHealth = collision.GetComponent<RookieHealth>();
        if (playerHealth == null) playerHealth = collision.GetComponentInParent<RookieHealth>();

        RemotePlayerController rpc = collision.GetComponent<RemotePlayerController>();
        if (rpc == null) rpc = collision.GetComponentInParent<RemotePlayerController>();

        if (playerHealth != null || rpc != null)
        {
            hasHit = true;
            if (playerHealth != null && !playerHealth.isDead)
            {
                playerHealth.TakeDamage(damage);
            }
            else if (rpc != null && !rpc.isDead && NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendPlayerDamaged(rpc.connectionId, damage);

            }

            Destroy(gameObject);
            return;
        }

        if (collision.CompareTag("Obstacle") || collision.CompareTag("Door"))
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}
