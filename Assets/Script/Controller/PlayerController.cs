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

    [Header("Độ trễ chuyển đổi thiết bị (Exclusive Lockout)")]
    [Tooltip("Thời gian phải buông thiết bị cũ (giây) trước khi game chấp nhận thiết bị mới")]
    public float switchCooldown = 3.0f;
    private float lastKeyboardMouseInputTime = -999f;
    private float lastGamepadInputTime = -999f;

    private WeaponAim weaponAim;

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
        weaponAim = GetComponentInChildren<WeaponAim>();

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.freezeRotation = true; // khóa cứng trục Z của nhân vật để nó ko bị ngã nghiên
        }
    }

    private void Update()
    {
        CheckAndSwitchInputMode();
        UpdateMoveInput();

        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
            if (weaponAim == null) weaponAim = GetComponentInChildren<WeaponAim>();
            bool lookUp = (weaponAim != null && weaponAim.isAimingUp);
            int yInt = lookUp ? 1 : 0;
            float yFloat = lookUp ? 1f : 0f;
            foreach (var param in animator.parameters)
            {
                if (param.name == "MoveY")
                {
                    if (param.type == AnimatorControllerParameterType.Int)
                        animator.SetInteger("MoveY", yInt);
                    else if (param.type == AnimatorControllerParameterType.Float)
                        animator.SetFloat("MoveY", yFloat);
                    break;
                }
            }
        }
    }

    private void UpdateMoveInput()
    {
        if (currentMode == InputMode.Gamepad)
        {
            // Ở chế độ Gamepad: Chỉ nhận di chuyển từ cần Analog trái (bỏ qua phím WASD)
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.05f)
            {
                moveInput = Gamepad.current.leftStick.ReadValue();
            }
            else
            {
                moveInput = Vector2.zero;
            }
        }
        else // InputMode.KeyboardMouse
        {
            // Ở chế độ Bàn phím: Chỉ nhận di chuyển từ phím WASD / Mũi tên (bỏ qua cần Gamepad)
            Vector2 kbMove = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kbMove.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kbMove.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kbMove.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kbMove.x += 1f;
            }
            moveInput = kbMove.normalized;
        }
    }

    // Update() là chạy theo từng frame
    // FixedUpdate() là chạy theo 1 khoảng thời gian cố định, tất cả những gì liên quan đến Rigibody thì sẽ bỏ trong FixedUpdate()

    private void FixedUpdate()
    {
        // Chú ý: thêm điều kiện dừng di chuyển nếu Shop UI hoặc Kho Vũ Khí (Weapon Vault) đang mở
        if ((ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen()) ||
            (WeaponVaultUIController.Instance != null && WeaponVaultUIController.Instance.IsVaultOpen()))
        {
            return;
        }

        if (rb2d != null)
        {
            rb2d.MovePosition(rb2d.position + moveInput.normalized * moveSpeed * Time.fixedDeltaTime);
        }
    }

    // Nhận tín hiệu di chuyển từ New Input System của Unity
    public void OnMove(InputValue value)
    {
        // Được quản lý và cập nhật liên tục qua UpdateMoveInput() để đảm bảo tính độc quyền thiết bị
    }

    public Vector2 GetMoveInput()
    {
        return moveInput;
    }

    private void CheckAndSwitchInputMode()
    {
        // 1. Kiểm tra xem Bàn phím hoặc Chuột có đang được chạm vào ở frame này không
        bool keyboardActive = Keyboard.current != null && (Keyboard.current.anyKey.wasPressedThisFrame || Keyboard.current.anyKey.isPressed);
        bool mouseActive = Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.5f || 
                                                    Mouse.current.leftButton.isPressed || 
                                                    Mouse.current.rightButton.isPressed ||
                                                    Mouse.current.middleButton.isPressed);

        bool isKBMActive = keyboardActive || mouseActive;
        if (isKBMActive)
        {
            lastKeyboardMouseInputTime = Time.time;
        }

        // 2. Kiểm tra xem Tay cầm Gamepad có đang được chạm vào ở frame này không
        bool isGamepadActive = false;
        if (Gamepad.current != null)
        {
            bool leftStickMoved = Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f;
            bool rightStickMoved = Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.1f;

            bool buttonPressed = Gamepad.current.aButton.isPressed ||
                                 Gamepad.current.bButton.isPressed ||
                                 Gamepad.current.xButton.isPressed ||
                                 Gamepad.current.yButton.isPressed ||
                                 Gamepad.current.leftShoulder.isPressed ||
                                 Gamepad.current.rightShoulder.isPressed ||
                                 Gamepad.current.leftTrigger.isPressed ||
                                 Gamepad.current.rightTrigger.isPressed ||
                                 Gamepad.current.startButton.isPressed ||
                                 Gamepad.current.selectButton.isPressed ||
                                 Gamepad.current.dpad.up.isPressed ||
                                 Gamepad.current.dpad.down.isPressed ||
                                 Gamepad.current.dpad.left.isPressed ||
                                 Gamepad.current.dpad.right.isPressed;

            isGamepadActive = leftStickMoved || rightStickMoved || buttonPressed;
            if (isGamepadActive)
            {
                lastGamepadInputTime = Time.time;
            }
        }

        // 3. Chuyển đổi độc quyền có độ trễ (Exclusive Cooldown 3s):
        if (currentMode == InputMode.KeyboardMouse)
        {
            // Đang chơi phím chuột: BẮT BUỘC phải buông phím chuột đủ 3 giây mới cho chuyển sang Tay cầm
            if (isGamepadActive && (Time.time - lastKeyboardMouseInputTime >= switchCooldown))
            {
                currentMode = InputMode.Gamepad;
                Debug.Log($"[PlayerController] Đã buông phím chuột đủ {switchCooldown}s -> Chuyển sang chế độ Gamepad!");
            }
        }
        else if (currentMode == InputMode.Gamepad)
        {
            // Đang chơi tay cầm: BẮT BUỘC phải buông tay cầm đủ 3 giây mới cho chuyển sang Bàn phím chuột
            if (isKBMActive && (Time.time - lastGamepadInputTime >= switchCooldown))
            {
                currentMode = InputMode.KeyboardMouse;
                Debug.Log($"[PlayerController] Đã buông tay cầm đủ {switchCooldown}s -> Chuyển sang chế độ KeyboardMouse!");
            }
        }
    }
}
