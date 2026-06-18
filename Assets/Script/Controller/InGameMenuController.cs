using UnityEngine;

public class InGameMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel; // Kéo Prefab Settings_Panel trong scene vào đây

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        Time.timeScale = 0f; // Tạm dừng trò chơi (Pause) khi đang cài đặt
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        Time.timeScale = 1f; // Tiếp tục chơi game (Resume)
    }
}