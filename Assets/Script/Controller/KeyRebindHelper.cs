using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class KeyRebindHelper : MonoBehaviour
{
    [Header("Input Action Reference")]
    [SerializeField] private InputActionReference inputActionReference; // Kéo file Input Actions và chọn đúng phím ở đây

    [Header("Composite Settings (Dành riêng cho phím WASD di chuyển)")]
    [SerializeField] private bool isComposite = false;
    [SerializeField] private string compositePartName = ""; // Nhập chính xác: "Up", "Down", "Left", hoặc "Right"

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI keyText; // Kéo đối tượng Text (TMP) hiển thị phím vào đây
    [SerializeField] private Button rebindButton;      // Kéo Button tương ứng vào đây

    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    private void Awake()
    {
        // Tải lại các phím đã lưu trước đó từ PlayerPrefs khi bắt đầu game
        LoadBindings();
    }

    private void OnEnable()
    {
        UpdateUI();
        if (rebindButton != null)
        {
            rebindButton.onClick.AddListener(StartRebinding);
        }
    }

    private void OnDisable()
    {
        if (rebindButton != null)
        {
            rebindButton.onClick.RemoveListener(StartRebinding);
        }
    }

    // Cập nhật phím hiển thị trên nút bấm thời gian thực
    public void UpdateUI()
    {
        if (inputActionReference == null || keyText == null) return;

        InputAction action = inputActionReference.action;
        int bindingIndex = GetBindingIndex(action);

        if (bindingIndex != -1)
        {
            // Lấy chuỗi ký tự mô tả phím (ví dụ: "W", "S", "Left Click")
            string displayString = action.GetBindingDisplayString(bindingIndex, out _, out _);
            keyText.text = displayString.ToUpper(); // Viết hoa toàn bộ chữ hiển thị phím
        }
    }

    // Tìm vị trí Binding Index chuẩn xác cho nút đơn lẻ hoặc nút phức hợp (WASD)
    private int GetBindingIndex(InputAction action)
    {
        if (isComposite)
        {
            // Đối với composite (như WASD/D-pad), tìm index theo tên Part ("Up", "Down", "Left", "Right")
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isPartOfComposite &&
                    action.bindings[i].name.Equals(compositePartName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        else
        {
            // Đối với nút đơn (như Attack): Tự động tìm binding thuộc nhóm "Keyboard&Mouse"
            for (int i = 0; i < action.bindings.Count; i++)
            {
                // Bỏ qua các phím phức hợp, tìm phím khớp với nhóm "Keyboard&Mouse" hoặc "Keyboard"
                if (!action.bindings[i].isPartOfComposite &&
                    (string.IsNullOrEmpty(action.bindings[i].groups) ||
                     action.bindings[i].groups.Contains("Keyboard&Mouse") ||
                     action.bindings[i].groups.Contains("Keyboard")))
                {
                    return i; // Sẽ trả về chính xác Index 1 (Left Button [Mouse])
                }
            }
        }
        return -1;
    }

    // Bắt đầu chế độ chờ người chơi nhấn phím mới
    public void StartRebinding()
    {
        if (inputActionReference == null) return;

        InputAction action = inputActionReference.action;
        int bindingIndex = GetBindingIndex(action);

        if (bindingIndex == -1) return;

        // Tạm thời vô hiệu hóa phím di chuyển để chuẩn bị lắng nghe phím mới
        action.Disable();

        keyText.text = "[PRESS KEY]";

        // Khởi chạy tác vụ rebind tương tác thời gian thực
        rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position") // Không nhận diện tọa độ rê chuột
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithExpectedControlType("Button") // Chỉ nhận tín hiệu nhấn nút (phím hoặc click)
            .OnMatchWaitForAnother(0.1f) // Thời gian trễ tránh click đúp phím
            .OnComplete(operation => FinishRebinding(action))
            .OnCancel(operation => FinishRebinding(action));

        // SỬA TẠI ĐÂY: Chạy tiến trình chờ người chơi thả chuột hoàn toàn
        StartCoroutine(StartRebindWithDelay());
    }

    private IEnumerator StartRebindWithDelay()
    {
        // Sử dụng New Input System để kiểm tra trạng thái chuột trái
        if (Mouse.current != null)
        {
            // Vòng lặp chờ cho đến khi phím chuột trái KHÔNG còn bị nhấn giữ nữa
            while (Mouse.current.leftButton.isPressed)
            {
                yield return null;
            }
        }

        // Chờ thêm 1 khung hình cho chắc chắn
        yield return null;

        if (rebindOperation != null)
        {
            rebindOperation.Start(); // Lúc này mới chính thức lắng nghe phím mới
        }
    }

    // Hoàn tất gán phím mới
    private void FinishRebinding(InputAction action)
    {
        rebindOperation.Dispose();
        rebindOperation = null;

        // Kích hoạt lại phím điều khiển sau khi gán
        action.Enable();

        // Cập nhật lại giao diện nút
        UpdateUI();

        // Lưu cài đặt phím xuống bộ nhớ máy khách
        SaveBindings(action);
    }

    private void SaveBindings(InputAction action)
    {
        // Lưu toàn bộ ghi đè phím thành tệp JSON vào PlayerPrefs
        string rebinds = action.actionMap.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("RogueKie_ControlRebinds", rebinds);
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        string rebinds = PlayerPrefs.GetString("RogueKie_ControlRebinds", string.Empty);
        if (!string.IsNullOrEmpty(rebinds) && inputActionReference != null)
        {
            inputActionReference.action.actionMap.asset.LoadBindingOverridesFromJson(rebinds);
        }
    }
}