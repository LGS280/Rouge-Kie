using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý cơ chế xoay ngắm, bắn đạn thường (chùm 10 viên / tỏa tròn 360) và xả chiêu Laser 360 độ cho vũ khí Braead_Weapon
/// </summary>
public class BraeadWeaponAim : MonoBehaviour
{
    [Header("Điểm bắn đạn (FirePoint)")]
    public Transform firePoint;

    [Header("Prefabs & Materials")]
    [Tooltip("Prefab đạn thường Braead_Bullet")]
    public GameObject bulletPrefab;

    [Tooltip("Prefab tia Laser Braead_Laser (chứa LineRenderer)")]
    public GameObject laserPrefab;

    [Tooltip("Material tia Laser dự phòng (Braead_Laser_Beam)")]
    public Material laserMaterial;

    [Header("Cấu hình tia Laser")]
    public float laserMaxDistance = 30f;
    public float laserWidth = 0.85f;

    [Header("Layer Mask va chạm Laser")]
    public LayerMask obstacleLayerMask;
    public LayerMask playerLayerMask;

    [HideInInspector] public bool isLaserActive = false;

    private LineRenderer activeLaserLine;
    private GameObject instantiatedLaserObj;
    private readonly HashSet<int> hitPlayerIds = new HashSet<int>();

    private void Awake()
    {
        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            if (firePoint == null) firePoint = transform;
        }

        // Tự động tìm nạp Prefab đạn và Laser nếu chưa gán trên Inspector
        AutoLoadAssets();

        // Thiết lập LayerMask mặc định nếu chưa gán
        if (obstacleLayerMask.value == 0)
        {
            obstacleLayerMask = LayerMask.GetMask("Obstacle", "Door", "Wall", "Default");
        }
        if (playerLayerMask.value == 0)
        {
            playerLayerMask = LayerMask.GetMask("Player", "Default");
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoConfigureEditor();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoConfigureEditor();
        }
    }

    private void AutoConfigureEditor()
    {
        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            if (firePoint == null) firePoint = transform;
        }

        if (bulletPrefab == null)
        {
            bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Mobs_Weapons/Bullets/Braead_Bullet.prefab");
        }
        if (laserPrefab == null)
        {
            laserPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Mobs_Weapons/Bullets/Braead_Laser.prefab");
        }
        if (laserMaterial == null)
        {
            laserMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Weapons/Materials/Braead_Laser_Beam.mat");
        }

        if (obstacleLayerMask.value == 0)
        {
            obstacleLayerMask = LayerMask.GetMask("Obstacle", "Door", "Wall", "Default");
        }
        if (playerLayerMask.value == 0)
        {
            playerLayerMask = LayerMask.GetMask("Player");
        }
    }
#endif

    private void AutoLoadAssets()
    {
#if UNITY_EDITOR
        AutoConfigureEditor();
#endif
    }

    /// <summary>
    /// Luôn luôn xoay ngắm thẳng về phía người chơi (khi không trong trạng thái xả laser)
    /// </summary>
    public void AimAtTarget(Transform target)
    {
        if (isLaserActive || target == null) return;

        Vector2 aimDirection = (Vector2)target.position - (Vector2)transform.position;
        if (aimDirection.sqrMagnitude < 0.001f) return;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Lật mặt vũ khí theo trục Y để không bị lộn ngược khi nhắm sang trái
        Vector3 localScale = transform.localScale;
        localScale.y = (aimDirection.x < 0) ? -Mathf.Abs(localScale.y) : Mathf.Abs(localScale.y);
        transform.localScale = localScale;
    }

    private Coroutine currentBarrageCoroutine;
    private bool sweepLeftToRight = true;

    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

        // Lấy tên Prefab sạch (bỏ (Clone) nếu có), đồng bộ chuẩn 100% với MobWeaponInfo và WeaponInfo
        string cleanName = gameObject.name.Replace("(Clone)", "").Trim();

        // 1. Tìm trực tiếp theo tên Prefab hiện tại trong WeaponDbByName
        if (GameConfigManager.Instance.WeaponDbByName.TryGetValue(cleanName, out WeaponConfig config))
        {
            return config;
        }

        // 2. Fallback nếu tên GameObject có biến thể khác nhưng map về Braead_Weapon
        if (GameConfigManager.Instance.WeaponDbByName.TryGetValue("Braead_Weapon", out config))
        {
            return config;
        }

        return null;
    }

    /// <summary>
    /// Bắn đạn thường (Phase 1: xả liên thanh giống Gatling của Melog; Phase 2: bung tỏa tròn 360 độ)
    /// Lấy chỉ số linh hoạt từ Database (bulletsPerShot và spreadAngle của Weapon ID 22)
    /// </summary>
    public void ShootNormalBarrage(Transform targetTransform, Vector2 fallbackTargetPos, bool isEnraged)
    {
        if (isLaserActive) return;

        WeaponConfig wConfig = GetWeaponConfig();
        int bulletCount = (wConfig != null && wConfig.bulletsPerShot > 0) ? wConfig.bulletsPerShot : 20;
        float spreadAngle = (wConfig != null && wConfig.spreadAngle > 0) ? wConfig.spreadAngle : 18f;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        Vector2 targetPos = (targetTransform != null) ? (Vector2)targetTransform.position : fallbackTargetPos;
        Vector2 baseDir = (targetPos - (Vector2)spawnPos).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        if (isEnraged)
        {
            // Phase 2 (Hóa nộ): Bắn tỏa tròn đều 360 độ (Nova Bullet Hell)
            if (currentBarrageCoroutine != null)
            {
                StopCoroutine(currentBarrageCoroutine);
                currentBarrageCoroutine = null;
            }

            float stepAngle = 360f / bulletCount;
            for (int i = 0; i < bulletCount; i++)
            {
                float finalAngle = baseAngle + (i * stepAngle);
                FireSingleBullet(spawnPos, finalAngle);
            }
        }
        else
        {
            // Phase 1 (Xả liên thanh Gatling giống Melog):
            // Bắn lần lượt bulletCount viên (mỗi viên có độ lệch ngẫu nhiên Random.Range(-spreadAngle, spreadAngle))
            if (currentBarrageCoroutine != null)
            {
                StopCoroutine(currentBarrageCoroutine);
            }
            currentBarrageCoroutine = StartCoroutine(GatlingBurstRoutine(targetTransform, fallbackTargetPos, bulletCount, spreadAngle));
        }
    }

    public void ShootNormalBarrage(Vector2 targetPosition, bool isEnraged)
    {
        ShootNormalBarrage(null, targetPosition, isEnraged);
    }

    private IEnumerator GatlingBurstRoutine(Transform targetTransform, Vector2 fallbackTargetPos, int bulletCount, float spreadAngle)
    {
        for (int i = 0; i < bulletCount; i++)
        {
            if (isLaserActive) yield break;

            Vector3 currentSpawn = (firePoint != null) ? firePoint.position : transform.position;
            Vector2 currentTargetPos = (targetTransform != null) ? (Vector2)targetTransform.position : fallbackTargetPos;
            Vector2 baseDir = (currentTargetPos - (Vector2)currentSpawn).normalized;
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            // Cơ chế giống hệt Gatling của Melog: mỗi viên đạn bắn nhắm về Player kèm góc lệch ngẫu nhiên trong khoảng [-spreadAngle, +spreadAngle]
            float randomSpread = UnityEngine.Random.Range(-spreadAngle, spreadAngle);
            float finalAngle = baseAngle + randomSpread;

            FireSingleBullet(currentSpawn, finalAngle);

            yield return new WaitForSeconds(0.05f); // Nhịp bắn liên thanh 0.05s giống súng Gatling của Melog
        }

        currentBarrageCoroutine = null;
    }

    private void FireSingleBullet(Vector3 spawnPos, float angleDeg)
    {
        if (bulletPrefab == null)
        {
            AutoLoadAssets();
            if (bulletPrefab == null) return;
        }

        Quaternion rotation = Quaternion.Euler(0, 0, angleDeg);
        GameObject bullet = Instantiate(bulletPrefab, spawnPos, rotation);

        // Gán đúng layer đạn quái
        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            bullet.layer = enemyBulletLayer;
        }

        // Khởi tạo chỉ số từ Database dựa theo cấu hình súng (không fix cứng ID)
        MobBullet mobBullet = bullet.GetComponent<MobBullet>();
        if (mobBullet == null)
        {
            mobBullet = bullet.AddComponent<MobBullet>();
        }

        if (mobBullet != null)
        {
            WeaponConfig wConfig = GetWeaponConfig();
            if (wConfig != null && wConfig.bulletId > 0)
            {
                mobBullet.InitFromDb(wConfig.bulletId);
            }
        }
    }

    /// <summary>
    /// Kích hoạt chuỗi xả chiêu Laser 360 độ (Kỹ năng tối thượng khi HP <= 50%)
    /// </summary>
    public IEnumerator LaserSweepRoutine(float sweepDuration, Action onComplete = null)
    {
        isLaserActive = true;
        if (currentBarrageCoroutine != null)
        {
            StopCoroutine(currentBarrageCoroutine);
            currentBarrageCoroutine = null;
        }

        hitPlayerIds.Clear();

        PrepareLaserLineRenderer();

        if (activeLaserLine != null)
        {
            activeLaserLine.enabled = true;
        }

        float currentAngle = transform.eulerAngles.z;
        float elapsed = 0f;

        while (elapsed < sweepDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / sweepDuration);

            // Quay đều đúng 1 vòng tròn 360 độ
            float angle = currentAngle + (progress * 360f);
            transform.rotation = Quaternion.Euler(0, 0, angle);

            Vector2 laserDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            // Cập nhật trục xoay vũ khí
            Vector3 localScale = transform.localScale;
            localScale.y = (laserDir.x < 0) ? -Mathf.Abs(localScale.y) : Mathf.Abs(localScale.y);
            transform.localScale = localScale;

            Vector3 startPos = (firePoint != null) ? firePoint.position : transform.position;
            Vector3 endPos = startPos + (Vector3)(laserDir * laserMaxDistance);
            float actualDistance = laserMaxDistance;

            // 1. Raycast quét tất cả vật thể để chặn tia laser tại tường / cửa / chướng ngại vật đầu tiên
            RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, laserDir, laserMaxDistance);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.isTrigger) continue; // Bỏ qua trigger vùng phòng, teleport, item...
                if (hit.collider.transform.IsChildOf(transform.root)) continue; // Bỏ qua bản thân boss

                // Kiểm tra xem có phải vật cản (Tilemap Tường, Cửa phòng, Chướng ngại vật)
                if (hit.collider.CompareTag("Obstacle") || hit.collider.CompareTag("Door") ||
                    hit.collider.gameObject.name.Contains("Wall") || hit.collider.gameObject.name.Contains("Door") || hit.collider.gameObject.name.Contains("Obstacle"))
                {
                    actualDistance = hit.distance;
                    endPos = hit.point;
                    break; // Tia laser dừng lại ngay tại vật cản đầu tiên gặp phải
                }
            }

            // 2. Raycast quét người chơi trong tầm tia laser (chỉ trong khoảng actualDistance trước vật cản)
            RaycastHit2D[] playerHits = Physics2D.RaycastAll(startPos, laserDir, actualDistance);
            foreach (var hit in playerHits)
            {
                if (hit.collider == null) continue;
                if (hit.distance > actualDistance) continue; // Nấp sau tường thì không bị trúng

                // Kiểm tra Player cục bộ (RookieHealth)
                RookieHealth rookie = hit.collider.GetComponent<RookieHealth>();
                if (rookie == null) rookie = hit.collider.GetComponentInParent<RookieHealth>();

                if (rookie != null && !rookie.isDead)
                {
                    int id = rookie.gameObject.GetInstanceID();
                    if (!hitPlayerIds.Contains(id))
                    {
                        hitPlayerIds.Add(id);

                        // Sát thương: -50% sinh lực HIỆN TẠI (Máu + Giáp), lấy phần nguyên máu còn lại
                        int currentPool = rookie.GetCurrentHealth() + rookie.GetCurrentArmor();
                        int remainingPool = Mathf.FloorToInt(currentPool * 0.5f);
                        int laserDamage = Mathf.Max(1, currentPool - remainingPool);

                        rookie.TakeDamage(laserDamage);
                        Debug.Log($"[Braead Laser] Quét trúng Player! Hiện tại: {currentPool} (HP:{rookie.GetCurrentHealth()}, Giáp:{rookie.GetCurrentArmor()}) -> Gây {laserDamage} sát thương");
                    }
                }

                // Kiểm tra Player mạng trong phòng Co-op
                RemotePlayerController rpc = hit.collider.GetComponent<RemotePlayerController>();
                if (rpc == null) rpc = hit.collider.GetComponentInParent<RemotePlayerController>();

                if (rpc != null && !rpc.isDead)
                {
                    int id = rpc.gameObject.GetInstanceID();
                    if (!hitPlayerIds.Contains(id))
                    {
                        hitPlayerIds.Add(id);
                        int laserDamage = 5; // Sát thương mặc định cho remote player (server đồng bộ)
                        if (NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn)
                        {
                            NetworkManager.Instance.SendPlayerDamaged(rpc.connectionId, laserDamage);
                        }
                    }
                }
            }

            // Cập nhật vị trí hiển thị của LineRenderer
            if (activeLaserLine != null)
            {
                activeLaserLine.SetPosition(0, startPos);
                activeLaserLine.SetPosition(1, endPos);
            }

            yield return null;
        }

        // Kết thúc đợt quét Laser
        if (activeLaserLine != null)
        {
            activeLaserLine.enabled = false;
        }

        hitPlayerIds.Clear();
        isLaserActive = false;

        onComplete?.Invoke();
    }

    private void PrepareLaserLineRenderer()
    {
        if (activeLaserLine != null) return;

        if (laserPrefab != null)
        {
            instantiatedLaserObj = Instantiate(laserPrefab, transform);
            activeLaserLine = instantiatedLaserObj.GetComponent<LineRenderer>();
        }

        if (activeLaserLine == null)
        {
            GameObject lineObj = new GameObject("Braead_Laser_Line");
            lineObj.transform.SetParent(transform, false);
            activeLaserLine = lineObj.AddComponent<LineRenderer>();
            activeLaserLine.startWidth = laserWidth;
            activeLaserLine.endWidth = laserWidth;
            activeLaserLine.sortingOrder = 12;

            if (laserMaterial != null)
            {
                activeLaserLine.material = laserMaterial;
            }
            else
            {
                activeLaserLine.material = new Material(Shader.Find("Sprites/Default"));
                activeLaserLine.startColor = new Color(1f, 0.1f, 0.4f, 0.95f);
                activeLaserLine.endColor = new Color(1f, 0.3f, 0.6f, 0.95f);
            }
        }

        activeLaserLine.positionCount = 2;
        activeLaserLine.enabled = false;
    }

    private void OnDisable()
    {
        if (currentBarrageCoroutine != null)
        {
            StopCoroutine(currentBarrageCoroutine);
            currentBarrageCoroutine = null;
        }

        if (activeLaserLine != null)
        {
            activeLaserLine.enabled = false;
        }
        isLaserActive = false;
    }

    private void OnDestroy()
    {
        if (instantiatedLaserObj != null)
        {
            Destroy(instantiatedLaserObj);
        }
    }
}
