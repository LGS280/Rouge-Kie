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
}
