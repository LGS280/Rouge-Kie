using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer playerRenderer;
    private PlayerController playerController;

    [Header("--- THIẾT LẬP BẮN ĐẠN ---")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;
    private float nextFireTime = 0f;

    private float currentGamepadAngle = 0f;

    void Start()
    {
        mainCamera = Camera.main;
        playerController = GetComponentInParent<PlayerController>();

        if (transform.parent != null)
        {
            playerRenderer = transform.parent.GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        float angle = 0f;

        // chơi bằng tay cầm
        if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
        {
            Vector2 gamepadDirection = playerController.GetMoveInput();

            if (gamepadDirection.sqrMagnitude > 0.05f)
            {
                angle = Mathf.Atan2(gamepadDirection.y, gamepadDirection.x) * Mathf.Rad2Deg;
                currentGamepadAngle = angle;

                // Chỉ lật mặt nhân vật dựa theo hướng gạt cần trái/phải
                if (gamepadDirection.x > 0.1f && playerRenderer != null) playerRenderer.flipX = false;
                else if (gamepadDirection.x < -0.1f && playerRenderer != null) playerRenderer.flipX = true;
            }
            else
            {
                angle = currentGamepadAngle; // Buông cần thì giữ nguyên hướng súng cũ
            }
        }
        // chơi bằng bàn phím + chuột
        else
        {
            Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition);
            Vector2 aimDirection = mousePosition - transform.position;
            angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            // Đứng yên hay chạy bằng phím thì tự động lật mặt nhân vật nhìn theo hướng con chuột
            if (playerRenderer != null)
            {
                if (angle > 90 || angle < -90) playerRenderer.flipX = true;
                else playerRenderer.flipX = false;
            }
        }

        // Tự xoay chính nó (Cây súng)
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

        // Chống ngược súng khi quay về bên trái (Lật trục Y của súng)
        if (playerRenderer != null && playerRenderer.flipX)
        {
            transform.localScale = new Vector3(1, -1, 1);
        }
        else
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        // Logic xả đạn
        HandleShooting();
    }

    void HandleShooting()
    {
        if (Time.time >= nextFireTime)
        {
            bool isShooting = false;

            if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
            {
                if (Gamepad.current != null && Gamepad.current.xButton.isPressed) isShooting = true;
            }
            else
            {
                if (Mouse.current != null && Mouse.current.leftButton.isPressed) isShooting = true;
            }

            if (isShooting)
            {
                nextFireTime = Time.time + fireRate;
                Shoot();
            }
        }
    }

    void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }
}