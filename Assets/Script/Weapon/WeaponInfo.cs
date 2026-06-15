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
    public float meleeRadius = 1.5f;

    public void Attack(LayerMask enemyLayer)
    {
        // Quét cận chiến dựa trên tầm quét riêng của súng này
        Collider2D closeEnemy = Physics2D.OverlapCircle(transform.position, meleeRadius, enemyLayer);

        if (closeEnemy != null && meleeSlashPrefab != null && firePoint != null)
        {
            // Cận chiến: Sinh ra vệt chém tại đầu nòng của chính nó
            Instantiate(meleeSlashPrefab, firePoint.position, firePoint.rotation);
        }
        else if (bulletPrefab != null && firePoint != null)
        {
            // Bắn xa: Sinh ra đạn tại đầu nòng của chính nó
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }
}