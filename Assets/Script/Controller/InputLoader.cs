using UnityEngine;
using UnityEngine.InputSystem;

public class InputLoader : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset inputActions; // Kéo file cấu hình phím (.inputactions) của bạn vào đây

    private void Awake()
    {
        // Tải lại các phím đã lưu từ PlayerPrefs khi vào màn chơi mới
        string rebinds = PlayerPrefs.GetString("RogueKie_ControlRebinds", string.Empty);
        if (!string.IsNullOrEmpty(rebinds) && inputActions != null)
        {
            inputActions.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("Đã tải lại cấu hình phím thành công!");
        }
    }
}