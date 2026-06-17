using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponManager : MonoBehaviour
{
    [Header("ĐIỂM NEO VỊ TRÍ")]
    public Transform handPosition;
    public Transform backPosition;

    [Header("HAI Ô VŨ KHÍ")]
    public GameObject weaponSlot1;
    public GameObject weaponSlot2;

    private bool isUsingSlot1 = true;
    private WeaponAim handWeaponAim; // Gọi script WeaponAim nằm trên Hand_Position

    void Start()
    {
        // Lấy script điều khiển xoay súng nằm trên điểm neo Hand_Position
        handWeaponAim = handPosition.GetComponent<WeaponAim>();

        // Vào game phát là đưa súng 1 lên tay, súng 2 ra sau lưng
        ResetWeaponsStatus();
    }

    void Update()
    {
        bool hasPressedSwapKey = false;

        // Bấm Q đổi súng
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        // Bấm nút Y/Tam giác trên tay cầm đổi súng
        else if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        // Lăn con lăn chuột giữa đổi súng
        else if (Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.1f)
        {
            hasPressedSwapKey = true;
        }

        if (hasPressedSwapKey)
        {
            SwapWeapon();
        }
    }

    void SwapWeapon()
    {
        isUsingSlot1 = !isUsingSlot1; // Đảo ô súng

        if (isUsingSlot1)
        {
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            UpdateWeaponParent(weaponSlot2, backPosition, false);
        }
        else
        {
            UpdateWeaponParent(weaponSlot2, handPosition, true);
            UpdateWeaponParent(weaponSlot1, backPosition, false);
        }
    }

    void UpdateWeaponParent(GameObject weapon, Transform newParent, bool isTargetHand)
    {
        weapon.transform.SetParent(newParent);
        weapon.transform.localRotation = Quaternion.identity;

        weapon.transform.localScale = Vector3.one;
        if (isTargetHand)
        {
            WeaponInfo info = weapon.GetComponent<WeaponInfo>();
            if (info != null)
            {
                // Thay vì gán bằng Vector3.zero, mình lấy tọa độ custom ông chỉnh trong Prefab gán vào!
                weapon.transform.localPosition = info.customHandPosition;
                handWeaponAim.currentWeapon = info;
            }
            else
            {
                weapon.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            weapon.transform.localPosition = Vector3.zero; // Ra sau lưng thì cứ về tâm Back_Position
        }

        SpriteRenderer weaponRenderer = weapon.GetComponent<SpriteRenderer>();
        SpriteRenderer playerRenderer = handPosition.parent.GetComponent<SpriteRenderer>(); // Lấy Sprite của Rookie

        if (weaponRenderer != null && playerRenderer != null)
        {
            if (isTargetHand) weaponRenderer.sortingOrder = playerRenderer.sortingOrder + 1; // Đè lên trước bụng
            else weaponRenderer.sortingOrder = playerRenderer.sortingOrder - 1; // Chui ra sau lưng áo
        }

        if (isTargetHand)
        {
            WeaponInfo info = weapon.GetComponent<WeaponInfo>();
            if (info != null && handWeaponAim != null)
            {
                handWeaponAim.currentWeapon = info;
            }
        }
    }

    void ResetWeaponsStatus()
    {
        if (weaponSlot1 != null && weaponSlot2 != null)
        {
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            UpdateWeaponParent(weaponSlot2, backPosition, false);
        }
    }
}