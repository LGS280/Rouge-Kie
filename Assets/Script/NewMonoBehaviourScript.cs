using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Cài ??t t?c ??")]
    public float moveSpeed = 5f; // T?c ?? ch?y c?a nv

    private Rigidbody2D rb2d;
    private Vector2 moveInput;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.freezeRotation = true;
        }
    }

    void Update()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        moveInput = moveInput.normalized;
    }

    void FixedUpdate()
    {
        rb2d.MovePosition(rb2d.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }
}
