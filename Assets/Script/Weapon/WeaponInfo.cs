using UnityEngine;
public class WeaponInfo : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip shootSound;
    [Range(0f, 1f)][SerializeField] private float shootVolume = 0.8f;

    [Header("VỊ TRÍ CẦM SÚNG")]
    public Vector3 customHandPosition;

    [Header("THIẾT LẬP BẮN ĐẠN")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.2f;

    [Header("MANA")]
    public int manaCostPerShot = 2; // mỗi khẩu súng chỉnh khác nhau trong Inspector

    [Header("RECOIL")]
    [SerializeField] float recoilDistance = 0.15f;
    [SerializeField] float recoilDuration = 0.05f;
    [SerializeField] float returnDuration = 0.1f;

    Vector3 originalLocalPos;
    bool positionSaved = false;

    void Awake() { }

    public void Attack()
    {
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        // Kiểm tra mana trước khi bắn
        RookieHealth playerHealth = GetComponentInParent<RookieHealth>();
        if (playerHealth != null && !playerHealth.UseMana(manaCostPerShot))
            return; // hết mana, không bắn

        if (bulletPrefab != null && firePoint != null)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            if (shootSound != null && RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(shootSound, transform.position, shootVolume);
            }
        }

        StopAllCoroutines();
        StartCoroutine(RecoilRoutine());
    }

    /// <summary>
    /// Được gọi từ mạng khi người chơi khác bắn súng.
    /// Hàm này tái sử dụng cơ chế sinh đạn giả, âm thanh không gian và hiệu ứng giật súng (recoil) 
    /// của chính bạn để đồng đội trông cực kỳ mượt mà.
    /// </summary>
    public void RemoteShoot(Vector3 position, Vector3 direction)
    {
        // Đảm bảo lưu lại vị trí gốc của súng trước khi chạy giật súng (recoil) cho đồng đội
        if (!positionSaved)
        {
            originalLocalPos = transform.localPosition;
            positionSaved = true;
        }

        if (bulletPrefab != null)
        {
            // Xác định vị trí nòng súng của đồng đội (ưu tiên firePoint nếu có)
            Vector3 spawnPosition = (firePoint != null) ? firePoint.position : position;

            // Tính toán góc xoay của viên đạn mạng gửi về
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // Sinh viên đạn
            Instantiate(bulletPrefab, spawnPosition, rotation);

            // Phát âm thanh không gian tại vị trí nòng súng của đồng đội
            if (shootSound != null && RogueKie.Audio.AudioManager.Instance != null)
            {
                RogueKie.Audio.AudioManager.Instance.PlaySFXAtPosition(shootSound, spawnPosition, shootVolume);
            }
        }

        // Kích hoạt hiệu ứng giật súng (Recoil) của đồng đội ngay trên máy khách của bạn
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