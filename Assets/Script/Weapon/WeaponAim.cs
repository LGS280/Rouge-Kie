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

        if ((ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen()) ||
            (WeaponVaultUIController.Instance != null && WeaponVaultUIController.Instance.IsVaultOpen()))
        {
            if (currentWeapon != null && currentWeapon.IsBowCharging())
            {
                currentWeapon.CancelBowCharge();
            }
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (currentWeapon != null && currentWeapon.IsBowCharging())
            {
                currentWeapon.CancelBowCharge();
            }
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

        WeaponInfo detectedWeapon = GetComponentInChildren<WeaponInfo>();
        if (detectedWeapon != currentWeapon)
        {
            if (currentWeapon != null && currentWeapon.IsBowCharging())
            {
                currentWeapon.CancelBowCharge();
            }
            currentWeapon = detectedWeapon;
        }

        if (currentWeapon != null)
        {
            float upFactor = Mathf.Clamp01(1f - Mathf.Abs(angle - 90f) / 45f);
            Vector3 targetUpPos = currentWeapon.IsBowWeapon()
                ? new Vector3(currentWeapon.upAimOffset, 0f, 0f)
                : Vector3.zero;
            Vector3 handPos = Vector3.Lerp(currentWeapon.customHandPosition, targetUpPos, upFactor);
            currentWeapon.transform.localPosition = handPos + currentWeapon.GetDrawBackOffset();
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
        if (weaponManager != null && weaponManager.nearbyWeapons.Count > 0)
        {
            if (currentWeapon != null && currentWeapon.IsBowCharging())
            {
                currentWeapon.CancelBowCharge();
            }
            return;
        }

        if (currentWeapon == null) return;

        float currentFireRate = PlayerStats.Instance != null
            ? currentWeapon.fireRate / PlayerStats.Instance.attackSpeedMultiplier
            : currentWeapon.fireRate;

        if (PlayerBuffManager.Instance != null)
        {
            currentFireRate *= PlayerBuffManager.Instance.fireRateMultiplier;
        }

        bool isHoldingFire = false;
        bool wasFirePressedThisFrame = false;
        bool wasFireReleasedThisFrame = false;

        if (playerController != null && playerController.currentMode == PlayerController.InputMode.Gamepad)
        {
            if (Gamepad.current != null)
            {
                isHoldingFire = Gamepad.current.xButton.isPressed;
                wasFirePressedThisFrame = Gamepad.current.xButton.wasPressedThisFrame;
                wasFireReleasedThisFrame = Gamepad.current.xButton.wasReleasedThisFrame;
            }
        }
        else
        {
            if (Mouse.current != null)
            {
                isHoldingFire = Mouse.current.leftButton.isPressed;
                wasFirePressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
                wasFireReleasedThisFrame = Mouse.current.leftButton.wasReleasedThisFrame;
            }
            else
            {
                isHoldingFire = Input.GetMouseButton(0);
                wasFirePressedThisFrame = Input.GetMouseButtonDown(0);
                wasFireReleasedThisFrame = Input.GetMouseButtonUp(0);
            }
        }

        // --- BOW WEAPON MECHANIC (Charge & Release) ---
        if (currentWeapon.IsBowWeapon())
        {
            if (isHoldingFire)
            {
                if (!currentWeapon.IsBowCharging())
                {
                    if (Time.time >= nextFireTime)
                    {
                        currentWeapon.StartBowCharge();
                    }
                }
                else
                {
                    currentWeapon.UpdateBowCharge(Time.deltaTime);
                }
            }
            else
            {
                if (currentWeapon.IsBowCharging())
                {
                    currentWeapon.ReleaseBowCharge();
                    nextFireTime = Time.time + Mathf.Max(0.12f, currentFireRate * 0.4f);

                    if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
                    {
                        Vector3 shootPos = currentWeapon.firePoint != null ? currentWeapon.firePoint.position : transform.position;
                        Vector3 shootDir = currentWeapon.firePoint != null ? currentWeapon.firePoint.right : transform.right;

                        NetworkManager.Instance.SendShootEvent(currentWeapon.name, shootPos, shootDir);
                    }
                }
            }
            return;
        }

        // --- STANDARD WEAPONS (Guns / Melee) ---
        if (Time.time >= nextFireTime)
        {
            if (isHoldingFire)
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
        if (currentWeapon != null && currentWeapon.IsBowCharging())
        {
            currentWeapon.CancelBowCharge();
        }

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
