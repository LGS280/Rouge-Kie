using UnityEngine;

public class WeaponInfo : MonoBehaviour
{
    [Header("Cấu hình API kết nối (Tự động theo PrefabName)")]
    [HideInInspector] public int weaponDbId;

    [Header("prefab vứt súng")]
    public GameObject weaponPrefab;

    [Header("âm thanh bắn súng")]
    public AudioClip shootSoundClip;

    private string soundFileName;
    private float soundVolume = 0.8f;

    [Header("vị trí custom cầm súng")]
    [HideInInspector] public Vector3 customHandPosition;

    [Header("đầu nòng súng 1 và 2")]
    public Transform firePoint;
    public Transform secondFirePoint;

    [Header("Danh sách các loại đạn Player (Tự động đóng gói Build)")]
    public GameObject[] allPlayerBulletPrefabs;

    [HideInInspector] public GameObject bulletPrefab;
    [HideInInspector] public float fireRate;
    [HideInInspector] public int manaCostPerShot;

    private float recoilDistance = 0.15f;
    private float recoilDuration = 0.05f;
    private float returnDuration = 0.1f;

    Vector3 originalLocalPos;
    bool positionSaved = false;

    void OnEnable()
    {
        ApplyConfigFromDb();
        GameConfigManager.OnConfigLoaded += ApplyConfigFromDb;
    }

    void OnDisable()
    {
        GameConfigManager.OnConfigLoaded -= ApplyConfigFromDb;
    }

    private void Start()
    {
        if (firePoint == null) firePoint = transform.Find("FirePoint");
        if (secondFirePoint == null) secondFirePoint = transform.Find("SecondFirePoint");
        if (secondFirePoint == null) secondFirePoint = transform.Find("MeleePoint");
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

        string bulletFolder = "Assets/Prefab/Bullet";
        string effectFolder = "Assets/Prefab/Effects";
        System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();

        if (System.IO.Directory.Exists(bulletFolder))
        {
            foreach (string file in System.IO.Directory.GetFiles(bulletFolder, "*.prefab"))
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null) list.Add(prefab);
            }
        }
        if (System.IO.Directory.Exists(effectFolder))
        {
            foreach (string file in System.IO.Directory.GetFiles(effectFolder, "*.prefab"))
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null) list.Add(prefab);
            }
        }
        allPlayerBulletPrefabs = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

        string rawName = weaponPrefab != null ? weaponPrefab.name : gameObject.name;
        string keyName = rawName.Replace("(Clone)", "").Trim();

        if (GameConfigManager.Instance.WeaponDbByName.TryGetValue(keyName, out WeaponConfig config))
        {
            return config;
        }

        if (weaponDbId > 0 && GameConfigManager.Instance.WeaponDb.TryGetValue(weaponDbId, out WeaponConfig idConfig))
        {
            return idConfig;
        }

        return null;
    }

    public void ApplyConfigFromDb()
    {
        WeaponConfig config = GetWeaponConfig();
        if (config != null)
        {
            fireRate = config.fireRate;
            manaCostPerShot = config.manaCost;

            customHandPosition = new Vector3(config.handPositionX, config.handPositionY, config.handPositionZ);
            transform.localPosition = customHandPosition;

            soundFileName = config.shootSound;
            soundVolume = config.shootVolume;

            recoilDistance = config.recoilDistance;
            recoilDuration = config.recoilDuration;
            returnDuration = config.returnDuration;
        }
    }

    public GameObject GetBulletPrefabFromDb(int bId)
    {
        if (GameConfigManager.Instance != null && GameConfigManager.Instance.BulletDb.TryGetValue(bId, out BulletConfig bConfig))
        {
            if (!string.IsNullOrEmpty(bConfig.prefabName))
            {
                string targetName = bConfig.prefabName.ToLower();

                if (allPlayerBulletPrefabs != null)
                {
                    foreach (var prefab in allPlayerBulletPrefabs)
                    {
                        if (prefab != null && prefab.name.ToLower() == targetName)
                        {
                            return prefab;
                        }
                    }
                    foreach (var prefab in allPlayerBulletPrefabs)
                    {
                        if (prefab != null && prefab.name.ToLower().Contains(targetName))
                        {
                            return prefab;
                        }
                    }
                }

#if UNITY_EDITOR
                string editorPath = $"Assets/Prefab/Bullet/{bConfig.prefabName}.prefab";
                GameObject loadedInEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
                if (loadedInEditor != null) return loadedInEditor;

                string editorEffectPath = $"Assets/Prefab/Effects/{bConfig.prefabName}.prefab";
                loadedInEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorEffectPath);
                if (loadedInEditor != null) return loadedInEditor;
#endif
            }
        }
        return bulletPrefab;
    }

    public bool HasBayonetStab()
    {
        WeaponConfig config = GetWeaponConfig();
        return config != null && config.secondBulletId > 0;
    }

    public bool TryBayonetStab(float meleeRadius, string enemyTag)
    {
        WeaponConfig config = GetWeaponConfig();
        if (config == null || config.secondBulletId <= 0) return false;

        Transform originPoint = (secondFirePoint != null) ? secondFirePoint : (firePoint != null ? firePoint : transform);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, meleeRadius);

        foreach (var hit in hits)
        {
            if (hit.CompareTag(enemyTag))
            {
                MobHealth hp = hit.GetComponent<MobHealth>();
                if (hp != null && hp.isDead) continue;

                Vector2 dirToEnemy = (hit.transform.position - transform.position).normalized;
                Vector2 weaponDir = originPoint.right;

                if (Vector2.Dot(weaponDir, dirToEnemy) > 0f)
                {
                    PerformBayonetStab(config.secondBulletId, originPoint);
                    return true;
                }
            }
        }
        return false;
    }

    private void PerformBayonetStab(int secondId, Transform originPoint)
    {
        GameObject stabPrefab = GetBulletPrefabFromDb(secondId);
        if (stabPrefab != null && originPoint != null)
        {
            GameObject spawnedStab = Instantiate(stabPrefab, originPoint.position, originPoint.rotation);

            Collider2D col = spawnedStab.GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var bullet = spawnedStab.GetComponent<NormalBullet>();
            if (bullet != null) bullet.InitFromDb(secondId);

            var bSlash = spawnedStab.GetComponent<MeleeSlash>();
            if (bSlash != null) bSlash.InitFromDb(secondId);

            TriggerAttackAnimation();
        }
    }

    private void PlayWeaponSound()
    {
        if (shootSoundClip != null && RogueKie.Audio.AudioManager.Instance != null)
        {
            RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(shootSoundClip, transform.position, soundVolume);
        }
        else if (!string.IsNullOrEmpty(soundFileName) && RogueKie.Audio.AudioManager.Instance != null)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/{soundFileName}");
            if (clip != null)
            {
                RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(clip, transform.position, soundVolume);
            }
        }
    }

    public void Attack()
    {
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        WeaponConfig wConfig = GetWeaponConfig();
        Transform spawnPoint = (firePoint != null) ? firePoint : transform;

        GameObject targetBulletPrefab = (wConfig != null && wConfig.bulletId > 0)
            ? GetBulletPrefabFromDb(wConfig.bulletId)
            : bulletPrefab;

        if (targetBulletPrefab == null)
        {

            return;
        }

        RookieHealth playerHealth = GetComponentInParent<RookieHealth>();
        if (playerHealth != null && !playerHealth.UseMana(manaCostPerShot))
            return;

        if (targetBulletPrefab != null && spawnPoint != null)
        {
            int count = (wConfig != null && wConfig.bulletsPerShot > 0) ? wConfig.bulletsPerShot : 1;
            float spread = (wConfig != null && wConfig.spreadAngle > 0) ? wConfig.spreadAngle : 20f;
            float baseAngle = spawnPoint.eulerAngles.z;

            for (int i = 0; i < count; i++)
            {
                float angleOffset = (count > 1)
                    ? Mathf.Lerp(-spread, spread, (float)i / (count - 1))
                    : Random.Range(-spread, spread);

                Quaternion bulletRotation = Quaternion.Euler(0, 0, baseAngle + angleOffset);
                GameObject spawnedBullet = Instantiate(targetBulletPrefab, spawnPoint.position, bulletRotation);

                if (wConfig != null && wConfig.bulletId > 0)
                {
                    var bullet = spawnedBullet.GetComponent<NormalBullet>();
                    if (bullet != null) bullet.InitFromDb(wConfig.bulletId);

                    var bSlash = spawnedBullet.GetComponent<MeleeSlash>();
                    if (bSlash != null) bSlash.InitFromDb(wConfig.bulletId);
                }
            }

            PlayWeaponSound();
        }

        TriggerAttackAnimation();
    }

    public void RemoteShoot(Vector3 position, Vector3 direction)
    {
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        WeaponConfig wConfig = GetWeaponConfig();
        Transform spawnPoint = (firePoint != null) ? firePoint : transform;
        Vector3 spawnPosition = (spawnPoint != null) ? spawnPoint.position : position;

        GameObject targetBulletPrefab = (wConfig != null && wConfig.bulletId > 0)
            ? GetBulletPrefabFromDb(wConfig.bulletId)
            : bulletPrefab;

        if (targetBulletPrefab != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion baseRotation = Quaternion.AngleAxis(angle, Vector3.forward);

            int count = (wConfig != null && wConfig.bulletsPerShot > 0) ? wConfig.bulletsPerShot : 1;
            float spread = (wConfig != null) ? wConfig.spreadAngle : 0f;

            for (int i = 0; i < count; i++)
            {
                float angleOffset = (count > 1)
                    ? Mathf.Lerp(-spread, spread, (float)i / (count - 1))
                    : Random.Range(-spread, spread);

                Quaternion bulletRotation = baseRotation * Quaternion.Euler(0, 0, angleOffset);
                GameObject spawnedBullet = Instantiate(targetBulletPrefab, spawnPosition, bulletRotation);
                spawnedBullet.name += "_Remote";

                if (wConfig != null && wConfig.bulletId > 0)
                {
                    var bullet = spawnedBullet.GetComponent<NormalBullet>();
                    if (bullet != null) bullet.InitFromDb(wConfig.bulletId);

                    var bSlash = spawnedBullet.GetComponent<MeleeSlash>();
                    if (bSlash != null) bSlash.InitFromDb(wConfig.bulletId);
                }
            }

            PlayWeaponSound();
        }

        TriggerAttackAnimation();
    }

    public bool IsMeleeWeapon()
    {
        WeaponConfig config = GetWeaponConfig();
        if (config != null && !string.IsNullOrEmpty(config.weaponType))
        {
            if (config.weaponType.Equals("Sword", System.StringComparison.OrdinalIgnoreCase) ||
                config.weaponType.Equals("Melee", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return (bulletPrefab != null && bulletPrefab.GetComponent<MeleeSlash>() != null);
    }

    private void TriggerAttackAnimation()
    {
        StopAllCoroutines();
        if (IsMeleeWeapon())
        {
            StartCoroutine(SwordSlashRoutine());
        }
        else
        {
            StartCoroutine(RecoilRoutine());
        }
    }

    private bool slashDownward = true;

    System.Collections.IEnumerator SwordSlashRoutine()
    {
        float slashDuration = 0.08f;
        float returnDuration = 0.12f;

        float startAngle = slashDownward ? 60f : -60f;
        float endAngle = slashDownward ? -60f : 60f;
        slashDownward = !slashDownward;

        Vector3 forwardThrust = originalLocalPos + Vector3.right * 0.25f;

        float t = 0f;
        while (t < slashDuration)
        {
            t += Time.deltaTime;
            float progress = t / slashDuration;

            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
            transform.localRotation = Quaternion.Euler(0, 0, currentAngle);

            transform.localPosition = Vector3.Lerp(originalLocalPos, forwardThrust, Mathf.Sin(progress * Mathf.PI));

            yield return null;
        }

        t = 0f;
        Quaternion currentRot = transform.localRotation;
        Quaternion targetRot = Quaternion.identity;

        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float progress = t / returnDuration;

            transform.localRotation = Quaternion.Lerp(currentRot, targetRot, progress);
            transform.localPosition = Vector3.Lerp(transform.localPosition, originalLocalPos, progress);
            yield return null;
        }

        transform.localRotation = Quaternion.identity;
        transform.localPosition = originalLocalPos;
    }

    System.Collections.IEnumerator RecoilRoutine()
    {
        Vector3 recoilPos = originalLocalPos + Vector3.left * recoilDistance;
        float t = 0f;
        while (t < recoilDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(originalLocalPos, recoilPos, t / recoilDuration);
            yield return null;
        }
        t = 0f;
        while (t < returnDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(recoilPos, originalLocalPos, t / returnDuration);
            yield return null;
        }
        transform.localPosition = originalLocalPos;
    }
}
