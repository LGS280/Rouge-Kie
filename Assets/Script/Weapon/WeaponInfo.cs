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
            if (wConfig == null)
            {
                string rawName = weaponPrefab != null ? weaponPrefab.name : gameObject.name;
                string keyName = rawName.Replace("(Clone)", "").Trim();
                Debug.LogWarning($"[WeaponInfo] ⚠️ KHÔNG TÌM THẤY cấu hình DB cho súng '{keyName}'! Hãy đảm bảo Backend API đã được khởi động lại và trả về tên PrefabName = '{keyName}'.");
            }

            int count = (wConfig != null && wConfig.bulletsPerShot > 0) ? wConfig.bulletsPerShot : 1;
            float spread = (wConfig != null && wConfig.spreadAngle > 0) ? wConfig.spreadAngle : 20f;
            float baseAngle = firePoint.eulerAngles.z;

            for (int i = 0; i < count; i++)
            {
                float angleOffset = 0f;
                if (count > 1)
                {
                    // Bắn tỏa đều các viên đạn theo hình quạt từ -spread đến +spread
                    angleOffset = Mathf.Lerp(-spread, spread, (float)i / (count - 1));
                }
                else
                {
                    // 1 viên duy nhất -> lệch ngẫu nhiên trong khoảng spread
                    angleOffset = Random.Range(-spread, spread);
                }

                Quaternion bulletRotation = Quaternion.Euler(0, 0, baseAngle + angleOffset);
                GameObject spawnedBullet = Instantiate(bulletPrefab, firePoint.position, bulletRotation);

                int targetBulletId = (wConfig != null) ? wConfig.bulletId : 13; // Fallback thử đạn nếu wConfig null
                var bullet = spawnedBullet.GetComponent<NormalBullet>();
                if (bullet != null) bullet.InitFromDb(targetBulletId);

                var bSlash = spawnedBullet.GetComponent<MeleeSlash>();
                if (bSlash != null) bSlash.InitFromDb(targetBulletId);
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

        if (bulletPrefab != null)
        {
            Vector3 spawnPosition = (firePoint != null) ? firePoint.position : position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion baseRotation = Quaternion.AngleAxis(angle, Vector3.forward);

            WeaponConfig wConfig = GetWeaponConfig();
            int count = (wConfig != null && wConfig.bulletsPerShot > 0) ? wConfig.bulletsPerShot : 1;
            float spread = (wConfig != null) ? wConfig.spreadAngle : 0f;

            for (int i = 0; i < count; i++)
            {
                float angleOffset = 0f;
                if (count > 1)
                {
                    angleOffset = Mathf.Lerp(-spread, spread, (float)i / (count - 1));
                }
                else
                {
                    angleOffset = Random.Range(-spread, spread);
                }

                Quaternion bulletRotation = baseRotation * Quaternion.Euler(0, 0, angleOffset);
                GameObject spawnedBullet = Instantiate(bulletPrefab, spawnPosition, bulletRotation);
                spawnedBullet.name += "_Remote";

                if (wConfig != null)
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

    private bool slashDownward = true; // Đổi hướng chém luân phiên (Chém xuôi & Chém ngược)

    System.Collections.IEnumerator SwordSlashRoutine()
    {
        float slashDuration = 0.08f;  // Thời gian vung kiếm quạt nhanh
        float returnDuration = 0.12f; // Thời gian thu kiếm về góc nghỉ

        float startAngle = slashDownward ? 60f : -60f;
        float endAngle = slashDownward ? -60f : 60f;
        slashDownward = !slashDownward;

        Vector3 forwardThrust = originalLocalPos + Vector3.right * 0.25f;

        float t = 0f;
        while (t < slashDuration)
        {
            t += Time.deltaTime;
            float progress = t / slashDuration;

            // Xoay vung lưỡi kiếm từ góc trên xuống góc dưới (hoặc ngược lại)
            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
            transform.localRotation = Quaternion.Euler(0, 0, currentAngle);

            // Nhích nhẹ lưỡi kiếm ra phía trước theo quán tính nhát chém
            transform.localPosition = Vector3.Lerp(originalLocalPos, forwardThrust, Mathf.Sin(progress * Mathf.PI));

            yield return null;
        }

        // Thu kiếm trở lại góc nghỉ ban đầu
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