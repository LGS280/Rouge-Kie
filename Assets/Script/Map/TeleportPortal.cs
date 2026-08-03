using UnityEngine;

/// <summary>
/// Xử lý va chạm khi người chơi bước vào cổng dịch chuyển chuyển tầng (Portal)
/// </summary>
public class TeleportPortal : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered) return;

        // Bỏ qua nếu đối tượng va chạm là Remote Player (người chơi khác qua mạng)
        if (collision.GetComponent<RemotePlayerController>() != null || collision.GetComponentInParent<RemotePlayerController>() != null)
        {
            return;
        }

        // Kiểm tra đối tượng va chạm có phải người chơi chính hay không
        if (collision.CompareTag("Player"))
        {
            hasTriggered = true;
            Debug.Log("[TeleportPortal] Người chơi chính đã bước vào cổng dịch chuyển chuyển tầng.");

            bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);

            if (isMultiplayer)
            {
                // CHẾ ĐỘ CO-OP: Gửi yêu cầu chuyển tầng đồng bộ qua SignalR tới toàn bộ máy trong phòng
                int targetFloor = (GameProgressionManager.Instance != null) ? GameProgressionManager.Instance.currentFloor + 1 : 2;
                NetworkManager.Instance.SendNextFloorRequest(targetFloor);
                Debug.Log($"[TeleportPortal] Co-op Mode: Phát lệnh chuyển sang Tầng {targetFloor} tới toàn bộ phòng.");
            }
            else
            {
                // CHẾ ĐỘ SOLO: Kích hoạt chuyển tầng hoặc mở UI chọn Buff trực tiếp cục bộ
                if (GameProgressionManager.Instance != null)
                {
                    int floor = GameProgressionManager.Instance.currentFloor;
                    if ((floor == 1 || floor == 3) && UpgradeSelectionUI.Instance != null)
                    {
                        UpgradeSelectionUI.Instance.OpenUpgradeMenu(() =>
                        {
                            if (GameProgressionManager.Instance != null)
                            {
                                GameProgressionManager.Instance.StartNextFloor();
                            }
                        });
                    }
                    else
                    {
                        GameProgressionManager.Instance.StartNextFloor();
                    }
                }
                else
                {
                    Debug.LogWarning("[TeleportPortal] Không tìm thấy GameProgressionManager. Tự động reset màn chơi.");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
            }

            // Hủy đối tượng cổng sau khi đã sử dụng
            Destroy(gameObject);
        }
    }
}
