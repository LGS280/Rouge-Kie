using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    private Camera mainCamera;
    private SpriteRenderer playerRenderer;
    private PlayerController playerController;
    private PlayerMeleeSlash playerMelee;
    private WeaponManager weaponManager;

    [HideInInspector] public WeaponInfo currentWeapon;
    private float nextFireTime = 0f;

    [Header("cài đặt aimbot (cho tay cầm)")]
    public float aimRadius = 7f;
    public LayerMask enemyLayer;
    private Transform currentTarget;

    private float currentGamepadAngle = 0f;
    private Transform previousTarget;

    public bool isAimingUp { get; private set; } = false;

    void Start()
    {
        mainCamera = Camera.main;
        playerController = GetComponentInParent<PlayerController>();
        playerMelee = GetComponentInParent<PlayerMeleeSlash>();
        weaponManager = GetComponentInParent<WeaponManager>();

        if (transform.parent != null)
        {
            playerRenderer = transform.parent.GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {

        if (playerController == null)
        {
            return;
        }

        if (ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen())
        {
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float angle = 0f;

        if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
        {
            Vector2 gamepadDirection = playerController.GetMoveInput();

            FindClosestEnemy();

            if (currentTarget != null)
            {

                Vector2 aimDirection = currentTarget.position - transform.position;
                angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                currentGamepadAngle = angle;

                HandleTargetRingUI(currentTarget, true);
            }
            else
            {

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
                    angle = currentGamepadAngle;
                }
            }

            if (currentTarget != previousTarget && previousTarget != null)
            {

                HandleTargetRingUI(previousTarget, false);
            }
            previousTarget = currentTarget;
        }

        else
        {

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

        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

        if (angle > 180f)
        {
            angle -= 360f;
        }
        if (angle < -180f)
        {
            angle += 360f;
        }

        isAimingUp = (angle > 25f && angle < 155f);

        if (playerRenderer != null)
        {
            if (angle > 90f || angle < -90f)
            {
                playerRenderer.flipX = true;
                transform.localScale = new Vector3(1f, -1f, 1f);
            }
            else
            {
                playerRenderer.flipX = false;
                transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        if (currentWeapon == null)
        {
            currentWeapon = GetComponentInChildren<WeaponInfo>();
        }

        if (currentWeapon != null)
        {
            float upFactor = Mathf.Clamp01(1f - Mathf.Abs(angle - 90f) / 45f);
            currentWeapon.transform.localPosition = Vector3.Lerp(currentWeapon.customHandPosition, Vector3.zero, upFactor);
        }

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

        if (weaponManager != null && weaponManager.nearbyWeapons.Count > 0) return;

        if (currentWeapon == null) return;

        float currentFireRate = PlayerStats.Instance != null
            ? currentWeapon.fireRate / PlayerStats.Instance.attackSpeedMultiplier
            : currentWeapon.fireRate;

        if (PlayerBuffManager.Instance != null)
        {
            currentFireRate *= PlayerBuffManager.Instance.fireRateMultiplier;
        }

        if (Time.time >= nextFireTime)
        {
            bool isShooting = false;
            bool isNewClick = false;

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

                bool isCurrentWeaponMelee = currentWeapon != null && currentWeapon.IsMeleeWeapon();

                if (currentWeapon != null && currentWeapon.HasBayonetStab())
                {
                    float checkRadius = (playerMelee != null) ? playerMelee.meleeRadius : 2.5f;
                    string checkTag = (playerMelee != null) ? playerMelee.enemyTag : "Enemy";
                    if (currentWeapon.TryBayonetStab(checkRadius, checkTag))
                    {
                        return;
                    }
                }

                if (playerMelee != null && !isCurrentWeaponMelee)
                {
                    if (playerMelee.TryMeleeAttack(currentWeapon.firePoint))
                    {
                        return;
                    }
                }

                currentWeapon.Attack();

                if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
                {
                    Vector3 shootPos = currentWeapon.firePoint != null ? currentWeapon.firePoint.position : transform.position;
                    Vector3 shootDir = currentWeapon.firePoint != null ? currentWeapon.firePoint.right : transform.right;

                    NetworkManager.Instance.SendShootEvent(currentWeapon.name, shootPos, shootDir);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, aimRadius);

        if (playerMelee != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, playerMelee.meleeRadius);
        }
    }

    private void OnDisable()
    {

        if (currentTarget != null)
        {
            HandleTargetRingUI(currentTarget, false);
        }
        if (previousTarget != null)
        {
            HandleTargetRingUI(previousTarget, false);
        }
    }
}
