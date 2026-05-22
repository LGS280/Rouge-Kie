using UnityEngine;

public class Camera : MonoBehaviour
{
    [Header("M?c tiêu s? theo dõi")]
    public Transform target;

    [Header("Cài ??t ?? m??t")]
    public float smoothSpeed = 0.125f;

    [Header("Kho?ng cách bù tr?c Z")]
    public Vector3 offset = new Vector3(0, 0, -10);

    void LateUpdate()
    {
        if (target != null)
        {
            // Xác ??nh v? trí camera mu?n t?i (v? trí c?a player)
            Vector3 desiredPosition = target.position + offset;

            // Dùng toán h?c n?i suy (Lerp) ?? tính v? trí trung gian, t?o hi?u ?ng ?u?i theo m??t mà
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

            // C?p nh?t v? trí m?i cho Camera
            transform.position = smoothedPosition;
        }
    }
}
