using UnityEngine;
public class CameraController : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("PIXEL PERFECT")]
    public float pixelsPerUnit = 100f;

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;

            // Snap thẳng vào pixel grid, không lerp
            desiredPosition.x = Mathf.Round(desiredPosition.x * pixelsPerUnit) / pixelsPerUnit;
            desiredPosition.y = Mathf.Round(desiredPosition.y * pixelsPerUnit) / pixelsPerUnit;

            transform.position = desiredPosition;
        }
    }
}