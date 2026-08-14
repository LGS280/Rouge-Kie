using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 8f;

    public Rigidbody2D rb2d;
    public PlayerInput playerInput;
    public Camera mainCamera;

    private Vector2 moveInput;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    public enum InputMode { KeyboardMouse, Gamepad}
    public InputMode currentMode = InputMode.KeyboardMouse;

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (rb2d == null)
        {
            GetComponent<Rigidbody2D>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();

        animator = GetComponent<Animator>();

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.freezeRotation = true; // khóa cứng trục Z của nhân vật để nó ko bị ngã nghiên
        }
    }

    private void Update()
    {
        CheckAndSwitchInputMode();
        if(animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
        }
    }

    // Update() là chạy theo từng frame
    // FixedUpdate() là chạy theo 1 khoảng thời gian cố định, tất cả những gì liên quan đến Rigibody thì sẽ bỏ trong FixedUpdate()

    private void FixedUpdate()
    {
        // Chỉ thêm điều kiện dừng di chuyển nếu Shop UI đang mở
        if (ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen())
        {
            return;
        }

        if (rb2d != null)
        {
            rb2d.MovePosition(rb2d.position + moveInput.normalized * moveSpeed * Time.fixedDeltaTime);
        }
    }

    // nhận tín hiệu di chuyển từ New Input System của Unity
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public Vector2 GetMoveInput()
    {
        return moveInput;
    }

    private void CheckAndSwitchInputMode()
    {
        if (currentMode != InputMode.KeyboardMouse)
        {
            // check xem có nút nào trên bàn phím dc bấm ko
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

            // check xem là chuột trái và chuột phải có bấm không
            bool mouseMoved = Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f || Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);

            //nếu có nút trên bàn phím dc bấm hoặc click chuột thì chuyển qua chế độ chuột + bàn phím
            if (keyboardPressed || mouseMoved)
            {
                currentMode = InputMode.KeyboardMouse;
            }
        }

        if (currentMode != InputMode.Gamepad && Gamepad.current != null)
        {
            // check xem nếu có nút bấm nào trên tay cầm dc bấm ko
            bool gamepadButtonPressed = Gamepad.current.wasUpdatedThisFrame;

            // check xem cái joystick bên trái có di chuyển ko
            bool leftStickMoved = Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.05f;

            // check xem cái jotstick bên phải có di chuyển ko
            bool rightStickMoved = Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.05f;

            // nếu nút bấm trên tay cầm dc bấm hoặc joystick bên trái di chuyển hoặc joystick bên phải di chuyển thì sẽ chuyển qua chơi bằng tay cầm
            if (gamepadButtonPressed || leftStickMoved || rightStickMoved)
            {
                currentMode = InputMode.Gamepad;
            }
        }
    }
}
