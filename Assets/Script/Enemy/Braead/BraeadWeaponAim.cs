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

    /// <summary>
    /// Bắn đạn thường 10 viên (Phase 1: bắn chùm hình nón; Phase 2: bung tỏa tròn 360 độ)
    /// </summary>
    public void ShootNormalBarrage(Vector2 targetPosition, bool isEnraged)
    {
        if (isLaserActive) return;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        Vector2 baseDir = (targetPosition - (Vector2)spawnPos).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        int bulletCount = 10;

        if (isEnraged)
        {
            // Phase 2 (Hóa nộ): Bắn 10 viên tỏa tròn đều 360 độ (Nova Bullet Hell)
            float stepAngle = 360f / bulletCount;
            for (int i = 0; i < bulletCount; i++)
            {
                float finalAngle = baseAngle + (i * stepAngle);
                FireSingleBullet(spawnPos, finalAngle);
            }
        }
        else
        {
            // Phase 1: Bắn chùm 10 viên hình nón (Cone Spread) nhắm về phía Player
            float spreadAngle = 50f; // Tổng góc mở hình nón
            float halfSpread = spreadAngle / 2f;
            float stepAngle = spreadAngle / (bulletCount - 1);

            for (int i = 0; i < bulletCount; i++)
            {
                float finalAngle = (baseAngle - halfSpread) + (i * stepAngle);
                FireSingleBullet(spawnPos, finalAngle);
            }
        }
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

        // Khởi tạo chỉ số từ Database (Bullet ID 24)
        MobBullet mobBullet = bullet.GetComponent<MobBullet>();
        if (mobBullet == null)
        {
            mobBullet = bullet.AddComponent<MobBullet>();
        }

        if (mobBullet != null)
        {
            mobBullet.InitFromDb(24);
        }
    }

    /// <summary>
    /// Kích hoạt chuỗi xả chiêu Laser 360 độ (Kỹ năng tối thượng khi HP <= 50%)
    /// </summary>
    public IEnumerator LaserSweepRoutine(float sweepDuration, Action onComplete = null)
    {
        isLaserActive = true;
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

            // 1. Raycast kiểm tra va chạm với Tường / Chướng ngại vật để tia laser bị chắn thực tế
            RaycastHit2D obstacleHit = Physics2D.Raycast(startPos, laserDir, laserMaxDistance, obstacleLayerMask);
            float actualDistance = laserMaxDistance;
            if (obstacleHit.collider != null && (obstacleHit.collider.CompareTag("Obstacle") || obstacleHit.collider.CompareTag("Door") || obstacleHit.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle")))
            {
                actualDistance = obstacleHit.distance;
                endPos = obstacleHit.point;
            }

            // 2. Raycast quét người chơi trong tầm tia laser (những người không đứng nấp sau cột/tường)
            RaycastHit2D[] playerHits = Physics2D.RaycastAll(startPos, laserDir, actualDistance);
            foreach (var hit in playerHits)
            {
                if (hit.collider == null) continue;

                // Kiểm tra Player cục bộ (RookieHealth)
                RookieHealth rookie = hit.collider.GetComponent<RookieHealth>();
                if (rookie == null) rookie = hit.collider.GetComponentInParent<RookieHealth>();

                if (rookie != null && !rookie.isDead)
                {
                    int id = rookie.gameObject.GetInstanceID();
                    if (!hitPlayerIds.Contains(id))
                    {
                        hitPlayerIds.Add(id);
                        // Gây sát thương -50% tổng máu và giáp của người chơi
                        int totalHpAndArmor = rookie.maxHealth + rookie.maxArmor;
                        int laserDamage = Mathf.Max(1, Mathf.RoundToInt(totalHpAndArmor * 0.5f));
                        rookie.TakeDamage(laserDamage);
                        Debug.Log($"[Braead Laser] Quét trúng Player! Gây {laserDamage} sát thương (-50% HP + Giáp)");
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

    private void OnDestroy()
    {
        if (instantiatedLaserObj != null)
        {
            Destroy(instantiatedLaserObj);
        }
    }
}
