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
            // Bỏ qua nếu bản thân player chạm cổng đang trong trạng thái gục ngã/hy sinh
            RookieHealth localHealth = collision.GetComponent<RookieHealth>();
            if (localHealth != null && localHealth.isDead)
            {
                return;
            }

            bool isMultiplayer = NetworkManager.Instance != null && NetworkManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId);

            if (isMultiplayer)
            {
                // Kiểm tra xem có đồng đội nào đang nằm gục ngã hay không
                if (IsAnyPlayerDead())
                {
                    Debug.LogWarning("[TeleportPortal] Vẫn còn đồng đội gục ngã! Không thể lên tầng tiếp theo.");
                    if (LoadingScreenUI.Instance != null)
                    {
                        LoadingScreenUI.Instance.ShowLoading("PORTAL LOCKED", "Must revive your teammate before moving to the next floor!");
                        StartCoroutine(HideWarningRoutine());
                    }
                    return; // Ngăn chặn chuyển tầng
                }

                hasTriggered = true;
                Debug.Log("[TeleportPortal] Người chơi chính đã bước vào cổng dịch chuyển chuyển tầng.");

                // CHẾ ĐỘ CO-OP: Gửi yêu cầu chuyển tầng đồng bộ qua SignalR tới toàn bộ máy trong phòng
                int targetFloor = (GameProgressionManager.Instance != null) ? GameProgressionManager.Instance.currentFloor + 1 : 2;
                NetworkManager.Instance.SendNextFloorRequest(targetFloor);
                Debug.Log($"[TeleportPortal] Co-op Mode: Phát lệnh chuyển sang Tầng {targetFloor} tới toàn bộ phòng.");
            }
            else
            {
                hasTriggered = true;
                Debug.Log("[TeleportPortal] Người chơi chính đã bước vào cổng dịch chuyển chuyển tầng.");

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

            // Hủy đối tượng cổng sau khi đã sử dụng thành công
            Destroy(gameObject);
        }
    }

    private bool IsAnyPlayerDead()
    {
        // Kiểm tra local player
        GameObject localPlayer = GameObject.FindWithTag("Player");
        if (localPlayer != null)
        {
            RookieHealth health = localPlayer.GetComponent<RookieHealth>();
            if (health != null && health.isDead) return true;
        }

        // Kiểm tra remote players
        if (MultiplayerSyncManager.Instance != null)
        {
            foreach (var kv in MultiplayerSyncManager.Instance.remotePlayers)
            {
                if (kv.Value != null)
                {
                    RemotePlayerController rpc = kv.Value.GetComponent<RemotePlayerController>();
                    if (rpc != null && rpc.isDead) return true;
                }
            }
        }

        return false;
    }

    private System.Collections.IEnumerator HideWarningRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        if (LoadingScreenUI.Instance != null)
        {
            LoadingScreenUI.Instance.HideLoading();
        }
    }
}
