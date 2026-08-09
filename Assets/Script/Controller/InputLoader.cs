using UnityEngine;
using UnityEngine.InputSystem;

public class InputLoader : MonoBehaviour
{
    public static InputLoader Instance { get; private set; }

    [Header("Input Action Asset")]
    public InputActionAsset inputActions; // Kéo file cấu hình phím (.inputactions) vào đây

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        LoadRebinds();
    }

    public void LoadRebinds()
    {
        string rebinds = PlayerPrefs.GetString("RogueKie_ControlRebinds", string.Empty);
        if (!string.IsNullOrEmpty(rebinds) && inputActions != null)
        {
            inputActions.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("Đã tải lại cấu hình phím từ PlayerPrefs thành công!");
        }

        if (inputActions != null)
        {
            inputActions.Enable();
        }
    }

    public InputAction GetAction(string actionName)
    {
        if (inputActions == null) return null;
        InputAction action = inputActions.FindAction(actionName);
        if (action != null && !action.enabled)
        {
            action.Enable();
        }
        return action;
    }
}