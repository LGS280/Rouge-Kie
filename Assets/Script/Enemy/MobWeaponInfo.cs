using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MobWeaponInfo : MonoBehaviour
{
    [Header("Vị trí nòng súng / Đầu thương")]
    public Transform firePoint;

    [Header("Danh sách các loại đạn quái (Tự động nạp theo DB)")]
    public GameObject[] mobBulletPrefabs;

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

    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

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
        string targetName = prefabName.ToLower();
        string targetBulletName = targetName.StartsWith("bullet_") ? targetName : "bullet_" + targetName;

        if (mobBulletPrefabs != null)
        {
            foreach (var prefab in mobBulletPrefabs)
            {
                if (prefab != null)
                {
                    string pName = prefab.name.ToLower();
                    if (pName == targetName || pName == targetBulletName || pName.Contains(targetName))
                    {
                        cachedBulletPrefab = prefab;
                        return;
                    }
                }
            }
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/Mobs_Weapons/Bullets" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            if (fileName == targetName || fileName == targetBulletName || fileName.Contains(targetName))
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    cachedBulletPrefab = prefab;
                    return;
                }
            }
        }
#endif
    }

    public void Shoot(Vector2 targetPosition, int damageOverride)
    {
        WeaponConfig wConfig = GetWeaponConfig();
        BulletConfig bConfig = GetBulletConfig();

        if (isMelee)
        {
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

            StartCoroutine(BurstShootRoutine(targetPosition, damageOverride, bulletCount, spread));
        }
        else
        {
            FireSingleBullet(targetPosition, damageOverride, 0f);
        }
    }

    private IEnumerator BurstShootRoutine(Vector2 targetPosition, int damageOverride, int bulletCount, float spreadAngle)
    {
        for (int i = 0; i < bulletCount; i++)
        {
            float randomSpread = Random.Range(-spreadAngle, spreadAngle);
            FireSingleBullet(targetPosition, damageOverride, randomSpread);

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

        MonoBehaviour[] scripts = bullet.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && !(script is MobBullet))
            {
                Destroy(script);
            }
        }

        int enemyBulletLayer = LayerMask.NameToLayer("EnemyBullet");
        if (enemyBulletLayer != -1)
        {
            bullet.layer = enemyBulletLayer;
        }

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
    private IEnumerator MeleeAttackRoutine(BulletConfig bConfig)
    {

        if (cachedBulletPrefab == null)
        {
            string pName = (bConfig != null && !string.IsNullOrEmpty(bConfig.prefabName)) ? bConfig.prefabName : "Stabbing_Effect_Mobs";
            LoadBulletPrefab(pName);
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

        if (cachedBulletPrefab != null)
        {
            GameObject slashObj = Instantiate(cachedBulletPrefab, spawnPos, spawnRot);
            if (firePoint != null)
            {
                slashObj.transform.SetParent(firePoint);
                slashObj.transform.localPosition = Vector3.zero;
                slashObj.transform.localRotation = Quaternion.identity;
            }

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
        Vector3 thrustPos = originalLocalPos + Vector3.right * 0.2f;

        float elapsed = 0f;
        float thrustTime = 0.08f;

        while (elapsed < thrustTime)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(originalLocalPos, thrustPos, elapsed / thrustTime);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < thrustTime)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(thrustPos, originalLocalPos, elapsed / thrustTime);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string folderPath = "Assets/Prefab/Mobs_Weapons/Bullets";
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] files = System.IO.Directory.GetFiles(folderPath, "*.prefab");
            List<GameObject> list = new List<GameObject>();
            foreach (string file in files)
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }

            bool isChanged = false;
            if (mobBulletPrefabs == null || mobBulletPrefabs.Length != list.Count)
            {
                isChanged = true;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (mobBulletPrefabs[i] != list[i])
                    {
                        isChanged = true;
                        break;
                    }
                }
            }

            if (isChanged)
            {
                mobBulletPrefabs = list.ToArray();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
    }
#endif
}
