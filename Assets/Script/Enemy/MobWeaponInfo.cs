using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component quản lý Vũ Khí dành riêng cho Quái (Mob / Enemy).
/// Đã được tối ưu sạch sẽ 100% giống WeaponInfo của Player:
/// Loại bỏ toàn bộ các biến lưu trữ thừa, trực tiếp tra cứu DB động qua GameConfigManager.
/// </summary>
public class MobWeaponInfo : MonoBehaviour
{
    [Header("Vị trí nòng súng / Đầu thương")]
    public Transform firePoint;

    [Header("Âm thanh bắn súng")]
    public AudioClip shootSoundClip;

    [HideInInspector] public GameObject cachedBulletPrefab;
    [HideInInspector] public bool isMelee = false;
    public bool IsMelee
    {
        get
        {
            GetWeaponConfig();
            return isMelee;
        }
    }

    private void Awake()
    {
        if (firePoint == null)
        {
            firePoint = transform.Find("FirePoint");
            if (firePoint == null) firePoint = transform;
        }
    }

    /// <summary>
    /// Tra cứu trực tiếp WeaponConfig từ DB dựa theo PrefabName (Ví dụ: Brog's_Spear, Old_Pistol)
    /// </summary>
    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

        // 🎯 DỰA HẲN VÀO PREFABNAME ĐỂ TRA CỨU DB WEAPONCONFIGS
        string cleanName = gameObject.name.Replace("(Clone)", "").Trim();

        if (cleanName.ToLower().Contains("spear") || cleanName.ToLower().Contains("sword") || cleanName.ToLower().Contains("blade"))
        {
            isMelee = true;
        }

        if (GameConfigManager.Instance.WeaponDbByName.TryGetValue(cleanName, out WeaponConfig config))
        {
            return config;
        }

        return null;
    }

    /// <summary>
    /// Tra cứu trực tiếp BulletConfig từ DB
    /// </summary>
    public BulletConfig GetBulletConfig()
    {
        WeaponConfig wConfig = GetWeaponConfig();
        if (wConfig != null && wConfig.bulletId > 0)
        {
            if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(wConfig.bulletId, out BulletConfig bConfig))
            {
                return bConfig;
            }
        }
        return null;
    }

    public void ApplyMobWeaponConfig()
    {
        transform.localPosition = Vector3.zero;

        BulletConfig bConfig = GetBulletConfig();
        if (bConfig != null && !string.IsNullOrEmpty(bConfig.prefabName))
        {
            LoadBulletPrefab(bConfig.prefabName);
        }
        else if (cachedBulletPrefab == null && !isMelee)
        {
            LoadBulletPrefab("Bullet_Old_Pistol");
        }
    }

    private void LoadBulletPrefab(string prefabName)
    {
#if UNITY_EDITOR
        // Quét tự động trong thư mục Assets/Prefab/Mobs_Weapons/Bullets/ theo prefabName từ DB
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/Mobs_Weapons/Bullets" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLower().Contains("/" + prefabName.ToLower() + ".prefab"))
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    cachedBulletPrefab = prefab;
                    return;
                }
            }
        }

        // Dự phòng lấy prefab đạn bất kỳ trong folder nếu không khớp tên tuyệt đối
        if (guids != null && guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedBulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (cachedBulletPrefab != null) return;
        }
#endif

        // Dự phòng nạp từ Resources
        GameObject loaded = Resources.Load<GameObject>("Prefab/Mobs_Weapons/Bullets/" + prefabName);
        if (loaded == null) loaded = Resources.Load<GameObject>(prefabName);
        if (loaded == null) loaded = Resources.Load<GameObject>("Bullet_Old_Pistol");

        if (loaded != null)
        {
            cachedBulletPrefab = loaded;
        }
    }

    /// <summary>
    /// Thực thi bắn đạn từ súng Quái hướng về phía Player (Nạp dữ liệu trực tiếp từ DB)
    /// </summary>
    public void Shoot(Vector2 targetPosition, int damageOverride)
    {
        WeaponConfig wConfig = GetWeaponConfig();
        BulletConfig bConfig = GetBulletConfig();

        if (isMelee)
        {
            PlayShootSound();
            StartCoroutine(MeleeAttackRoutine(bConfig));
            return;
        }

        int bulletCount = 1;
        float spread = 6.0f;

        if (wConfig != null)
        {
            if (wConfig.bulletsPerShot > 1) bulletCount = wConfig.bulletsPerShot;
            if (wConfig.spreadAngle > 0) spread = wConfig.spreadAngle;
        }

        if (bulletCount > 1)
        {
            // Xả bão đạn Gatling nhiều viên: Giãn cách vài ms giữa từng viên + Độ tỏa ngẫu nhiên
            StartCoroutine(BurstShootRoutine(targetPosition, damageOverride, bulletCount, spread));
        }
        else
        {
            PlayShootSound();
            FireSingleBullet(targetPosition, damageOverride, 0f);
        }
    }

    private IEnumerator BurstShootRoutine(Vector2 targetPosition, int damageOverride, int bulletCount, float spreadAngle)
    {
        for (int i = 0; i < bulletCount; i++)
        {
            PlayShootSound();
            float randomSpread = Random.Range(-spreadAngle, spreadAngle);
            FireSingleBullet(targetPosition, damageOverride, randomSpread);

            // Giãn cách vài mili giây (50ms = 0.05s) giữa từng viên đạn trong 1 đợt xả đạn Gatling
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void FireSingleBullet(Vector2 targetPosition, int damageOverride, float spreadAngleOffset)
    {
        WeaponConfig wConfig = GetWeaponConfig();
        BulletConfig bConfig = GetBulletConfig();

        if (cachedBulletPrefab == null)
        {
            string pName = bConfig != null ? bConfig.prefabName : "Bullet_Old_Pistol";
            LoadBulletPrefab(pName);
            if (cachedBulletPrefab == null) return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector2 baseDir = (targetPosition - (Vector2)spawnPos).normalized;

        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + spreadAngleOffset;

        GameObject bullet = Instantiate(cachedBulletPrefab, spawnPos, Quaternion.Euler(0, 0, finalAngle));

        // Gỡ bỏ tất cả script Đạn của Player bám trên Prefab nếu có
        MonoBehaviour[] scripts = bullet.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && !(script is MobBullet))
            {
                Destroy(script);
            }
        }

        // Gán Layer EnemyBullet
        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            bullet.layer = enemyBulletLayer;
        }

        // Gán script MobBullet dành riêng cho Quái (Chỉ bắn trúng Player)
        MobBullet mobBullet = bullet.GetComponent<MobBullet>();
        if (mobBullet == null)
        {
            mobBullet = bullet.AddComponent<MobBullet>();
        }

        if (mobBullet != null)
        {
            int bId = wConfig != null ? wConfig.bulletId : 21;
            mobBullet.InitFromDb(bId);
        }
    }

    private void PlayShootSound()
    {
        if (shootSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(shootSoundClip, transform.position);
        }
    }

    private IEnumerator MeleeAttackRoutine(BulletConfig bConfig)
    {
        // 🎯 TỰ ĐỘNG NẠP VỆT CHÉM TỪ PREFABNAME TRONG DB NẾU CACHED BULLET PREFAB DỰ PHÒNG CHƯA NẠP
        if (cachedBulletPrefab == null)
        {
            string pName = (bConfig != null && !string.IsNullOrEmpty(bConfig.prefabName)) ? bConfig.prefabName : "Stabbing_Effect_Mobs";
            LoadBulletPrefab(pName);
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

        // Sinh hiệu ứng vệt đâm cận chiến (Stabbing_Effect_Mobs) từ DB
        if (cachedBulletPrefab != null)
        {
            GameObject slashObj = Instantiate(cachedBulletPrefab, spawnPos, spawnRot);

            // Gỡ bỏ script MeleeSlash của Player nếu có
            MonoBehaviour[] scripts = slashObj.GetComponents<MonoBehaviour>();
            foreach (var s in scripts)
            {
                if (s != null && !(s is MobMeleeSlash)) Destroy(s);
            }

            MobMeleeSlash mobSlash = slashObj.GetComponent<MobMeleeSlash>();
            if (mobSlash == null) mobSlash = slashObj.AddComponent<MobMeleeSlash>();
            if (mobSlash != null)
            {
                WeaponConfig wConfig = GetWeaponConfig();
                int bId = wConfig != null ? wConfig.bulletId : 22;
                mobSlash.InitFromDb(bId);
            }
        }

        Vector3 originalLocalPos = transform.localPosition;
        Vector3 thrustPos = originalLocalPos + Vector3.right * 0.2f; // 🗡️ Đâm thương vươn xa 0.8m cắm sâu vào ngực Player

        float elapsed = 0f;
        float thrustTime = 0.08f;

        // Đâm ra
        while (elapsed < thrustTime)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(originalLocalPos, thrustPos, elapsed / thrustTime);
            yield return null;
        }

        // Giật về
        elapsed = 0f;
        while (elapsed < thrustTime)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(thrustPos, originalLocalPos, elapsed / thrustTime);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
    }
}
