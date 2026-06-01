using UnityEngine;

public class WeaponAim : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer playerRenderer;

    [Header("--- THIẾT LẬP BẮN ĐẠN ---")]
    public GameObject bulletPrefab;   // Kéo file Prefab viên đạn vào đây
    public Transform firePoint;       // Kéo Object FirePoint vào đây
    public float fireRate = 0.2f;     // Tốc độ xả đạn (0.2 giây 1 viên)
    private float nextFireTime = 0f;

    void Start()
    {
        mainCamera = Camera.main;
        if (transform.parent != null)
        {
            playerRenderer = transform.parent.GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 aimDirection = mousePosition - transform.position;
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        float moveX = Input.GetAxisRaw("Horizontal");

        if (moveX > 0)
        {
            if (playerRenderer != null) playerRenderer.flipX = false;
            angle = Mathf.Clamp(angle, -90f, 90f);
        }
        else if (moveX < 0)
        {
            if (playerRenderer != null) playerRenderer.flipX = true;
            if (angle < 0) angle += 360f;
            angle = Mathf.Clamp(angle, 90f, 270f);
        }
        else
        {
            if (angle > 90 || angle < -90)
            {
                if (playerRenderer != null) playerRenderer.flipX = true;
            }
            else
            {
                if (playerRenderer != null) playerRenderer.flipX = false;
            }
        }

        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

        if (playerRenderer != null && playerRenderer.flipX)
        {
            transform.localScale = new Vector3(1, -1, 1);
        }
        else
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate; // giới hạn tốc độ bắn
            Shoot();
        }
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // Triệu hồi viên đạn ra ngay tại vị trí và góc xoay của đầu nòng súng
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }
}
