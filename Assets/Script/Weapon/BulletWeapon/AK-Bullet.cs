using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;     // Tốc độ bay của đạn
    public float lifeTime = 3f;   // Sau 3 giây không chạm gì cũng tự hủy cho nhẹ game

    void Start()
    {
        // Vừa sinh ra là tự hủy sau lifeTime giây
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // Đạn luôn luôn bay thẳng về phía trước theo trục X của chính nó
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    // Hàm xử lý khi đạn chạm vào tường hoặc quái vật
    void OnTriggerEnter2D(Collider2D collision)
    {
        // Nếu chạm vào Tường (Tilemap Obstacle) hoặc Kẻ địch
        if (collision.CompareTag("Obstacle"))
        {
            // Cho viên đạn biến mất ngay lập tức
            Destroy(gameObject);
        }
    }
}
