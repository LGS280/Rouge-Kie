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
        timer += Time.deltaTime;

        if (timer > holdDuration)
        {
            float fadeProgress = (timer - holdDuration) / fadeDuration;

            if (fadeProgress >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1.0f, 0.0f, fadeProgress);
                sr.color = c;
            }
        }
    }
}
