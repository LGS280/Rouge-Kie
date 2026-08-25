using UnityEngine;

/// <summary>
/// Quản lý mũi tên chỉ báo vị trí nhân vật chính (Local Player Indicator).
/// Mũi tên này lơ lửng trên đầu nhân vật và có hiệu ứng nhấp nhô nhẹ nhàng giúp người chơi dễ nhận diện nhân vật của mình trong các trận Co-op đông người.
/// </summary>
public class LocalPlayerIndicator : MonoBehaviour
{
    [Header("Bobbing Animation")]
    [Tooltip("Khoảng cách nhấp nhô lên xuống")]
    [SerializeField] private float bobHeight = 0.12f;
    [Tooltip("Tốc độ nhấp nhô")]
    [SerializeField] private float bobSpeed = 3.5f;

    [Header("Visibility")]
    [Tooltip("Tự động ẩn nếu GameObject cha này là Remote Player")]
    [SerializeField] private bool onlyForLocalPlayer = true;

    private Vector3 initialLocalPos;

    private void Awake()
    {
        initialLocalPos = transform.localPosition;
    }

    private void Start()
    {
        if (onlyForLocalPlayer)
        {
            // Nếu cha không phải là Local Player mang Tag 'Player' -> Ẩn mũi tên
            Transform root = transform.root;
            if (root != null && !root.CompareTag("Player"))
            {
                gameObject.SetActive(false);
                return;
            }
        }
    }

    private void Update()
    {
        // Hiệu ứng dao động hình sin nhấp nhô lơ lửng
        float newY = initialLocalPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);
    }
}
