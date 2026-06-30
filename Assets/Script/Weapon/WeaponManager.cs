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
    private WeaponAim handWeaponAim;

    void Start()
    {
        handWeaponAim = handPosition.GetComponent<WeaponAim>();
        ResetWeaponsStatus();
    }

    void Update()
    {
        bool hasPressedSwapKey = false;

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
        else if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
        {
            hasPressedSwapKey = true;
        }
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
        if (weaponSlot1 == null && weaponSlot2 != null)
        {
            weaponSlot1 = weaponSlot2;
            weaponSlot2 = null;
            isUsingSlot1 = true;
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            return;
        }

        if (weaponSlot1 != null && weaponSlot2 == null)
        {
            isUsingSlot1 = true;
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            return;
        }

        if (weaponSlot1 == null && weaponSlot2 == null) return;

        isUsingSlot1 = !isUsingSlot1;

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
        if (weapon == null) return;

        weapon.transform.SetParent(newParent);
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        WeaponInfo info = weapon.GetComponent<WeaponInfo>();
        WeaponLaser laserScript = weapon.GetComponent<WeaponLaser>();

        if (isTargetHand)
        {
            if (info != null)
            {
                weapon.transform.localPosition = info.customHandPosition;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = info;
            }
            else if (laserScript != null)
            {
                weapon.transform.localPosition = laserScript.customHandPosition;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = null;
            }
            else
            {
                weapon.transform.localPosition = Vector3.zero;
                if (handWeaponAim != null) handWeaponAim.currentWeapon = null;
            }
        }
        else
        {
            weapon.transform.localPosition = Vector3.zero;

            if (laserScript != null)
            {
                laserScript.StopLaser();
            }
        }

        SpriteRenderer weaponRenderer = weapon.GetComponent<SpriteRenderer>();
        SpriteRenderer playerRenderer = handPosition.parent.GetComponent<SpriteRenderer>();

        if (weaponRenderer != null && playerRenderer != null)
        {
            if (isTargetHand) weaponRenderer.sortingOrder = playerRenderer.sortingOrder + 1;
            else weaponRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
        }
    }

    void ResetWeaponsStatus()
    {
        if (weaponSlot1 == null && weaponSlot2 != null)
        {
            weaponSlot1 = weaponSlot2;
            weaponSlot2 = null;
        }

        if (weaponSlot1 != null)
        {
            UpdateWeaponParent(weaponSlot1, handPosition, true);
            isUsingSlot1 = true;
        }

        if (weaponSlot2 != null)
        {
            UpdateWeaponParent(weaponSlot2, backPosition, false);
        }
    }

    // public void PickupWeapon(GameObject newWeapon)
    // {
    //     if (newWeapon == null) return;
    //     if (weaponSlot1 != null && weaponSlot2 == null)
    //     {
    //         weaponSlot2 = newWeapon;
    //         UpdateWeaponParent(weaponSlot2, backPosition, false);
    //     }
    // }
}