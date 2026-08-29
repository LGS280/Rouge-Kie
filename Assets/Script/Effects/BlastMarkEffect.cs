using UnityEngine;

public class BlastMarkEffect : MonoBehaviour
{
    [Header("CẤU HÌNH VẾT XÁM TÂN TRÊN SÀN")]
    public float holdDuration = 2.0f; // Thời gian giữ nguyên đọng trên sàn (2 giây)
    public float fadeDuration = 1.0f; // Thời gian mờ dần rồi tự hủy (1 giây)

    private float timer = 0f;
    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 1; // Nằm sát dưới sàn (dưới chân nhân vật/quái)
        }
    }

    void Update()
    {
        timer += Time.deltaTime; // cộng dồn thời gian thực của vết xám

        if (timer > holdDuration) // ktr vết cháy xám tồn tại qua 2s chưa để làm mờ
        {
            float fadeProgress = (timer - holdDuration) / fadeDuration;

            if (fadeProgress >= 1.0f)
            {
                Destroy(gameObject); // nếu fadeProgess >= 1f thì xóa nó đi
                return;
            }

            if (sr != null) // làm mờ màu
            {
                Color c = sr.color; // láy màu hiện tại
                c.a = Mathf.Lerp(1.0f, 0.0f, fadeProgress); // hạ độ màu xuống
                sr.color = c; // gán màu mới thành màu hiện tại
            }
        }
    }
}
