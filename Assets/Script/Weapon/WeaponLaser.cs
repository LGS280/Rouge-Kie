using UnityEngine;
using System.Collections.Generic;

public class WeaponLaser : MonoBehaviour
{
    [Header("Cấu hình API kết nối (Tự động theo PrefabName)")]
    [HideInInspector] public int weaponDbId;

    [Header("prefab để vứt súng")]
    public GameObject weaponPrefab;

    [Header("cấu hình đạn laser")]
    public GameObject laserPrefab;
    public Transform firePoint;
    public float maxLaserDistance = 15f;

    [Header("đọc vị trí cầm súng lấy từ db")]
    [HideInInspector] public Vector3 customHandPosition;

    [Header("quản lý tag")]
    public string obstacleTag = "Obstacle";
    public string enemyTag = "Enemy";
    public string doorTag = "Door";

    public float maxLaserWidth = 1.0f;
    public float lerpSpeed = 15f;

    [Header("Hitbox & Tần suất sát thương")]
    [Tooltip("Độ dày vùng trúng đòn của tia laser (bán kính quét)")]
    public float laserHitboxRadius = 0.45f;
    [Tooltip("Khoảng thời gian giữa các lần giật sát thương khi chiếu liên tục (giây)")]
    public float damageTickInterval = 0.2f;

    [Header("Cấu hình Mana & Sát thương (Tự động từ DB)")]
    public int manaCostPerShot = 1;
    public float baseDamage = 36f;
    [HideInInspector] public float chargeDuration = 0.15f;

    private LineRenderer currentLaserLine;
    private Transform startGlowCircle;
    private Transform endGlowCircle;
    private GameObject instantiatedLaser;

    private bool isHoldingAttack = false;
    private bool isFiring = false;
    private float manaHoldTimer = 0f;
    private float currentLaserWidth = 0f;
    private float startGlowScale = 0f;

    private Dictionary<int, float> lastEnemyDamageTime = new Dictionary<int, float>();
    private PlayerMeleeSlash playerMelee;
    private RookieHealth playerHealth;

    void Start()
    {
        playerMelee = GetComponentInParent<PlayerMeleeSlash>();
        playerHealth = GetComponentInParent<RookieHealth>();

        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            if (firePoint == null) firePoint = transform;
        }

        ApplyConfigFromDb();
    }

    void OnEnable()
    {
        ApplyConfigFromDb();
        GameConfigManager.OnConfigLoaded += ApplyConfigFromDb;
    }

    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

        string keyName = weaponPrefab != null ? weaponPrefab.name : gameObject.name.Replace("(Clone)", "").Trim();

        // 1. Tìm theo PrefabName
        if (GameConfigManager.Instance.WeaponDbByName.TryGetValue(keyName, out WeaponConfig config))
        {
            return config;
        }

        // 2. Tìm theo ID
        if (weaponDbId > 0 && GameConfigManager.Instance.WeaponDb.TryGetValue(weaponDbId, out WeaponConfig idConfig))
        {
            return idConfig;
        }

        // 3. Fallback: Duyệt toàn bộ danh mục vũ khí trong DB
        foreach (var kvp in GameConfigManager.Instance.WeaponDb)
        {
            if (kvp.Value != null)
            {
                if (!string.IsNullOrEmpty(kvp.Value.prefabName) && kvp.Value.prefabName.Equals(keyName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
                if (!string.IsNullOrEmpty(kvp.Value.weaponName) && kvp.Value.weaponName.Equals(keyName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return null;
    }

    public void ApplyConfigFromDb()
    {
        WeaponConfig wConfig = GetWeaponConfig();
        if (wConfig != null)
        {
            chargeDuration = wConfig.fireRate;
            manaCostPerShot = wConfig.manaCost > 0 ? wConfig.manaCost : 1;

            customHandPosition = new Vector3(wConfig.handPositionX, wConfig.handPositionY, wConfig.handPositionZ);
            if (transform.parent != null && transform.parent.name.Contains("Hand"))
            {
                transform.localPosition = customHandPosition;
            }

            if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(wConfig.bulletId, out BulletConfig bConfig))
            {
                baseDamage = bConfig.damage;
            }

            Debug.Log($"[WeaponLaser] Đã nạp cấu hình từ DB: Vũ khí={wConfig.weaponName}, Sát thương={baseDamage}, ManaCost={manaCostPerShot}");
        }
    }

    void Update()
    {
        if (transform.parent == null || !transform.parent.name.Contains("Hand"))
        {
            isHoldingAttack = false;
            if (isFiring || instantiatedLaser != null)
            {
                StopLaser();
            }
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<RookieHealth>();
        }

        // Đảm bảo luôn lấy được sát thương từ DB nếu lúc đầu DB chưa tải xong
        if (baseDamage <= 0f)
        {
            ApplyConfigFromDb();
        }

        CheckAttackInput();

        if (isHoldingAttack && playerMelee != null && playerMelee.TryMeleeAttack(firePoint))
        {
            isHoldingAttack = false;
            StopLaser();
        }

        if (isHoldingAttack && laserPrefab != null && firePoint != null)
        {
            // 1. Phút đầu tiên bấm nút (1 Click): Kiểm tra trừ mana và phóng tia laser ngay lập tức
            if (!isFiring)
            {
                if (playerHealth != null && !playerHealth.UseMana(manaCostPerShot))
                {
                    // Không đủ mana -> Ngắt ngay, không cho bắn
                    isHoldingAttack = false;
                    StopLaser();
                    return;
                }

                isFiring = true;
                manaHoldTimer = 0f;

                EnsureLaserInstantiated();

                // 1 CLICK RA TIA LUÔN: Hiển thị độ rộng tối đa ngay lập tức
                currentLaserWidth = maxLaserWidth;
                startGlowScale = 1.0f;
            }
            else
            {
                // 2. Khi giữ bắn: Cứ mỗi 1 giây trôi qua là 1 lần tốn mana
                manaHoldTimer += Time.deltaTime;
                if (manaHoldTimer >= 1.0f)
                {
                    manaHoldTimer -= 1.0f;

                    if (playerHealth != null && !playerHealth.UseMana(manaCostPerShot))
                    {
                        // Hết mana giữa chừng -> Dừng laser ngay lập tức
                        isHoldingAttack = false;
                        isFiring = false;
                        StopLaser();
                        return;
                    }
                }

                currentLaserWidth = Mathf.Lerp(currentLaserWidth, maxLaserWidth, Time.deltaTime * lerpSpeed);
                startGlowScale = Mathf.Lerp(startGlowScale, 1.0f, Time.deltaTime * lerpSpeed);
            }

            AnimateAndCalculateLaser();
        }
        else
        {
            // Khi nhả nút bắn: thu nhỏ tia laser và tắt dần
            isFiring = false;
            manaHoldTimer = 0f;

            if (instantiatedLaser != null)
            {
                currentLaserWidth = Mathf.Lerp(currentLaserWidth, 0f, Time.deltaTime * lerpSpeed * 2.5f);
                startGlowScale = Mathf.Lerp(startGlowScale, 0f, Time.deltaTime * lerpSpeed * 2.5f);

                AnimateAndCalculateLaser();

                if (currentLaserWidth < 0.02f && startGlowScale < 0.02f)
                {
                    StopLaser();
                }
            }
        }
    }

    private void EnsureLaserInstantiated()
    {
        if (instantiatedLaser == null && laserPrefab != null && firePoint != null)
        {
            instantiatedLaser = Instantiate(laserPrefab, firePoint.position, Quaternion.identity, firePoint);
            currentLaserLine = instantiatedLaser.GetComponent<LineRenderer>();
            startGlowCircle = instantiatedLaser.transform.Find("Start_Glow");
            endGlowCircle = instantiatedLaser.transform.Find("End_Glow");

            if (currentLaserLine != null) currentLaserLine.widthMultiplier = maxLaserWidth;
            if (startGlowCircle != null) startGlowCircle.localScale = Vector3.one;
            if (endGlowCircle != null) endGlowCircle.localScale = Vector3.one;
        }
    }

    void CheckAttackInput()
    {
        WeaponManager wm = GetComponentInParent<WeaponManager>();
        if (wm != null && wm.nearbyWeapons.Count > 0)
        {
            isHoldingAttack = false;
            return;
        }

        PlayerController pc = GetComponentInParent<PlayerController>();
        if (pc != null && pc.currentMode == PlayerController.InputMode.Gamepad)
        {
            isHoldingAttack = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.xButton.isPressed;
        }
        else
        {
            isHoldingAttack = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
        }
    }

    void AnimateAndCalculateLaser()
    {
        if (currentLaserLine == null) return;

        currentLaserLine.widthMultiplier = currentLaserWidth;

        if (startGlowCircle != null)
        {
            startGlowCircle.position = firePoint.position;
            startGlowCircle.localScale = Vector3.one * startGlowScale;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(firePoint.position, firePoint.right, maxLaserDistance);
        Vector3 finalEndPoint = firePoint.position + firePoint.right * maxLaserDistance;

        foreach (var hit in hits)
        {
            if (hit.collider != null)
            {
                if (hit.collider.CompareTag(obstacleTag) || hit.collider.CompareTag(doorTag))
                {
                    finalEndPoint = hit.point;
                    break;
                }
            }
        }

        currentLaserLine.SetPosition(0, firePoint.position);
        currentLaserLine.SetPosition(1, finalEndPoint);

        if (endGlowCircle != null)
        {
            endGlowCircle.position = finalEndPoint;
            endGlowCircle.localScale = Vector3.one * (currentLaserWidth / maxLaserWidth);
            endGlowCircle.gameObject.SetActive(currentLaserWidth > 0.05f);
        }

        if (currentLaserWidth > maxLaserWidth * 0.25f)
        {
            ApplyLaserDamage(finalEndPoint);
        }
    }

    void ApplyLaserDamage(Vector3 endPoint)
    {
        Vector2 startPos = firePoint.position;
        Vector2 diff = (Vector2)endPoint - startPos;
        float dist = diff.magnitude;
        Vector2 dir = dist > 0.001f ? diff.normalized : (Vector2)firePoint.right;

        // Quét hình trụ dày (CircleCastAll) theo độ dày của tia laser thay vì dùng Linecast mỏng 0 pixel
        float radius = Mathf.Max(laserHitboxRadius, maxLaserWidth * 0.5f);
        RaycastHit2D[] enemyHits = Physics2D.CircleCastAll(startPos, radius, dir, dist);

        HashSet<int> currentFrameEnemies = new HashSet<int>();

        float damageToDeal = baseDamage;
        if (PlayerBuffManager.Instance != null)
        {
            damageToDeal *= PlayerBuffManager.Instance.damageMultiplier;
        }
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damageToDeal));

        foreach (var enemyHit in enemyHits)
        {
            if (enemyHit.collider != null && enemyHit.collider.CompareTag(enemyTag))
            {
                int enemyID = enemyHit.collider.GetInstanceID();
                currentFrameEnemies.Add(enemyID);

                MobHealth enemyHealth = enemyHit.collider.GetComponent<MobHealth>();
                if (enemyHealth != null && !enemyHealth.isDead)
                {
                    // Cứ chiếu trúng là trừ sát thương ngay lập tức, và giật sát thương liên tục mỗi damageTickInterval (0.2s)
                    if (!lastEnemyDamageTime.ContainsKey(enemyID) || (Time.time - lastEnemyDamageTime[enemyID] >= damageTickInterval))
                    {
                        lastEnemyDamageTime[enemyID] = Time.time;
                        enemyHealth.TakeDamage(finalDamage, false);
                    }
                }
            }
        }

        // Dọn dẹp cache cho các kẻ địch đã ra khỏi phạm vi tia laser sau khi hết nhịp cooldown
        List<int> keysToRemove = new List<int>();
        foreach (var key in lastEnemyDamageTime.Keys)
        {
            if (!currentFrameEnemies.Contains(key) && (Time.time - lastEnemyDamageTime[key] >= damageTickInterval))
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            lastEnemyDamageTime.Remove(key);
        }
    }

    public void StopLaser()
    {
        isFiring = false;
        manaHoldTimer = 0f;

        if (instantiatedLaser != null)
        {
            Destroy(instantiatedLaser);
            instantiatedLaser = null;
        }

        if (firePoint != null)
        {
            foreach (Transform child in firePoint)
            {
                if (child.name.Contains("Laser_Beam_Effect") || child.GetComponent<LineRenderer>() != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        currentLaserLine = null;
        startGlowCircle = null;
        endGlowCircle = null;
        lastEnemyDamageTime.Clear();
    }

    void OnDisable()
    {
        StopLaser();
        GameConfigManager.OnConfigLoaded -= ApplyConfigFromDb;
    }

    void OnDestroy()
    {
        StopLaser();
        GameConfigManager.OnConfigLoaded -= ApplyConfigFromDb;
    }
}
