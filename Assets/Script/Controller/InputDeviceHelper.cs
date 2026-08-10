using UnityEngine;
using UnityEngine.InputSystem;

public static class InputDeviceHelper
{
    private static bool lastUsedGamepad = false;

    public static bool IsGamepadActive()
    {
        if (Gamepad.current == null)
        {
            lastUsedGamepad = false;
            return false;
        }

        // Kiểm tra xem có phím nào trên tay cầm vừa được nhấn
        foreach (var control in Gamepad.current.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
            {
                lastUsedGamepad = true;
                return true;
            }
        }

        // Kiểm tra cần gạt tay cầm có hoạt động
        if (Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.1f)
        {
            lastUsedGamepad = true;
            return true;
        }

        // Kiểm tra xem có phím trên bàn phím vừa được nhấn
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            lastUsedGamepad = false;
            return false;
        }

        // Kiểm tra chuột có click hoặc di chuyển mạnh
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                lastUsedGamepad = false;
                return false;
            }
            if (Mouse.current.delta.ReadValue().sqrMagnitude > 2f)
            {
                lastUsedGamepad = false;
                return false;
            }
        }

        return lastUsedGamepad;
    }

    public static string GetBindingDisplayString(string actionName, string defaultFallback = "E")
    {
        if (IsGamepadActive())
        {
            if (actionName == "Interact") return "B";
            if (actionName == "Skill") return "Y";
            if (actionName == "SwitchWeapon") return "A";
        }

        if (InputLoader.Instance != null && InputLoader.Instance.inputActions != null)
        {
            InputAction action = InputLoader.Instance.GetAction(actionName);
            if (action != null)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!action.bindings[i].isPartOfComposite &&
                        (string.IsNullOrEmpty(action.bindings[i].groups) ||
                         action.bindings[i].groups.Contains("Keyboard&Mouse") ||
                         action.bindings[i].groups.Contains("Keyboard")))
                    {
                        string displayStr = action.GetBindingDisplayString(i);
                        if (!string.IsNullOrEmpty(displayStr)) return displayStr.ToUpper();
                    }
                }
            }
        }
        return defaultFallback;
    }

    public static string GetInteractKeyDisplayString()
    {
        return GetBindingDisplayString("Interact", "E");
    }

    public static string GetSkillKeyDisplayString()
    {
        return GetBindingDisplayString("Skill", "F");
    }

    public static string GetSwitchWeaponKeyDisplayString()
    {
        return GetBindingDisplayString("SwitchWeapon", "SCROLLWHEEL");
    }
}
