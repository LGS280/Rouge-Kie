using UnityEngine;

public class InGameMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel; // Kéo Prefab Settings_Panel vào đây

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);

        // KHÔNG dùng Time.timeScale = 0f nữa để thế giới game (quái vật, đạn) vẫn tiếp tục chạy
        Time.timeScale = 1f;

        // Tạm thời khóa điều khiển của nhân vật Rookie để tránh việc vừa chỉnh setting vừa bắn súng/di chuyển
        TogglePlayerControls(false);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);

        // Trả lại quyền điều khiển cho nhân vật Rookie
        TogglePlayerControls(true);
    }

    // Hàm phụ trợ bật/tắt script điều khiển của nhân vật Rookie
    private void TogglePlayerControls(bool enable)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerController controller = player.GetComponent<PlayerController>();

            // THÊM MỚI: Tìm kiếm component ngắm/bắn súng trên Rookie hoặc các GameObject con của nó
            WeaponAim weaponAim = player.GetComponentInChildren<WeaponAim>();

            if (movement != null) movement.enabled = enable;
            if (controller != null) controller.enabled = enable;
            if (weaponAim != null) weaponAim.enabled = enable; // Bật/tắt súng đồng bộ

            // Dừng lực quán tính vật lý của nhân vật ngay khi mở cài đặt để tránh bị trượt tự do
            if (!enable)
            {
                Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy GameObject nhân vật có Tag 'Player'!");
        }
    }

    // Thêm mới: Hàm xử lý thoát game ra Menu chính
    public async void QuitToMainMenu()
    {
        // Khôi phục timeScale đề phòng game đang bị pause
        Time.timeScale = 1f;

        // Nếu đang kết nối mạng, tiến hành ngắt kết nối phòng và reconnect để reset trạng thái
        if (NetworkManager.Instance != null)
        {
            await NetworkManager.Instance.DisconnectAndReconnect();
        }

        // Chuyển về Scene Menu chính
        UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Menu");
    }
}