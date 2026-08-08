using UnityEngine;

public class RocketBullet : MissileBullet
{
    [Header("Rocket Impact Effect")]
    public GameObject blastMarkPrefab; // Prefab vết cháy xám trên sàn dành riêng cho Rocket

    protected override void Start()
    {
        // Tự động gắn bộ nhả dải khói liên tục cho đạn Rocket
        if (GetComponent<SmokeTrailEmitter>() == null)
        {
            gameObject.AddComponent<SmokeTrailEmitter>();
        }

        base.Start();
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle") || collision.CompareTag("Enemy") || collision.CompareTag("Door"))
        {
            GameObject markPrefab = blastMarkPrefab;
            if (markPrefab == null)
            {
                markPrefab = Resources.Load<GameObject>("Prefab/Effects/Blast_Mark");
                if (markPrefab == null) markPrefab = Resources.Load<GameObject>("Effects/Blast_Mark");
#if UNITY_EDITOR
                if (markPrefab == null) markPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Effects/Blast_Mark.prefab");
#endif
            }

            if (markPrefab != null)
            {
                Instantiate(markPrefab, transform.position, Quaternion.identity);
            }
        }

        base.OnTriggerEnter2D(collision);
    }
}
