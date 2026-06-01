using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f; // tốc độ đạn bay
    public float lifeTime = 3f;   // đạn biến mất sau 3 giây

    private Rigidbody2D rb2d;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.linearVelocity = transform.right * speed;
        Destroy(gameObject, lifeTime);
    }

    //void Update()
    //{
    //    // đạn luôn bay thẳng về phía trước của nó
    //    transform.Translate(Vector2.right * speed * Time.deltaTime);
    //}

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Nếu chạm vào tường thì đạn mất
        if (collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
