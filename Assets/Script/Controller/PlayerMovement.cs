using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Cài tốc độ nv")]
    public float moveSpeed = 8f; // Tốc chạy nhân vật

    private Rigidbody2D rb2d;
    private Vector2 moveInput;
    Animator animo;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.freezeRotation = true;
        }

        animo = GetComponent<Animator>();
    }

    void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        animo.SetFloat("Speed", moveInput.magnitude);

        float moveX = Input.GetAxisRaw("Horizontal");
        if (moveX < 0)
        {
            spriteRenderer.flipX = true;
        }
        else if (moveX > 0)
        {
            spriteRenderer.flipX = false;
        }

            moveInput = moveInput.normalized;
    }

    void FixedUpdate()
    {
        rb2d.MovePosition(rb2d.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }
}
