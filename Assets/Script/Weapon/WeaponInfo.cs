using UnityEngine;

public class WeaponInfo : MonoBehaviour
{
    [Header("Cấu hình API kết nối (Tự động theo PrefabName)")]
    [HideInInspector] public int weaponDbId;

    [Header("Prefab tham chiếu để vứt súng")]
    public GameObject weaponPrefab;

    [Header("Âm thanh bắn súng")]
    public AudioClip shootSoundClip;

    private string soundFileName;
    private float soundVolume = 0.8f;

    [Header("VỊ TRÍ CẦM SÚNG (Đọc từ DB)")]
    [HideInInspector] public Vector3 customHandPosition;

    [Header("THIẾT LẬP BẮN ĐẠN")]
    public GameObject bulletPrefab;
    public Transform firePoint;

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

    public WeaponConfig GetWeaponConfig()
    {
        if (GameConfigManager.Instance == null) return null;

        string keyName = weaponPrefab != null ? weaponPrefab.name : gameObject.name.Replace("(Clone)", "").Trim();
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

            // 1. Nạp vị trí cầm súng từ DB và cập nhật ngay
            customHandPosition = new Vector3(config.handPositionX, config.handPositionY, config.handPositionZ);
            transform.localPosition = customHandPosition;

            // 2. Nạp thông số âm thanh
            soundFileName = config.shootSound;
            soundVolume = config.shootVolume;

            // 3. Nạp thông số độ giật
            recoilDistance = config.recoilDistance;
            recoilDuration = config.recoilDuration;
            returnDuration = config.returnDuration;
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
            // Tự load file âm thanh dựa vào tên file trong thư mục Assets/Resources/Audio/
            AudioClip clip = Resources.Load<AudioClip>($"Audio/{soundFileName}");
            if (clip != null)
            {
                RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(clip, transform.position, soundVolume);
            }
            else
            {
                Debug.LogWarning($"[Audio Warning] Không tìm thấy file sound tên '{soundFileName}' trong Resources/Audio/");
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

        RookieHealth playerHealth = GetComponentInParent<RookieHealth>();
        if (playerHealth != null && !playerHealth.UseMana(manaCostPerShot))
            return;

        if (bulletPrefab != null && firePoint != null)
        {
            WeaponConfig wConfig = GetWeaponConfig();

            // Áp dụng góc lệch tâm SpreadAngle từ DB vào hướng đạn bắn ra
            Quaternion bulletRotation = firePoint.rotation;
            if (wConfig != null)
            {
                float randomSpread = Random.Range(-wConfig.spreadAngle, wConfig.spreadAngle);
                bulletRotation *= Quaternion.Euler(0, 0, randomSpread);
            }

            GameObject spawnedBullet = Instantiate(bulletPrefab, firePoint.position, bulletRotation);

            if (wConfig != null)
            {
                var bullet = spawnedBullet.GetComponent<NormalBullet>();
                if (bullet != null) bullet.InitFromDb(wConfig.bulletId);

                var bSlash = spawnedBullet.GetComponent<MeleeSlash>();
                if (bSlash != null) bSlash.InitFromDb(wConfig.bulletId);
            }

            PlayWeaponSound();
        }

        StopAllCoroutines();
        StartCoroutine(RecoilRoutine());
    }

    public void RemoteShoot(Vector3 position, Vector3 direction)
    {
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        if (bulletPrefab != null)
        {
            Vector3 spawnPosition = (firePoint != null) ? firePoint.position : position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            WeaponConfig wConfig = GetWeaponConfig();

            // Đồng bộ góc lệch đạn cho client remote mạng
            if (wConfig != null)
            {
                float randomSpread = Random.Range(-wConfig.spreadAngle, wConfig.spreadAngle);
                rotation *= Quaternion.Euler(0, 0, randomSpread);
            }

            GameObject spawnedBullet = Instantiate(bulletPrefab, spawnPosition, rotation);
            spawnedBullet.name += "_Remote";

            if (wConfig != null)
            {
                var bullet = spawnedBullet.GetComponent<NormalBullet>();
                if (bullet != null) bullet.InitFromDb(wConfig.bulletId);

                var bSlash = spawnedBullet.GetComponent<MeleeSlash>();
                if (bSlash != null) bSlash.InitFromDb(wConfig.bulletId);
            }

            PlayWeaponSound();
        }

        StopAllCoroutines();
        StartCoroutine(RecoilRoutine());
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