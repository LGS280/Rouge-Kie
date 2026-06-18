using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer playerRenderer;
    private PlayerController playerController;

    // biến ẩn để biết súng nào đang trên tay nhằm kích hoạt bắn/tốc độ bắn
    [HideInInspector] public WeaponInfo currentWeapon;
    private float nextFireTime = 0f;

    [Header("Setting Aim Bot")]
    public float aimRadius = 7f; // bán kính vòng tròn quét quái 
    public LayerMask enemyLayer;
    private Transform currentTarget; // lưu con quái bị aim 

    private float currentGamepadAngle = 0f;
    private Transform previousTarget;

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

        if(Time.timeScale == 0f) return; // nếu đang trong menu thì không cần update hướng súng

        // chơi bằng tay cầm
        if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
        {
            Vector2 gamepadDirection = playerController.GetMoveInput();

            FindClosestEnemy();

            if (currentTarget != null)
            {
                // khi có quái trong tầm thì sẽ aim vô quái bỏ qua joystick
                Vector2 aimDirection = currentTarget.position - transform.position;
                angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                currentGamepadAngle = angle; // lưu lại góc quay súng để khi quái chết ko bị giật 

                HandleTargetRingUI(currentTarget, true); // bật vòng đỏ dưới chân quái để hiện aim bot
            }
            else
            {
                // Khi mất quái thì thò tay tắt vòng đỏ của con quái cũ đi trước
                if (previousTarget != null)
                {
                    HandleTargetRingUI(previousTarget, false);
                }

                if (gamepadDirection.sqrMagnitude > 0.05f)
                {
                    angle = Mathf.Atan2(gamepadDirection.y, gamepadDirection.x) * Mathf.Rad2Deg;
                    currentGamepadAngle = angle;
                }
                else
                {
                    angle = currentGamepadAngle; // Buông cần thì giữ nguyên hướng súng cũ
                }
            }

            if (currentTarget != previousTarget && previousTarget != null)
            {
                // Tắt vòng đỏ của con quái cũ (A) đi để bật con quái mới (B)
                HandleTargetRingUI(previousTarget, false);
            }
            previousTarget = currentTarget;
        }
        // chơi bằng bàn phím + chuột
        else
        {
            // nếu người chơi qua bàn phím thì tắt vòng đỏ đi
            if (currentTarget != null)
            {
                HandleTargetRingUI(currentTarget, false);
            }
            if (previousTarget != null)
            {
                HandleTargetRingUI(previousTarget, false);
            }

            Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition);
            Vector2 aimDirection = mousePosition - transform.position;
            angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        }

        //  BỘ XỬ LÝ LẬT MẶT VÀ XOAY SÚNG ĐỒNG BỘ (CHỐNG XUNG ĐỘT)

        // Tự xoay chính nó (Cây súng)
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

        // Chuẩn hóa góc về khoảng -180 đến 180 độ để tính toán hướng lật mặt
        if (angle > 180f)
        {
            angle -= 360f;
        }
        if (angle < -180f)
        {
            angle += 360f;
        }

        // Quy định hướng: Cứ họng súng hướng sang trái (góc > 90 hoặc < -90) là người và súng cùng lật
        if (playerRenderer != null)
        {
            if (angle > 90f || angle < -90f)
            {
                playerRenderer.flipX = true; // Nhân vật nhìn sang trái
                transform.localScale = new Vector3(1f, -1f, 1f); // Lật trục Y của súng chống ngược súng
            }
            else
            {
                playerRenderer.flipX = false; // Nhân vật nhìn sang phải
                transform.localScale = new Vector3(1f, 1f, 1f); // Súng thẳng bình thường
            }
        }

        // Logic xả đạn
        HandleShooting();
    }

    private void HandleTargetRingUI(Transform enemyTransform, bool isActive)
    {
        if (enemyTransform == null) return;
        Transform mobRing = enemyTransform.Find("Mob_Ring");
        if (mobRing != null) mobRing.gameObject.SetActive(isActive);
    }

    private void FindClosestEnemy()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, aimRadius, enemyLayer);
        Transform closestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (var collider in hitColliders)
        {
            float distanceToEnemy = Vector2.Distance(transform.position, collider.transform.position);
            if (distanceToEnemy < minDistance)
            {
                minDistance = distanceToEnemy;
                closestEnemy = collider.transform;
            }
        }
        currentTarget = closestEnemy;
    }

    void HandleShooting()
    {
        if (currentWeapon == null) return;

        float currentFireRate = PlayerStats.Instance != null
            ? currentWeapon.fireRate / PlayerStats.Instance.attackSpeedMultiplier
            : currentWeapon.fireRate;

        if (Time.time >= nextFireTime)
        {
            bool isShooting = false;
            bool isNewClick = false; // click mới hay đang giữ

            if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
            {
                if (Gamepad.current != null && Gamepad.current.xButton.isPressed)
                {
                    isShooting = true;
                    isNewClick = Gamepad.current.xButton.wasPressedThisFrame;
                }
            }
            else
            {
                if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    isShooting = true;
                    isNewClick = Mouse.current.leftButton.wasPressedThisFrame;
                }
            }

            if (isShooting)
            {
                nextFireTime = Time.time + currentFireRate;
                currentWeapon.Attack(enemyLayer, isNewClick); // truyền thêm isNewClick
            }
        }
    }

    private void OnDrawGizmosSelected() // vẽ vòng tròn trong scene để xem tầm aim bot tới đâu
    {
        // Vòng đỏ: xem tầm aim bot tới đâu
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, aimRadius);

        // Vòng xanh dương: Vẽ tầm cận chiến của súng đang cầm nếu có
        if (currentWeapon != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, currentWeapon.meleeRadius);
        }
    }
}