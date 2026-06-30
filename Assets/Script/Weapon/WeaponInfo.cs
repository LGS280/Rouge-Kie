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