using UnityEngine;
using System.Collections.Generic;

public class WeaponLaser : MonoBehaviour
{
    [Header("Cấu hình API kết nối")]
    public int weaponDbId;

    [Header("Prefab tham chiếu để vứt súng")]
    public GameObject weaponPrefab;

    [Header("--- THIẾT LẬP LASER ---")]
    public GameObject laserPrefab;
    public Transform firePoint;
    public float maxLaserDistance = 15f;

    [Header("VỊ TRÍ CẦM SÚNG (Đọc từ DB)")]
    [HideInInspector] public Vector3 customHandPosition;

    [Header("--- THIẾT LẬP TAG ---")]
    public string obstacleTag = "Obstacle";
    public string enemyTag = "Enemy";
    public string doorTag = "Door";

    public float maxLaserWidth = 1.0f;
    public float lerpSpeed = 15f;

    [HideInInspector] public float baseDamage;
    [HideInInspector] public float chargeDuration;

    private LineRenderer currentLaserLine;
    private Transform startGlowCircle;
    private Transform endGlowCircle;
    private GameObject instantiatedLaser;

    private bool isHoldingAttack = false;
    private float chargeTimer = 0f;
    private float currentLaserWidth = 0f;
    private float startGlowScale = 0f;

    private Dictionary<int, float> damageAccumulators = new Dictionary<int, float>();
    private PlayerMeleeSlash playerMelee;

    void Start()
    {
        playerMelee = GetComponentInParent<PlayerMeleeSlash>();
    }

    void OnEnable()
    {
        ApplyConfigFromDb();
        GameConfigManager.OnConfigLoaded += ApplyConfigFromDb;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (weaponPrefab == null)
        {
            string myPath = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
            if (!string.IsNullOrEmpty(myPath))
            {
                weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(myPath);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
    }
#endif

    public void ApplyConfigFromDb()
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.WeaponDb.TryGetValue(weaponDbId, out WeaponConfig wConfig))
        {
            chargeDuration = wConfig.fireRate;

            // Nạp vị trí cầm súng từ DB
            customHandPosition = new Vector3(wConfig.handPositionX, wConfig.handPositionY, wConfig.handPositionZ);
            transform.localPosition = customHandPosition;

            if (GameConfigManager.Instance.BulletDb.TryGetValue(wConfig.bulletId, out BulletConfig bConfig))
            {
                baseDamage = bConfig.damage;
            }
        }
    }

    void Update()
    {
        if (transform.parent == null || !transform.parent.name.Contains("Hand"))
        {
            isHoldingAttack = false;
            StopLaser();
            return;
        }

        CheckAttackInput();

        if (isHoldingAttack && playerMelee != null && playerMelee.TryMeleeAttack(firePoint))
        {
            isHoldingAttack = false;
        }

        if (isHoldingAttack && laserPrefab != null && firePoint != null)
        {
            if (instantiatedLaser == null)
            {
                instantiatedLaser = Instantiate(laserPrefab, firePoint.position, Quaternion.identity, firePoint);
                currentLaserLine = instantiatedLaser.GetComponent<LineRenderer>();
                startGlowCircle = instantiatedLaser.transform.Find("Start_Glow");
                endGlowCircle = instantiatedLaser.transform.Find("End_Glow");

                chargeTimer = 0f;
                currentLaserWidth = 0f;
                startGlowScale = 0f;

                if (currentLaserLine != null) currentLaserLine.widthMultiplier = 0f;
                if (startGlowCircle != null) startGlowCircle.localScale = Vector3.zero;
                if (endGlowCircle != null) endGlowCircle.localScale = Vector3.zero;
            }

            chargeTimer += Time.deltaTime;

            if (chargeTimer < chargeDuration)
            {
                startGlowScale = Mathf.Lerp(startGlowScale, 1.2f, Time.deltaTime * lerpSpeed);
                currentLaserWidth = 0f;
            }
            else
            {
                startGlowScale = Mathf.Lerp(startGlowScale, 1.0f, Time.deltaTime * lerpSpeed);
                currentLaserWidth = Mathf.Lerp(currentLaserWidth, maxLaserWidth, Time.deltaTime * lerpSpeed);
            }

            AnimateAndCalculateLaser();
        }
        else
        {
            if (instantiatedLaser != null)
            {
                currentLaserWidth = Mathf.Lerp(currentLaserWidth, 0f, Time.deltaTime * lerpSpeed * 1.5f);
                startGlowScale = Mathf.Lerp(startGlowScale, 0f, Time.deltaTime * lerpSpeed * 1.5f);

                AnimateAndCalculateLaser();

                if (currentLaserWidth < 0.01f && startGlowScale < 0.01f)
                {
                    StopLaser();
                }
            }
        }
    }

    void CheckAttackInput()
    {
        // Khóa bắn laser nếu người chơi đang đứng gần súng trên đất để nhặt
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

        if (currentLaserWidth > maxLaserWidth * 0.5f)
        {
            ApplyLaserDamage(finalEndPoint);
        }
    }

    void ApplyLaserDamage(Vector3 endPoint)
    {
        RaycastHit2D[] enemyHits = Physics2D.LinecastAll(firePoint.position, endPoint);
        HashSet<int> currentFrameEnemies = new HashSet<int>();

        foreach (var enemyHit in enemyHits)
        {
            if (enemyHit.collider != null && enemyHit.collider.CompareTag(enemyTag))
            {
                int enemyID = enemyHit.collider.GetInstanceID();
                currentFrameEnemies.Add(enemyID);

                MobHealth enemyHealth = enemyHit.collider.GetComponent<MobHealth>();
                if (enemyHealth != null)
                {
                    if (!damageAccumulators.ContainsKey(enemyID))
                    {
                        damageAccumulators[enemyID] = 0f;
                    }

                    float finalDamage = baseDamage;
                    if (PlayerBuffManager.Instance != null)
                    {
                        finalDamage *= PlayerBuffManager.Instance.damageMultiplier;
                    }

                    damageAccumulators[enemyID] += finalDamage * Time.deltaTime;

                    if (damageAccumulators[enemyID] >= 1f)
                    {
                        int damageToApply = Mathf.FloorToInt(damageAccumulators[enemyID]);
                        enemyHealth.TakeDamage(damageToApply, false);
                        damageAccumulators[enemyID] -= damageToApply;
                    }
                }
            }
        }

        List<int> keysToRemove = new List<int>();
        foreach (var key in damageAccumulators.Keys)
        {
            if (!currentFrameEnemies.Contains(key))
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            damageAccumulators.Remove(key);
        }
    }

    public void StopLaser()
    {
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
        damageAccumulators.Clear();
    }

    void OnDisable()
    {
        StopLaser();
        GameConfigManager.OnConfigLoaded -= ApplyConfigFromDb;
    }

    void OnDestroy()
    {
        StopLaser();
    }
}