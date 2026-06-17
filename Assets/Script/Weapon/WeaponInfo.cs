using UnityEngine;
public class WeaponInfo : MonoBehaviour
{
    [Header("VỊ TRÍ CẦM SÚNG")]
    public Vector3 customHandPosition;

    [Header("THIẾT LẬP BẮN ĐẠN")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;

    [Header("THIẾT LẬP CẬN CHIẾN")]
    public GameObject meleeSlashPrefab;
    public float meleeRadius = 3f;

    [Header("RECOIL")]
    [SerializeField] float recoilDistance = 0.15f;  // giật lùi bao nhiêu unit
    [SerializeField] float recoilDuration = 0.05f;  // thời gian giật ra
    [SerializeField] float returnDuration = 0.1f;   // thời gian hồi về

    Vector3 originalLocalPos;
    bool positionSaved = false;

    void Awake()
    {
        
    }

    public void Attack(LayerMask enemyLayer, bool isNewClick = false)
    {
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        Collider2D closeEnemy = Physics2D.OverlapCircle(transform.position, meleeRadius, enemyLayer);
        bool didMelee = false;

        // Melee chỉ kích hoạt khi click mới (không phải giữ chuột)
        if (isNewClick && closeEnemy != null && meleeSlashPrefab != null && firePoint != null)
        {
            // Cận chiến: Sinh ra vệt chém tại đầu nòng của chính nó
            Instantiate(meleeSlashPrefab, firePoint.position, firePoint.rotation);
            didMelee = true;
        }
        else if (bulletPrefab != null && firePoint != null)
        {
            // Bắn xa: Sinh ra đạn tại đầu nòng của chính nó
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }

        // Recoil chỉ khi bắn đạn, không phải melee
        if (!didMelee)
        {
            StopAllCoroutines();
            StartCoroutine(RecoilRoutine());
        }
    }

    System.Collections.IEnumerator RecoilRoutine()
    {
        Vector3 recoilPos = originalLocalPos + Vector3.left * recoilDistance;

        // Phase 1: giật ra nhanh
        float t = 0f;
        while (t < recoilDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(originalLocalPos, recoilPos, t / recoilDuration);
            yield return null;
        }

        // Phase 2: hồi về chậm hơn
        t = 0f;
        while (t < returnDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(recoilPos, originalLocalPos, t / returnDuration);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
    }
}