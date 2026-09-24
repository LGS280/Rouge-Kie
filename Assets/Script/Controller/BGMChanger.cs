using UnityEngine;
using RogueKie.Audio; // Gọi namespace chứa AudioManager của bạn

public class BossRoomBGM : MonoBehaviour
{
    [Header("Nhạc nền phòng Boss")]
    [SerializeField] private AudioClip bossMusic;

    // Hàm này tự động kích hoạt khi có vật thể chạm vào vùng Trigger
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem vật thể chạm vào có phải là Player không
        if (collision.CompareTag("Player"))
        {
            // Nếu đúng là Player, gọi AudioManager để đổi nhạc
            if (bossMusic != null)
            {
                AudioManager.Instance.PlayBGM(bossMusic);
            }
        }
    }
}